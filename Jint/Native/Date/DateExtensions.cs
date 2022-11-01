namespace Jint.Native.Date;

internal static class DateExtensions
{
    public static DateTime ToDateTime(this double t)
    {
        return DateConstructor.Epoch.AddMilliseconds(t);
    }
        
    internal static double TimeClip(this double time)
    {
        if (double.IsInfinity(time) || double.IsNaN(time))
        {
            return double.NaN;
        }

        if (System.Math.Abs(time) > 8640000000000000)
        {
            return double.NaN;
        }
        
        return (long) time;
    }
}
