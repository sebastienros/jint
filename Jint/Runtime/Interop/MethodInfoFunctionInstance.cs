using System;
using System.Globalization;
using System.Linq;
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
            // filter methods with the expected number of parameters
            var methods = _methods
                .Where(m => m.GetParameters().Count() == arguments.Length)
                .ToArray()
                ;

            if (!methods.Any())
            {
                throw new JavaScriptException(Engine.TypeError, "Invalid number of arguments");
            }

            // todo: look for compatible types    
            var method = methods.First();
            var parameters = new object[arguments.Length];
            for (var i = 0; i < arguments.Length; i++)
            {
                parameters[i] = Convert.ChangeType(
                    arguments[i].ToObject(),
                    method.GetParameters()[i].ParameterType,
                    CultureInfo.InvariantCulture);
            }

            var obj = thisObject.ToObject() as ObjectWrapper;

            if (obj == null)
            {
                throw new JavaScriptException(Engine.TypeError, "Can't call a CLR method on a non CLR instance");
            }

            return JsValue.FromObject(Engine, method.Invoke(obj.Target, parameters.ToArray()));
        }
    }
}
