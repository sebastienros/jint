namespace System.Numerics;

#if !NETCOREAPP3_0_OR_GREATER
/// <summary>
/// The members of .NET Core 3.0's <c>System.Numerics.BitOperations</c> that Jint's code reaches for, for the targets
/// that do not carry the type (net472, netstandard2.0, netstandard2.1); the vendored GregsEx matcher uses them to walk
/// its class bitmaps. Retire this file with those targets.
/// </summary>
internal static class BitOperations
{
    /// <summary>The number of bits set in <paramref name="value"/>.</summary>
    public static int PopCount(ulong value)
    {
        // SWAR population count (Hacker's Delight 5-2).
        value -= (value >> 1) & 0x5555555555555555UL;
        value = (value & 0x3333333333333333UL) + ((value >> 2) & 0x3333333333333333UL);
        value = (value + (value >> 4)) & 0x0F0F0F0F0F0F0F0FUL;
        return (int) ((value * 0x0101010101010101UL) >> 56);
    }

    /// <summary>The number of trailing zero bits in <paramref name="value"/>; 64 when it is 0.</summary>
    public static int TrailingZeroCount(ulong value)
    {
        if (value == 0)
        {
            return 64;
        }

        // The lowest set bit isolated, then counted below it.
        return PopCount((value & (ulong) -(long) value) - 1);
    }
}
#endif
