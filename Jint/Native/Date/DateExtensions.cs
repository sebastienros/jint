namespace Jint.Native.Date;

internal static class DateExtensions
{
    public static DateTime ToDateTime(this double t)
    {
        return DateConstructor.Epoch.AddMilliseconds(t);
    }

    internal static DatePresentation TimeClip(this double time)
    {
        if (double.IsInfinity(time) || double.IsNaN(time))
        {
            return new DatePresentation(0, DateFlags.NaN);
        }

        if (System.Math.Abs(time) > 8640000000000000)
        {
            return new DatePresentation(0, DateFlags.NaN);
        }

        return new DatePresentation((long) time, DateFlags.None);
    }
}
