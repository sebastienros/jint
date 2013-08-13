using System;
using Jint.Native;
using Jint.Native.Function;

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

        public DelegateWrapper(Engine engine, Delegate d) : base(engine, null, null, null, false)
        {
            _engine = engine;
            _d = d;
        }

        public override object Call(object thisObject, object[] arguments)
        {
            var result = _d.DynamicInvoke(arguments);
            return new Completion(Completion.Normal, result, null);
        }
    }
}
