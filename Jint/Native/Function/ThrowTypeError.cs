using Jint.Runtime;
using Jint.Runtime.Descriptors;

namespace Jint.Native.Function
{
    public sealed class ThrowTypeError : FunctionInstance
    {
        private static readonly JsString _functionName = new JsString("throwTypeError");

        private readonly string _message;

        public ThrowTypeError(
            Engine engine,
            Realm realm,
            string message = null)
            : base(engine, realm, _functionName)
        {
            _message = message;
            _length = PropertyDescriptor.AllForbiddenDescriptor.NumberZero;
            _environment = realm.GlobalEnv;
            PreventExtensions();
        }

        public override JsValue Call(JsValue thisObject, JsValue[] arguments)
        {
            ExceptionHelper.ThrowTypeError(_realm, _message);
            return null;
        }
    }
}
