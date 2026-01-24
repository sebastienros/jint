using System.Numerics;
using Jint.Native.Object;
using Jint.Runtime;

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
        var days = (long)(localNs / nsPerDay);
        var remaining = (long)(localNs % nsPerDay);
        if (remaining < 0)
        {
            days--;
            remaining += nsPerDay;
        }

        // Convert days to date
        var date = EpochDaysToIsoDate(days);

        // Convert remaining nanoseconds to time
        var hour = (int)(remaining / nsPerHour);
        remaining %= nsPerHour;
        var minute = (int)(remaining / nsPerMinute);
        remaining %= nsPerMinute;
        var second = (int)(remaining / nsPerSecond);
        remaining %= nsPerSecond;
        var millisecond = (int)(remaining / nsPerMs);
        remaining %= nsPerMs;
        var microsecond = (int)(remaining / nsPerUs);
        var nanosecond = (int)(remaining % nsPerUs);

        _cachedIsoDateTime = new IsoDateTime(date, new IsoTime(hour, minute, second, millisecond, microsecond, nanosecond));
        return _cachedIsoDateTime.Value;
    }

    private static IsoDate EpochDaysToIsoDate(long days)
    {
        // Algorithm to convert days since Unix epoch to ISO date
        // Based on Howard Hinnant's algorithm
        days += 719468; // Shift epoch from 1970-01-01 to 0000-03-01

        var era = (days >= 0 ? days : days - 146096) / 146097;
        var doe = days - era * 146097; // day of era [0, 146096]
        var yoe = (doe - doe / 1460 + doe / 36524 - doe / 146096) / 365; // year of era [0, 399]
        var y = yoe + era * 400;
        var doy = doe - (365 * yoe + yoe / 4 - yoe / 100); // day of year [0, 365]
        var mp = (5 * doy + 2) / 153; // month index [0, 11]
        var d = doy - (153 * mp + 2) / 5 + 1; // day [1, 31]
        var m = mp < 10 ? mp + 3 : mp - 9; // month [1, 12]
        y += m <= 2 ? 1 : 0;

        return new IsoDate((int)y, (int)m, (int)d);
    }

    internal override bool IsTemporalZonedDateTime => true;
}
