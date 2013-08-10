using System;
using Jint.Native;
using Jint.Native.Function;

namespace Jint.Runtime.Interop
{
    /// <summary>
    /// Wraps a Clr method into a FunctionInstance
    /// </summary>
    public sealed class ClrFunctionInstance<T> : FunctionInstance
    {
        private readonly Engine _engine;
        private readonly Func<T, object[], object> _func;

        public ClrFunctionInstance(Engine engine, Func<T, object[], object> func)
            : base(engine, null, null, null)
        {
            _engine = engine;
            _func = func;
        }

        public override object Call(object thisObject, object[] arguments)
        {
            // initialize Return flag
            _engine.CurrentExecutionContext.Return = Undefined.Instance;


            return _func((T) thisObject, arguments);
        }
    }
}
