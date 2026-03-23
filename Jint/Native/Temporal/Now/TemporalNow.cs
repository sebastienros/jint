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
    private JsPlainDateTime PlainDateTime(JsValue thisObject, JsCallArguments arguments)
    {
        var calendarLike = arguments.At(0);
        var temporalTimeZoneLike = arguments.At(1);

        var calendar = ToTemporalCalendarIdentifier(calendarLike);
        var timeZoneId = ToTemporalTimeZoneIdentifier(temporalTimeZoneLike);

        var epochNs = SystemUTCEpochNanoseconds();
        var isoDateTime = GetIsoDateTimeFor(timeZoneId, epochNs);

        return new JsPlainDateTime(_engine, _realm.Intrinsics.TemporalPlainDateTime.PrototypeObject, isoDateTime, calendar);
    }

    /// <summary>
    /// https://tc39.es/proposal-temporal/#sec-temporal.now.plaindatetimeiso
    /// </summary>
    private JsPlainDateTime PlainDateTimeISO(JsValue thisObject, JsCallArguments arguments)
    {
        var timeZoneId = ToTemporalTimeZoneIdentifier(arguments.At(0));

        var epochNs = SystemUTCEpochNanoseconds();
        var isoDateTime = GetIsoDateTimeFor(timeZoneId, epochNs);

        return new JsPlainDateTime(_engine, _realm.Intrinsics.TemporalPlainDateTime.PrototypeObject, isoDateTime, "iso8601");
    }

    /// <summary>
    /// https://tc39.es/proposal-temporal/#sec-temporal.now.zoneddatetime
    /// </summary>
    private JsZonedDateTime ZonedDateTime(JsValue thisObject, JsCallArguments arguments)
    {
        var calendarLike = arguments.At(0);
        var temporalTimeZoneLike = arguments.At(1);

        var calendar = ToTemporalCalendarIdentifier(calendarLike);
        var timeZoneId = ToTemporalTimeZoneIdentifier(temporalTimeZoneLike);

        var epochNs = SystemUTCEpochNanoseconds();

        return new JsZonedDateTime(_engine, _realm.Intrinsics.TemporalZonedDateTime.PrototypeObject, epochNs, timeZoneId, calendar);
    }

    /// <summary>
    /// https://tc39.es/proposal-temporal/#sec-temporal.now.zoneddatetimeiso
    /// </summary>
    private JsZonedDateTime ZonedDateTimeISO(JsValue thisObject, JsCallArguments arguments)
    {
        var timeZoneId = ToTemporalTimeZoneIdentifier(arguments.At(0));

        var epochNs = SystemUTCEpochNanoseconds();

        return new JsZonedDateTime(_engine, _realm.Intrinsics.TemporalZonedDateTime.PrototypeObject, epochNs, timeZoneId, "iso8601");
    }

    /// <summary>
    /// https://tc39.es/proposal-temporal/#sec-temporal.now.plaindate
    /// </summary>
    private JsPlainDate PlainDate(JsValue thisObject, JsCallArguments arguments)
    {
        var calendarLike = arguments.At(0);
        var temporalTimeZoneLike = arguments.At(1);

        var calendar = ToTemporalCalendarIdentifier(calendarLike);
        var timeZoneId = ToTemporalTimeZoneIdentifier(temporalTimeZoneLike);

        var epochNs = SystemUTCEpochNanoseconds();
        var isoDateTime = GetIsoDateTimeFor(timeZoneId, epochNs);

        return new JsPlainDate(_engine, _realm.Intrinsics.TemporalPlainDate.PrototypeObject, isoDateTime.Date, calendar);
    }

    /// <summary>
    /// https://tc39.es/proposal-temporal/#sec-temporal.now.plaindateiso
    /// </summary>
    private JsPlainDate PlainDateISO(JsValue thisObject, JsCallArguments arguments)
    {
        var timeZoneId = ToTemporalTimeZoneIdentifier(arguments.At(0));

        var epochNs = SystemUTCEpochNanoseconds();
        var isoDateTime = GetIsoDateTimeFor(timeZoneId, epochNs);

        return new JsPlainDate(_engine, _realm.Intrinsics.TemporalPlainDate.PrototypeObject, isoDateTime.Date, "iso8601");
    }

    /// <summary>
    /// https://tc39.es/proposal-temporal/#sec-temporal.now.plaintimeiso
    /// </summary>
    private JsPlainTime PlainTimeISO(JsValue thisObject, JsCallArguments arguments)
    {
        var timeZoneId = ToTemporalTimeZoneIdentifier(arguments.At(0));

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

        return TemporalHelpers.EpochNanosecondsToIsoDateTime(localNs);
    }

    /// <summary>
    /// Validates and canonicalizes a calendar identifier.
    /// https://tc39.es/proposal-temporal/#sec-temporal-totemporalcalendar
    /// </summary>
    private string ToTemporalCalendarIdentifier(JsValue calendarLike)
    {
        return TemporalHelpers.ToTemporalCalendarIdentifier(_realm, calendarLike);
    }

    /// <summary>
    /// Extracts and validates a time zone identifier.
    /// For Temporal.Now, undefined means the system default time zone.
    /// </summary>
    private string ToTemporalTimeZoneIdentifier(JsValue timeZoneLike)
    {
        if (timeZoneLike.IsUndefined())
        {
            return _engine.Options.Temporal.TimeZoneProvider.GetDefaultTimeZone();
        }

        return TemporalHelpers.ToTemporalTimeZoneIdentifier(_engine, _realm, timeZoneLike);
    }

}
