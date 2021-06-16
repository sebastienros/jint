using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Descriptors;

namespace Jint.Native.Error
{
    public class ErrorInstance : ObjectInstance
    {
        private readonly JsString _name;
        private PropertyDescriptor _descriptor;

        internal ErrorInstance(
            Engine engine,
            JsString name,
            ObjectClass objectClass = ObjectClass.Error)
            : base(engine, objectClass)
        {
            _name = name;
        }

        public override PropertyDescriptor GetOwnProperty(JsValue property)
        {
            if (property == CommonProperties.Name)
            {
                return _descriptor ??= new PropertyDescriptor(_name, PropertyFlag.Configurable | PropertyFlag.Writable);
            };
            return base.GetOwnProperty(property);
        }

        public override string ToString()
        {
            return Engine.Realm.Intrinsics.Error.PrototypeObject.ToString(this, Arguments.Empty).ToObject().ToString();
        }
    }
}
