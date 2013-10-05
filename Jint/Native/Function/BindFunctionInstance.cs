using System;
using System.Linq;
using Jint.Native.Object;
using Jint.Runtime;

namespace Jint.Native.Function
{
    public class BindFunctionInstance : FunctionInstance, IConstructor
    {
        public BindFunctionInstance(Engine engine) : base(engine, new string[0], null, false)
        {
        }

        public object TargetFunction { get; set; }

        public object BoundThis { get; set; }

        public object[] BoundArgs { get; set; }

        public override object Call(object thisObject, object[] arguments)
        {
            var f = TargetFunction as FunctionInstance;
            if (f == null)
            {
                throw new JavaScriptException(Engine.TypeError);
            }

            return f.Call(BoundThis, BoundArgs.Union(arguments).ToArray());
        }

        public ObjectInstance Construct(object[] arguments)
        {
            var target = TargetFunction as IConstructor;
            if (target == null)
            {
                throw new JavaScriptException(Engine.TypeError);
            }

            return target.Construct(BoundArgs.Union(arguments).ToArray());
        }

        public override bool HasInstance(object v)
        {
            var f = TargetFunction as FunctionInstance;
            if (f == null)
            {
                throw new JavaScriptException(Engine.TypeError);
            }

            return f.HasInstance(v);
        }
    }
}
