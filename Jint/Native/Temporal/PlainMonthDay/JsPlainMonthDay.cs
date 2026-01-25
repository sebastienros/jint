using Jint.Native.Object;

namespace Jint.Native.Temporal;

/// <summary>
/// https://tc39.es/proposal-temporal/#sec-temporal-plainmonthday-objects
/// </summary>
internal sealed class JsPlainMonthDay : ObjectInstance
{
    internal JsPlainMonthDay(Engine engine, ObjectInstance prototype, IsoDate isoDate, string calendar) : base(engine)
    {
        _prototype = prototype;
        IsoDate = isoDate;
        Calendar = calendar;
    }

    internal IsoDate IsoDate { get; }
    internal string Calendar { get; }

    internal override bool IsTemporalPlainMonthDay => true;
}
