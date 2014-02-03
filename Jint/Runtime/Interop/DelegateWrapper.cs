using System;
using System.Linq;
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
        private readonly Delegate _d;

        public DelegateWrapper(Engine engine, Delegate d) : base(engine, null, null, false)
        {
            _d = d;
        }

        public override JsValue Call(JsValue thisObject, JsValue[] arguments)
        {
            return JsValue.FromObject(Engine, _d.DynamicInvoke(arguments.Select(x => x.ToObject()).ToArray()));
        }
    }
}
