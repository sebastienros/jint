#if NET8_0_OR_GREATER
using System.Text;

namespace Jint.WebApi.Url.Pattern;

/// <summary>
/// Converts a part list back into the two strings a component keeps: the regular expression source of
/// https://urlpattern.spec.whatwg.org/#converting-part-lists-to-regular-expressions, and the normalized pattern
/// string of https://urlpattern.spec.whatwg.org/#converting-part-lists-to-pattern-strings that the eight
/// <c>URLPattern</c> accessors return.
/// </summary>
internal static class UrlPatternGenerator
{
    /// <summary>https://urlpattern.spec.whatwg.org/#full-wildcard-regexp-value</summary>
    internal const string FullWildcardRegexpValue = ".*";

    /// <summary>
    /// https://urlpattern.spec.whatwg.org/#generate-a-segment-wildcard-regexp — what an unadorned "<c>:foo</c>"
    /// matches, which is everything up to the component's own delimiter.
    /// </summary>
    internal static string GenerateSegmentWildcardRegexp(UrlPatternCompileOptions options)
    {
        var result = new ValueStringBuilder(stackalloc char[8]);
        try
        {
            result.Append("[^");
            AppendEscapedRegexpString(ref result, options.Delimiter);
            result.Append("]+?");
            return result.AsSpan().ToString();
        }
        finally
        {
            result.Dispose();
        }
    }

    /// <summary>
    /// https://urlpattern.spec.whatwg.org/#generate-a-regular-expression-and-name-list — the anchored regular
    /// expression source, plus the capture group names in the order the groups appear in it.
    /// </summary>
    /// <remarks>
    /// The names ride alongside rather than becoming named capture groups: that is what path-to-regexp does, and
    /// the spec keeps it deliberately, so a group named "<c>0</c>" (the automatic name of an unnamed group) stays
    /// expressible.
    /// </remarks>
    internal static (string Source, string[] NameList) GenerateRegularExpressionAndNameList(
        List<UrlPatternPart> partList,
        UrlPatternCompileOptions options)
    {
        var nameList = new List<string>();
        var result = new ValueStringBuilder(stackalloc char[128]);
        try
        {
            result.Append('^');

            foreach (var part in partList)
            {
                if (part.Type == UrlPatternPartType.FixedText)
                {
                    if (part.Modifier == UrlPatternModifier.None)
                    {
                        AppendEscapedRegexpString(ref result, part.Value);
                    }
                    else
                    {
                        // "(?:<fixed text>)<modifier>"
                        result.Append("(?:");
                        AppendEscapedRegexpString(ref result, part.Value);
                        result.Append(')');
                        result.Append(ConvertModifierToString(part.Modifier));
                    }

                    continue;
                }

                nameList.Add(part.Name);

                var regexpValue = part.Type switch
                {
                    UrlPatternPartType.SegmentWildcard => GenerateSegmentWildcardRegexp(options),
                    UrlPatternPartType.FullWildcard => FullWildcardRegexpValue,
                    _ => part.Value,
                };

                if (part.Prefix.Length == 0 && part.Suffix.Length == 0)
                {
                    if (part.Modifier is UrlPatternModifier.None or UrlPatternModifier.Optional)
                    {
                        // "(<regexp value>)<modifier>"
                        result.Append('(');
                        result.Append(regexpValue);
                        result.Append(')');
                        result.Append(ConvertModifierToString(part.Modifier));
                    }
                    else
                    {
                        // "((?:<regexp value>)<modifier>)"
                        result.Append("((?:");
                        result.Append(regexpValue);
                        result.Append(')');
                        result.Append(ConvertModifierToString(part.Modifier));
                        result.Append(')');
                    }

                    continue;
                }

                if (part.Modifier is UrlPatternModifier.None or UrlPatternModifier.Optional)
                {
                    // "(?:<prefix>(<regexp value>)<suffix>)<modifier>"
                    result.Append("(?:");
                    AppendEscapedRegexpString(ref result, part.Prefix);
                    result.Append('(');
                    result.Append(regexpValue);
                    result.Append(')');
                    AppendEscapedRegexpString(ref result, part.Suffix);
                    result.Append(')');
                    result.Append(ConvertModifierToString(part.Modifier));
                    continue;
                }

                // A repeating part with a prefix or suffix excludes the first prefix and the last suffix but keeps
                // both between repetitions, so the body appears twice:
                // "(?:<prefix>((?:<regexp value>)(?:<suffix><prefix>(?:<regexp value>))*)<suffix>)?"
                result.Append("(?:");
                AppendEscapedRegexpString(ref result, part.Prefix);
                result.Append("((?:");
                result.Append(regexpValue);
                result.Append(")(?:");
                AppendEscapedRegexpString(ref result, part.Suffix);
                AppendEscapedRegexpString(ref result, part.Prefix);
                result.Append("(?:");
                result.Append(regexpValue);
                result.Append("))*)");
                AppendEscapedRegexpString(ref result, part.Suffix);
                result.Append(')');

                if (part.Modifier == UrlPatternModifier.ZeroOrMore)
                {
                    result.Append('?');
                }
            }

            result.Append('$');
            return (result.AsSpan().ToString(), nameList.ToArray());
        }
        finally
        {
            result.Dispose();
        }
    }

