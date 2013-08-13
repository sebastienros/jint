using System;
using Jint.Native;
using Jint.Native.Function;

namespace Jint.Runtime.Interop
{
    /// <summary>
    /// Wraps a Clr method into a FunctionInstance
    /// </summary>
    public sealed class ClrFunctionInstance<TObject, TResult> : FunctionInstance
    {
        private readonly Engine _engine;
        private readonly Func<TObject, object[], TResult> _func;

        public ClrFunctionInstance(Engine engine, Func<TObject, object[], TResult> func)
            : base(engine, null, null, null, false)
        {
            _engine = engine;
            _func = func;
        }

        public override object Call(object thisObject, object[] arguments)
        {
            var result = _func((TObject) thisObject, arguments);
            return new Completion(Completion.Normal, result, null);
        }
    }
}
