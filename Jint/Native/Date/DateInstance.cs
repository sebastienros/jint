using System.Globalization;
using Jint.Native.Object;
using Jint.Runtime;

namespace Jint.Native.Date;

public sealed class DateInstance : ObjectInstance
{
    // Maximum allowed value to prevent DateTime overflow
    private static readonly long Max = (long) (DateTime.MaxValue - DateConstructor.Epoch).TotalMilliseconds;

    // Minimum allowed value to prevent DateTime overflow
    private static readonly long Min = (long) -(DateConstructor.Epoch - DateTime.MinValue).TotalMilliseconds;

    internal double _dateValue;

    public DateInstance(Engine engine, double dateValue)
        : base(engine, ObjectClass.Date)
    {
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
