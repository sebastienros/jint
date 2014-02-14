using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Interop;

namespace Jint.Native.Error
{
    /// <summary>
    /// http://www.ecma-international.org/ecma-262/5.1/#sec-15.11.4
    /// </summary>
    public sealed class ErrorPrototype : ErrorInstance
    {
        private ErrorPrototype(Engine engine, string name)
            : base(engine, name)
        {
        }

        public static ErrorPrototype CreatePrototypeObject(Engine engine, ErrorConstructor errorConstructor, string name)
        {
            var obj = new ErrorPrototype(engine, name) { Extensible = true };
            obj.FastAddProperty("constructor", errorConstructor, true, false, true);
            obj.FastAddProperty("message", "", true, false, true);

            if (name != "Error")
            {
                obj.Prototype = engine.Error.PrototypeObject;
            }
            else
            {
                obj.Prototype = engine.Object.PrototypeObject;
            }

            return obj;
        }

        public void Configure()
        {
            // Error prototype functions
            FastAddProperty("toString", new ClrFunctionInstance(Engine, ToString), true, false, true);
        }

        public JsValue ToString(JsValue thisObject, JsValue[] arguments)
        {
            var o = thisObject.TryCast<ObjectInstance>();
            if (o == null)
            {
                throw new JavaScriptException(Engine.TypeError);
            }

            var name = TypeConverter.ToString(o.Get("name"));

            var msgProp = o.Get("message");
            string msg;
            if (msgProp == Undefined.Instance)
            {
                msg = "";
            }
            else
            {
                msg = TypeConverter.ToString(msgProp);
            }
            if (name == "")
            {
                return msg;
            }
            if (msg == "")
            {
                return name;
            }
            return name + ": " + msg;
        }
    }
}
