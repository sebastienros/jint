using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Linq.Expressions;
using System.Reflection;
using Jint.Extensions;
using Jint.Native;
using Jint.Native.Function;

#pragma warning disable IL2067
#pragma warning disable IL2072
#pragma warning disable IL3050

namespace Jint.Runtime.Interop;

internal sealed class MethodInfoFunction : Function
{
    private readonly Type _targetType;
    private readonly object? _target;
    private readonly string _name;
    private readonly MethodDescriptor[] _methods;
    private readonly Function? _fallbackFunctionInstance;

    public MethodInfoFunction(
        Engine engine,
        Type targetType,
        object? target,
        string name,
        MethodDescriptor[] methods,
        Function? fallbackFunctionInstance = null)
        : base(engine, engine.Realm, new JsString(name))
    {
        _targetType = targetType;
        _target = target;
        _name = name;
        _methods = methods;
        _fallbackFunctionInstance = fallbackFunctionInstance;
        _prototype = engine.Realm.Intrinsics.Function.PrototypeObject;
    }

    private static bool IsGenericParameter(object? argObj, Type parameterType)
    {
        if (argObj is null)
        {
            return false;
        }

        var result = InteropHelper.IsAssignableToGenericType(argObj.GetType(), parameterType);
        if (result.Score < 0)
        {
            return false;
        }

        if (parameterType.IsGenericParameter || parameterType.IsGenericType)
        {
            // For fully concrete generic types (no open type parameters), verify the argument
            // is actually assignable to prevent incorrect direct assignment when type arguments
            // differ (e.g., object[] should not be directly assigned to IList<string>)
            if (!parameterType.ContainsGenericParameters && !parameterType.IsAssignableFrom(argObj.GetType()))
            {
                return false;
            }
            return true;
        }
        return false;
    }

    private static void HandleGenericParameter(object? argObj, Type parameterType, Type[] genericArgTypes)
    {
        if (argObj is null)
        {
            return;
        }

        var result = InteropHelper.IsAssignableToGenericType(argObj.GetType(), parameterType);
        if (result.Score < 0)
        {
            return;
        }

        if (parameterType.IsGenericParameter)
        {
            var genericParamPosition = parameterType.GenericParameterPosition;
            if (genericParamPosition >= 0)
            {
                genericArgTypes[genericParamPosition] = argObj.GetType();
            }
        }
        else if (parameterType.IsGenericType)
        {
            // TPC: maybe we can pull the generic parameters from the arguments?
            var genericArgs = parameterType.GetGenericArguments();
            for (int j = 0; j < genericArgs.Length; ++j)
            {
                var genericArg = genericArgs[j];
                if (genericArg.IsGenericParameter)
                {
                    var genericParamPosition = genericArg.GenericParameterPosition;
                    if (genericParamPosition >= 0)
                    {
                        var givenTypeGenericArgs = result.MatchingGivenType.GetGenericArguments();
                        genericArgTypes[genericParamPosition] = givenTypeGenericArgs[j];
                    }
                }
            }
        }
        else
        {
            return;
        }
    }

    private static MethodBase ResolveMethod(MethodBase method, ParameterInfo[] methodParameters, JsCallArguments arguments)
    {
        if (!method.IsGenericMethod)
        {
            return method;
        }
        if (!method.IsGenericMethodDefinition)
        {
            return method;
        }
        var methodInfo = method as MethodInfo;
        if (methodInfo == null)
        {
            // probably should issue at least a warning here
            return method;
        }

        // TPC: we could also && "(method.Method.IsGenericMethodDefinition)" because we won't create a generic method if that isn't the case
        var methodGenericArgs = method.GetGenericArguments();
        var genericArgTypes = new Type[methodGenericArgs.Length];

        for (var i = 0; i < methodParameters.Length; ++i)
        {
            var methodParameter = methodParameters[i];
            var parameterType = methodParameter.ParameterType;
            var argObj = i < arguments.Length ? arguments[i].ToObject() : typeof(object);
            HandleGenericParameter(argObj, parameterType, genericArgTypes);
        }

        for (int i = 0; i < genericArgTypes.Length; ++i)
        {
            if (genericArgTypes[i] == null)
            {
                // this is how we're dealing with things like "void" return types - you can't use "void" as a type:
                genericArgTypes[i] = typeof(object);
            }
        }

        var genericMethodInfo = methodInfo.MakeGenericMethod(genericArgTypes);
        return genericMethodInfo;
    }

