using Jint.Runtime;
using Jint.Runtime.Descriptors;
using System.Threading.Tasks;

namespace Jint.Native.Function
{
    public sealed class ThrowTypeError : FunctionInstance
    {
        private static readonly JsString _functionName = new JsString("throwTypeError");

        private readonly string _message;

        public ThrowTypeError(Engine engine, string message = null)
            : base(engine, _functionName)
        {
            _message = message;
            _length = PropertyDescriptor.AllForbiddenDescriptor.NumberZero;
            _environment = engine.GlobalEnvironment;
            PreventExtensions();
        }

        public override JsValue Call(JsValue thisObject, JsValue[] arguments)
        {
            return ExceptionHelper.ThrowTypeError<JsValue>(_engine, _message);
        }

        public override Task<JsValue> CallAsync(JsValue thisObject, JsValue[] arguments) => Task.FromResult(Call(thisObject, arguments));
    }
}
