using Jint.Native.Temporal;

namespace Jint.Runtime;

public sealed partial class Intrinsics
{
    private TemporalInstance? _temporal;
    private TemporalNow? _temporalNow;
    private DurationConstructor? _temporalDuration;
    private InstantConstructor? _temporalInstant;
    private PlainDateConstructor? _temporalPlainDate;
    private PlainDateTimeConstructor? _temporalPlainDateTime;
    private PlainMonthDayConstructor? _temporalPlainMonthDay;
    private PlainTimeConstructor? _temporalPlainTime;
    private PlainYearMonthConstructor? _temporalPlainYearMonth;
    private ZonedDateTimeConstructor? _temporalZonedDateTime;

    internal TemporalInstance Temporal =>
        _temporal ??= new TemporalInstance(_engine, _realm, Object.PrototypeObject);

    internal TemporalNow TemporalNow =>
        _temporalNow ??= new TemporalNow(_engine, _realm, Object.PrototypeObject);

    internal DurationConstructor TemporalDuration =>
        _temporalDuration ??= new DurationConstructor(_engine, _realm, Function.PrototypeObject, Object.PrototypeObject);

    internal InstantConstructor TemporalInstant =>
        _temporalInstant ??= new InstantConstructor(_engine, _realm, Function.PrototypeObject, Object.PrototypeObject);

    internal PlainDateConstructor TemporalPlainDate =>
        _temporalPlainDate ??= new PlainDateConstructor(_engine, _realm, Function.PrototypeObject, Object.PrototypeObject);

    internal PlainDateTimeConstructor TemporalPlainDateTime =>
        _temporalPlainDateTime ??= new PlainDateTimeConstructor(_engine, _realm, Function.PrototypeObject, Object.PrototypeObject);

    internal PlainMonthDayConstructor TemporalPlainMonthDay =>
        _temporalPlainMonthDay ??= new PlainMonthDayConstructor(_engine, _realm, Function.PrototypeObject, Object.PrototypeObject);

    internal PlainTimeConstructor TemporalPlainTime =>
        _temporalPlainTime ??= new PlainTimeConstructor(_engine, _realm, Function.PrototypeObject, Object.PrototypeObject);

    internal PlainYearMonthConstructor TemporalPlainYearMonth =>
        _temporalPlainYearMonth ??= new PlainYearMonthConstructor(_engine, _realm, Function.PrototypeObject, Object.PrototypeObject);

    internal ZonedDateTimeConstructor TemporalZonedDateTime =>
        _temporalZonedDateTime ??= new ZonedDateTimeConstructor(_engine, _realm, Function.PrototypeObject, Object.PrototypeObject);
}
