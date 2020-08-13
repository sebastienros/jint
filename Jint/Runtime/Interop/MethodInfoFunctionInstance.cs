using System.Globalization;
using System.Linq.Expressions;
using System.Reflection;
using System.Threading.Tasks;
using Jint.Native;
using Jint.Native.Function;

namespace Jint.Runtime.Interop
{
    public sealed class MethodInfoFunctionInstance : FunctionInstance
    {
        private static readonly JsString _name = new JsString("Function");
        private readonly MethodInfo[] _methods;

        public MethodInfoFunctionInstance(Engine engine, MethodInfo[] methods)
            : base(engine, _name)
        {
            _methods = methods;
            _prototype = engine.Function.PrototypeObject;
        }

        public override JsValue Call(JsValue thisObject, JsValue[] arguments)
        {
            return Invoke(_methods, thisObject, arguments);
        }

        public JsValue Invoke(MethodInfo[] methodInfos, JsValue thisObject, JsValue[] jsArguments)
        {
            JsValue[] ArgumentProvider(MethodInfo method, bool hasParams) =>
                hasParams
                    ? ProcessParamsArrays(jsArguments, method)
                    : jsArguments;

            var converter = Engine.ClrTypeConverter;

            foreach (var tuple in TypeConverter.FindBestMatch(_engine, methodInfos, ArgumentProvider))
            {
                var method = tuple.Item1;
                var arguments = tuple.Item2;

                var parameters = new object[arguments.Length];
                var methodParameters = method.GetParameters();
                var argumentsMatch = true;

                for (var i = 0; i < arguments.Length; i++)
                {
                    var parameterType = methodParameters[i].ParameterType;

                    if (typeof(JsValue).IsAssignableFrom(parameterType))
                    {
                        parameters[i] = arguments[i];
                    }
                    else if (parameterType == typeof(JsValue[]) && arguments[i].IsArray())
                    {
                        // Handle specific case of F(params JsValue[])

                        var arrayInstance = arguments[i].AsArray();
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
                        if (!converter.TryConvert(arguments[i].ToObject(), parameterType, CultureInfo.InvariantCulture, out parameters[i]))
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
                    return FromObject(Engine, method.Invoke(thisObject.ToObject(), parameters));
                }
                catch (TargetInvocationException exception)
                {
                    ExceptionHelper.ThrowMeaningfulException(_engine, exception);
                }
            }

            return ExceptionHelper.ThrowTypeError<JsValue>(_engine, "No public methods with the specified arguments were found.");
        }

        public override Task<JsValue> CallAsync(JsValue thisObject, JsValue[] arguments)
        {
            return InvokeAsync(_methods, thisObject, arguments);
        }

        public async Task<JsValue> InvokeAsync(MethodInfo[] methodInfos, JsValue thisObject, JsValue[] jsArguments)
        {
            JsValue[] ArgumentProvider(MethodInfo method, bool hasParams) =>
                hasParams
                    ? ProcessParamsArrays(jsArguments, method)
                    : jsArguments;

            var converter = Engine.ClrTypeConverter;

            foreach (var tuple in TypeConverter.FindBestMatch(_engine, methodInfos, ArgumentProvider))
            {
                var method = tuple.Item1;
                var arguments = tuple.Item2;

                var parameters = new object[arguments.Length];
                var methodParameters = method.GetParameters();
                var argumentsMatch = true;

                for (var i = 0; i < arguments.Length; i++)
                {
                    var parameterType = methodParameters[i].ParameterType;

                    if (typeof(JsValue).IsAssignableFrom(parameterType))
                    {
                        parameters[i] = arguments[i];
                    }
                    else if (parameterType == typeof(JsValue[]) && arguments[i].IsArray())
                    {
                        // Handle specific case of F(params JsValue[])

                        var arrayInstance = arguments[i].AsArray();
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
                        if (!converter.TryConvert(arguments[i].ToObject(), parameterType, CultureInfo.InvariantCulture, out parameters[i]))
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
                    // Store and leave the current execution context while performing an async operation
                    var saveLexEnv = _engine.ExecutionContext.LexicalEnvironment;
                    var saveVarEnv = _engine.ExecutionContext.VariableEnvironment;
                    _engine.LeaveExecutionContext();
                    try {
                        var result = await method.Invoke(thisObject.ToObject(), parameters).AwaitWhenAsyncResult();
                        return FromObject(Engine, result);
                    } finally {
                        // Return to the original context when continuing
                        _engine.EnterExecutionContext(saveLexEnv, saveVarEnv);
                    }
                }
                catch (TargetInvocationException exception)
                {
                    ExceptionHelper.ThrowMeaningfulException(_engine, exception);
                }
            }

            return ExceptionHelper.ThrowTypeError<JsValue>(_engine, "No public methods with the specified arguments were found.");
        }

        /// <summary>
        /// Reduces a flat list of parameters to a params array, if needed
        /// </summary>
        private JsValue[] ProcessParamsArrays(JsValue[] jsArguments, MethodInfo methodInfo)
        {
            var parameters = methodInfo.GetParameters();

            var nonParamsArgumentsCount = parameters.Length - 1;
            if (jsArguments.Length < nonParamsArgumentsCount)
                return jsArguments;

            var argsToTransform = jsArguments.Skip(nonParamsArgumentsCount);

            if (argsToTransform.Length == 1 && argsToTransform[0].IsArray())
                return jsArguments;

            var jsArray = Engine.Array.Construct(Arguments.Empty);
            Engine.Array.PrototypeObject.Push(jsArray, argsToTransform);

            var newArgumentsCollection = new JsValue[nonParamsArgumentsCount + 1];
            for (var j = 0; j < nonParamsArgumentsCount; ++j)
            {
                newArgumentsCollection[j] = jsArguments[j];
            }

            newArgumentsCollection[nonParamsArgumentsCount] = jsArray;
            return newArgumentsCollection;
        }
    }
}
