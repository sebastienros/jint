using Jint.Runtime;
using Jint.Runtime.Descriptors;

namespace Jint.Native.Function
{
    public sealed class ThrowTypeError : FunctionInstance
    {
        private static readonly JsString _functionName = new JsString("throwTypeError");

        public ThrowTypeError(Engine engine)
            : base(engine, _functionName, System.ArrayExt.Empty<string>(), engine.GlobalEnvironment, false)
        {
            _length = PropertyDescriptor.AllForbiddenDescriptor.NumberZero;
            Extensible = false;
        }

        public override JsValue Call(JsValue thisObject, JsValue[] arguments)
        {
            ExceptionHelper.ThrowTypeError(_engine);
            return null;
        }
    }
}