    /// <summary>
    /// https://urlpattern.spec.whatwg.org/#generate-a-pattern-string — the normalized pattern string, which is
    /// what every one of the eight component accessors returns and is itself a well formed pattern string.
    /// </summary>
    internal static string GeneratePatternString(List<UrlPatternPart> partList, UrlPatternCompileOptions options)
    {
        var result = new ValueStringBuilder(stackalloc char[128]);
        try
        {
            for (var index = 0; index < partList.Count; index++)
            {
                var part = partList[index];
                var previousPart = index > 0 ? partList[index - 1] : (UrlPatternPart?) null;
                var nextPart = index < partList.Count - 1 ? partList[index + 1] : (UrlPatternPart?) null;

                if (part.Type == UrlPatternPartType.FixedText)
                {
                    if (part.Modifier == UrlPatternModifier.None)
                    {
                        AppendEscapedPatternString(ref result, part.Value);
                        continue;
                    }

                    result.Append('{');
                    AppendEscapedPatternString(ref result, part.Value);
                    result.Append('}');
                    result.Append(ConvertModifierToString(part.Modifier));
                    continue;
                }

                var customName = !IsAsciiDigit(part.Name[0]);
                var needsGrouping = part.Suffix.Length != 0
                    || (part.Prefix.Length != 0 && !options.IsPrefix(part.Prefix));

                if (!needsGrouping
                    && customName
                    && part.Type == UrlPatternPartType.SegmentWildcard
                    && part.Modifier == UrlPatternModifier.None
                    && nextPart is { } following
                    && following.Prefix.Length == 0
                    && following.Suffix.Length == 0)
                {
                    // Without braces, whatever follows would be read as more of this group's name.
                    needsGrouping = following.Type == UrlPatternPartType.FixedText
                        ? following.Value.Length != 0
                            && UrlPatternTokenizer.IsValidNameCodePoint(FirstCodePoint(following.Value), first: false)
                        : IsAsciiDigit(following.Name[0]);
                }

                if (!needsGrouping
                    && part.Prefix.Length == 0
                    && previousPart is { Type: UrlPatternPartType.FixedText } preceding
                    && preceding.Value.Length != 0
                    && options.PrefixCodePoint != '\0'
                    && preceding.Value[preceding.Value.Length - 1] == options.PrefixCodePoint)
                {
                    // The preceding fixed text ends with the prefix code point, which would otherwise be read as
                    // this group's automatic prefix.
                    needsGrouping = true;
                }

                if (needsGrouping)
                {
                    result.Append('{');
                }

                AppendEscapedPatternString(ref result, part.Prefix);

                if (customName)
                {
                    result.Append(':');
                    result.Append(part.Name);
                }

                if (part.Type == UrlPatternPartType.Regexp)
                {
                    result.Append('(');
                    result.Append(part.Value);
                    result.Append(')');
                }
                else if (part.Type == UrlPatternPartType.SegmentWildcard && !customName)
                {
                    result.Append('(');
                    result.Append(GenerateSegmentWildcardRegexp(options));
                    result.Append(')');
                }
                else if (part.Type == UrlPatternPartType.FullWildcard)
                {
                    if (!customName
                        && (previousPart is null
                            || previousPart.Value.Type == UrlPatternPartType.FixedText
                            || previousPart.Value.Modifier != UrlPatternModifier.None
                            || needsGrouping
                            || part.Prefix.Length != 0))
                    {
                        result.Append('*');
                    }
                    else
                    {
                        result.Append('(');
                        result.Append(FullWildcardRegexpValue);
                        result.Append(')');
                    }
                }

                if (part.Type == UrlPatternPartType.SegmentWildcard
                    && customName
                    && part.Suffix.Length != 0
                    && UrlPatternTokenizer.IsValidNameCodePoint(FirstCodePoint(part.Suffix), first: false))
                {
                    // The suffix would otherwise extend the name.
                    result.Append('\\');
                }

                AppendEscapedPatternString(ref result, part.Suffix);

                if (needsGrouping)
                {
                    result.Append('}');
                }

                result.Append(ConvertModifierToString(part.Modifier));
            }

            return result.AsSpan().ToString();
        }
        finally
        {
            result.Dispose();
        }
    }

    /// <summary>https://urlpattern.spec.whatwg.org/#convert-a-modifier-to-a-string</summary>
    private static string ConvertModifierToString(UrlPatternModifier modifier) => modifier switch
    {
        UrlPatternModifier.ZeroOrMore => "*",
        UrlPatternModifier.Optional => "?",
        UrlPatternModifier.OneOrMore => "+",
        _ => string.Empty,
    };

    /// <summary>https://urlpattern.spec.whatwg.org/#escape-a-regexp-string</summary>
    private static void AppendEscapedRegexpString(ref ValueStringBuilder builder, string input)
    {
        foreach (var c in input)
        {
            if (c is '.' or '+' or '*' or '?' or '^' or '$' or '{' or '}' or '(' or ')' or '[' or ']' or '|' or '/' or '\\')
            {
                builder.Append('\\');
            }

            builder.Append(c);
        }
    }

    /// <summary>https://urlpattern.spec.whatwg.org/#escape-a-pattern-string</summary>
    internal static string EscapePatternString(string input)
    {
        var builder = new ValueStringBuilder(stackalloc char[64]);
        try
        {
            AppendEscapedPatternString(ref builder, input);
            return builder.AsSpan().ToString();
        }
        finally
        {
            builder.Dispose();
        }
    }

    private static void AppendEscapedPatternString(ref ValueStringBuilder builder, string input)
    {
        foreach (var c in input)
        {
            if (c is '+' or '*' or '?' or ':' or '{' or '}' or '(' or ')' or '\\')
            {
                builder.Append('\\');
            }

            builder.Append(c);
        }
    }

    private static bool IsAsciiDigit(char c) => c is >= '0' and <= '9';

    private static int FirstCodePoint(string value)
    {
        var c = value[0];
        if (char.IsHighSurrogate(c) && value.Length > 1 && char.IsLowSurrogate(value[1]))
        {
            return char.ConvertToUtf32(c, value[1]);
        }

        return c;
    }
}
#endif
