using Jint.Native.Object;
using Jint.Runtime;
using System.Linq;
using System.Threading.Tasks;

namespace Jint.Native.Function
{
    public sealed partial class BindFunctionInstance : FunctionInstance, IConstructor
    {
        public async override Task<JsValue> CallAsync(JsValue thisObject, JsValue[] arguments)
        {
            if (!(TargetFunction is FunctionInstance f))
            {
                return ExceptionHelper.ThrowTypeError<ObjectInstance>(Engine);
            }

            var args = CreateArguments(arguments);
            var value = await f.CallAsync(BoundThis, args);
            _engine._jsValueArrayPool.ReturnArray(args);

            return value;
        }
    }
}