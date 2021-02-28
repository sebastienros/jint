using Jint.Native.Function;
using System.Threading.Tasks;

namespace Jint.Native.String
{
    public sealed partial class StringConstructor : FunctionInstance, IConstructor
    {
        public override Task<JsValue> CallAsync(JsValue thisObject, JsValue[] arguments) => Task.FromResult(Call(thisObject, arguments));
    }
}