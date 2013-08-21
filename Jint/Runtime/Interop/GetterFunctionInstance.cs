using System;
using Jint.Native.Function;

namespace Jint.Runtime.Interop
{
    /// <summary>
    /// Represents a FunctionInstance wrapping a Clr getter.
    /// </summary>
    public sealed class GetterFunctionInstance<T> : FunctionInstance
    {
        private readonly Func<T, object> _getter;

        public GetterFunctionInstance(Engine engine, Func<T, object> getter)
            : base(engine,  null, null, false)
        {
            _getter = getter;
        }

        public override object Call(object thisObject, object[] arguments)
        {
            return _getter((T)thisObject);
        }
    }
}
