using System;
using Jint.Runtime.Interop;

namespace Jint.Runtime.Descriptors.Specialized
{
    public sealed class ClrAccessDescriptor<T> : PropertyDescriptor
    {
        public ClrAccessDescriptor(Engine engine, Func<T, object> get)
            : this(engine, get, null)
        {
        }

        public ClrAccessDescriptor(Engine engine, Func<T, object> get, Action<T, object> set)
            : base(
                get: new GetterFunctionInstance<T>(engine, get),
                set: set == null ? Native.Undefined.Instance : new SetterFunctionInstance<T>(engine, set)
                )
        {
        }
    }
}
