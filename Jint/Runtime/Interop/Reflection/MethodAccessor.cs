using Jint.Runtime.Descriptors;

namespace Jint.Runtime.Interop.Reflection
{
    internal sealed class MethodAccessor : ReflectionAccessor
    {
        private readonly Type _targetType;
        private readonly string _name;
        private readonly MethodDescriptor[] _methods;

        public MethodAccessor(Type targetType, string name, MethodDescriptor[] methods)
            : base(null!, name)
        {
            _targetType = targetType;
            _name = name;
            _methods = methods;
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
            return new(new MethodInfoFunctionInstance(engine, _targetType, _name, _methods), PropertyFlag.AllForbidden);
        }
    }
}
