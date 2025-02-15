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
    private readonly ClrFunction? _fallbackClrFunctionInstance;

    public MethodInfoFunction(
        Engine engine,
        Type targetType,
        object? target,
        string name,
        MethodDescriptor[] methods,
        ClrFunction? fallbackClrFunctionInstance = null)
        : base(engine, engine.Realm, new JsString(name))
    {
        _targetType = targetType;
        _target = target;
        _name = name;
        _methods = methods;
        _fallbackClrFunctionInstance = fallbackClrFunctionInstance;
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

    protected internal override JsValue Call(JsValue thisObject, JsCallArguments jsArguments)
    {
        JsValue[] ArgumentProvider(MethodDescriptor method)
        {
            if (method.IsExtensionMethod)
            {
                var jsArgumentsTemp = new JsValue[1 + jsArguments.Length];
                jsArgumentsTemp[0] = thisObject;
                Array.Copy(jsArguments, 0, jsArgumentsTemp, 1, jsArguments.Length);
                return method.HasParams
                    ? ProcessParamsArrays(jsArgumentsTemp, method)
                    : jsArgumentsTemp;
            }

            return method.HasParams
                ? ProcessParamsArrays(jsArguments, method)
                : jsArguments;
        }

        var converter = Engine.TypeConverter;
        var thisObj = thisObject.ToObject() ?? _target;
        object?[]? parameters = null;
        foreach (var (method, arguments, _) in InteropHelper.FindBestMatch(_engine, _methods, ArgumentProvider))
        {
            var methodParameters = method.Parameters;
            if (parameters == null || parameters.Length != methodParameters.Length)
            {
                parameters = new object[methodParameters.Length];
            }

            var argumentsMatch = true;
            var resolvedMethod = ResolveMethod(method.Method, methodParameters, arguments);
            // TPC: if we're concerned about cost of MethodInfo.GetParameters() - we could only invoke it if this ends up being a generic method (i.e. they will be different in that scenario)
            methodParameters = resolvedMethod.GetParameters();
            for (var i = 0; i < parameters.Length; i++)
            {
                var methodParameter = methodParameters[i];
                var parameterType = methodParameter.ParameterType;
                var argument = arguments.Length > i ? arguments[i] : null;

                if (typeof(JsValue).IsAssignableFrom(parameterType))
                {
                    parameters[i] = argument;
                }
                else if (argument is null)
                {
                    // optional
                    parameters[i] = System.Type.Missing;
                }
                else if (IsGenericParameter(argument.ToObject(), parameterType)) // don't think we need the condition preface of (argument == null) because of earlier condition
                {
                    parameters[i] = argument.ToObject();
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
                else
                {
                    if (!ReflectionExtensions.TryConvertViaTypeCoercion(parameterType, _engine.Options.Interop.ValueCoercion, argument, out parameters[i])
                        && !converter.TryConvert(argument.ToObject(), parameterType, CultureInfo.InvariantCulture, out parameters[i]))
                    {
                        argumentsMatch = false;
                        break;
                    }

                    if (parameters[i] is LambdaExpression lambdaExpression)
                    {
                        parameters[i] = lambdaExpression.Compile();
                    }
                }
            }

            if (!argumentsMatch)
            {
                continue;
            }

            // todo: cache method info
            try
            {
                if (method.Method is MethodInfo { IsGenericMethodDefinition: true })
                {
                    var result = resolvedMethod.Invoke(thisObj, parameters);
                    return FromObjectWithType(Engine, result, type: (resolvedMethod as MethodInfo)?.ReturnType);
                }

                return FromObjectWithType(Engine, method.Method.Invoke(thisObj, parameters), type: (method.Method as MethodInfo)?.ReturnType);
            }
            catch (TargetInvocationException exception)
            {
                ExceptionHelper.ThrowMeaningfulException(_engine, exception);
            }
        }

        if (_fallbackClrFunctionInstance is not null)
        {
            return _fallbackClrFunctionInstance.Call(thisObject, jsArguments);
        }

        ExceptionHelper.ThrowTypeError(_engine.Realm, "No public methods with the specified arguments were found.");
        return null;
    }

    /// <summary>
    /// Reduces a flat list of parameters to a params array, if needed
    /// </summary>
    private JsValue[] ProcessParamsArrays(JsCallArguments arguments, MethodDescriptor methodInfo)
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

        var jsArray = new JsArray(_engine, argsToTransform);
        var newArgumentsCollection = new JsValue[nonParamsArgumentsCount + 1];
        for (var j = 0; j < nonParamsArgumentsCount; ++j)
        {
            newArgumentsCollection[j] = arguments[j];
        }

        newArgumentsCollection[nonParamsArgumentsCount] = jsArray;
        return newArgumentsCollection;
    }

    public override string ToString()
    {
        return $"function {_targetType}.{_name}() {{ [native code] }}";
    }
}
