using Jint.Native.Function;
using System.Threading.Tasks;

namespace Jint.Native.Map
{
    public sealed partial class MapConstructor : FunctionInstance, IConstructor
    {
        public override Task<JsValue> CallAsync(JsValue thisObject, JsValue[] arguments) => Task.FromResult(Call(thisObject, arguments));
    }
}