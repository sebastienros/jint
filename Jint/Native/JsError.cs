using Jint.Native.Error;
using Jint.Native.Object;

namespace Jint.Native;

public sealed class JsError : ErrorInstance
{
    internal JsError(Engine engine) : base(engine, ObjectClass.Error)
    {
    }
}
