using Jint.Native.Object;

namespace Jint.Native;

internal interface IConstructor
{
    ObjectInstance Construct(JsCallArguments arguments, JsValue newTarget);
}
