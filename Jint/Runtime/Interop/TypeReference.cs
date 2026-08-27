using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using Jint.Native;
using Jint.Native.Object;
using Jint.Native.Symbol;
using Jint.Runtime.Descriptors;
using Jint.Runtime.Interop.Reflection;

namespace Jint.Runtime.Interop;

/// <summary>
/// An explicit host-granted capability for constructing a CLR type and accessing its static members.
/// </summary>
/// <remarks>
/// An explicitly exported reference is not restricted by <see cref="Options.InteropOptions.AllowedAssemblies"/>,
/// which governs namespace discovery. Its constructors, static members, and nested types still pass through
/// <see cref="TypeResolver.MemberFilter"/>, <see cref="Options.InteropOptions.AllowGetType"/>, and the other
/// member-resolution safeguards.
/// </remarks>
public sealed class TypeReference : Constructor, IObjectWrapper
{
    private static readonly JsString _name = new("typereference");
    private TypeReference(
        Engine engine,
        [DynamicallyAccessedMembers(InteropHelper.DefaultDynamicallyAccessedMemberTypes)] Type type)
        : base(engine, engine.Realm, _name)
    {
        ReferenceType = type;

        _prototype = new TypeReferencePrototype(engine, this);
        _prototypeDescriptor = new PropertyDescriptor(_prototype, PropertyFlag.AllForbidden);
        _length = PropertyDescriptor.AllForbiddenDescriptor.NumberZero;

        PreventExtensions();
    }

    /// <summary>
    /// The CLR type this reference exposes.
    /// </summary>
    /// <remarks>
    /// Annotated so that what arrives through <see cref="CreateTypeReference(Engine, Type)"/> is still
    /// annotated when the four reflective lanes below read it back. Without it the promise the caller made
    /// stopped at this property, and a trimmer preserved nothing for a type handed over as a
    /// <see cref="TypeReference"/>. It satisfies construction — through <see cref="Activator"/> and through
    /// the resolved constructor set — outright; the two static-member resolution lanes additionally read
    /// nested types, which this deliberately does not promise, for the reason
    /// <c>InteropHelper.DefaultDynamicallyAccessedMemberTypes</c> gives.
    /// </remarks>
    [DynamicallyAccessedMembers(InteropHelper.DefaultDynamicallyAccessedMemberTypes)]
    public Type ReferenceType { get; }

    public static TypeReference CreateTypeReference<
        [DynamicallyAccessedMembers(InteropHelper.DefaultDynamicallyAccessedMemberTypes)] T>(Engine engine)
    {
        return CreateTypeReference(engine, typeof(T));
    }

    public static TypeReference CreateTypeReference(
        Engine engine,
        [DynamicallyAccessedMembers(InteropHelper.DefaultDynamicallyAccessedMemberTypes)] Type type)
    {
        var reference = new TypeReference(engine, type);
        engine.RegisterTypeReference(reference);
        return reference;
    }

    protected internal override JsValue Call(JsValue thisObject, JsCallArguments arguments)
    {
        // direct calls on a TypeReference constructor object is equivalent to the new operator
        return Construct(arguments, Undefined);
    }

    private readonly record struct MethodResolverState(Engine Engine, JsCallArguments Arguments);

