using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Interop;

namespace Jint.Native.Error
{
    /// <summary>
    /// http://www.ecma-international.org/ecma-262/5.1/#sec-15.11.4
    /// </summary>
    public sealed class ErrorPrototype : ObjectInstance
    {
        private readonly string _name;

        private ErrorPrototype(Engine engine, string name)
            : base(engine)
        {
            _name = name;
        }

        public static ErrorPrototype CreatePrototypeObject(Engine engine, ErrorConstructor errorConstructor, string name)
        {
            var obj = new ErrorPrototype(engine, name) { Extensible = true };
            obj.Prototype = engine.Object.PrototypeObject;

            obj.FastAddProperty("constructor", errorConstructor, false, false, false);

            return obj;
        }

        public void Configure()
        {
            // Error prototype properties
            FastAddProperty("message", "", true, false, true);
            FastAddProperty("name", _name, true, false, true);

            // Error prototype functions
            FastAddProperty("toString", new ClrFunctionInstance<object, object>(Engine, ToString), true, false, true);
        }

        private object ToString(object thisObject, object[] arguments)
        {
            var o = thisObject as ObjectInstance;
            if (o == null)
            {
                throw new JavaScriptException(Engine.TypeError);
            }

            var name = o.Get("name");
            if (name == Undefined.Instance)
            {
                name = _name;
            }
            else
            {
                name = TypeConverter.ToString(name);
            }

            var msg = o.Get("message");
            if (msg == Undefined.Instance)
            {
                msg = "";
            }
            else
            {
                msg = TypeConverter.ToString(msg);
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
