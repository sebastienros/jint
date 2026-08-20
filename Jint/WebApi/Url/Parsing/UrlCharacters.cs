#if NET8_0_OR_GREATER
using System.Buffers;
using System.Runtime.CompilerServices;

namespace Jint.WebApi.Url.Parsing;

/// <summary>
/// The code point classes the URL and host parsers branch on.
/// </summary>
/// <remarks>
/// The two forbidden sets are <see cref="SearchValues{T}"/> rather than switches because they are consulted
/// once per code point of every host, and because <c>SearchValues.Create</c> picks the representation for the
/// set it is handed — a bitmap here, which is a load and a shift.
/// </remarks>
internal static class UrlCharacters
{
    /// <summary>
    /// https://url.spec.whatwg.org/#forbidden-host-code-point
    /// </summary>
    private static readonly SearchValues<char> _forbiddenHost = SearchValues.Create("\0\t\n\r #/:<>?@[\\]^|");

    /// <summary>
    /// https://url.spec.whatwg.org/#forbidden-host-code-point
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static bool IsForbiddenHost(char c) => _forbiddenHost.Contains(c);

    /// <summary>
    /// https://url.spec.whatwg.org/#forbidden-domain-code-point — a forbidden host code point, a C0 control,
    /// U+0025 (%), or U+007F DELETE. U+0020 SPACE is already a forbidden host code point, so the
    /// <c>c &lt; 0x20</c> test covers exactly the C0 controls the first set does not.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static bool IsForbiddenDomain(char c) => c < 0x20 || c == 0x7F || c == '%' || _forbiddenHost.Contains(c);

    internal static bool ContainsForbiddenHost(ReadOnlySpan<char> input) => input.IndexOfAny(_forbiddenHost) >= 0;

