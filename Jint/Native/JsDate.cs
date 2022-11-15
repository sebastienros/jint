using System.Globalization;
using Jint.Native.Date;
using Jint.Native.Object;
using Jint.Runtime;

namespace Jint.Native;

public sealed class JsDate : ObjectInstance
{
    // Maximum allowed value to prevent DateTime overflow
    private static readonly long Max = (long) (DateTime.MaxValue - DateConstructor.Epoch).TotalMilliseconds;

    // Minimum allowed value to prevent DateTime overflow
    private static readonly long Min = (long) -(DateConstructor.Epoch - DateTime.MinValue).TotalMilliseconds;

    internal double _dateValue;

    public JsDate(Engine engine, DateTimeOffset value) : this(engine, value.UtcDateTime)
    {
    }

    public JsDate(Engine engine, DateTime value) : this(engine, engine.Realm.Intrinsics.Date.FromDateTime(value))
    {
    }

    public JsDate(Engine engine, double dateValue) : base(engine, ObjectClass.Date, InternalTypes.Object | InternalTypes.PlainObject)
    {
        _prototype = engine.Realm.Intrinsics.Date.PrototypeObject;
        _dateValue = dateValue.TimeClip();
    }

    public DateTime ToDateTime()
    {
        if (DateTimeRangeValid)
        {
            return DateConstructor.Epoch.AddMilliseconds(DateValue);
        }

        ExceptionHelper.ThrowRangeError(_engine.Realm);
        return DateTime.MinValue;
    }

    public double DateValue => _dateValue;

    internal bool DateTimeRangeValid => !double.IsNaN(DateValue) && DateValue <= Max && DateValue >= Min;

    public override string ToString()
    {
        if (double.IsNaN(DateValue))
        {
            return "NaN";
        }

        if (double.IsInfinity(DateValue))
        {
            return "Infinity";
        }

        return ToDateTime().ToString("ddd MMM dd yyyy HH:mm:ss 'GMT'zzz", CultureInfo.InvariantCulture);
    }
}
