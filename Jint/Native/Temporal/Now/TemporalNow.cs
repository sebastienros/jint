using System.Numerics;
using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Descriptors;

namespace Jint.Native.Temporal;

/// <summary>
/// The Temporal.Now object.
/// https://tc39.es/proposal-temporal/#sec-temporal-now-object
/// </summary>
[JsObject]
internal sealed partial class TemporalNow : ObjectInstance
{
    private readonly Realm _realm;

    [JsSymbol("ToStringTag", Flags = PropertyFlag.Configurable)]
    private static readonly JsString ToStringTagValue = new("Temporal.Now");

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
        CreateProperties_Generated();
        CreateSymbols_Generated();
    }

    /// <summary>
    /// https://tc39.es/proposal-temporal/#sec-temporal.now.timezoneid
    /// </summary>
    [JsFunction]
    private JsString TimeZoneId(JsValue thisObject)
    {
        var provider = _engine.Options.Temporal.TimeZoneProvider;
        return new JsString(provider.GetDefaultTimeZone());
    }

    /// <summary>
    /// https://tc39.es/proposal-temporal/#sec-temporal.now.instant
    /// </summary>
    [JsFunction]
    private JsInstant Instant(JsValue thisObject)
    {
        var epochNs = SystemUTCEpochNanoseconds();
        return new JsInstant(_engine, _realm.Intrinsics.TemporalInstant.PrototypeObject, epochNs);
    }

    /// <summary>
    /// https://tc39.es/proposal-temporal/#sec-temporal.now.plaindatetime
    /// </summary>
    [JsFunction(Length = 1)]
    private JsPlainDateTime PlainDateTime(JsValue thisObject, JsValue calendarLike, JsValue temporalTimeZoneLike)
    {
        var calendar = ToTemporalCalendarIdentifier(calendarLike);
        var timeZoneId = ToTemporalTimeZoneIdentifier(temporalTimeZoneLike);

        var epochNs = SystemUTCEpochNanoseconds();
        var isoDateTime = GetIsoDateTimeFor(timeZoneId, epochNs);

        return new JsPlainDateTime(_engine, _realm.Intrinsics.TemporalPlainDateTime.PrototypeObject, isoDateTime, calendar);
    }

    /// <summary>
    /// https://tc39.es/proposal-temporal/#sec-temporal.now.plaindatetimeiso
    /// </summary>
    [JsFunction(Length = 0)]
    private JsPlainDateTime PlainDateTimeISO(JsValue thisObject, JsValue temporalTimeZoneLike)
    {
        var timeZoneId = ToTemporalTimeZoneIdentifier(temporalTimeZoneLike);

        var epochNs = SystemUTCEpochNanoseconds();
        var isoDateTime = GetIsoDateTimeFor(timeZoneId, epochNs);

        return new JsPlainDateTime(_engine, _realm.Intrinsics.TemporalPlainDateTime.PrototypeObject, isoDateTime, "iso8601");
    }

    /// <summary>
    /// https://tc39.es/proposal-temporal/#sec-temporal.now.zoneddatetime
    /// </summary>
    [JsFunction(Length = 1)]
    private JsZonedDateTime ZonedDateTime(JsValue thisObject, JsValue calendarLike, JsValue temporalTimeZoneLike)
    {
        var calendar = ToTemporalCalendarIdentifier(calendarLike);
        var timeZoneId = ToTemporalTimeZoneIdentifier(temporalTimeZoneLike);

        var epochNs = SystemUTCEpochNanoseconds();

        return new JsZonedDateTime(_engine, _realm.Intrinsics.TemporalZonedDateTime.PrototypeObject, epochNs, timeZoneId, calendar);
    }

    /// <summary>
    /// https://tc39.es/proposal-temporal/#sec-temporal.now.zoneddatetimeiso
    /// </summary>
    [JsFunction(Length = 0)]
    private JsZonedDateTime ZonedDateTimeISO(JsValue thisObject, JsValue temporalTimeZoneLike)
    {
        var timeZoneId = ToTemporalTimeZoneIdentifier(temporalTimeZoneLike);

        var epochNs = SystemUTCEpochNanoseconds();

        return new JsZonedDateTime(_engine, _realm.Intrinsics.TemporalZonedDateTime.PrototypeObject, epochNs, timeZoneId, "iso8601");
    }

    /// <summary>
    /// https://tc39.es/proposal-temporal/#sec-temporal.now.plaindate
    /// </summary>
    [JsFunction(Length = 1)]
    private JsPlainDate PlainDate(JsValue thisObject, JsValue calendarLike, JsValue temporalTimeZoneLike)
    {
        var calendar = ToTemporalCalendarIdentifier(calendarLike);
        var timeZoneId = ToTemporalTimeZoneIdentifier(temporalTimeZoneLike);

        var epochNs = SystemUTCEpochNanoseconds();
        var isoDateTime = GetIsoDateTimeFor(timeZoneId, epochNs);

        return new JsPlainDate(_engine, _realm.Intrinsics.TemporalPlainDate.PrototypeObject, isoDateTime.Date, calendar);
    }

    /// <summary>
    /// https://tc39.es/proposal-temporal/#sec-temporal.now.plaindateiso
    /// </summary>
    [JsFunction(Length = 0)]
    private JsPlainDate PlainDateISO(JsValue thisObject, JsValue temporalTimeZoneLike)
    {
        var timeZoneId = ToTemporalTimeZoneIdentifier(temporalTimeZoneLike);

        var epochNs = SystemUTCEpochNanoseconds();
        var isoDateTime = GetIsoDateTimeFor(timeZoneId, epochNs);

        return new JsPlainDate(_engine, _realm.Intrinsics.TemporalPlainDate.PrototypeObject, isoDateTime.Date, "iso8601");
    }

    /// <summary>
    /// https://tc39.es/proposal-temporal/#sec-temporal.now.plaintimeiso
    /// </summary>
    [JsFunction(Length = 0)]
    private JsPlainTime PlainTimeISO(JsValue thisObject, JsValue temporalTimeZoneLike)
    {
        var timeZoneId = ToTemporalTimeZoneIdentifier(temporalTimeZoneLike);

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
