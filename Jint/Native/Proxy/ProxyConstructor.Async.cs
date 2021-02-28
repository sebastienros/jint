using Jint.Native.Function;
using System.Threading.Tasks;

namespace Jint.Native.Proxy
{
    public sealed partial class ProxyConstructor : FunctionInstance, IConstructor
    {
        public override Task<JsValue> CallAsync(JsValue thisObject, JsValue[] arguments) => Task.FromResult(Call(thisObject, arguments));
    }
}
