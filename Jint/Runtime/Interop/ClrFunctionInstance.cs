using System;
using Jint.Native.Function;

namespace Jint.Runtime.Interop
{
    /// <summary>
    /// Wraps a Clr method into a FunctionInstance
    /// </summary>
    public sealed class ClrFunctionInstance<TObject, TResult> : FunctionInstance
    {
        private readonly Func<TObject, object[], TResult> _func;

        public ClrFunctionInstance(Engine engine, Func<TObject, object[], TResult> func, int length)
            : base(engine, null, null, false)
        {
            _func = func;
            Prototype = engine.Function.PrototypeObject;
            FastAddProperty("length", length, false, false, false);
        }

        public ClrFunctionInstance(Engine engine, Func<TObject, object[], TResult> func)
            : this(engine, func, 0)
        {
        }

        public override object Call(object thisObject, object[] arguments)
        {
            var result = _func((TObject) thisObject, arguments);
            return result;
        }
    }
}
