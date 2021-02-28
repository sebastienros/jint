using Jint.Native.Function;
using System.Threading.Tasks;

namespace Jint.Native.RegExp
{
    public sealed partial class RegExpConstructor : FunctionInstance, IConstructor
    {
        public override Task<JsValue> CallAsync(JsValue thisObject, JsValue[] arguments) => Task.FromResult(Call(thisObject, arguments));
    }
}