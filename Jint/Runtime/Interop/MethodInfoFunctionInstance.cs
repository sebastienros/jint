using System.Collections.Generic;
using System.Globalization;
using System.Linq.Expressions;
using System.Reflection;
using Jint.Native;
using Jint.Native.Function;

namespace Jint.Runtime.Interop
{
    internal sealed class MethodInfoFunctionInstance : FunctionInstance
    {
        private static readonly JsString _name = new JsString("Function");
        private readonly MethodDescriptor[] _methods;

        public MethodInfoFunctionInstance(Engine engine, MethodDescriptor[] methods)
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
                    var jsArgumentsTemp = new List<JsValue>() { thisObject };
                    jsArgumentsTemp.AddRange(jsArguments);
                    jsArguments = jsArgumentsTemp.ToArray();
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

                if (parameters == null || parameters.Length != arguments.Length)
                {
                    parameters = new object[arguments.Length];
                }
                var methodParameters = method.Parameters;
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
                    return FromObject(Engine, method.Method.Invoke(thisObject.ToObject(), parameters));
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
