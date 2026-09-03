using System.Collections;
using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;
using Jint.Extensions;
using Jint.Native;
using Jint.Native.Function;
using Jint.Native.Object;
using Expression = System.Linq.Expressions.Expression;

namespace Jint.Runtime.Interop;

public class DefaultTypeConverter : ClrTypeConverter
{
    private readonly Engine _engine;

    private readonly record struct TypeConversionKey(Type Source, Type Target);

    private static readonly ConcurrentDictionary<TypeConversionKey, MethodInfo?> _knownCastOperators = new();
    private static readonly ConcurrentDictionary<TypeConversionKey, MethodInfo?> _knownFromResultGenerics = new();

    private static readonly Type intType = typeof(int);
    private static readonly Type iCallableType = typeof(JsCallDelegate);
    private static readonly Type jsValueType = typeof(JsValue);
    private static readonly Type objectType = typeof(object);
    private static readonly Type taskType = typeof(Task);
    private static readonly Type genTaskType = typeof(Task<>);
#if !NETFRAMEWORK && !NETSTANDARD2_0
    private static readonly Type valueTaskType = typeof(ValueTask);
    private static readonly Type genValueTaskType = typeof(ValueTask<>);
#endif

    // Every lookup below spells its receiver as `typeof(X)` rather than reading one of the Type fields
    // above, and that is not a style choice. A `typeof` token is a constant the trim analyzer folds, so
    // it both PRESERVES the member being looked up and reports nothing; the same lookup through a field
    // read loses the type's identity on the way and was six IL2080 in every embedder's publish. Keep new
    // lookups in this shape - the fields exist for the identity comparisons further down, not for
    // reflection.
    private static readonly MethodInfo taskFromResultInfo = typeof(Task).GetMethod("FromResult")!;
#if !NETFRAMEWORK && !NETSTANDARD2_0
    private static readonly MethodInfo valueTaskFromResultInfo = typeof(ValueTask).GetMethod("FromResult")!;
#endif

    private static readonly MethodInfo changeTypeIfConvertible = typeof(DefaultTypeConverter).GetMethod(
        nameof(ChangeTypeOnlyIfConvertible), BindingFlags.NonPublic | BindingFlags.Static)!;
    private static readonly MethodInfo jsValueFromObject = typeof(JsValue).GetMethod(nameof(JsValue.FromObject))!;
    private static readonly MethodInfo enterHostCallback = typeof(Engine).GetMethod(nameof(Engine.EnterTransferredHostCallback), BindingFlags.Instance | BindingFlags.NonPublic)!;
    private static readonly MethodInfo getHostCallbackOwner = typeof(Engine).GetMethod(nameof(Engine.GetHostCallbackAuthorization), BindingFlags.Instance | BindingFlags.NonPublic)!;
    private static readonly MethodInfo exitHostCallback = typeof(Engine.HostCallScope).GetMethod(nameof(IDisposable.Dispose))!;
    private static readonly MethodInfo jsValueToObject = typeof(JsValue).GetMethod(nameof(JsValue.ToObject))!;

    // Expression.Property(Expression, string) resolves the property by NAME and is [RequiresUnreferencedCode]
    // for it. The declaring type here is a constant and so is the name, so the PropertyInfo overload says the
    // same thing while preserving the property instead of warning about it.
    private static readonly PropertyInfo objectInstanceEngine = typeof(ObjectInstance).GetProperty(nameof(ObjectInstance.Engine))!;

    public DefaultTypeConverter(Engine engine)
    {
        _engine = engine;
    }

