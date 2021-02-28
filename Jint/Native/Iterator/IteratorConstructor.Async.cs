using Jint.Native.Function;
using System.Threading.Tasks;

namespace Jint.Native.Iterator
{
    public sealed partial class IteratorConstructor : FunctionInstance, IConstructor
    {
        public override Task<JsValue> CallAsync(JsValue thisObject, JsValue[] arguments) => Task.FromResult(Call(thisObject, arguments));
    }
}