using Jint.Native.Date;
using Jint.Native.Object;
using Jint.Runtime;

namespace Jint.Native;

public sealed class JsDate : ObjectInstance
{
    // Maximum allowed value to prevent DateTime overflow
    internal static readonly long Max = (long) (DateTime.MaxValue - DateConstructor.Epoch).TotalMilliseconds;

    // Minimum allowed value to prevent DateTime overflow
    internal static readonly long Min = (long) -(DateConstructor.Epoch - DateTime.MinValue).TotalMilliseconds;

    internal DatePresentation _dateValue;

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

        ExceptionHelper.ThrowRangeError(_engine.Realm);
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
