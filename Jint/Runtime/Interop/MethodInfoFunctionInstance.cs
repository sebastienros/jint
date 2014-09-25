using System.Globalization;
using System.Linq;
using System.Reflection;
using Jint.Native;
using Jint.Native.Function;

namespace Jint.Runtime.Interop
{
    using System;

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

        public JsValue Invoke(MethodInfo[] methodInfos, JsValue thisObject, JsValue[] arguments)
        {
            var methods = TypeConverter.FindBestMatch(Engine, methodInfos, arguments).ToList();

            foreach (var method in methods)
            {
                var parameters = new object[arguments.Length];
                try
                {
                    for (var i = 0; i < arguments.Length; i++)
                    {
                        var parameterType = method.GetParameters()[i].ParameterType;

                        if (parameterType == typeof(JsValue))
                        {
                            parameters[i] = arguments[i];
                        }
                        else
                        {
                            parameters[i] = Engine.ClrTypeConverter.Convert(
                                arguments[i].ToObject(),
                                parameterType,
                                CultureInfo.InvariantCulture);

                            if (typeof(System.Linq.Expressions.LambdaExpression).IsAssignableFrom(parameters[i].GetType()))
                            {
                                parameters[i] = (parameters[i] as System.Linq.Expressions.LambdaExpression).Compile();
                            }
                        }
                    }

                    var result = JsValue.FromObject(Engine, method.Invoke(thisObject.ToObject(), parameters.ToArray()));

                    // todo: cache method info

                    return result;
                }
                catch 
                {
                    // ignore method
                }
            }

            throw new JavaScriptException(Engine.TypeError, "No public methods with the specified arguments were found.");
        }

    }
}
