using Jint.Runtime.Descriptors;

namespace Jint.Runtime.Interop.Reflection
{
    internal sealed class MethodAccessor : ReflectionAccessor
    {
        private readonly MethodDescriptor[] _methods;

        public MethodAccessor(MethodDescriptor[] methods) : base(null, null)
        {
            _methods = methods;
        }

        public override bool Writable => false;

        protected override object DoGetValue(object target)
        {
            return null;
        }

        protected override void DoSetValue(object target, object value)
        {
        }

        public override PropertyDescriptor CreatePropertyDescriptor(Engine engine, object target)
        {
            return new(new MethodInfoFunctionInstance(engine, _methods), PropertyFlag.AllForbidden);
        }
    }
}
