using Jint.Native.Function;
using System.Threading.Tasks;

namespace Jint.Native.Object
{
    public sealed partial class ObjectConstructor : FunctionInstance, IConstructor
    {
        public override Task<JsValue> CallAsync(JsValue thisObject, JsValue[] arguments) => Task.FromResult(Call(thisObject, arguments));

        private sealed partial class CreateDataPropertyOnObject : ICallable
        {
            public Task<JsValue> CallAsync(JsValue thisObject, JsValue[] arguments)
            {
                return Task.FromResult(Call(thisObject, arguments));
            }
        }
    }
}