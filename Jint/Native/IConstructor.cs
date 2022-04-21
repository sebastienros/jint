using Jint.Native.Object;
using Jint.Runtime;

namespace Jint.Native;

internal interface IConstructor
{
    ObjectInstance Construct(in Arguments arguments, JsValue newTarget);
}
