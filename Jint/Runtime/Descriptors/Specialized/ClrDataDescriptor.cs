using System;
using Jint.Runtime.Interop;

namespace Jint.Runtime.Descriptors.Specialized
{
    public sealed class ClrDataDescriptor<TObject, TResult> : PropertyDescriptor
    {
        public ClrDataDescriptor(Engine engine, Func<TObject, object[], TResult> func)
            : base(value: new ClrFunctionInstance<TObject, TResult>(engine, func), writable: null, enumerable: null, configurable: null)
        {
        }
    }
}
