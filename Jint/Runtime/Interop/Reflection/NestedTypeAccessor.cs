
namespace Jint.Runtime.Interop.Reflection
{
    internal sealed class NestedTypeAccessor : ReflectionAccessor
    {
        public NestedTypeAccessor(Type type, string name) : base(type, name)
        {
        }

        public override bool Writable => false;

        protected override object? DoGetValue(object target)
        {
            return _memberType;
        }

        protected override void DoSetValue(object target, object? value)
        {
        }
    }
}
