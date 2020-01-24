using Jint.Native;

namespace Jint.Runtime.Environments
{
    public class Binding
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

        public bool IsInitialized => !ReferenceEquals(Value, null);
    }
}
