using System;
using Jint.Native;
using Jint.Native.Function;
using Jint.Native.Object;

namespace Jint.Runtime.Interop
{
    /// <summary>
    /// Reprensents a Property wrapper for static methods representing built-in properties.
    /// </summary>
    public sealed class BuiltInPropertyWrapper : FunctionInstance
    {
        private readonly Engine _engine;
        private readonly Delegate _d;

        public BuiltInPropertyWrapper(Engine engine, Delegate d, ObjectInstance prototype)
            : base(engine, prototype, null, null)
        {
            _engine = engine;
            _d = d;
        }

        public override object Call(object thisObject, object[] arguments)
        {
            // initialize Return flag
            _engine.CurrentExecutionContext.Return = Undefined.Instance;

            // built-in static method must have their first parameter as 'this'
            var allArguments = new object[arguments.Length + 1];
            allArguments[0] = thisObject;
            Array.Copy(arguments, 0, allArguments, 1, arguments.Length);

            return _d.DynamicInvoke(allArguments);
        }
    }
}
