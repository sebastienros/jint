namespace Jint.Native
{
    public partial interface ICallable
    {
        JsValue Call(JsValue thisObject, JsValue[] arguments);
    }
}