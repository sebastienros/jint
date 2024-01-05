using System.Runtime.CompilerServices;

namespace Jint.Native.Number.Dtoa;

internal static class NumberExtensions
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static long UnsignedShift(this long l, int shift)
    {
        return (long) ((ulong) l >> shift);
    }
        
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static ulong UnsignedShift(this ulong l, int shift)
    {
        return l >> shift;
    }
}