    /// <summary>
    /// Converts value to the given type, throwing if the conversion cannot be done.
    /// Dispatches through the virtual <see cref="TryConvert"/> first so that subclass overrides are honored,
    /// then falls back to the built-in conversion pipeline to produce a detailed error message and to honor
    /// exception propagation semantics (<see cref="Options.InteropOptions.ExceptionHandler"/>).
    /// </summary>
    public override object? Convert(
        object? value,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.PublicFields)] Type type,
        IFormatProvider formatProvider)
    {
        if (TryConvert(value, type, formatProvider, out var converted))
        {
            return converted;
        }

        if (!TryConvertInternal(value, type, formatProvider, propagateException: true, out converted, out var problemMessage))
        {
            Throw.Error(_engine, problemMessage ?? $"Unable to convert {value} to type {type}");
        }
        return converted;
    }

    /// <summary>
    /// Converts value to the given type, returning false if the conversion cannot be done.
    /// This is the extension point for custom conversions: both <see cref="Convert"/> and the engine's
    /// interop paths dispatch through it. Overrides should call <c>base.TryConvert</c> as the fallback;
    /// do not call <see cref="Convert"/> from an override as that would cause infinite recursion.
    /// </summary>
    public override bool TryConvert(
        object? value,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.PublicFields)] Type type,
        IFormatProvider formatProvider,
        [NotNullWhen(true)] out object? converted)
    {
        return TryConvertInternal(value, type, formatProvider, propagateException: false, out converted, out _);
    }

    /// <summary>
    /// Converts one part of a composite - an array element, a collection item, a target dictionary's value,
    /// a member of a POCO built from a dictionary - under the same <paramref name="propagateException"/>
    /// contract as the frame that is assembling the composite.
    /// </summary>
    /// <remarks>
    /// Every one of those sites reached for the public, throwing <see cref="Convert"/> whatever its own frame
    /// had been asked, so a part that could not be converted escaped <see cref="TryConvert"/> as a CLR
    /// exception rather than as the <see langword="false"/> that method documents - and an exception is not
    /// something <c>MethodInfoFunction.Call</c> can move on from: it tries candidates in score order and
    /// declines its way to the next one, so a throwing conversion ends the call rather than the candidate
    /// (<see href="https://github.com/sebastienros/jint/issues/3754">#3754</see>). The body mirrors
    /// <see cref="Convert"/> so that behaviour under <paramref name="propagateException"/> is unchanged: the
    /// virtual <see cref="TryConvert"/> first, so a subclass override still answers for the parts, and only
    /// then the internal pipeline that produces the detailed message and honours
    /// <see cref="Options.InteropOptions.ExceptionHandler"/>.
    /// </remarks>
    private bool TryConvertPart(
        object? value,
        [DynamicallyAccessedMembers(InteropHelper.DefaultDynamicallyAccessedMemberTypes)] Type type,
        IFormatProvider formatProvider,
        bool propagateException,
        out object? converted,
        out string? problemMessage)
    {
        problemMessage = null;

        if (TryConvert(value, type, formatProvider, out converted))
        {
            return true;
        }

        if (!propagateException)
        {
            converted = null;
            problemMessage = $"Unable to convert a value of type '{value?.GetType()}' to '{type}'";
            return false;
        }

        if (!TryConvertInternal(value, type, formatProvider, propagateException: true, out converted, out problemMessage))
        {
            Throw.Error(_engine, problemMessage ?? $"Unable to convert {value} to type {type}");
        }

        return true;
    }

    private static readonly ConditionalWeakTable<IFunction, TypeKeyedCache<Func<object, Delegate>>> _targetBinderDelegateCache = new();
    private static readonly ConditionalWeakTable<object, TypeKeyedCache<Delegate>> _boundTargetDelegateCache = new();
    private static readonly ConditionalWeakTable<Delegate, ObjectInstance> _hostCallbackDelegates = new();

    private static readonly ConditionalWeakTable<IFunction, TypeKeyedCache<Func<object, Delegate>>>.CreateValueCallback _createBinderCache =
        static _ => new TypeKeyedCache<Func<object, Delegate>>();

    private static readonly ConditionalWeakTable<object, TypeKeyedCache<Delegate>>.CreateValueCallback _createBoundDelegateCache =
        static _ => new TypeKeyedCache<Delegate>();

    /// <summary>
    /// An append-only map from a target delegate <see cref="Type"/> to the artefact that was built for it.
    /// </summary>
    /// <remarks>
    /// The two delegate caches above are process-wide and keyed on something that does not carry the target
    /// type: a function instance, and one level below it that function's AST node, which a shared
    /// <c>Prepared&lt;Script&gt;</c> makes process-wide state outliving every engine that runs it. Entries are
    /// added and never replaced, so the delegate identity an event unregistration needs holds per target type;
    /// the list is one node long for the overwhelming majority of functions, which are converted to exactly
    /// one delegate type. Nothing stored here is engine-affine - a <see cref="Type"/>, and a binder that takes
    /// its target as a parameter - so the AST node stays shareable across engines.
    /// </remarks>
    private sealed class TypeKeyedCache<T> where T : class
    {
        private Node? _head;

        internal bool TryGetValue(Type type, [NotNullWhen(true)] out T? value)
        {
            var node = Volatile.Read(ref _head);
            while (node is not null)
            {
                if (ReferenceEquals(node._type, type))
                {
                    value = node._value;
                    return true;
                }

                node = node._next;
            }

            value = null;
            return false;
        }

        /// <summary>
        /// Publishes <paramref name="value"/> for <paramref name="type"/>, or returns what a concurrent
        /// caller published for it first - the winner is the one instance every caller then sees.
        /// </summary>
        internal T GetOrAdd(Type type, T value)
        {
            while (true)
            {
                var head = Volatile.Read(ref _head);
                for (var node = head; node is not null; node = node._next)
                {
                    if (ReferenceEquals(node._type, type))
                    {
                        return node._value;
                    }
                }

                if (ReferenceEquals(Interlocked.CompareExchange(ref _head, new Node(type, value, head), head), head))
                {
                    return value;
                }
            }
        }

        private sealed class Node(Type type, T value, Node? next)
        {
            internal readonly Type _type = type;
            internal readonly T _value = value;
            internal readonly Node? _next = next;
        }
    }

    private bool TryConvertInternal(
        object? value,
        [DynamicallyAccessedMembers(InteropHelper.DefaultDynamicallyAccessedMemberTypes)] Type type,
        IFormatProvider formatProvider,
        bool propagateException,
        out object? converted,
        out string? problemMessage)
    {
        converted = null;
        problemMessage = null;

        if (value is null)
        {
            if (InteropHelper.TypeIsNullable(type))
            {
                return true;
            }

            problemMessage = $"Unable to convert null to '{type.FullName}'";
            return false;
        }

        // don't try to convert if value is derived from type
        if (type.IsInstanceOfType(value))
        {
            converted = value;
            return true;
        }

        // Handle conversion from object[] (JS array) to generic collection types like List<T>, IList<T>, IEnumerable<T>, etc.
        // This must come before the generic assignability check because object[] incorrectly satisfies
        // the assignability check for IList<string> etc. (since object[] implements IList<object>).
        if (value is object?[] sourceArray && type.IsGenericType)
        {
            var genericArgs = type.GetGenericArguments();

            if (genericArgs.Length == 1)
            {
                var genericTypeDef = type.GetGenericTypeDefinition();
                var elementType = genericArgs[0];

                if (genericTypeDef != typeof(Collection<>) && InteropHelper.GenericCollectionTypeDefinitions.Contains(genericTypeDef))
                {
                    var targetList = (IList) Activator.CreateInstance(typeof(List<>).MakeGenericType(elementType))!;
                    foreach (var item in sourceArray)
                    {
                        if (!TryConvertPart(item, elementType, formatProvider, propagateException, out var convertedItem, out problemMessage))
                        {
                            return false;
                        }

                        targetList.Add(convertedItem);
                    }
                    converted = targetList;
                    return true;
                }

                if (genericTypeDef == typeof(Collection<>))
                {
                    var innerListType = typeof(List<>).MakeGenericType(elementType);
                    var innerList = (IList) Activator.CreateInstance(innerListType)!;
                    foreach (var item in sourceArray)
                    {
                        if (!TryConvertPart(item, elementType, formatProvider, propagateException, out var convertedItem, out problemMessage))
                        {
                            return false;
                        }

                        innerList.Add(convertedItem);
                    }
                    converted = Activator.CreateInstance(type, innerList)!;
                    return true;
                }
            }
        }

        if (type.IsGenericType)
        {
            var result = InteropHelper.IsAssignableToGenericType(value.GetType(), type);

            // IsAssignableToGenericType matches on the generic type *definition*, so on its own it calls a
            // List<object> assignable to IEnumerable<string>. Handing such a value straight over would let it
            // reach a reflection Invoke unconverted and die there as a raw ArgumentException (#2987), so apply
            // the same rule MethodInfoFunction.IsGenericParameter states: for a fully concrete generic type,
            // verify the argument really is assignable before assigning it directly. The check is only
            // skipped for a target that still has open type parameters, where assignability cannot be judged
            // - the engine's own binding path closes them first, so that is for hosts calling TryConvert
            // themselves. Note IsInstanceOfType above already accepted everything genuinely assignable,
            // variance included, which is why nothing legal is lost here.
            if (result.IsAssignable
                && (type.ContainsGenericParameters || type.IsAssignableFrom(result.MatchingGivenType)))
            {
                converted = value;
                return true;
            }
        }

        if (type.IsNullable())
        {
            type = Nullable.GetUnderlyingType(type)!;
        }

        if (type.IsEnum)
        {
            if (EnumTryParse(type, value.ToString(), out converted))
            {
                return true;
            }
        }

        var valueType = value.GetType();

        // is the javascript value an ICallable instance ?
        if (valueType == iCallableType)
        {
            if (typeof(Delegate).IsAssignableFrom(type) && !type.IsAbstract)
            {
                var func = (JsCallDelegate) value;
                var functionInstance = func.Target;
                if (functionInstance is ObjectInstance callback)
                {
                    callback.Engine.AuthorizeHostCallback(callback);
                }

                // caching of .NET delegates per function instance is required to be able to support
                // unregistering event handlers (see ShouldExecuteActionCallbackOnEventChanged). Both caches
                // are keyed by the target delegate type as well, because that type is what the compiled
                // binder bakes in and neither a function instance nor its AST node carries it - and the AST
                // node is process-wide state, so a shared Prepared<Script> otherwise lets whichever engine
                // converted first decide the delegate type for every engine after it (#3434).
                Delegate d;
                if (functionInstance is not null)
                {
                    var boundDelegates = _boundTargetDelegateCache.GetValue(functionInstance, _createBoundDelegateCache);
                    if (!boundDelegates.TryGetValue(type, out var bound))
                    {
                        var astFunction = (functionInstance as Function)?._functionDefinition?.Function;

                        // use a single builder per unique function AST and target delegate type
                        var targetBinder = astFunction is not null
                            ? GetOrBuildTargetBinderDelegate(astFunction, type, func)
                            : BuildTargetBinderDelegate(type, func);

                        bound = boundDelegates.GetOrAdd(type, targetBinder(functionInstance)!);
                    }

                    d = bound;
                }
                else
                {
                    d = BuildDelegate(type, func, Expression.Constant(functionInstance, functionInstance!.GetType())).Compile();
                }

                if (functionInstance is ObjectInstance callbackTarget && !_hostCallbackDelegates.TryGetValue(d, out _))
                {
                    _hostCallbackDelegates.GetValue(d, _ => callbackTarget);
                }

                converted = d;
                return true;
            }
        }

        if (type.IsArray)
        {
            if (value is not object[] source)
            {
                problemMessage = $"Value of object[] type is expected, but actual type is {value.GetType()}";
                return false;
            }

            var targetElementType = type.GetElementType()!;
            var itemsConverted = new object?[source.Length];
            for (var i = 0; i < source.Length; i++)
            {
                if (!TryConvertPart(source[i], targetElementType, formatProvider, propagateException, out itemsConverted[i], out problemMessage))
                {
                    return false;
                }
            }
            var result = Array.CreateInstance(targetElementType, source.Length);
            itemsConverted.CopyTo(result, 0);

            converted = result;
            return true;
        }

        var typeDescriptor = TypeDescriptor.Get(valueType);
        if (typeDescriptor.IsStringKeyedGenericDictionary)
        {
            // public empty constructor required
            var constructors = type.GetConstructors();
            // value types
            if (type.IsValueType && constructors.Length > 0)
            {
                problemMessage = $"No valid constructors found for {type}";
                return false;
            }

            var constructorParameters = Array.Empty<object>();

            // reference types - return null if no valid constructor is found
            if (!type.IsValueType)
            {
                var found = false;
                foreach (var constructor in constructors)
                {
                    if (constructor.GetParameters().Length == 0 && constructor.IsPublic)
                    {
                        found = true;
                        break;
                    }
                }

                if (!found)
                {
                    foreach (var constructor in constructors)
                    {
                        var parameterInfos = constructor.GetParameters();
                        if (Array.TrueForAll(parameterInfos, static p => p.IsOptional) && constructor.IsPublic)
                        {
                            constructorParameters = new object[parameterInfos.Length];
                            found = true;
                            break;
                        }
                    }
                }

                if (!found)
                {
                    problemMessage = $"No valid constructors found for type {type}";
                    return false;
                }
            }

            var obj = Activator.CreateInstance(type, constructorParameters)!;

            // Check if the target type is also a string-keyed dictionary (e.g. Dictionary<string, object>).
            // In that case, populate the dictionary entries from the source rather than mapping to target members.
            var targetTypeDescriptor = TypeDescriptor.Get(type);
            if (targetTypeDescriptor.IsStringKeyedGenericDictionary && obj is IDictionary targetDict && typeDescriptor.KeysAccessor != null)
            {
                // Determine the value type expected by the target dictionary from its generic arguments.
                var targetValueType = typeof(object);
                if (type.IsGenericType)
                {
                    var genericArgs = type.GetGenericArguments();
                    if (genericArgs.Length == 2)
                    {
                        targetValueType = genericArgs[1];
                    }
                }

                var keys = (IEnumerable<string>) typeDescriptor.KeysAccessor.GetValue(value)!;
                foreach (var key in keys)
                {
                    if (typeDescriptor.TryGetDictionaryValue(value, key, out var sourceVal))
                    {
                        if (!TryConvertPart(sourceVal, targetValueType, formatProvider, propagateException, out var convertedValue, out problemMessage))
                        {
                            return false;
                        }

                        targetDict[key] = convertedValue;
                    }
                }
            }
            else
            {
                // The two lookups this loop actually wants, rather than GetMembers() filtered down to them.
                // GetMembers() reports methods, constructors, events and nested types as well, so a trimmer
                // read it as needing PublicNestedTypes of `type` - a requirement that propagated out to every
                // annotated caller for members this loop skips - and every call materialized a MemberInfo for
                // each of them only to discard it. Same public instance-and-static set.
                foreach (var member in type.GetProperties())
                {
                    if (!CopyDictionaryEntryToMember(this, typeDescriptor, value, obj, member, formatProvider, propagateException, out problemMessage))
                    {
                        return false;
                    }
                }

                foreach (var member in type.GetFields())
                {
                    if (!CopyDictionaryEntryToMember(this, typeDescriptor, value, obj, member, formatProvider, propagateException, out problemMessage))
                    {
                        return false;
                    }
                }

                // propagateException is threaded in as a parameter because this is a static local function,
                // and it has to be threaded in at all for the same reason the other four composite sites take
                // it: a member the dictionary supplies but the target cannot hold is a decline of the whole
                // conversion, not an exception out of a Try method.
                static bool CopyDictionaryEntryToMember(
                    DefaultTypeConverter converter,
                    TypeDescriptor typeDescriptor,
                    object value,
                    object target,
                    MemberInfo member,
                    IFormatProvider formatProvider,
                    bool propagateException,
                    out string? problemMessage)
                {
                    problemMessage = null;

                    if (typeDescriptor.TryGetDictionaryValue(value, member.Name, out var val)
                        || typeDescriptor.TryGetDictionaryValue(value, member.Name.UpperToLowerCamelCase(), out val))
                    {
                        if (!converter.TryConvertPart(val, member.GetDefinedType(), formatProvider, propagateException, out var output, out problemMessage))
                        {
                            return false;
                        }

                        member.SetValue(target, output);
                    }

                    return true;
                }
            }

            converted = obj;
            return true;
        }

        try
        {
            converted = System.Convert.ChangeType(value, type, formatProvider);
            return true;
        }
        catch (Exception e)
        {
            if (Throw.MustPropagateHostException(e))
            {
                throw;
            }

            // check if we can do a cast with operator overloading
            if (TryCastWithOperators(value, type, valueType, out var invoke))
            {
                converted = invoke;
                return true;
            }

            if (propagateException && !_engine.Options.Interop.ExceptionHandler(e))
            {
                throw;
            }

            problemMessage = e.Message;
            return false;
        }
    }

    private static bool EnumTryParse(Type enumType, string? value, [NotNullWhen(true)] out object? result)
    {
        if (value is null)
        {
            result = null;
            return false;
        }

#if !NETFRAMEWORK && !NETSTANDARD2_0
        return Enum.TryParse(enumType, value, ignoreCase: false, out result!);
#else
        try
        {
            result = Enum.Parse(enumType, value, ignoreCase: false);
            return true;
        }
        catch (ArgumentException)
        {
            result = null!;
            return false;
        }
#endif
    }

    private static Func<object, Delegate> GetOrBuildTargetBinderDelegate(
        IFunction astFunction,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicMethods)] Type delegateType,
        JsCallDelegate function)
    {
        var binders = _targetBinderDelegateCache.GetValue(astFunction, _createBinderCache);
        return binders.TryGetValue(delegateType, out var binder)
            ? binder
            : binders.GetOrAdd(delegateType, BuildTargetBinderDelegate(delegateType, function));
    }

    private static Func<object, Delegate> BuildTargetBinderDelegate(
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicMethods)] Type delegateType,
        JsCallDelegate function)
    {
        // Parameter for the target object
        var targetParam = Expression.Parameter(typeof(object), "target");

        var castedTarget = Expression.Convert(targetParam, function.Target!.GetType());

        var innerDelegate = BuildDelegate(delegateType, function, castedTarget);

        // Create the outer delegate: Func<object, Delegate>
        var outerDelegateType = typeof(Func<object, Delegate>);
        var curried = Expression.Lambda(
            outerDelegateType,
            innerDelegate,
            targetParam);

        return (Func<object, Delegate>) curried.Compile();
    }

    internal static bool ContainsHostCallback(object?[] parameters)
    {
        for (var i = 0; i < parameters.Length; i++)
        {
            if (IsHostCallback(parameters[i]))
            {
                return true;
            }

            if (parameters[i] is Array callbacks)
            {
                foreach (var callback in callbacks)
                {
                    if (IsHostCallback(callback))
                    {
                        return true;
                    }
                }
            }
        }

        return false;
    }

    internal static bool IsHostCallback(object? value)
        => value is Delegate callback && _hostCallbackDelegates.TryGetValue(callback, out _);

    [UnconditionalSuppressMessage("AotAnalysis", "IL3050:RequiresDynamicCode",
        Justification = "Expression.NewArrayInit is [RequiresDynamicCode] because the array type may have to " +
                        "be constructed at run time. The element type here is always typeof(JsValue), and " +
                        "JsValue[] is used throughout this assembly, so the array type is already in any image " +
                        "that contains Jint. Jint.AotExample's 'JS function -> Func<int, int>' probe runs this " +
                        "path on a published native binary.")]
    private static LambdaExpression BuildDelegate(
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicMethods)] Type type,
        JsCallDelegate function,
        Expression targetExpression)
    {
        var method = type.GetMethod("Invoke");
        var arguments = method!.GetParameters();

        var parameters = new ParameterExpression[arguments.Length];
        for (var i = 0; i < parameters.Length; i++)
        {
            parameters[i] = Expression.Parameter(arguments[i].ParameterType, arguments[i].Name);
        }

        var initializers = new List<MethodCallExpression>(parameters.Length);
        var targetEngine = Expression.Property(
            Expression.Convert(targetExpression, typeof(ObjectInstance)),
            objectInstanceEngine);

        for (var i = 0; i < parameters.Length; i++)
        {
            var param = parameters[i];
            if (param.Type.IsValueType)
            {
                var boxing = Expression.Convert(param, objectType);
                initializers.Add(Expression.Call(null, jsValueFromObject, targetEngine, boxing));
            }
            else if (param.Type.IsArray &&
                     arguments[i].GetCustomAttribute<ParamArrayAttribute>() is not null &&
                     function.Target is Function instance)
            {
                for (var j = 0; j < instance.GetLength(); j++)
                {
                    var returnLabel = Expression.Label(typeof(object));
                    // ArrayLength rather than Expression.Property(param, "Length"), which resolves by name
                    // and is [RequiresUnreferencedCode] for it; the node produced is the same ldlen.
                    var checkIndex = Expression.GreaterThanOrEqual(Expression.ArrayLength(param), Expression.Constant(j));
                    var condition = Expression.IfThen(checkIndex, Expression.Return(returnLabel, Expression.ArrayAccess(param, Expression.Constant(j))));
                    var block = Expression.Block(condition, Expression.Label(returnLabel, Expression.Constant(JsValue.Undefined)));

                    initializers.Add(Expression.Call(null, jsValueFromObject, targetEngine, block));
                }
            }
            else
            {
                initializers.Add(Expression.Call(null, jsValueFromObject, targetEngine, param));
            }
        }

        var vars = Expression.NewArrayInit(jsValueType, initializers);

        var callExpression = Expression.Call(
            targetExpression,
            function.Method,
            Expression.Constant(JsValue.Undefined, jsValueType),
            vars);

        Expression body;
        if (method.ReturnType != typeof(void))
        {
            body = Expression.Convert(
                    Expression.Call(
                        null,
                        changeTypeIfConvertible,
                        Expression.Call(callExpression, jsValueToObject),
                        Expression.Constant(method.ReturnType),
                        Expression.Constant(System.Globalization.CultureInfo.InvariantCulture, typeof(IFormatProvider))
                    ),
                    method.ReturnType);
        }
        else
        {
            body = callExpression;
        }

        var ownership = Expression.Variable(typeof(Engine.HostCallScope), "ownership");
        var guardedBody = Expression.Block(
            [ownership],
            Expression.Assign(
                ownership,
                Expression.Call(
                    targetEngine,
                    enterHostCallback,
                    Expression.Call(
                        targetEngine,
                        getHostCallbackOwner,
                        Expression.Convert(targetExpression, typeof(ObjectInstance))))),
            Expression.TryFinally(body, Expression.Call(ownership, exitHostCallback)));

        return Expression.Lambda(
            type,
            guardedBody,
            new ReadOnlyCollection<ParameterExpression>(parameters));
    }

    [return: NotNullIfNotNull(nameof(value))]
    private static object? ChangeTypeOnlyIfConvertible(object? value, Type conversionType, IFormatProvider? provider)
    {
        if (conversionType == taskType)
        {
            return Task.CompletedTask;
        }

#if !NETFRAMEWORK && !NETSTANDARD2_0
        if (conversionType == valueTaskType)
        {
            return default(ValueTask);
        }
#endif

        if (conversionType.IsGenericType && conversionType.GetGenericTypeDefinition() == genTaskType)
        {
            var key = new TypeConversionKey(conversionType.GetGenericArguments()[0], genTaskType);
            var fromResultMethod = _knownFromResultGenerics.GetOrAdd(key, GetFromResultMethod);
            if (fromResultMethod != null)
            {
                return fromResultMethod.Invoke(null, [value]);
            }
        }

#if NET8_0_OR_GREATER
        if (conversionType.IsGenericType && conversionType.GetGenericTypeDefinition() == genValueTaskType)
        {
            var key = new TypeConversionKey(conversionType.GetGenericArguments()[0], genValueTaskType);
            var fromResultMethod = _knownFromResultGenerics.GetOrAdd(key, GetFromResultMethod);
            if (fromResultMethod != null)
            {
                return fromResultMethod.Invoke(null, [value]);
            }
        }
#endif

        if (value == null || value is IConvertible)
            return System.Convert.ChangeType(value, conversionType, provider);

        return value;
    }

    private static MethodInfo? GetFromResultMethod(TypeConversionKey key)
    {
        var (target, taskType) = key;
#if NET8_0_OR_GREATER
        if (taskType == genValueTaskType)
        {
            return valueTaskFromResultInfo.MakeGenericMethod(target);
        }
#endif
        return taskFromResultInfo.MakeGenericMethod(target);
    }

    private static bool TryCastWithOperators(object value, Type type, Type valueType, [NotNullWhen(true)] out object? converted)
    {
        var key = new TypeConversionKey(valueType, type);

        static MethodInfo? CreateValueFactory(TypeConversionKey k)
        {
            var (source, target) = k;
            foreach (var m in source.GetOperatorOverloadMethods().Concat(target.GetOperatorOverloadMethods()))
            {
                if (!target.IsAssignableFrom(m.ReturnType) || m.Name is not ("op_Implicit" or "op_Explicit"))
                {
                    continue;
                }

                var parameters = m.GetParameters();
                if (parameters.Length != 1 || !parameters[0].ParameterType.IsAssignableFrom(source))
                {
                    continue;
                }

                // we found a match
                return m;
            }

            return null;
        }

        var castOperator = _knownCastOperators.GetOrAdd(key, CreateValueFactory);

        if (castOperator != null)
        {
            try
            {
                converted = castOperator.Invoke(null, [value]);
                return converted is not null;
            }
            catch
            {
                converted = null;
                return false;
            }
        }

        converted = null;
        return false;
    }

}
