using Jint.Native.Object;
using Jint.Runtime;

namespace Jint.Native.Function
{
    public sealed class BindFunctionInstance : FunctionInstance, IConstructor
    {
        public BindFunctionInstance(Engine engine) 
            : base(engine, name: null, thisMode: FunctionThisMode.Strict)
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

            var args = CreateArguments(arguments);
            var value = f.Call(BoundThis, args);
            _engine._jsValueArrayPool.ReturnArray(args);

            return value;
        }

        public ObjectInstance Construct(JsValue[] arguments, JsValue newTarget)
        {
            if (!(TargetFunction is IConstructor target))
            {
                return ExceptionHelper.ThrowTypeError<ObjectInstance>(Engine);
            }

            var args = CreateArguments(arguments);

            if (ReferenceEquals(this, newTarget))
            {
                newTarget = TargetFunction;
            }
            
            var value = target.Construct(args, newTarget);
            _engine._jsValueArrayPool.ReturnArray(args);

            return value;
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
            var combined = _engine._jsValueArrayPool.RentArray(BoundArgs.Length + arguments.Length);
            System.Array.Copy(BoundArgs, combined, BoundArgs.Length);
            System.Array.Copy(arguments, 0, combined, BoundArgs.Length, arguments.Length);
            return combined;
        }

        internal override bool IsConstructor => TargetFunction.IsConstructor;

        public override string ToString() => "function () { [native code] }";
    }
}
