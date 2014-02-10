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
                parameters[i] = Engine.Options.GetTypeConverter().Convert(
                    arguments[i].ToObject(),
                    method.GetParameters()[i].ParameterType,
                    CultureInfo.InvariantCulture);
            }

            return JsValue.FromObject(Engine, method.Invoke(thisObject.ToObject(), parameters.ToArray()));
        }
    }
}
