using Jint.Collections;
using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Descriptors;

namespace Jint.Native.Error
{
    public class ErrorInstance : ObjectInstance
    {
        public ErrorInstance(Engine engine, JsString name)
            : base(engine, objectClass: "Error")
        {
            _properties = new StringDictionarySlim<PropertyDescriptor>(2)
            {
                ["name"] = new PropertyDescriptor(name, true, false, true)
            };
        }

        public override string ToString()
        {
            return Engine.Error.PrototypeObject.ToString(this, Arguments.Empty).ToObject().ToString();
        }
    }
}
