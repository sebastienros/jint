using Jint.Native.Object;
using Jint.Runtime;

namespace Jint.Native.Temporal;

/// <summary>
/// https://tc39.es/proposal-temporal/#sec-temporal-plaintime-objects
/// </summary>
internal sealed class JsPlainTime : ObjectInstance
{
    internal JsPlainTime(Engine engine, ObjectInstance prototype, IsoTime isoTime) : base(engine)
    {
        _prototype = prototype;
        IsoTime = isoTime;
    }

    internal IsoTime IsoTime { get; }

    internal override bool IsTemporalPlainTime => true;
}
