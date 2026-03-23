using Jint.Native.Object;

namespace Jint.Native.Temporal;

/// <summary>
/// https://tc39.es/proposal-temporal/#sec-temporal-plaindatetime-objects
/// </summary>
internal sealed class JsPlainDateTime : ObjectInstance
{
    internal JsPlainDateTime(Engine engine, ObjectInstance prototype, IsoDateTime isoDateTime, string calendar) : base(engine)
    {
        _prototype = prototype;
        IsoDateTime = isoDateTime;
        Calendar = calendar;
    }

    internal IsoDateTime IsoDateTime { get; }
    internal string Calendar { get; }

    internal override bool IsTemporalPlainDateTime => true;
}
