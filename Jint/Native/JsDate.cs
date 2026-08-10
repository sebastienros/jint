using Jint.Native.Date;
using Jint.Native.Object;
using Jint.Runtime;

namespace Jint.Native;

public sealed class JsDate : ObjectInstance
{
    // Maximum allowed value to prevent DateTime overflow. Counted in ticks rather than read off
    // TimeSpan.TotalMilliseconds: that property is a double division, and the correctly rounded quotient
    // for DateTime.MaxValue (9999-12-31T23:59:59.9999999, so 253402300799999.9999 ms) is 253402300800000
    // exactly - the first millisecond of year 10000, which DateTime cannot represent at all. The bound
    // therefore used to admit one value that every conversion behind it threw ArgumentOutOfRangeException
    // on, and a CLR exception escapes engine.Evaluate rather than reaching script as a JavaScriptException.
    internal static readonly long Max = (DateTime.MaxValue - DateConstructor.Epoch).Ticks / TimeSpan.TicksPerMillisecond;

    // Minimum allowed value to prevent DateTime overflow. The epoch is a whole number of milliseconds
    // after DateTime.MinValue, so this one was never off; it is counted the same way for symmetry.
    internal static readonly long Min = -((DateConstructor.Epoch - DateTime.MinValue).Ticks / TimeSpan.TicksPerMillisecond);

    // The clipped time value is decomposed into a long + a DateFlags byte rather than stored as the
    // 16-byte DatePresentation struct: the flag byte rides in the object's existing field padding, so
    // every JsDate instance is 8 bytes smaller on the hot `new Date()` allocation path. DatePresentation
    // remains the transient computation/return type, reconstructed on access below.
    private long _dateValueMs;
    private DateFlags _dateFlags;

    internal DatePresentation _dateValue
    {
        get => new(_dateValueMs, _dateFlags);
        set
        {
            _dateValueMs = value.Value;
            _dateFlags = value.Flags;
        }
    }

    public JsDate(Engine engine, DateTimeOffset value) : this(engine, value.UtcDateTime)
    {
    }

    public JsDate(Engine engine, DateTime value) : this(engine, engine.Realm.Intrinsics.Date.FromDateTime(value))
    {
    }

    public JsDate(Engine engine, long dateValue) : this(engine, new DatePresentation(dateValue, DateFlags.None))
    {
    }

    internal JsDate(Engine engine, DatePresentation dateValue) : base(engine, ObjectClass.Date, InternalTypes.Object | InternalTypes.PlainObject)
    {
        _prototype = engine.Realm.Intrinsics.Date.PrototypeObject;
        _dateValue = dateValue.TimeClip();
    }

    private JsDate(Engine engine, long dateValueMs, DateFlags flags) : base(engine, ObjectClass.Date, InternalTypes.Object | InternalTypes.PlainObject)
    {
        _prototype = engine.Realm.Intrinsics.Date.PrototypeObject;
        _dateValueMs = dateValueMs;
        _dateFlags = flags;
    }

    /// <summary>
    /// The `new Date()` fast path: a millisecond value derived from clock ticks is always inside
    /// the clip range, so the TimeClip validation and the double round-trip are unnecessary.
    /// </summary>
    internal static JsDate CreateForCurrentTime(Engine engine, long dateValueMs) => new(engine, dateValueMs, DateFlags.None);

    public DateTime ToDateTime()
    {
        if (_dateValue.Flags == DateFlags.DateTimeMinValue)
        {
            return DateTime.MinValue;
        }

        if (_dateValue.Flags == DateFlags.DateTimeMaxValue)
        {
            return DateTime.MaxValue;
        }

        if (_dateValue.DateTimeRangeValid)
        {
            var dateTime = DateConstructor.Epoch.AddMilliseconds(_dateValue.Value);
            if (_engine.Options.Interop.DateTimeKind == DateTimeKind.Local)
            {
                dateTime = TimeZoneInfo.ConvertTimeFromUtc(dateTime, _engine.Options.TimeSystem.DefaultTimeZone);
                dateTime = DateTime.SpecifyKind(dateTime, DateTimeKind.Local);
            }
            return dateTime;
        }

        Throw.RangeError(_engine.Realm);
        return DateTime.MinValue;
    }

    public double DateValue => _dateValue.IsFinite ? _dateValue.Value : double.NaN;

    internal bool DateTimeRangeValid => _dateValue.DateTimeRangeValid;

    public override string ToString()
    {
        if (_dateValue.IsNaN)
        {
            return "NaN";
        }

        if (_dateValue.IsInfinity)
        {
            return "Infinity";
        }

        return TypeConverter.ToString(this);
    }
}
