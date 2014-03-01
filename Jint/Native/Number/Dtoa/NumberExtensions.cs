namespace Jint.Native.Number.Dtoa
{
    public static class NumberExtensions
    {
        public static long UnsignedShift(this long l, int shift)
        {
            return (long) ((ulong) l >> shift);
        }
    }
}
