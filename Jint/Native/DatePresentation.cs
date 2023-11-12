using System.Runtime.InteropServices;
using Jint.Native.Date;

namespace Jint.Native;

[Flags]
internal enum DateFlags : byte
{
    None = 0,
    NaN = 1,
    Infinity = 2,
    DateTimeMinValue = 4,
    DateTimeMaxValue = 8
}

[StructLayout(LayoutKind.Auto)]
internal readonly record struct DatePresentation(long Value, DateFlags Flags)
{
    public static readonly DatePresentation NaN = new(0, DateFlags.NaN);
    public static readonly DatePresentation MinValue = new(JsDate.Min, DateFlags.DateTimeMinValue);
    public static readonly DatePresentation MaxValue = new(JsDate.Max, DateFlags.DateTimeMaxValue);

    public bool DateTimeRangeValid => IsFinite && Value <= JsDate.Max && Value >= JsDate.Min;
    public bool IsNaN => (Flags & DateFlags.NaN) != DateFlags.None;
    public bool IsInfinity => (Flags & DateFlags.Infinity) != DateFlags.None;
    public bool IsFinite => (Flags & (DateFlags.NaN | DateFlags.Infinity)) == DateFlags.None;

    public DateTime ToDateTime()
    {
        return DateConstructor.Epoch.AddMilliseconds(Value);
    }

    public static implicit operator DatePresentation(long value)
    {
        return new DatePresentation(value, DateFlags.None);
    }

    public static implicit operator DatePresentation(double value)
    {
        if (double.IsInfinity(value))
        {
            return new DatePresentation(0, DateFlags.Infinity);
        }

        if (double.IsNaN(value))
        {
            return new DatePresentation(0, DateFlags.NaN);
        }

        return new DatePresentation((long) value, DateFlags.None);
    }

    public static DatePresentation operator +(DatePresentation a, DatePresentation b)
    {
        if (a.IsNaN || b.IsNaN)
        {
            return NaN;
        }

        return new DatePresentation(a.Value + b.Value, DateFlags.None);
    }

    public static DatePresentation operator -(DatePresentation a, DatePresentation b) => a + -b;
    public static DatePresentation operator +(DatePresentation a) => a;
    public static DatePresentation operator -(DatePresentation a) => a with { Value = -a.Value };

    internal DatePresentation TimeClip()
    {
        if ((Flags & (DateFlags.NaN | DateFlags.Infinity)) != DateFlags.None)
        {
            return NaN;
        }

        if (Value is < -8640000000000000 or > 8640000000000000)
        {
            return NaN;
        }

        return this;
    }

    internal JsNumber ToJsValue()
    {
        if (IsNaN || Value is < -8640000000000000 or > 8640000000000000)
        {
            return JsNumber.DoubleNaN;
        }

        return JsNumber.Create(Value);
    }
}
