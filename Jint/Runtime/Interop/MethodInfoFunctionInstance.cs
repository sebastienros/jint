using System;
using System.Globalization;
using System.Linq.Expressions;
using System.Reflection;
using Jint.Native;
using Jint.Native.Function;

namespace Jint.Runtime.Interop
{
    internal sealed partial class MethodInfoFunctionInstance : FunctionInstance
    {
        private static readonly JsString _name = new JsString("Function");
        private readonly MethodDescriptor[] _methods;

        internal MethodInfoFunctionInstance(Engine engine, MethodDescriptor[] methods)
            : base(engine, _name)
        {
            _methods = methods;
            _prototype = engine.Function.PrototypeObject;
        }

        public override JsValue Call(JsValue thisObject, JsValue[] jsArguments)
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
                try
                {
                    var result = method.Method.Invoke(thisObject.ToObject(), parameters).AwaitWhenAsyncResult();
                    if (!result.IsCompleted) ExceptionHelper.ThrowError(_engine, "Cannot not await call to async method from a synchroneous context. The current async invocation is possibly executing synchroneously due to a missing code implementation on the async execution path (check call stack).");
                    return FromObject(Engine, result.Result);
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
        private JsValue[] ProcessParamsArrays(JsValue[] jsArguments, MethodDescriptor methodInfo)
        {
            var parameters = methodInfo.Parameters;

            var nonParamsArgumentsCount = parameters.Length - 1;
            if (jsArguments.Length < nonParamsArgumentsCount)
            {
                return jsArguments;
            }

            var argsToTransform = jsArguments.Skip(nonParamsArgumentsCount);

            if (argsToTransform.Length == 1 && argsToTransform[0].IsArray())
            {
                return jsArguments;
            }

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
