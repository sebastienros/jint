using Jint.Native.Function;
using System.Threading.Tasks;

namespace Jint.Native.Boolean
{
    public sealed partial class BooleanConstructor : FunctionInstance, IConstructor
    {
        public override Task<JsValue> CallAsync(JsValue thisObject, JsValue[] arguments) => Task.FromResult(Call(thisObject, arguments));
    }
}