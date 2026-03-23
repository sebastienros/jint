using System.Numerics;
using Jint.Native.Object;

namespace Jint.Native.Temporal;

/// <summary>
/// https://tc39.es/proposal-temporal/#sec-temporal-zoneddatetime-objects
/// </summary>
internal sealed class JsZonedDateTime : ObjectInstance
{
    private readonly ITimeZoneProvider _timeZoneProvider;
    private IsoDateTime? _cachedIsoDateTime;

    internal JsZonedDateTime(
        Engine engine,
        ObjectInstance prototype,
        BigInteger epochNanoseconds,
        string timeZone,
        string calendar) : base(engine)
    {
        _prototype = prototype;
        _timeZoneProvider = engine.Options.Temporal.TimeZoneProvider;
        EpochNanoseconds = epochNanoseconds;
        TimeZone = timeZone;
        Calendar = calendar;
    }

    internal BigInteger EpochNanoseconds { get; }
    internal string TimeZone { get; }
    internal string Calendar { get; }

    internal long OffsetNanoseconds => _timeZoneProvider.GetOffsetNanosecondsFor(TimeZone, EpochNanoseconds);

    internal IsoDateTime GetIsoDateTime()
    {
        if (_cachedIsoDateTime.HasValue)
            return _cachedIsoDateTime.Value;

        var offsetNs = OffsetNanoseconds;
        var localNs = EpochNanoseconds + offsetNs;

        // Convert nanoseconds to date/time components
        const long nsPerDay = 86_400_000_000_000L;
        const long nsPerHour = 3_600_000_000_000L;
        const long nsPerMinute = 60_000_000_000L;
        const long nsPerSecond = 1_000_000_000L;
        const long nsPerMs = 1_000_000L;
        const long nsPerUs = 1_000L;

        // Days since Unix epoch
        var days = (long) (localNs / nsPerDay);
        var remaining = (long) (localNs % nsPerDay);
        if (remaining < 0)
        {
            days--;
            remaining += nsPerDay;
        }

        // Convert days to date
        var date = TemporalHelpers.DaysToIsoDate(days);

        // Convert remaining nanoseconds to time
        var hour = (int) (remaining / nsPerHour);
        remaining %= nsPerHour;
        var minute = (int) (remaining / nsPerMinute);
        remaining %= nsPerMinute;
        var second = (int) (remaining / nsPerSecond);
        remaining %= nsPerSecond;
        var millisecond = (int) (remaining / nsPerMs);
        remaining %= nsPerMs;
        var microsecond = (int) (remaining / nsPerUs);
        var nanosecond = (int) (remaining % nsPerUs);

        _cachedIsoDateTime = new IsoDateTime(date, new IsoTime(hour, minute, second, millisecond, microsecond, nanosecond));
        return _cachedIsoDateTime.Value;
    }

    internal override bool IsTemporalZonedDateTime => true;
}
