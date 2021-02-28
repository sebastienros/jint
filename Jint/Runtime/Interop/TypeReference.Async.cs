using Jint.Native;
using Jint.Native.Function;
using System.Threading.Tasks;

namespace Jint.Runtime.Interop
{
    public sealed partial class TypeReference : FunctionInstance, IConstructor, IObjectWrapper
    {
        public override Task<JsValue> CallAsync(JsValue thisObject, JsValue[] arguments) => Task.FromResult(Call(thisObject, arguments));
    }
}