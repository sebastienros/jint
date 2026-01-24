using System.Numerics;
using Jint.Native.Object;
using Jint.Native.Symbol;
using Jint.Runtime;
using Jint.Runtime.Descriptors;
using Jint.Runtime.Interop;

namespace Jint.Native.Temporal;

/// <summary>
/// The Temporal.Now object.
/// https://tc39.es/proposal-temporal/#sec-temporal-now-object
/// </summary>
internal sealed class TemporalNow : ObjectInstance
{
    private readonly Realm _realm;

    internal TemporalNow(
        Engine engine,
        Realm realm,
        ObjectPrototype objectPrototype) : base(engine)
    {
        _realm = realm;
        _prototype = objectPrototype;
    }

    protected override void Initialize()
    {
        const PropertyFlag PropertyFlags = PropertyFlag.Writable | PropertyFlag.Configurable;
        const PropertyFlag LengthFlags = PropertyFlag.Configurable;

        var properties = new PropertyDictionary(8, checkExistingKeys: false)
        {
            ["timeZoneId"] = new(new ClrFunction(Engine, "timeZoneId", TimeZoneId, 0, LengthFlags), PropertyFlags),
            ["instant"] = new(new ClrFunction(Engine, "instant", Instant, 0, LengthFlags), PropertyFlags),
            ["plainDateTime"] = new(new ClrFunction(Engine, "plainDateTime", PlainDateTime, 1, LengthFlags), PropertyFlags),
            ["plainDateTimeISO"] = new(new ClrFunction(Engine, "plainDateTimeISO", PlainDateTimeISO, 0, LengthFlags), PropertyFlags),
            ["zonedDateTime"] = new(new ClrFunction(Engine, "zonedDateTime", ZonedDateTime, 1, LengthFlags), PropertyFlags),
            ["zonedDateTimeISO"] = new(new ClrFunction(Engine, "zonedDateTimeISO", ZonedDateTimeISO, 0, LengthFlags), PropertyFlags),
            ["plainDate"] = new(new ClrFunction(Engine, "plainDate", PlainDate, 1, LengthFlags), PropertyFlags),
            ["plainDateISO"] = new(new ClrFunction(Engine, "plainDateISO", PlainDateISO, 0, LengthFlags), PropertyFlags),
            ["plainTimeISO"] = new(new ClrFunction(Engine, "plainTimeISO", PlainTimeISO, 0, LengthFlags), PropertyFlags),
        };
        SetProperties(properties);

        var symbols = new SymbolDictionary(1)
        {
            [GlobalSymbolRegistry.ToStringTag] = new("Temporal.Now", PropertyFlag.Configurable)
        };
        SetSymbols(symbols);
    }

    /// <summary>
    /// https://tc39.es/proposal-temporal/#sec-temporal.now.timezoneid
    /// </summary>
    private JsString TimeZoneId(JsValue thisObject, JsCallArguments arguments)
    {
        var provider = _engine.Options.Temporal.TimeZoneProvider;
        return new JsString(provider.GetDefaultTimeZone());
    }

    /// <summary>
    /// https://tc39.es/proposal-temporal/#sec-temporal.now.instant
    /// </summary>
    private JsInstant Instant(JsValue thisObject, JsCallArguments arguments)
    {
        var epochNs = SystemUTCEpochNanoseconds();
        return new JsInstant(_engine, _realm.Intrinsics.TemporalInstant.PrototypeObject, epochNs);
    }

    /// <summary>
    /// https://tc39.es/proposal-temporal/#sec-temporal.now.plaindatetime
    /// </summary>
    private JsValue PlainDateTime(JsValue thisObject, JsCallArguments arguments)
    {
        // TODO: Implement with calendar support
        Throw.TypeError(_realm, "Temporal.Now.plainDateTime is not yet implemented");
        return Undefined;
    }

    /// <summary>
    /// https://tc39.es/proposal-temporal/#sec-temporal.now.plaindatetimeiso
    /// </summary>
    private JsPlainDateTime PlainDateTimeISO(JsValue thisObject, JsCallArguments arguments)
    {
        var timeZone = arguments.At(0);
        var timeZoneId = timeZone.IsUndefined()
            ? _engine.Options.Temporal.TimeZoneProvider.GetDefaultTimeZone()
            : TypeConverter.ToString(timeZone);

        var epochNs = SystemUTCEpochNanoseconds();
        var isoDateTime = GetIsoDateTimeFor(timeZoneId, epochNs);

        return new JsPlainDateTime(_engine, _realm.Intrinsics.TemporalPlainDateTime.PrototypeObject, isoDateTime, "iso8601");
    }

    /// <summary>
    /// https://tc39.es/proposal-temporal/#sec-temporal.now.zoneddatetime
    /// </summary>
    private JsValue ZonedDateTime(JsValue thisObject, JsCallArguments arguments)
    {
        // TODO: Implement with calendar support
        Throw.TypeError(_realm, "Temporal.Now.zonedDateTime is not yet implemented");
        return Undefined;
    }

