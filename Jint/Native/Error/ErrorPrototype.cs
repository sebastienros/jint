using Jint.Collections;
using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Descriptors;
using Jint.Runtime.Interop;

namespace Jint.Native.Error
{
    /// <summary>
    /// http://www.ecma-international.org/ecma-262/5.1/#sec-15.11.4
    /// </summary>
    public sealed class ErrorPrototype : ErrorInstance
    {
        private ErrorConstructor _errorConstructor;

        private ErrorPrototype(Engine engine, JsString name)
            : base(engine, name)
        {
        }

        public static ErrorPrototype CreatePrototypeObject(Engine engine, ErrorConstructor errorConstructor, JsString name)
        {
            var obj = new ErrorPrototype(engine, name)
            {
                _errorConstructor = errorConstructor,
            };

            if (name._value != "Error")
            {
                obj._prototype = engine.Error.PrototypeObject;
            }
            else
            {
                obj._prototype = engine.Object.PrototypeObject;
            }

            return obj;
        }

        protected override void Initialize()
        {
            var properties = new PropertyDictionary(3, checkExistingKeys: false)
            {
                ["constructor"] = new PropertyDescriptor(_errorConstructor, PropertyFlag.NonEnumerable),
                ["message"] = new PropertyDescriptor("", PropertyFlag.Configurable | PropertyFlag.Writable),
                ["toString"] = new PropertyDescriptor(new ClrFunctionInstance(Engine, "toString", ToString), PropertyFlag.Configurable | PropertyFlag.Writable)
            };
            SetProperties(properties);
        }

        public JsValue ToString(JsValue thisObject, JsValue[] arguments)
        {
            var o = thisObject.TryCast<ObjectInstance>();
            if (ReferenceEquals(o, null))
            {
                ExceptionHelper.ThrowTypeError(Engine);
            }

            var name = TypeConverter.ToString(o.Get("name", this));

            var msgProp = o.Get("message", this);
            string msg;
            if (msgProp.IsUndefined())
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
