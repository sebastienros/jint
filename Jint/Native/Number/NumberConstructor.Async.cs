using Jint.Native.Function;
using System.Threading.Tasks;

namespace Jint.Native.Number
{
    public sealed partial class NumberConstructor : FunctionInstance, IConstructor
    {
        public override Task<JsValue> CallAsync(JsValue thisObject, JsValue[] arguments) => Task.FromResult(Call(thisObject, arguments));
    }
}