using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using Jint.Collections;
using Jint.Native;
using Jint.Native.Object;
using Jint.Native.Symbol;
using Jint.Runtime.Descriptors;
using Jint.Runtime.Interop.Reflection;

#pragma warning disable IL2072

namespace Jint.Runtime.Interop;

public sealed class TypeReference : Constructor, IObjectWrapper
{
    private static readonly JsString _name = new("typereference");
    private static readonly ConcurrentDictionary<Type, MethodDescriptor[]> _constructorCache = new();
    private static readonly ConcurrentDictionary<MemberAccessorKey, ReflectionAccessor> _memberAccessors = new();

    private readonly record struct MemberAccessorKey(Type Type, string PropertyName);

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

    public override ObjectInstance Construct(JsCallArguments arguments, JsValue newTarget)
    {
        static ObjectInstance ObjectCreator(Engine engine, Realm realm, ObjectCreateState state)
        {
            var arguments = state.Arguments;
            var referenceType = state.TypeReference.ReferenceType;

            var fromOptionsCreator = engine.Options.Interop.CreateTypeReferenceObject(engine, referenceType, arguments);
            ObjectInstance? result = null;
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
                var constructors = _constructorCache.GetOrAdd(
                    referenceType,
                    t => MethodDescriptor.Build(t.GetConstructors(BindingFlags.Public | BindingFlags.Instance)));

                var argumentProvider = new Func<MethodDescriptor, JsValue[]>(method =>
                {
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
                        if (currentArgument.IsArray())
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
                        int start = parameters.Length - arguments.Length;
                        for (var i = start; i < parameters.Length; i++)
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
                });

                foreach (var (method, methodArguments, _) in InteropHelper.FindBestMatch(engine, constructors, argumentProvider))
                {
                    var retVal = method.Call(engine, null, methodArguments);
                    result = TypeConverter.ToObject(realm, retVal);

                    // todo: cache method info
                    break;
                }
            }

            if (result is null)
            {
                ExceptionHelper.ThrowTypeError(realm, $"Could not resolve a constructor for type {referenceType} for given arguments");
            }

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
            var key = jsString._value;

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
        var key = new MemberAccessorKey(ReferenceType, name);
        var accessor = _memberAccessors.GetOrAdd(key, x => ResolveMemberAccessor(_engine, x.Type, x.PropertyName));
        return accessor.CreatePropertyDescriptor(_engine, ReferenceType, name, enumerable: true);
    }

    private static ReflectionAccessor ResolveMemberAccessor(
        Engine engine,
        [DynamicallyAccessedMembers(InteropHelper.DefaultDynamicallyAccessedMemberTypes | DynamicallyAccessedMemberTypes.PublicNestedTypes | DynamicallyAccessedMemberTypes.Interfaces)]
        Type type,
        string name)
    {
        var typeResolver = engine.Options.Interop.TypeResolver;

        if (type.IsEnum)
        {
            var memberNameComparer = typeResolver.MemberNameComparer;
            var typeResolverMemberNameCreator = typeResolver.MemberNameCreator;
#if NET7_0_OR_GREATER
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
                        return new ConstantValueAccessor(JsNumber.Create(value));
                    }
                }
            }

            return ConstantValueAccessor.NullAccessor;
        }

        const BindingFlags BindingFlags = BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy;
        return typeResolver.TryFindMemberAccessor(engine, type, name, BindingFlags, indexerToTry: null, out var accessor)
            ? accessor
            : ConstantValueAccessor.NullAccessor;
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
