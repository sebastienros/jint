using Jint.Native.Object;

namespace Jint.Native.Temporal;

/// <summary>
/// https://tc39.es/proposal-temporal/#sec-temporal-duration-objects
/// </summary>
internal sealed class JsDuration : ObjectInstance
{
    internal JsDuration(Engine engine, ObjectInstance prototype, DurationRecord duration) : base(engine)
    {
        _prototype = prototype;
        DurationRecord = duration;
    }

    internal DurationRecord DurationRecord { get; }

    public double Years => DurationRecord.Years;
    public double Months => DurationRecord.Months;
    public double Weeks => DurationRecord.Weeks;
    public double Days => DurationRecord.Days;
    public double Hours => DurationRecord.Hours;
    public double Minutes => DurationRecord.Minutes;
    public double Seconds => DurationRecord.Seconds;
    public double Milliseconds => DurationRecord.Milliseconds;
    public double Microseconds => DurationRecord.Microseconds;
    public double Nanoseconds => DurationRecord.Nanoseconds;

    internal override bool IsTemporalDuration => true;
}
