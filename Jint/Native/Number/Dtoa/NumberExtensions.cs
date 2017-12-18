using System.Runtime.CompilerServices;

namespace Jint.Native.Number.Dtoa
{
    internal static class NumberExtensions
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static long UnsignedShift(this long l, int shift)
        {
            return (long) ((ulong) l >> shift);
        }
    }
}
