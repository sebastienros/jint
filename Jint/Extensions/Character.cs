using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace Jint.Extensions;

internal static class Character
{
    /// <summary>
    /// https://tc39.es/ecma262/#ASCII-word-characters
    /// </summary>
    public const string AsciiWordCharacters = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789_";

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsInRange(this char c, ushort min, ushort max) => (uint)(c - min) <= (uint)(max - min);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsOctalDigit(this char c) => c.IsInRange('0', '7');

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsDecimalDigit(this char c) => c.IsInRange('0', '9');

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsHexDigit(this char c)
    {
        // NOTE: On 32-bit architectures this is not optimal, lookup is supposed to be faster.
        // But to keep it simple, we use this method regardless of CPU architecture, and if performance
        // needs to be improved further, the lookup approach can be ported from Esprima.HexConverter.

        // This code path, when used, has no branches and doesn't depend on cache hits,
        // so it's faster and does not vary in speed depending on input data distribution.
        // The magic constant 18428868213665201664 is a 64 bit value containing 1s at the
        // indices corresponding to all the valid hex characters (ie. "0123456789ABCDEFabcdef")
        // minus 48 (ie. '0'), and backwards (so from the most significant bit and downwards).
        // The offset of 48 for each bit is necessary so that the entire range fits in 64 bits.
        // First, we subtract '0' to the input digit (after casting to uint to account for any
        // negative inputs). Note that even if this subtraction underflows, this happens before
        // the result is zero-extended to ulong, meaning that `i` will always have upper 32 bits
        // equal to 0. We then left shift the constant with this offset, and apply a bitmask that
        // has the highest bit set (the sign bit) if and only if `c` is in the ['0', '0' + 64) range.
        // Then we only need to check whether this final result is less than 0: this will only be
        // the case if both `i` was in fact the index of a set bit in the magic constant, and also
        // `c` was in the allowed range (this ensures that false positive bit shifts are ignored).
        ulong i = (uint) c - '0';
        ulong shift = 18428868213665201664UL << (int) i;
        ulong mask = i - 64;

        return (long) (shift & mask) < 0 ? true : false;
    }
}
