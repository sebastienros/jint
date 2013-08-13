using System;
using Jint.Native;
using Jint.Native.Function;

namespace Jint.Runtime.Interop
{
    /// <summary>
    /// Represents a FunctionInstance wrapping a Clr setter.
    /// </summary>
    public sealed class SetterFunctionInstance<T> : FunctionInstance
    {
        private readonly Action<T, object> _setter;

        public SetterFunctionInstance(Engine engine, Action<T, object> setter)
            : base(engine, null, null, null, false)
        {
            _setter = setter;
        }

        public override object Call(object thisObject, object[] arguments)
        {
            _setter((T)thisObject, arguments[0]);
            
            return Null.Instance;
        }
    }
}
