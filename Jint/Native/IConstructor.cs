using Jint.Native.Object;

namespace Jint.Native
{
    public interface IConstructor
    {
        ObjectInstance Construct(JsValue[] arguments, JsValue newTarget);
    }
}
