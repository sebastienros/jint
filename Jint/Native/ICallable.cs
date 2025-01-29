namespace Jint.Native;

internal interface ICallable
{
    JsValue Call(JsValue thisObject, params JsValue[] arguments);
    Task<JsValue> CallAsync(JsValue thisObject, params JsValue[] arguments);
}
