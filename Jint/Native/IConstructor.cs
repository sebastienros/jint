using Jint.Native.Object;

namespace Jint.Native
{
    public interface IConstructor
    {
        JsValue Call(JsValue thisObject, JsValue[] arguments);
        ObjectInstance Construct(JsValue[] arguments);
    }
}
