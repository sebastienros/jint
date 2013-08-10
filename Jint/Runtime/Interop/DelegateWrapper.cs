using System;
using Jint.Native;
using Jint.Native.Function;
using Jint.Native.Object;

namespace Jint.Runtime.Interop
{
    /// <summary>
    /// Represents a FunctionInstance wrapper around a CLR method. This is used by user to pass
    /// custom methods to the engine.
    /// </summary>
    public sealed class DelegateWrapper : FunctionInstance
    {
        private readonly Engine _engine;
        private readonly Delegate _d;

        public DelegateWrapper(Engine engine, Delegate d, ObjectInstance prototype) : base(engine, prototype, null, null)
        {
            _engine = engine;
            _d = d;
        }

        public override object Call(object thisObject, object[] arguments)
        {
            // initialize Return flag
            _engine.CurrentExecutionContext.Return = Undefined.Instance;

            _d.DynamicInvoke(arguments);

            return _engine.CurrentExecutionContext.Return;
        }
    }
}