    public override ObjectInstance Construct(JsCallArguments arguments, JsValue newTarget)
    {
        static ObjectInstance ObjectCreator(Engine engine, Realm realm, ObjectCreateState state)
        {
            var arguments = state.Arguments;
            var referenceType = state.TypeReference.ReferenceType;

            var fromOptionsCreator = engine.Options.Interop.CreateTypeReferenceObject(engine, referenceType, arguments);
            ObjectInstance? result = null;
            MethodDescriptor[]? constructors = null;
            if (fromOptionsCreator is not null)
            {
                result = TypeConverter.ToObject(realm, FromObject(engine, fromOptionsCreator));
            }
            else if (arguments.Length == 0 && referenceType.IsValueType)
            {
                var instance = Activator.CreateInstance(referenceType);
                result = TypeConverter.ToObject(realm, FromObject(engine, instance));
            }
            else
            {
                constructors = engine.Options.Interop.TypeResolver.GetConstructors(engine, referenceType);

                Func<MethodDescriptor, MethodResolverState, JsCallArguments> argumentProvider = static (method, state) =>
                {
                    var engine = state.Engine;
                    var arguments = state.Arguments;
                    var parameters = method.Parameters;

                    if (parameters.Length == 0)
                    {
                        return arguments;
                    }

                    var newArguments = new JsValue[parameters.Length];
                    var currentParameter = parameters[parameters.Length - 1];
                    var isParamArray = currentParameter.ParameterType.IsArray &&
                                       currentParameter.GetCustomAttribute<ParamArrayAttribute>() is not null;

                    // last parameter is a ParamArray
                    if (isParamArray && arguments.Length >= parameters.Length - 1)
                    {
                        var currentArgument = JsValue.Undefined;

                        if (arguments.Length > parameters.Length - 1)
                        {
                            currentArgument = arguments[parameters.Length - 1];
                        }

                        // nothing to do, is an array as expected
                        if (currentArgument.IsSpecArray())
                        {
                            return arguments;
                        }

                        Array.Copy(arguments, 0, newArguments, 0, parameters.Length - 1);

                        // the last argument is null or undefined and there are exactly the same arguments and parameters
                        if (currentArgument.IsNullOrUndefined() && parameters.Length == arguments.Length)
                        {
                            // this fix the issue with CLR that receives a null ParamArray instead of an empty one
                            newArguments[parameters.Length - 1] = new JsArray(engine, 0);
                            return newArguments;
                        }

                        // pack the rest of the arguments into an array, as CLR expects
                        var paramArray = new JsValue[Math.Max(0, arguments.Length - (parameters.Length - 1))];
                        if (paramArray.Length > 0)
                        {
                            Array.Copy(arguments, parameters.Length - 1, paramArray, 0, paramArray.Length);
                        }
                        newArguments[parameters.Length - 1] = new JsArray(engine, paramArray);

                        return newArguments;
                    }

                    // TODO: edge case, last parameter is ParamArray with optional parameter before?
                    if (isParamArray && arguments.Length < parameters.Length - 1)
                    {
                        return arguments;
                    }

                    // optional parameters
                    if (parameters.Length > arguments.Length)
                    {
                        // all missing ones must be optional
                        for (var i = arguments.Length; i < parameters.Length; i++)
                        {
                            if (!parameters[i].IsOptional)
                            {
                                // use original arguments
                                return arguments;
                            }
                        }

                        Array.Copy(arguments, 0, newArguments, 0, arguments.Length);

                        for (var i = parameters.Length - 1; i >= 0; i--)
                        {
                            currentParameter = parameters[i];

                            if (i >= arguments.Length - 1)
                            {
                                if (!currentParameter.IsOptional)
                                {
                                    break;
                                }

                                if (arguments.Length - 1 < i || arguments[i].IsUndefined())
                                {
                                    newArguments[i] = FromObject(engine, currentParameter.DefaultValue);
                                }
                            }
                        }

                        return newArguments;
                    }

                    return arguments;
                };

                var resolverState = new MethodResolverState(engine, arguments);
                foreach (var (method, methodArguments, _) in InteropHelper.FindBestMatch(engine, constructors, argumentProvider, resolverState))
                {
                    var retVal = method.Call(engine, null, methodArguments);
                    result = TypeConverter.ToObject(realm, retVal);

                    // todo: cache method info
                    break;
                }
            }

            if (result is null)
            {
                var message = engine.Options.Interop.ExposeDetailedResolutionErrors
                    ? InteropErrorHelper.CreateNoMatchingConstructorMessage(referenceType, arguments, constructors)
                    : "Could not resolve a constructor for the specified arguments.";
                Throw.InteropResolutionError(realm, message, referenceType, memberName: null, arguments, constructors);
            }

            // all three creation branches above (user factory, Activator, reflected constructor)
            // ran user CLR code
            engine.CheckAmortizedConstraintsAtHostBoundary();

            result.SetPrototypeOf(state.TypeReference);

            return result;
        }

        // TODO should inject prototype that reflects TypeReference's target's layout
        var thisArgument = OrdinaryCreateFromConstructor(
            newTarget,
            static intrinsics => intrinsics.Object.PrototypeObject,
            ObjectCreator,
            new ObjectCreateState(this, arguments));

        return thisArgument;
    }

    private readonly record struct ObjectCreateState(TypeReference TypeReference, JsCallArguments Arguments);

    public override bool Equals(JsValue? other)
    {
        if (other is TypeReference typeReference)
        {
            return this.ReferenceType == typeReference.ReferenceType;
        }

        return base.Equals(other);
    }

    internal override bool OrdinaryHasInstance(JsValue v)
    {
        if (v is IObjectWrapper wrapper)
        {
            return wrapper.Target.GetType() == ReferenceType;
        }

        return base.OrdinaryHasInstance(v);
    }

    public override bool DefineOwnProperty(JsValue property, PropertyDescriptor desc)
    {
        return false;
    }

    public override bool Delete(JsValue property)
    {
        return false;
    }

    public override bool Set(JsValue property, JsValue value, JsValue receiver)
    {
        if (!CanPut(property))
        {
            return false;
        }

        var ownDesc = GetOwnProperty(property);
        ownDesc.Value = value;
        return true;
    }

