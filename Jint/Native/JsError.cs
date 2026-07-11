using Jint.Native.Error;
using Jint.Native.Object;
using Jint.Runtime.CallStack;

namespace Jint.Native;

public sealed class JsError : ErrorInstance
{
    /// <summary>
    /// The rendered implementation-defined stack trace string, materialized on first read from
    /// <see cref="_stackCapture"/> (or set directly by a materializing path). Returned by the
    /// <c>get Error.prototype.stack</c> accessor (the error-stack-accessor proposal).
    /// </summary>
    internal JsValue? _stack;

    /// <summary>
    /// A deferred snapshot of the call stack captured at construction; rendered into <see cref="_stack"/>
    /// on the first <c>error.stack</c> read. Non-null until rendered (then cleared to release the snapshot).
    /// </summary>
    internal ErrorStackCapture? _stackCapture;

    internal JsError(Engine engine) : base(engine, ObjectClass.Error)
    {
    }
}
