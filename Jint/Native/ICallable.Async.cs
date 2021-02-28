using System.Threading.Tasks;

namespace Jint.Native
{
    public partial interface ICallable
    {
        Task<JsValue> CallAsync(JsValue thisObject, JsValue[] arguments);
    }
}