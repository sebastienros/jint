using System.Threading.Tasks;

namespace Jint.Native
{
    public interface ICallable
    {
        JsValue Call(JsValue thisObject, JsValue[] arguments);

        Task<JsValue> CallAsync(JsValue thisObject, JsValue[] arguments);
    }
}