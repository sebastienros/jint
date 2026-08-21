#if NET8_0_OR_GREATER
using System.Runtime.InteropServices;
using Jint.Runtime;

namespace Jint.WebApi.Url.Pattern;

/// <summary>https://urlpattern.spec.whatwg.org/#part-type</summary>
internal enum UrlPatternPartType
{
    /// <summary>A simple fixed text string.</summary>
    FixedText,

    /// <summary>A matching group with a custom regular expression.</summary>
    Regexp,

    /// <summary>A matching group that matches up to the next delimiter code point, such as "<c>:foo</c>".</summary>
    SegmentWildcard,

    /// <summary>A matching group that greedily matches everything, such as "<c>*</c>".</summary>
    FullWildcard,
}

/// <summary>https://urlpattern.spec.whatwg.org/#part-modifier</summary>
internal enum UrlPatternModifier
{
    /// <summary>No modifier.</summary>
    None,

    /// <summary>U+003F (<c>?</c>).</summary>
    Optional,

    /// <summary>U+002A (<c>*</c>).</summary>
    ZeroOrMore,

    /// <summary>U+002B (<c>+</c>).</summary>
    OneOrMore,
}

/// <summary>
/// https://urlpattern.spec.whatwg.org/#part — one piece of a parsed pattern string: at most one matching group,
/// with fixed text before and after it and a modifier.
/// </summary>
[StructLayout(LayoutKind.Auto)]
internal readonly record struct UrlPatternPart(
    UrlPatternPartType Type,
    string Value,
    UrlPatternModifier Modifier,
    string Name,
    string Prefix,
    string Suffix);

/// <summary>
/// https://urlpattern.spec.whatwg.org/#options-header — the per-component settings that decide how a pattern
/// string behaves.
/// </summary>
/// <remarks>
/// Both code points are "one ASCII code point or the empty string"; <c>'\0'</c> is how the empty string is spelled
/// here, and <see cref="Delimiter"/> / <see cref="IsPrefix"/> are the two places that difference is observable.
/// The three named sets are the spec's
/// <a href="https://urlpattern.spec.whatwg.org/#default-options">default</a>,
/// <a href="https://urlpattern.spec.whatwg.org/#hostname-options">hostname</a> and
/// <a href="https://urlpattern.spec.whatwg.org/#pathname-options">pathname</a> options.
/// </remarks>
[StructLayout(LayoutKind.Auto)]
internal readonly record struct UrlPatternCompileOptions(char DelimiterCodePoint, char PrefixCodePoint, bool IgnoreCase)
{
    /// <summary>https://urlpattern.spec.whatwg.org/#default-options</summary>
    internal static UrlPatternCompileOptions Default(bool ignoreCase = false) => new('\0', '\0', ignoreCase);

    /// <summary>https://urlpattern.spec.whatwg.org/#hostname-options</summary>
    internal static UrlPatternCompileOptions Hostname(bool ignoreCase = false) => new('.', '\0', ignoreCase);

    /// <summary>https://urlpattern.spec.whatwg.org/#pathname-options</summary>
    internal static UrlPatternCompileOptions Pathname(bool ignoreCase = false) => new('/', '/', ignoreCase);

    /// <summary>The delimiter as a string, empty when there is none.</summary>
    internal string Delimiter => DelimiterCodePoint == '\0' ? string.Empty : DelimiterCodePoint.ToString();

    /// <summary>
    /// Whether <paramref name="value"/> is exactly the prefix code point — the spec's "is <i>options</i>'s prefix
    /// code point" comparison, which an empty prefix code point can never satisfy.
    /// </summary>
    internal bool IsPrefix(string value)
        => PrefixCodePoint != '\0' && value.Length == 1 && value[0] == PrefixCodePoint;
}

/// <summary>
/// https://urlpattern.spec.whatwg.org/#encoding-callback — validates and encodes one piece of fixed text of a
/// pattern string, or throws a <c>TypeError</c>.
/// </summary>
internal delegate string UrlPatternEncodingCallback(Realm realm, string input);
#endif
