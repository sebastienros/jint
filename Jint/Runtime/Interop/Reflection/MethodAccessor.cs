using Jint.Runtime.Descriptors;

namespace Jint.Runtime.Interop.Reflection
{
    internal sealed class MethodAccessor : ReflectionAccessor
    {
        private readonly string _name;
        private readonly MethodDescriptor[] _methods;

        public MethodAccessor(MethodDescriptor[] methods, string name) : base(null!, null!)
        {
            _methods = methods;
            _name = name;
        }

        public override bool Writable => false;

        protected override object? DoGetValue(object target)
        {
            return null;
        }

        protected override void DoSetValue(object target, object? value)
        {
        }

        public override PropertyDescriptor CreatePropertyDescriptor(Engine engine, object target, bool enumerable = true)
        {
            return new(new MethodInfoFunctionInstance(engine, _methods, _name), PropertyFlag.AllForbidden);
        }
    }
}
