using Jint.Native.Object;
using System.Threading.Tasks;

namespace Jint.Native.Proxy
{
    public partial class ProxyInstance : ObjectInstance, IConstructor, ICallable
    {
        public Task<JsValue> CallAsync(JsValue thisObject, JsValue[] arguments) => Task.FromResult(Call(thisObject, arguments));
    }
}