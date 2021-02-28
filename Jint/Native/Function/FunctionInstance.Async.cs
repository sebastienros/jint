using Jint.Native.Object;
using System.Threading.Tasks;

namespace Jint.Native.Function
{
    public abstract partial class FunctionInstance : ObjectInstance, ICallable
    {
        public abstract Task<JsValue> CallAsync(JsValue thisObject, JsValue[] arguments);
    }
}