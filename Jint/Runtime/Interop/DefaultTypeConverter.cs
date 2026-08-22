using System.Collections;
using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;
using Jint.Extensions;
using Jint.Native;
using Jint.Native.Function;
using Jint.Native.Object;
using Expression = System.Linq.Expressions.Expression;

#pragma warning disable IL2026
#pragma warning disable IL2062
#pragma warning disable IL2067
#pragma warning disable IL2070
#pragma warning disable IL2072
#pragma warning disable IL3050

namespace Jint.Runtime.Interop;

public class DefaultTypeConverter : ITypeConverter
{
    private readonly Engine _engine;

    private readonly record struct TypeConversionKey(Type Source, Type Target);

    private static readonly ConcurrentDictionary<TypeConversionKey, MethodInfo?> _knownCastOperators = new();
    private static readonly ConcurrentDictionary<TypeConversionKey, MethodInfo?> _knownFromResultGenerics = new();

    private static readonly Type intType = typeof(int);
    private static readonly Type iCallableType = typeof(JsCallDelegate);
    private static readonly Type jsValueType = typeof(JsValue);
    private static readonly Type objectType = typeof(object);
    private static readonly Type engineType = typeof(Engine);
    private static readonly Type taskType = typeof(Task);
    private static readonly Type genTaskType = typeof(Task<>);
    private static readonly MethodInfo taskFromResultInfo = taskType.GetMethod("FromResult")!;
#if !NETFRAMEWORK && !NETSTANDARD2_0
    private static readonly Type valueTaskType = typeof(ValueTask);
    private static readonly Type genValueTaskType = typeof(ValueTask<>);
    private static readonly MethodInfo valueTaskFromResultInfo = valueTaskType.GetMethod("FromResult")!;
#endif

    private static readonly MethodInfo changeTypeIfConvertible = typeof(DefaultTypeConverter).GetMethod(
        nameof(ChangeTypeOnlyIfConvertible), BindingFlags.NonPublic | BindingFlags.Static)!;
    private static readonly MethodInfo jsValueFromObject = jsValueType.GetMethod(nameof(JsValue.FromObject))!;
    private static readonly MethodInfo enterHostCallback = engineType.GetMethod(nameof(Engine.EnterTransferredHostCallback), BindingFlags.Instance | BindingFlags.NonPublic)!;
    private static readonly MethodInfo getHostCallbackOwner = engineType.GetMethod(nameof(Engine.GetHostCallbackAuthorization), BindingFlags.Instance | BindingFlags.NonPublic)!;
    private static readonly MethodInfo exitHostCallback = typeof(Engine.HostCallScope).GetMethod(nameof(IDisposable.Dispose))!;
    private static readonly MethodInfo jsValueToObject = jsValueType.GetMethod(nameof(JsValue.ToObject))!;


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
    public virtual object? Convert(
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
    public virtual bool TryConvert(
        object? value,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.PublicFields)] Type type,
        IFormatProvider formatProvider,
        [NotNullWhen(true)] out object? converted)
    {
        return TryConvertInternal(value, type, formatProvider, propagateException: false, out converted, out _);
    }

    private static readonly ConditionalWeakTable<IFunction, Func<object, Delegate>> _targetBinderDelegateCache = new();
    private static readonly ConditionalWeakTable<object, Delegate> _boundTargetDelegateCache = new();
    private static readonly ConditionalWeakTable<Delegate, ObjectInstance> _hostCallbackDelegates = new();

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
                        targetList.Add(item is null ? null : Convert(item, elementType, formatProvider));
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
                        innerList.Add(item is null ? null : Convert(item, elementType, formatProvider));
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
                // unregistering event handlers (see ShouldExecuteActionCallbackOnEventChanged)
                var d = functionInstance is not null ?
                    _boundTargetDelegateCache.GetValue(functionInstance!, target =>
                    {
                        var astFunction = (functionInstance as Function)?._functionDefinition?.Function;

                        // use a single builder per unique function AST
                        var targetBinder = astFunction is not null
                            ? _targetBinderDelegateCache.GetValue(astFunction, _ => BuildTargetBinderDelegate(type, func))
                            : BuildTargetBinderDelegate(type, func);

                        return targetBinder(target)!;
                    }) :
                    BuildDelegate(type, func, Expression.Constant(functionInstance, functionInstance!.GetType())).Compile();

                if (functionInstance is ObjectInstance callbackTarget)
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
                itemsConverted[i] = Convert(source[i], targetElementType, formatProvider);
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
                        targetDict[key] = Convert(sourceVal, targetValueType, formatProvider);
                    }
                }
            }
            else
            {
                var members = type.GetMembers();
                foreach (var member in members)
                {
                    // only use fields and properties
                    if (member.MemberType != MemberTypes.Property &&
                        member.MemberType != MemberTypes.Field)
                    {
                        continue;
                    }

                    if (typeDescriptor.TryGetDictionaryValue(value, member.Name, out var val)
                        || typeDescriptor.TryGetDictionaryValue(value, member.Name.UpperToLowerCamelCase(), out val))
                    {
                        var output = Convert(val, member.GetDefinedType(), formatProvider);
                        member.SetValue(obj, output);
                    }
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
            nameof(ObjectInstance.Engine));

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
                    var checkIndex = Expression.GreaterThanOrEqual(Expression.Property(param, nameof(Array.Length)), Expression.Constant(j));
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
