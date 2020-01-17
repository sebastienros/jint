using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Descriptors;

namespace Jint.Native.Error
{
    public class ErrorInstance : ObjectInstance
    {
        private readonly JsString _name;
        private PropertyDescriptor _descriptor;

        public ErrorInstance(Engine engine, JsString name)
            : base(engine, objectClass: "Error")
        {
            _name = name;
        }

        public override PropertyDescriptor GetOwnProperty(in Key propertyName)
        {
            if (propertyName.Name == "name")
            {
                return _descriptor ??= new PropertyDescriptor(_name, PropertyFlag.Configurable | PropertyFlag.Writable);
            };
            return base.GetOwnProperty(in propertyName);
        }

        public override string ToString()
        {
            return Engine.Error.PrototypeObject.ToString(this, Arguments.Empty).ToObject().ToString();
        }
    }
}