    private readonly record struct MethodResolverState(Engine Engine, JsValue This, JsCallArguments Arguments);

    protected internal override JsValue Call(JsValue thisObject, JsCallArguments jsArguments)
    {
        var converter = Engine.TypeConverter;
        var thisObj = thisObject.ToObject() ?? _target;
        var state = new MethodResolverState(_engine, thisObject, jsArguments);

        if (_methods.Length == 1)
        {
            // single candidate, no overload resolution needed - bind directly and skip scoring
            var method = _methods[0];
            var parameterInfos = method.Parameters;
            var arguments = ArgumentProvider(method, state);
            if (arguments.Length <= parameterInfos.Length
                && arguments.Length >= parameterInfos.Length - method.ParameterDefaultValuesCount
                && CanBindNullArguments(parameterInfos, arguments)
                && TryCall(method, arguments, thisObj, converter, out var fastResult))
            {
                return fastResult;
            }
        }
        else
        {
            foreach (var (method, arguments, _) in InteropHelper.FindBestMatch(_engine, _methods, static (method, state) => ArgumentProvider(method, state), state))
            {
                if (TryCall(method, arguments, thisObj, converter, out var result))
                {
                    return result;
                }
            }
        }

        if (_fallbackFunctionInstance is not null)
        {
            return _fallbackFunctionInstance.Call(thisObject, jsArguments);
        }

        var message = _engine.Options.Interop.ExposeDetailedResolutionErrors
            ? InteropErrorHelper.CreateNoMatchingMethodMessage(_targetType, _name, jsArguments, _methods)
            : "No public methods with the specified arguments were found.";
        Throw.InteropResolutionError(_engine.Realm, message, _targetType, _name, jsArguments, _methods);
        return null;
    }

    private static JsCallArguments ArgumentProvider(MethodDescriptor method, in MethodResolverState state)
    {
        if (method.IsExtensionMethod)
        {
            var jsArgumentsTemp = new JsValue[1 + state.Arguments.Length];
            jsArgumentsTemp[0] = state.This;
            Array.Copy(state.Arguments, 0, jsArgumentsTemp, 1, state.Arguments.Length);
            return method.HasParams
                ? ProcessParamsArrays(state.Engine, method, jsArgumentsTemp)
                : jsArgumentsTemp;
        }

        return method.HasParams
            ? ProcessParamsArrays(state.Engine, method, state.Arguments)
            : state.Arguments;
    }

    /// <summary>
    /// Mirrors the null/undefined rejection rule of overload scoring: null cannot bind to a
    /// non-optional parameter of non-nullable value type (even when value coercion could
    /// otherwise produce a value).
    /// </summary>
    private static bool CanBindNullArguments(ParameterInfo[] parameterInfos, JsCallArguments arguments)
    {
        for (var i = 0; i < arguments.Length; i++)
        {
            if (arguments[i].IsNullOrUndefined())
            {
                var parameter = parameterInfos[i];
                if (!parameter.IsOptional && !InteropHelper.TypeIsNullable(parameter.ParameterType))
                {
                    return false;
                }
            }
        }

        return true;
    }

