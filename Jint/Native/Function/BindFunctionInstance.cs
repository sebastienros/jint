using System.Linq;
using Jint.Native.Object;
using Jint.Runtime;

namespace Jint.Native.Function
{
    public class BindFunctionInstance : FunctionInstance, IConstructor
    {
        public BindFunctionInstance(Engine engine) : base(engine, System.Array.Empty<string>(), null, false)
        {
        }

        public JsValue TargetFunction { get; set; }

        public JsValue BoundThis { get; set; }

        public JsValue[] BoundArgs { get; set; }

        public override JsValue Call(JsValue thisObject, JsValue[] arguments)
        {
            var f = TargetFunction.TryCast<FunctionInstance>(x =>
            {
                ExceptionHelper.ThrowTypeError(Engine);
            });

            return f.Call(BoundThis, BoundArgs.Union(arguments).ToArray());
        }

        public ObjectInstance Construct(JsValue[] arguments)
        {
            var target = TargetFunction.TryCast<IConstructor>(x =>
            {
                ExceptionHelper.ThrowTypeError(Engine);
            });

            return target.Construct(BoundArgs.Union(arguments).ToArray());
        }

        public override bool HasInstance(JsValue v)
        {
            var f = TargetFunction.TryCast<FunctionInstance>(x =>
            {
                ExceptionHelper.ThrowTypeError(Engine);
            });

            return f.HasInstance(v);
        }
    }
}
