#if NET8_0_OR_GREATER
using Jint.Native;
using Jint.Runtime;

namespace Jint.WebApi.WebSockets;

/// <summary>
/// The value conversions and grammars the <c>WebSocket</c> and <c>CloseEvent</c> bindings share.
/// </summary>
internal static class WebSocketValues
{
    private const double UnsignedShortRange = 65536d;

    /// <summary>The largest <c>unsigned short</c>, which is what <c>[Clamp]</c> saturates at.</summary>
    internal const int UnsignedShortMax = 65535;

    /// <summary>
    /// The plain <c>unsigned short</c> conversion, https://webidl.spec.whatwg.org/#js-unsigned-short: a value
    /// that is not finite becomes zero, and anything else is truncated towards zero and wrapped modulo
    /// 2<sup>16</sup> — so <c>{ code: 65536 }</c> is 0 and <c>{ code: -1 }</c> is 65535.
    /// </summary>
    internal static int ToUnsignedShort(JsValue value)
    {
        var number = TypeConverter.ToNumber(value);
        if (!double.IsFinite(number))
        {
            return 0;
        }

        var wrapped = Math.Truncate(number) % UnsignedShortRange;
        if (wrapped < 0)
        {
            wrapped += UnsignedShortRange;
        }

        return (int) wrapped;
    }

    /// <summary>
    /// The <c>[Clamp] unsigned short</c> conversion, https://webidl.spec.whatwg.org/#Clamp — which is what
    /// <c>close()</c>'s <c>code</c> argument is declared as, so an out-of-range number saturates instead of
    /// wrapping and then fails the standard's own range check as the out-of-range value it was.
    /// </summary>
    /// <remarks>
    /// NaN becomes zero, and a value exactly halfway between two integers rounds to the even one, both as the
    /// extended attribute's algorithm specifies.
    /// </remarks>
    internal static int ToClampedUnsignedShort(JsValue value)
    {
        var number = TypeConverter.ToNumber(value);
        if (double.IsNaN(number))
        {
            return 0;
        }

        if (number <= 0)
        {
            return 0;
        }

        if (number >= UnsignedShortMax)
        {
            return UnsignedShortMax;
        }

        return (int) Math.Round(number, MidpointRounding.ToEven);
    }

    /// <summary>
    /// Whether <paramref name="value"/> is a valid element of a <c>Sec-WebSocket-Protocol</c> field, which is
    /// what https://websockets.spec.whatwg.org/#dom-websocket-websocket step 10 requires of every entry in
    /// <c>protocols</c>.
    /// </summary>
    /// <remarks>
    /// The protocol's own requirement — https://www.rfc-editor.org/rfc/rfc6455#section-4.1 — is that the
    /// elements "MUST be non-empty strings with characters in the range U+0021 to U+007E not including
    /// separator characters as defined in [RFC2616]", which is the HTTP <c>token</c> production.
    /// </remarks>
    internal static bool IsToken(string value)
    {
        if (value.Length == 0)
        {
            return false;
        }

        foreach (var c in value)
        {
            if (c is < '!' or > '~' || IsSeparator(c))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// RFC 2616's separators, minus the two — space and horizontal tab — that the character range above has
    /// already excluded.
    /// </summary>
    private static bool IsSeparator(char c)
        => c is '(' or ')' or '<' or '>' or '@'
            or ',' or ';' or ':' or '\\' or '"'
            or '/' or '[' or ']' or '?' or '='
            or '{' or '}';
}
#endif
