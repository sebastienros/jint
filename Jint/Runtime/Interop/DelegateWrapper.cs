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
    public class DelegateWrapper : FunctionInstance
    {
        private readonly Delegate _d;

        public DelegateWrapper(Delegate d, ObjectInstance prototype) : base(prototype, null, null)
        {
            _d = d;
        }

        public override dynamic Call(Engine engine, object thisObject, dynamic[] arguments)
        {
            // initialize Return flag
            engine.CurrentExecutionContext.Return = Undefined.Instance;

            _d.DynamicInvoke(arguments);

            return engine.CurrentExecutionContext.Return;
        }
    }
}
