using Jint.Native;

namespace Jint.Runtime.Environments
{
    public sealed class Binding
    {
        public JsValue Value;
        public bool CanBeDeleted;
        public bool Mutable;
    }
}
