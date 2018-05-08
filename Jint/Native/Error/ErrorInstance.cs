using Jint.Native.Object;
using Jint.Runtime;

namespace Jint.Native.Error
{
    public class ErrorInstance : ObjectInstance
    {
        public ErrorInstance(Engine engine, string name)
            : base(engine, objectClass: "Error")
        {
            FastAddProperty("name", name, true, false, true);
        }

        public override string ToString()
        {
            return Engine.Error.PrototypeObject.ToString(this, Arguments.Empty).ToObject().ToString();
        }
    }
}