    public override PropertyDescriptor GetOwnProperty(JsValue property)
    {
        if (property is not JsString jsString)
        {
            if (property == GlobalSymbolRegistry.HasInstance)
            {
                var hasInstanceFunction = new ClrFunction(
                    Engine,
                    "[Symbol.hasInstance]",
                    HasInstance,
                    1,
                    PropertyFlag.Configurable);

                var hasInstanceProperty = new PropertyDescriptor(hasInstanceFunction, PropertyFlag.AllForbidden);
                SetProperty(GlobalSymbolRegistry.HasInstance, hasInstanceProperty);
                return hasInstanceProperty;
            }
        }
        else
        {
            var key = jsString.ToString();

            if (_properties?.TryGetValue(key, out var descriptor) != true)
            {
                descriptor = CreatePropertyDescriptor(key);
                if (!ReferenceEquals(descriptor, PropertyDescriptor.Undefined))
                {
                    _properties ??= new PropertyDictionary();
                    _properties[key] = descriptor;
                    return descriptor;
                }
            }
        }

        return base.GetOwnProperty(property);
    }

    private PropertyDescriptor CreatePropertyDescriptor(string name)
    {
        if (ReferenceType.IsEnum)
        {
            // An enum constant resolves to the value the engine's EnumConversion mode calls for - a JsString
            // name under String mode, a JsNumber otherwise - which the (type, name) key of the process-wide
            // cache below cannot express. Rather than widen that key for every member of every type just to
            // carry a bit only enum types read, enum references resolve per instance: this reference memoizes
            // the descriptor in its own _properties on the first read anyway, and resolving one is a scan over
            // the enum's own members, so the shared cache bought little here to begin with.
            return ResolveMemberAccessor(_engine, ReferenceType, name)
                .CreatePropertyDescriptor(_engine, ReferenceType, name, enumerable: true);
        }

        var accessor = _engine.Options.Interop.TypeResolver.GetStaticAccessor(_engine, ReferenceType, name);

        return accessor.CreatePropertyDescriptor(_engine, ReferenceType, name, enumerable: true);
    }

    private static ReflectionAccessor ResolveMemberAccessor(
        Engine engine,
        [DynamicallyAccessedMembers(InteropHelper.DefaultDynamicallyAccessedMemberTypes | DynamicallyAccessedMemberTypes.PublicNestedTypes)]
        Type type,
        string name)
    {
        var typeResolver = engine.Options.Interop.TypeResolver;

        if (type.IsEnum)
        {
            var memberNameComparer = typeResolver.MemberNameComparer;
            var typeResolverMemberNameCreator = typeResolver.MemberNameCreator;
#if NET8_0_OR_GREATER
            var enumValues = type.GetEnumValuesAsUnderlyingType();
#else
            var enumValues = Enum.GetValues(type);
#endif
            var enumNames = Enum.GetNames(type);

            for (var i = 0; i < enumValues.Length; i++)
            {
                var enumOriginalName = enumNames.GetValue(i)?.ToString() ?? "";
                var member = type.GetMember(enumOriginalName)[0];
                foreach (var exposedName in typeResolverMemberNameCreator(member))
                {
                    if (memberNameComparer.Equals(name, exposedName))
                    {
                        var value = enumValues.GetValue(i)!;
                        if (!engine._enumsAsStrings)
                        {
                            return new ConstantValueAccessor(JsNumber.Create(value));
                        }

                        // Under EnumConversionMode.String the same enum value coming the other way — off a
                        // CLR member, out of a method return — becomes its name, so a constant read here has
                        // to agree or `host.Level == Level.Two` compares a string against a number. Take the
                        // canonical name for the value rather than the name matched above, which is what
                        // Enum.ToString() (the conversion those other routes end in) reports when several
                        // constants share one value.
                        var canonicalName = Enum.GetName(type, value) ?? enumOriginalName;
                        return new ConstantValueAccessor(JsString.Create(canonicalName));
                    }
                }
            }

            return ConstantValueAccessor.NullAccessor;
        }

        return typeResolver.GetStaticAccessor(engine, type, name);
    }

    public object Target => ReferenceType;

    private static JsBoolean HasInstance(JsValue thisObject, JsCallArguments arguments)
    {
        var typeReference = thisObject as TypeReference;
        var other = arguments.At(0);

        if (typeReference is null)
        {
            return JsBoolean.False;
        }

        var baseType = typeReference.ReferenceType;

        var derivedType = other switch
        {
            IObjectWrapper wrapper => wrapper.Target.GetType(),
            TypeReferencePrototype otherTypeReference => otherTypeReference.TypeReference.ReferenceType,
            _ => null
        };

        return derivedType != null && baseType != null && (derivedType == baseType || derivedType.IsSubclassOf(baseType))
            ? JsBoolean.True
            : JsBoolean.False;
    }

    public override string ToString()
    {
        return "[CLR type: " + ReferenceType + "]";
    }
}
