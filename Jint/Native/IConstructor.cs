using Jint.Native.Object;

namespace Jint.Native;

internal interface IConstructor
{
    ObjectInstance Construct(JsValue[] arguments, JsValue newTarget);
}
