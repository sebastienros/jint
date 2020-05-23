using Jint.Runtime;
using Jint.Runtime.Descriptors;

namespace Jint.Native.Function
{
    public sealed class ThrowTypeError : FunctionInstance
    {
        private static readonly JsString _functionName = new JsString("throwTypeError");

        public ThrowTypeError(Engine engine)
            : base(engine, _functionName)
        {
            _length = PropertyDescriptor.AllForbiddenDescriptor.NumberZero;
            _environment = engine.GlobalEnvironment;
            PreventExtensions();
        }

        public override JsValue Call(JsValue thisObject, JsValue[] arguments)
        {
            return ExceptionHelper.ThrowTypeError<JsValue>(_engine);
        }
    }
}
