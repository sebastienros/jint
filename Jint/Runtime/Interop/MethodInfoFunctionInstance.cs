using System;
using System.Globalization;
using System.Linq.Expressions;
using System.Reflection;
using Jint.Native;
using Jint.Native.Function;

namespace Jint.Runtime.Interop
{
    public sealed class MethodInfoFunctionInstance : FunctionInstance
    {
        private readonly MethodInfo[] _methods;

        public MethodInfoFunctionInstance(Engine engine, MethodInfo[] methods)
            : base(engine, null, null, false)
        {
            _methods = methods;
            Prototype = engine.Function.PrototypeObject;
        }

        public override JsValue Call(JsValue thisObject, JsValue[] arguments)
        {
            return Invoke(_methods, thisObject, arguments);
        }

        public JsValue Invoke(MethodInfo[] methodInfos, JsValue thisObject, JsValue[] jsArguments)
        {
            var arguments = ProcessParamsArrays(jsArguments, methodInfos);
            var converter = Engine.ClrTypeConverter;

            foreach (var method in TypeConverter.FindBestMatch(Engine, methodInfos, arguments))
            {
                var parameters = new object[arguments.Length];
                var argumentsMatch = true;

                for (var i = 0; i < arguments.Length; i++)
                {
                    var parameterType = method.GetParameters()[i].ParameterType;

                    if (typeof(JsValue).IsAssignableFrom(parameterType))
                    {
                        parameters[i] = arguments[i];
                    }
                    else if (parameterType == typeof(JsValue[]) && arguments[i].IsArray())
                    {
                        // Handle specific case of F(params JsValue[])

                        var arrayInstance = arguments[i].AsArray();
                        var len = TypeConverter.ToInt32(arrayInstance.Get("length"));
                        var result = new JsValue[len];
                        for (uint k = 0; k < len; k++)
                        {
                            result[k] = arrayInstance.TryGetValue(k, out var value) ? value : JsValue.Undefined;
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

                        var lambdaExpression = parameters[i] as LambdaExpression;
                        if (lambdaExpression != null)
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
                    return JsValue.FromObject(Engine, method.Invoke(thisObject.ToObject(), parameters));
                }
                catch (TargetInvocationException exception)
                {
                    var meaningfulException = exception.InnerException ?? exception;
                    var handler = Engine.Options._ClrExceptionsHandler;

                    if (handler != null && handler(meaningfulException))
                    {
                        throw new JavaScriptException(Engine.Error, meaningfulException.Message);
                    }

                    throw meaningfulException;
                }
            }

            throw new JavaScriptException(Engine.TypeError, "No public methods with the specified arguments were found.");
        }

        /// <summary>
        /// Reduces a flat list of parameters to a params array
        /// </summary>
        private JsValue[] ProcessParamsArrays(JsValue[] jsArguments, MethodInfo[] methodInfos)
        {
            for (var i = 0; i < methodInfos.Length; i++)
            {
                var methodInfo = methodInfos[i];
                var parameters = methodInfo.GetParameters();

                bool hasParamArrayAttribute = false;
                for (int j = 0; j < parameters.Length; ++j)
                {
                    if (parameters[j].HasAttribute<ParamArrayAttribute>())
                    {
                        hasParamArrayAttribute = true;
                        break;
                    }
                }
                if (!hasParamArrayAttribute)
                    continue;

                var nonParamsArgumentsCount = parameters.Length - 1;
                if (jsArguments.Length < nonParamsArgumentsCount)
                    continue;

                var argsToTransform = jsArguments.Skip(nonParamsArgumentsCount);

                if (argsToTransform.Length == 1 && argsToTransform[0].IsArray())
                    continue;

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

            return jsArguments;
        }

    }
}
