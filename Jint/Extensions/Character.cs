using System.Runtime.CompilerServices;

namespace Jint.Extensions;

internal static class Character
{
    /// <summary>
    /// https://tc39.es/ecma262/#ASCII-word-characters
    /// </summary>
    public const string AsciiWordCharacters = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789_";

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
        if (c < 0x80)
        {
            // SP, then the TAB..CR run, which holds VT and FF along with the two ASCII line terminators.
            return c == ' ' || c.IsInRange('\t', '\r');
        }

        return IsNonAsciiJsWhiteSpace(c);
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

    /// <summary>
    /// The members above U+007F: the non-ASCII half of <c>Space_Separator</c>, ZWNBSP, and the two
    /// non-ASCII line terminators.
    /// </summary>
    /// <remarks>
    /// The <c>Space_Separator</c> members are enumerated rather than read out of
    /// <c>char.GetUnicodeCategory</c> so that every target framework answers alike: .NET Framework carries
    /// an older Unicode table than .NET 10 does, and the whole point of one definition is that it does not
    /// move under an embedder. The category has not gained or lost a code point since Unicode 6.3 dropped
    /// U+180E from it, and <c>Jint.Tests/Runtime/WhiteSpaceDefinitionTests.cs</c> checks the enumeration
    /// against the running framework's table on every target framework so a future addition cannot land
    /// unnoticed.
    /// </remarks>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static bool IsNonAsciiJsWhiteSpace(char c) => c switch
    {
        '\u00A0' or '\u1680' or '\u2028' or '\u2029' or '\u202F' or '\u205F' or '\u3000' or '\uFEFF' => true,
        _ => c.IsInRange('\u2000', '\u200A'),
    };
}
