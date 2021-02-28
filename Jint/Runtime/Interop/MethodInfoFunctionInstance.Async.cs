using Jint.Native;
using Jint.Native.Function;
using System.Globalization;
using System.Linq.Expressions;
using System.Reflection;
using System.Threading.Tasks;

namespace Jint.Runtime.Interop
{
    public sealed partial class MethodInfoFunctionInstance : FunctionInstance
    {
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
                    try
                    {
                        var result = await method.Invoke(thisObject.ToObject(), parameters).AwaitWhenAsyncResult();
                        return FromObject(Engine, result);
                    }
                    finally
                    {
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
    }
}