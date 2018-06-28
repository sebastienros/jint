using Jint.Native;

namespace Jint.Runtime.Environments
{
    public struct Binding
    {
        public Binding(JsValue value, bool canBeDeleted, bool mutable)
        {
            Value = value;
            CanBeDeleted = canBeDeleted;
            Mutable = mutable;
        }

        public JsValue Value;
        public readonly bool CanBeDeleted;
        public readonly bool Mutable;
    }
}
