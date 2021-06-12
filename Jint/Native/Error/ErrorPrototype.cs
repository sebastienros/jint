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
        private readonly Realm _realm;
        private readonly ErrorConstructor _constructor;

        internal ErrorPrototype(
            Engine engine,
            Realm realm,
            ErrorConstructor constructor,
            ObjectInstance prototype,
            JsString name,
            ObjectClass objectClass)
            : base(engine, name, objectClass)
        {
            _realm = realm;
            _constructor = constructor;
            _prototype = prototype;
        }

        protected override void Initialize()
        {
            var properties = new PropertyDictionary(3, checkExistingKeys: false)
            {
                ["constructor"] = new PropertyDescriptor(_constructor, PropertyFlag.NonEnumerable),
                ["message"] = new PropertyDescriptor("", PropertyFlag.Configurable | PropertyFlag.Writable),
                ["toString"] = new PropertyDescriptor(new ClrFunctionInstance(Engine, "toString", ToString, 0, PropertyFlag.Configurable), PropertyFlag.Configurable | PropertyFlag.Writable)
            };
            SetProperties(properties);
        }

        public JsValue ToString(JsValue thisObject, JsValue[] arguments)
        {
            var o = thisObject.TryCast<ObjectInstance>();
            if (ReferenceEquals(o, null))
            {
                ExceptionHelper.ThrowTypeError(_realm);
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
