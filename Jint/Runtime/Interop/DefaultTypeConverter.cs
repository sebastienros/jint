using System;
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
using Expression = System.Linq.Expressions.Expression;

#pragma warning disable IL2026
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
#if NETSTANDARD2_1_OR_GREATER || NETCOREAPP
    private static readonly Type valueTaskType = typeof(ValueTask);
    private static readonly Type genValueTaskType = typeof(ValueTask<>);
    private static readonly MethodInfo valueTaskFromResultInfo = valueTaskType.GetMethod("FromResult")!;
#endif

    private static readonly MethodInfo changeTypeIfConvertible = typeof(DefaultTypeConverter).GetMethod(
        nameof(ChangeTypeOnlyIfConvertible), BindingFlags.NonPublic | BindingFlags.Static)!;
    private static readonly MethodInfo jsValueFromObject = jsValueType.GetMethod(nameof(JsValue.FromObject))!;
    private static readonly MethodInfo jsValueToObject = jsValueType.GetMethod(nameof(JsValue.ToObject))!;


    public DefaultTypeConverter(Engine engine)
    {
        _engine = engine;
    }

    public virtual object? Convert(
        object? value,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.PublicFields)] Type type,
        IFormatProvider formatProvider)
    {
        if (!TryConvert(value, type, formatProvider, propagateException: true, out var converted, out var problemMessage))
        {
            Throw.Error(_engine, problemMessage ?? $"Unable to convert {value} to type {type}");
        }
        return converted;
    }

    public virtual bool TryConvert(
        object? value,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.PublicFields)] Type type,
        IFormatProvider formatProvider,
        [NotNullWhen(true)] out object? converted)
    {
        return TryConvert(value, type, formatProvider, propagateException: false, out converted, out _);
    }

    private static readonly ConditionalWeakTable<IFunction, Func<object, Delegate>> _targetBinderDelegateCache = new();
    private static readonly ConditionalWeakTable<object, Delegate> _boundTargetDelegateCache = new();

    private bool TryConvert(
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

        if (type.IsGenericType)
        {
            var result = InteropHelper.IsAssignableToGenericType(value.GetType(), type);
            if (result.IsAssignable)
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

            var members = type.GetMembers();
            foreach (var member in members)
            {
                // only use fields an properties
                if (member.MemberType != MemberTypes.Property &&
                    member.MemberType != MemberTypes.Field)
                {
                    continue;
                }

                if (typeDescriptor.TryGetValue(value, member.Name, out var val)
                    || typeDescriptor.TryGetValue(value, member.Name.UpperToLowerCamelCase(), out val))
                {
                    var output = Convert(val, member.GetDefinedType(), formatProvider);
                    member.SetValue(obj, output);
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

#if NETSTANDARD2_1_OR_GREATER || NETCOREAPP
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

    private Func<object, Delegate> BuildTargetBinderDelegate(
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

    private LambdaExpression BuildDelegate(
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

        for (var i = 0; i < parameters.Length; i++)
        {
            var param = parameters[i];
            if (param.Type.IsValueType)
            {
                var boxing = Expression.Convert(param, objectType);
                initializers.Add(Expression.Call(null, jsValueFromObject, Expression.Constant(_engine, engineType), boxing));
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

                    initializers.Add(Expression.Call(null, jsValueFromObject, Expression.Constant(_engine, engineType), block));
                }
            }
            else
            {
                initializers.Add(Expression.Call(null, jsValueFromObject, Expression.Constant(_engine, engineType), param));
            }
        }

        var vars = Expression.NewArrayInit(jsValueType, initializers);

        var callExpression = Expression.Call(
            targetExpression,
            function.Method,
            Expression.Constant(JsValue.Undefined, jsValueType),
            vars);

        if (method.ReturnType != typeof(void))
        {
            return Expression.Lambda(
                type,
                Expression.Convert(
                    Expression.Call(
                        null,
                        changeTypeIfConvertible,
                        Expression.Call(callExpression, jsValueToObject),
                        Expression.Constant(method.ReturnType),
                        Expression.Constant(System.Globalization.CultureInfo.InvariantCulture, typeof(IFormatProvider))
                    ),
                    method.ReturnType
                ),
                new ReadOnlyCollection<ParameterExpression>(parameters));
        }

        return Expression.Lambda(
            type,
            callExpression,
            new ReadOnlyCollection<ParameterExpression>(parameters));
    }

    [return: NotNullIfNotNull(nameof(value))]
    private static object? ChangeTypeOnlyIfConvertible(object? value, Type conversionType, IFormatProvider? provider)
    {
        if (conversionType == taskType)
        {
            return Task.CompletedTask;
        }

#if NETSTANDARD2_1_OR_GREATER || NETCOREAPP
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

#if NETCOREAPP
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
#if NETCOREAPP
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
