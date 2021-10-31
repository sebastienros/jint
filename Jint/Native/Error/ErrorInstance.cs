using Jint.Native.Object;
using Jint.Runtime;

namespace Jint.Native.Error
{
    public class ErrorInstance : ObjectInstance
    {
        internal ErrorInstance(
            Engine engine,
            ObjectClass objectClass = ObjectClass.Error)
            : base(engine, objectClass)
        {
        }

        public override string ToString()
        {
            return Engine.Realm.Intrinsics.Error.PrototypeObject.ToString(this, Arguments.Empty).ToObject().ToString();
        }
    }
}
