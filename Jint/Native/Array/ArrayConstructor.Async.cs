using Jint.Native.Function;
using System.Threading.Tasks;

namespace Jint.Native.Array
{
    public sealed partial class ArrayConstructor : FunctionInstance, IConstructor
    {
        public override Task<JsValue> CallAsync(JsValue thisObject, JsValue[] arguments) => Task.FromResult(Call(thisObject, arguments));
    }
}