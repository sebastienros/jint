using Jint.Native.Function;
using System.Threading.Tasks;

namespace Jint.Native.Set
{
    public sealed partial class SetConstructor : FunctionInstance, IConstructor
    {
        public override Task<JsValue> CallAsync(JsValue thisObject, JsValue[] arguments) => Task.FromResult(Call(thisObject, arguments));
    }
}