using System;

namespace Jint.Runtime.Descriptors.Specialized
{
    public sealed class MethodProperty : PropertyDescriptor
    {
        private readonly Engine _engine;
        private readonly Action<object> _setter;
        private readonly Func<object> _getter;

        public MethodProperty(Engine engine, Func<object> getter, Action<object> setter)
        {
            _engine = engine;
            _setter = setter;
            _getter = getter;
        }

        public override object Get()
        {
            return _getter();
        }

        public override void Set(object value)
        {
            _setter(value);
        }

        public override bool IsAccessorDescriptor()
        {
            return true;
        }

        public override bool IsDataDescriptor()
        {
            return false;
        }
    }
}