    /// <summary>
    /// https://tc39.es/proposal-temporal/#sec-temporal.now.zoneddatetimeiso
    /// </summary>
    private JsZonedDateTime ZonedDateTimeISO(JsValue thisObject, JsCallArguments arguments)
    {
        var timeZone = arguments.At(0);
        var timeZoneId = timeZone.IsUndefined()
            ? _engine.Options.Temporal.TimeZoneProvider.GetDefaultTimeZone()
            : TypeConverter.ToString(timeZone);

        var epochNs = SystemUTCEpochNanoseconds();

        return new JsZonedDateTime(_engine, _realm.Intrinsics.TemporalZonedDateTime.PrototypeObject, epochNs, timeZoneId, "iso8601");
    }

    /// <summary>
    /// https://tc39.es/proposal-temporal/#sec-temporal.now.plaindate
    /// </summary>
    private JsValue PlainDate(JsValue thisObject, JsCallArguments arguments)
    {
        // TODO: Implement with calendar support
        Throw.TypeError(_realm, "Temporal.Now.plainDate is not yet implemented");
        return Undefined;
    }

    /// <summary>
    /// https://tc39.es/proposal-temporal/#sec-temporal.now.plaindateiso
    /// </summary>
    private JsPlainDate PlainDateISO(JsValue thisObject, JsCallArguments arguments)
    {
        var timeZone = arguments.At(0);
        var timeZoneId = timeZone.IsUndefined()
            ? _engine.Options.Temporal.TimeZoneProvider.GetDefaultTimeZone()
            : TypeConverter.ToString(timeZone);

        var epochNs = SystemUTCEpochNanoseconds();
        var isoDateTime = GetIsoDateTimeFor(timeZoneId, epochNs);

        return new JsPlainDate(_engine, _realm.Intrinsics.TemporalPlainDate.PrototypeObject, isoDateTime.Date, "iso8601");
    }

    /// <summary>
    /// https://tc39.es/proposal-temporal/#sec-temporal.now.plaintimeiso
    /// </summary>
    private JsPlainTime PlainTimeISO(JsValue thisObject, JsCallArguments arguments)
    {
        var timeZone = arguments.At(0);
        var timeZoneId = timeZone.IsUndefined()
            ? _engine.Options.Temporal.TimeZoneProvider.GetDefaultTimeZone()
            : TypeConverter.ToString(timeZone);

        var epochNs = SystemUTCEpochNanoseconds();
        var isoDateTime = GetIsoDateTimeFor(timeZoneId, epochNs);

        return new JsPlainTime(_engine, _realm.Intrinsics.TemporalPlainTime.PrototypeObject, isoDateTime.Time);
    }

    /// <summary>
    /// Gets the current system time as nanoseconds since Unix epoch.
    /// </summary>
    private BigInteger SystemUTCEpochNanoseconds()
    {
        var now = _engine.Options.TimeSystem.GetUtcNow();
        // Convert DateTimeOffset to nanoseconds since Unix epoch
        var milliseconds = now.ToUnixTimeMilliseconds();
        return (BigInteger) milliseconds * 1_000_000;
    }

    /// <summary>
    /// Converts an instant to an ISO date-time in the given time zone.
    /// </summary>
    private IsoDateTime GetIsoDateTimeFor(string timeZoneId, BigInteger epochNs)
    {
        var provider = _engine.Options.Temporal.TimeZoneProvider;
        var offsetNs = provider.GetOffsetNanosecondsFor(timeZoneId, epochNs);
        var localNs = epochNs + offsetNs;

        return EpochNanosecondsToIsoDateTime(localNs);
    }

    /// <summary>
    /// Converts nanoseconds since epoch to ISO date-time components.
    /// </summary>
    private static IsoDateTime EpochNanosecondsToIsoDateTime(BigInteger localNs)
    {
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
        var date = EpochDaysToIsoDate(days);

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

        return new IsoDateTime(date, new IsoTime(hour, minute, second, millisecond, microsecond, nanosecond));
    }

    /// <summary>
    /// Converts days since Unix epoch to ISO date.
    /// </summary>
    private static IsoDate EpochDaysToIsoDate(long days)
    {
        // Algorithm based on Howard Hinnant's algorithm
        days += 719468; // Shift epoch from 1970-01-01 to 0000-03-01

        var era = (days >= 0 ? days : days - 146096) / 146097;
        var doe = days - era * 146097;
        var yoe = (doe - doe / 1460 + doe / 36524 - doe / 146096) / 365;
        var y = yoe + era * 400;
        var doy = doe - (365 * yoe + yoe / 4 - yoe / 100);
        var mp = (5 * doy + 2) / 153;
        var d = doy - (153 * mp + 2) / 5 + 1;
        var m = mp < 10 ? mp + 3 : mp - 9;
        y += m <= 2 ? 1 : 0;

        return new IsoDate((int) y, (int) m, (int) d);
    }
}
