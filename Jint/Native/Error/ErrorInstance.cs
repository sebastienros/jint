using Jint.Native.Object;
using Jint.Runtime;

namespace Jint.Native.Error
{
    public sealed class ErrorInstance : ObjectInstance
    {
        public ErrorInstance(Engine engine, string name)
            : base(engine)
        {
            FastAddProperty("name", name, true, false, true);
            FastAddProperty("message", "", true, false, true);
        }

        public override string Class
        {
            get
            {
                return "Error";
            }
        }
    }
}
