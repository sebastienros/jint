using System;
using Jint.Runtime.Interop;

namespace Jint.Runtime.Descriptors.Specialized
{
    public sealed class ClrDataDescriptor<TObject, TResult> : DataDescriptor
    {
        public ClrDataDescriptor(Engine engine, Func<TObject, object[], TResult> func)
            : base(new ClrFunctionInstance<TObject, TResult>(engine, func))
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
