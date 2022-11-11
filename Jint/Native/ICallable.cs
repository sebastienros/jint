namespace Jint.Native;

internal interface ICallable
{
    JsValue Call(JsValue thisObject, params JsValue[] arguments);
}
