using Jint.Native.Object;
using Jint.Runtime;
using System.Threading.Tasks;

namespace Jint.Native.Proxy
{
    public partial class ProxyInstance : ObjectInstance, IConstructor, ICallable
    {
        public async Task<JsValue> CallAsync(JsValue thisObject, JsValue[] arguments)
        {
            var jsValues = new[] { _target, thisObject, _engine.Array.Construct(arguments) };
            if (TryCallHandler(TrapApply, jsValues, out var result))
            {
                return result;
            }

            if (!(_target is ICallable callable))
            {
                return ExceptionHelper.ThrowTypeError<JsValue>(_engine, _target + " is not a function");
            }

            return await callable.CallAsync(thisObject, arguments);
        }
    }
}