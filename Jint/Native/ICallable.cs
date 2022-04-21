using Jint.Runtime;

namespace Jint.Native;

internal interface ICallable
{
    JsValue Call(JsValue thisObject, in Arguments arguments);
}
