using Jint.Native;

namespace Jint.Runtime.Environments
{
    public readonly struct Binding
    {
        public Binding(JsValue value, bool canBeDeleted, bool mutable)
        {
            Value = value;
            CanBeDeleted = canBeDeleted;
            Mutable = mutable;
        }

        public readonly JsValue Value;
        public readonly bool CanBeDeleted;
        public readonly bool Mutable;

        public Binding ChangeValue(JsValue argument)
        {
            return new Binding(argument, CanBeDeleted, Mutable);
        }

        public bool IsInitialized() => !(Value is null);
    }
}
