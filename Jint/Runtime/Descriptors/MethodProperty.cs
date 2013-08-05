using System;
using Jint.Runtime.Interop;

namespace Jint.Runtime.Descriptors
{
    public class MethodProperty : PropertyDescriptor
    {
        private readonly Delegate _d;

        public MethodProperty(Delegate d)
        {
            _d = d;
        }

        public override object Get()
        {
            return new DelegateWrapper(_d, null);
        }

        public override void Set(object value)
        {
        }

        public override bool IsAccessorDescriptor()
        {
            return false;
        }

        public override bool IsDataDescriptor()
        {
            return false;
        }
    }
}
