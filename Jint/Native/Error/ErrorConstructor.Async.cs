using Jint.Native.Function;
using System.Threading.Tasks;

namespace Jint.Native.Error
{
    public sealed partial class ErrorConstructor : FunctionInstance, IConstructor
    {
        public override Task<JsValue> CallAsync(JsValue thisObject, JsValue[] arguments) => Task.FromResult(Call(thisObject, arguments));
    }
}