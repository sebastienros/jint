using System.Linq;
using Jint.Native.Object;
using Jint.Runtime;

namespace Jint.Native.Function
{
    public sealed class BindFunctionInstance : FunctionInstance, IConstructor
    {
        private static readonly JsString _name = new JsString("bind");

        public BindFunctionInstance(Engine engine)
            : base(engine, _name, FunctionThisMode.Global)
        {
        }

        public JsValue TargetFunction { get; set; }

        public JsValue BoundThis { get; set; }

        public JsValue[] BoundArgs { get; set; }

        public override JsValue Call(JsValue thisObject, JsValue[] arguments)
        {
            if (!(TargetFunction is FunctionInstance f))
            {
                return ExceptionHelper.ThrowTypeError<ObjectInstance>(Engine);
            }

            return f.Call(BoundThis, CreateArguments(arguments));
        }

        public ObjectInstance Construct(JsValue[] arguments, JsValue newTarget)
        {
            if (!(TargetFunction is IConstructor target))
            {
                return ExceptionHelper.ThrowTypeError<ObjectInstance>(Engine);
            }

            return target.Construct(CreateArguments(arguments), newTarget);
        }

        public override bool HasInstance(JsValue v)
        {
            var f = TargetFunction.TryCast<FunctionInstance>(x =>
            {
                ExceptionHelper.ThrowTypeError(Engine);
            });

            return f.HasInstance(v);
        }

        private JsValue[] CreateArguments(JsValue[] arguments)
        {
            return Enumerable.Union(BoundArgs, arguments).ToArray();
        }

        internal override bool IsConstructor => TargetFunction is IConstructor;
    }
}
