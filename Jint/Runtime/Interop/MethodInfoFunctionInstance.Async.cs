using System;
using System.Globalization;
using System.Linq.Expressions;
using System.Reflection;
using Jint.Native;
using Jint.Native.Function;
using System.Threading.Tasks;

namespace Jint.Runtime.Interop
{
    internal sealed partial class MethodInfoFunctionInstance : FunctionInstance
    {
        public async override Task<JsValue> CallAsync(JsValue thisObject, JsValue[] jsArguments)
        {
            JsValue[] ArgumentProvider(MethodDescriptor method)
            {
                if (method.IsExtensionMethod)
                {
                    var jsArgumentsTemp = new JsValue[1 + jsArguments.Length];
                    jsArgumentsTemp[0] = thisObject;
                    Array.Copy(jsArguments, 0, jsArgumentsTemp, 1, jsArguments.Length);
                    jsArguments = jsArgumentsTemp;
                }
                return method.HasParams
                    ? ProcessParamsArrays(jsArguments, method)
                    : jsArguments;
            }

            var converter = Engine.ClrTypeConverter;

            object[] parameters = null;
            foreach (var tuple in TypeConverter.FindBestMatch(_engine, _methods, ArgumentProvider))
            {
                var method = tuple.Item1;
                var arguments = tuple.Item2;
                var methodParameters = method.Parameters;

                if (parameters == null || parameters.Length != methodParameters.Length)
                {
                    parameters = new object[methodParameters.Length];
                }
                var argumentsMatch = true;

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
                        if (!converter.TryConvert(argument.ToObject(), parameterType, CultureInfo.InvariantCulture, out parameters[i]))
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

                // Store and leave the current execution context while performing an async operation
                var saveLexEnv = _engine.ExecutionContext.LexicalEnvironment;
                var saveVarEnv = _engine.ExecutionContext.VariableEnvironment;
                _engine.LeaveExecutionContext();
                try
                {
                    var result = await method.Method.Invoke(thisObject.ToObject(), parameters).AwaitWhenAsyncResult();
                    return FromObject(Engine, result);
                }
                catch (TargetInvocationException exception)
                {
                    ExceptionHelper.ThrowMeaningfulException(_engine, exception);
                }
                finally
                {
                    // Return to the original context when continuing
                    _engine.EnterExecutionContext(saveLexEnv, saveVarEnv);
                }
            }
            return ExceptionHelper.ThrowTypeError<JsValue>(_engine, "No public methods with the specified arguments were found.");
        }
    }
}