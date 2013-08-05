using System;
using Jint.Native;

namespace Jint.Runtime.Descriptors
{
    public sealed class AccessorDescriptor : PropertyDescriptor
    {
        private readonly Func<object> _getter;
        private readonly Action<object> _setter;

        public AccessorDescriptor(Func<object> getter, Action<object> setter)
        {
            _getter = getter;
            _setter = setter;
        }

        public override object Get()
        {
            if (_getter == null)
            {
                return Undefined.Instance;
            }

            return _getter();

        }

        public override void Set(object value)
        {
            if (_setter != null)
            {
                _setter(value);
            }
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