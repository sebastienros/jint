using System.Buffers;
using System.Runtime.CompilerServices;

namespace Jint.Extensions;

internal static class Character
{
    /// <summary>
    /// https://tc39.es/ecma262/#ASCII-word-characters
    /// </summary>
    public const string AsciiWordCharacters = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789_";

    public const string JsWhiteSpaceCharacters = "\x20\x09\x0A\x0B\x0C\x0D\xA0\u1680\u2000\u2001\u2002\u2003\u2004\u2005\u2006\u2007\u2008\u2009\u200A\u2028\u2029\u202F\u205F\u3000\uFEFF";

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsInRange(this char c, ushort min, ushort max) => (uint) (c - min) <= (uint) (max - min);

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

    private static SearchValues<char> JsWhiteSpaceSearchValues { [MethodImpl(MethodImplOptions.AggressiveInlining)] get; } = SearchValues.Create(JsWhiteSpaceCharacters);

    /// <summary>
    /// Whether <paramref name="c"/> is JavaScript white space: the union of the <c>WhiteSpace</c> and
    /// <c>LineTerminator</c> productions. https://tc39.es/ecma262/#sec-trimstring
    /// </summary>
    /// <remarks>
    /// This is the set every language-level lane means by "white space" — <c>String.prototype.trim</c>
    /// and its two halves, the <c>StrWhiteSpace</c> a <c>StringNumericLiteral</c> may be padded with, the
    /// <c>StringIntegerLiteral</c> behind <c>BigInt</c>, and the regular-expression <c>\s</c> class. It is
    /// deliberately not <c>char.IsWhiteSpace</c>, which answers a Unicode question rather than an
    /// ECMAScript one and disagrees in both directions.
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsJsWhiteSpace(this char c)
    {
        return JsWhiteSpaceSearchValues.Contains(c);
    }

    /// <summary>
    /// Whether <paramref name="c"/> is the <c>WhiteSpace</c> production alone, which is every member of
    /// <see cref="IsJsWhiteSpace"/> that is not a line terminator. https://tc39.es/ecma262/#sec-white-space
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsJsWhiteSpaceExceptLineTerminator(this char c) => c.IsJsWhiteSpace() && !c.IsJsLineTerminator();

    /// <summary>
    /// Whether <paramref name="c"/> is <c>LineTerminator</c>: LF, CR, LS and PS.
    /// https://tc39.es/ecma262/#sec-line-terminators
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsJsLineTerminator(this char c) => c is '\n' or '\r' or '\u2028' or '\u2029';
}