    private bool TryCall(
        MethodDescriptor method,
        JsCallArguments arguments,
        object? thisObj,
        ITypeConverter converter,
        [NotNullWhen(true)] out JsValue? callResult)
    {
        callResult = null;

        var methodParameters = method.Parameters;
        var resolvedMethod = ResolveMethod(method.Method, methodParameters, arguments);
        // We only need to call GetParameters it if this ends up being a generic method (i.e. they will be different in that scenario)
        var isGenericDefinition = false;
        if (resolvedMethod.IsGenericMethod)
        {
            methodParameters = resolvedMethod.GetParameters();
            isGenericDefinition = method.Method is MethodInfo { IsGenericMethodDefinition: true };
        }

        // NOTE: pooling this buffer via ArrayPool was measured slower than allocation (tiny
        // gen0 arrays), keep the plain exact-size allocation
        var parameterCount = methodParameters.Length;
        object?[] parameters = parameterCount == 0 ? [] : new object[parameterCount];

        for (var i = 0; i < parameterCount; i++)
        {
            var methodParameter = methodParameters[i];
            var parameterType = methodParameter.ParameterType;
            var argument = arguments.Length > i ? arguments[i] : null;
            object? argumentObject = null;

            if (typeof(JsValue).IsAssignableFrom(parameterType))
            {
                parameters[i] = argument;
            }
            else if (argument is null)
            {
                // optional
                parameters[i] = System.Type.Missing;
            }
            else if (IsGenericParameter(argumentObject = argument.ToObject(), parameterType)) // don't think we need the condition preface of (argument == null) because of earlier condition
            {
                // ^ this is the best way I could think of to only eval argument.ToObject() when needed.
                // But it's very cursed, I'm sorry.
                parameters[i] = argumentObject;
            }
            else if (parameterType == typeof(JsValue[]) && argument.IsArray())
            {
                // Handle specific case of F(params JsValue[])
                var arrayInstance = argument.AsArray();
                var len = TypeConverter.ToInt32(arrayInstance.Get(CommonProperties.Length, this));
                var result = new JsValue[len];
                for (uint k = 0; k < len; k++)
                {
                    result[k] = arrayInstance.TryGetValue(k, out var value) ? value : Undefined;
                }

                parameters[i] = result;
            }
            else if (argument is JsNumber jsNumber && InteropHelper.TryConvertNumberFast(jsNumber._value, parameterType, out parameters[i]))
            {
                // common numeric argument converted without the generic converter
            }
            else
            {
                if (!ReflectionExtensions.TryConvertViaTypeCoercion(parameterType, _engine.Options.Interop.ValueCoercion, argument, out parameters[i])
                    && !converter.TryConvert(argumentObject, parameterType, CultureInfo.InvariantCulture, out parameters[i]))
                {
                    // arguments don't match this method
                    return false;
                }

                if (parameters[i] is LambdaExpression lambdaExpression)
                {
                    parameters[i] = lambdaExpression.Compile();
                }
            }
        }

        try
        {
            if (isGenericDefinition)
            {
                // the resolved generic method differs per call, cannot use the cached invoker
                var result = resolvedMethod.Invoke(thisObj, parameters);
                // conversion before the check so an awaitable result gets its continuation attached
                callResult = FromObjectWithType(Engine, result, type: (resolvedMethod as MethodInfo)?.ReturnType);
                _engine.CheckAmortizedConstraintsAtHostBoundary();
                return true;
            }

            var invokeResult = method.Invoke(thisObj, parameters);
            callResult = FromObjectWithType(Engine, invokeResult, type: (method.Method as MethodInfo)?.ReturnType);
            _engine.CheckAmortizedConstraintsAtHostBoundary();
            return true;
        }
        catch (TargetInvocationException exception)
        {
            _engine.CheckAmortizedConstraintsAtHostBoundary();
            Throw.MeaningfulException(_engine, exception);
            return false;
        }
    }

    /// <summary>
    /// Reduces a flat list of parameters to a params array, if needed
    /// </summary>
    private static JsCallArguments ProcessParamsArrays(Engine engine, MethodDescriptor methodInfo, JsCallArguments arguments)
    {
        var parameters = methodInfo.Parameters;

        var nonParamsArgumentsCount = parameters.Length - 1;
        if (arguments.Length < nonParamsArgumentsCount)
        {
            return arguments;
        }

        var argsToTransform = arguments.Skip(nonParamsArgumentsCount);

        if (argsToTransform.Length == 1 && argsToTransform[0].IsArray())
        {
            return arguments;
        }

        var array = new JsArray(engine, argsToTransform);
        var newArguments = new JsValue[nonParamsArgumentsCount + 1];
        for (var j = 0; j < nonParamsArgumentsCount; ++j)
        {
            newArguments[j] = arguments[j];
        }

        newArguments[nonParamsArgumentsCount] = array;
        return newArguments;
    }

    public override string ToString()
    {
        return $"function {_targetType}.{_name}() {{ [native code] }}";
    }
}
