using System;
using Jint.Runtime.Interop;

namespace Jint.Runtime.Descriptors.Specialized
{
    public sealed class ClrDataDescriptor<T> : DataDescriptor
    {
        public ClrDataDescriptor(Engine engine, Func<T, object[], object> func) 
            : base(new ClrFunctionInstance<T>(engine, func))
        {
        }

        public override bool IsAccessorDescriptor()
        {
            return false;
        }

        public override bool IsDataDescriptor()
        {
            return true;
        }
    }
}