    internal static bool ContainsForbiddenDomain(ReadOnlySpan<char> input)
    {
        foreach (var c in input)
        {
            if (IsForbiddenDomain(c))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>A C0 control or space, https://infra.spec.whatwg.org/#c0-control-or-space.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static bool IsC0ControlOrSpace(char c) => c <= 0x20;

    /// <summary>An ASCII tab or newline, https://infra.spec.whatwg.org/#ascii-tab-or-newline.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static bool IsTabOrNewline(char c) => c == '\t' || c == '\n' || c == '\r';

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static bool IsAsciiAlpha(char c) => (uint) ((c | 0x20) - 'a') <= 'z' - 'a';

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static bool IsAsciiDigit(char c) => (uint) (c - '0') <= '9' - '0';

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static bool IsAsciiAlphanumeric(char c) => IsAsciiAlpha(c) || IsAsciiDigit(c);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static bool IsAsciiHexDigit(char c) => IsAsciiDigit(c) || (uint) ((c | 0x20) - 'a') <= 'f' - 'a';

    internal static int HexValue(char c) => IsAsciiDigit(c) ? c - '0' : (c | 0x20) - 'a' + 10;

    internal static bool IsAsciiString(ReadOnlySpan<char> input)
    {
        foreach (var c in input)
        {
            if (c > 0x7F)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// ASCII-lowercases a string, https://infra.spec.whatwg.org/#ascii-lowercase. Deliberately not
    /// <c>ToLowerInvariant</c>: that lowercases every Unicode code point with a mapping, and a host is
    /// lowercased by the domain parser precisely at the point where only the ASCII mapping is wanted.
    /// </summary>
    internal static string AsciiLowercase(string input)
    {
        var index = -1;
        for (var i = 0; i < input.Length; i++)
        {
            if ((uint) (input[i] - 'A') <= 'Z' - 'A')
            {
                index = i;
                break;
            }
        }

        if (index < 0)
        {
            return input;
        }

        return string.Create(input.Length, (input, index), static (span, state) =>
        {
            var (source, start) = state;
            source.AsSpan(0, start).CopyTo(span);
            for (var i = start; i < source.Length; i++)
            {
                var c = source[i];
                span[i] = (uint) (c - 'A') <= 'Z' - 'A' ? (char) (c | 0x20) : c;
            }
        });
    }

    /// <summary>
    /// WebIDL's USVString conversion, https://webidl.spec.whatwg.org/#js-USVString: every unpaired surrogate
    /// becomes U+FFFD, so what follows is a scalar value string.
    /// </summary>
    /// <remarks>
    /// Every entry point of the URL API takes USVString, so this runs before anything else looks at a string.
    /// <c>UrlParser</c> folds the same conversion into its own input preparation, which is why only
    /// <c>URLSearchParams</c> — whose list holds strings the parser never sees — calls this directly.
    /// </remarks>
    internal static string ToScalarValueString(string input)
    {
        var index = -1;
        for (var i = 0; i < input.Length; i++)
        {
            var c = input[i];
            if (!char.IsSurrogate(c))
            {
                continue;
            }

            if (char.IsHighSurrogate(c) && i + 1 < input.Length && char.IsLowSurrogate(input[i + 1]))
            {
                i++;
                continue;
            }

            index = i;
            break;
        }

        if (index < 0)
        {
            return input;
        }

        return string.Create(input.Length, (input, index), static (span, state) =>
        {
            var (source, start) = state;
            source.AsSpan(0, start).CopyTo(span);
            for (var i = start; i < source.Length; i++)
            {
                var c = source[i];
                if (char.IsHighSurrogate(c) && i + 1 < source.Length && char.IsLowSurrogate(source[i + 1]))
                {
                    span[i] = c;
                    span[i + 1] = source[i + 1];
                    i++;
                    continue;
                }

                span[i] = char.IsSurrogate(c) ? '\uFFFD' : c;
            }
        });
    }

    /// <summary>
    /// https://url.spec.whatwg.org/#windows-drive-letter — two code points, an ASCII alpha followed by
    /// U+003A (:) or U+007C (|).
    /// </summary>
    internal static bool IsWindowsDriveLetter(ReadOnlySpan<char> input)
        => input.Length == 2 && IsAsciiAlpha(input[0]) && (input[1] == ':' || input[1] == '|');

    /// <summary>
    /// https://url.spec.whatwg.org/#normalized-windows-drive-letter — a Windows drive letter whose second code
    /// point is U+003A (:).
    /// </summary>
    internal static bool IsNormalizedWindowsDriveLetter(ReadOnlySpan<char> input)
        => input.Length == 2 && IsAsciiAlpha(input[0]) && input[1] == ':';

    /// <summary>
    /// https://url.spec.whatwg.org/#start-with-a-windows-drive-letter
    /// </summary>
    internal static bool StartsWithWindowsDriveLetter(ReadOnlySpan<char> input)
    {
        if (input.Length < 2 || !IsAsciiAlpha(input[0]) || (input[1] != ':' && input[1] != '|'))
        {
            return false;
        }

        return input.Length == 2 || input[2] == '/' || input[2] == '\\' || input[2] == '?' || input[2] == '#';
    }

    /// <summary>
    /// https://url.spec.whatwg.org/#single-dot-path-segment — "." or an ASCII case-insensitive match for
    /// "%2e".
    /// </summary>
    internal static bool IsSingleDotSegment(ReadOnlySpan<char> segment)
        => segment.Length switch
        {
            1 => segment[0] == '.',
            3 => segment[0] == '%' && segment[1] == '2' && (segment[2] | 0x20) == 'e',
            _ => false,
        };

    /// <summary>
    /// https://url.spec.whatwg.org/#double-dot-path-segment — "..", or an ASCII case-insensitive match for
    /// ".%2e", "%2e." or "%2e%2e".
    /// </summary>
    internal static bool IsDoubleDotSegment(ReadOnlySpan<char> segment)
    {
        return segment.Length switch
        {
            2 => segment[0] == '.' && segment[1] == '.',
            4 => (IsSingleDotSegment(segment.Slice(0, 1)) && IsSingleDotSegment(segment.Slice(1, 3)))
                 || (IsSingleDotSegment(segment.Slice(0, 3)) && IsSingleDotSegment(segment.Slice(3, 1))),
            6 => IsSingleDotSegment(segment.Slice(0, 3)) && IsSingleDotSegment(segment.Slice(3, 3)),
            _ => false,
        };
    }
}
#endif
