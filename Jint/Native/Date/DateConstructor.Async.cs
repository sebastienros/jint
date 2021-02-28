using Jint.Native.Function;
using System.Threading.Tasks;

namespace Jint.Native.Date
{
    public sealed partial class DateConstructor : FunctionInstance, IConstructor
    {
        public override Task<JsValue> CallAsync(JsValue thisObject, JsValue[] arguments) => Task.FromResult(Call(thisObject, arguments));
    }
}