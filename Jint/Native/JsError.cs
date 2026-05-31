using Jint.Native.Error;
using Jint.Native.Object;

namespace Jint.Native;

public sealed class JsError : ErrorInstance
{
    /// <summary>
    /// The implementation-defined stack trace string captured when the error was constructed.
    /// Returned by the <c>get Error.prototype.stack</c> accessor (the error-stack-accessor proposal).
    /// </summary>
    internal JsValue? _stack;

    internal JsError(Engine engine) : base(engine, ObjectClass.Error)
    {
    }
}
