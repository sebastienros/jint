using System;
using Jint.Native;
using Jint.Native.Function;
using Jint.Native.Object;

namespace Jint.Runtime.Interop
{

    /// <summary>
    /// Reprensents a Property wrapper for static methods representing built-in properties.
    /// </summary>
    public class BuiltInPropertyWrapper : FunctionInstance
    {
        private readonly Delegate _d;

        public BuiltInPropertyWrapper(Delegate d, ObjectInstance prototype)
            : base(prototype, null, null)
        {
            _d = d;
        }

        public override dynamic Call(Engine engine, object thisObject, dynamic[] arguments)
        {
            // initialize Return flag
            engine.CurrentExecutionContext.Return = Undefined.Instance;

            // built-in static method must have their first parameter as 'this'
            var allArguments = new object[arguments.Length + 1];
            allArguments[0] = thisObject;
            Array.Copy(arguments, 0, allArguments, 1, arguments.Length);

            return _d.DynamicInvoke(allArguments);
        }
    }
}
