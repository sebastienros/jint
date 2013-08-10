using System;
using Jint.Runtime.Interop;

namespace Jint.Runtime.Descriptors.Specialized
{
    public sealed class MethodPropertyDescriptor<T> : AccessorDescriptor
    {
        public MethodPropertyDescriptor(Engine engine, Func<T, object> get)
            : this(engine, get, null)
        {
        }

        public MethodPropertyDescriptor(Engine engine, Func<T, object> get, Action<T, object> set)
            : base(
                new GetterFunctionInstance<T>(engine, get),
                set == null ? null : new SetterFunctionInstance<T>(engine, set)
                )
        {
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
