#if NET8_0_OR_GREATER
using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Jint.Runtime;
using Jint.WebApi.Url.Parsing;

namespace Jint.WebApi.Url.Pattern;

/// <summary>
/// A URL pattern without regexp groups that matches with no engine at all: compiled on the engine's thread,
/// then safe to run on any thread — https://urlpattern.spec.whatwg.org/#dom-urlpattern-test for a string input
/// with no base URL.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why it exists.</b> <see cref="UrlPatternRecord"/> matches through the engine's own <c>RegExp</c>
/// objects, which only the thread that owns the engine may run. A host that decides requests on a transport
/// thread — <c>Jint.Browser</c> answering <c>Network.emulateNetworkConditionsByRule</c> — needs the same answer
/// with no engine in reach. Chromium has the same split for the same command: its network service matches with
/// <c>SimpleUrlPatternMatcher</c> (<c>components/url_pattern</c>), and this is that class's shape.
/// </para>
/// <para>
/// <b>Compiled by the engine, matched without it.</b> <see cref="TryCompile"/> parses and canonicalizes the
/// constructor string with <see cref="UrlPatternRecord.Create"/> — the one parser, so a pattern means exactly what
/// <c>new URLPattern(string)</c> means — and then keeps only each component's part list, turned into an ordinal
/// comparison where the part list allows one and into a .NET <see cref="Regex"/> generated from the part list
/// otherwise. The record and its <c>RegExp</c> objects are dropped. What is left is strings, enums and
/// <see cref="Regex"/> instances, every field <see langword="readonly"/>, and <see cref="Regex"/> is documented
/// as safe for concurrent matching; <c>UrlPatternMatcherTests.TheMatcherHoldsNothingOfTheEngine</c> walks the
/// fields so that it stays that way. It never depends on the engine's regular-expression implementation either.
/// </para>
/// <para>
/// <b>Two restrictions, both Chromium's.</b> A pattern with a regexp group is refused: its body is JavaScript
/// regular-expression source, which only the engine can be trusted to read, and Chromium refuses it in the network
/// service for the security reason a user-defined expression on a privileged thread carries. And a pattern is
/// case-sensitive, as <c>new URLPattern(string)</c> is without <c>ignoreCase</c>. Without regexp groups, every
/// generated expression is a regular language with no backreference or lookaround, so it is matched with
/// <see cref="RegexOptions.NonBacktracking"/>, in time linear in the input.
/// </para>
/// </remarks>
internal sealed class UrlPatternMatcher
{
    private readonly Component _protocol;
    private readonly Component _username;
    private readonly Component _password;
    private readonly Component _hostname;
    private readonly Component _port;
    private readonly Component _pathname;
    private readonly Component _search;
    private readonly Component _hash;

    private UrlPatternMatcher(UrlPatternRecord record)
    {
        _protocol = Component.Create(record.Protocol);
        _username = Component.Create(record.Username);
        _password = Component.Create(record.Password);
        _hostname = Component.Create(record.Hostname);
        _port = Component.Create(record.Port);
        _pathname = Component.Create(record.Pathname);
        _search = Component.Create(record.Search);
        _hash = Component.Create(record.Hash);
    }

    /// <summary>
    /// Compiles <paramref name="constructorString"/> on <paramref name="engine"/>'s thread, or answers why it
    /// cannot be: it is not a pattern, it is relative, or it has a regexp group.
    /// </summary>
    /// <param name="engine">An engine this thread owns; it is used for the parse and never retained.</param>
    /// <param name="constructorString">A URLPattern constructor string, which must be absolute.</param>
    /// <param name="matcher">The matcher, when the pattern is one.</param>
    /// <param name="error">Why the pattern was refused, when it was.</param>
    internal static bool TryCompile(
        Engine engine,
        string constructorString,
        [NotNullWhen(true)] out UrlPatternMatcher? matcher,
        [NotNullWhen(false)] out string? error)
    {
        UrlPatternRecord record;
        try
        {
            record = UrlPatternRecord.Create(engine, constructorString, initInput: null, baseUrl: null, ignoreCase: false);
        }
        catch (JavaScriptException exception)
        {
            // Everything the parse raises for a bad pattern is one of its own TypeErrors: the input is a string, so
            // nothing user-written runs. The sibling JintExceptions that bound execution are left to propagate.
            matcher = null;
            error = exception.Message;
            return false;
        }

        if (record.HasRegexpGroups)
        {
            matcher = null;
            error = "Regexp groups are not supported";
            return false;
        }

        matcher = new UrlPatternMatcher(record);
        error = null;
        return true;
    }

    /// <summary>Whether <paramref name="url"/> parses and matches; a string that is not a URL matches nothing.</summary>
    internal bool Matches(string url) => UrlParser.Parse(url) is { } parsed && Matches(parsed);

    /// <summary>
    /// Whether <paramref name="url"/> matches, each component compared as
    /// https://urlpattern.spec.whatwg.org/#url-pattern-match reads it off a URL record.
    /// </summary>
    internal bool Matches(UrlRecord url)
        => _protocol.Matches(url.Scheme)
            && _username.Matches(url.Username)
            && _password.Matches(url.Password)
            && _hostname.Matches(url.SerializeHost())
            && _port.Matches(url.SerializePort())
            && _pathname.Matches(url.SerializePath())
            && _search.Matches(url.Query ?? string.Empty)
            && _hash.Matches(url.Fragment ?? string.Empty);

    /// <summary>What one component is matched with.</summary>
    private enum ComponentKind
    {
        /// <summary>A lone "<c>*</c>": any value with no line terminator, which is what <c>.*</c> admits.</summary>
        Any,

        /// <summary>Fixed text, or nothing at all: exactly that value.</summary>
        Exact,

        /// <summary>A prefixed lone wildcard, as in a pathname's "<c>/*</c>": that prefix, then anything <see cref="Any"/> admits.</summary>
        PrefixThenAny,

        /// <summary>Anything else: the generated <see cref="Regex"/>.</summary>
        Expression,
    }

    /// <summary>One component's matcher: an ordinal comparison where the part list allows one, a regular expression otherwise.</summary>
    /// <remarks>
    /// The comparisons are the shapes Chromium's <c>liburlpattern</c> matches directly rather than through RE2 —
    /// an empty pattern, one piece of fixed text, one full wildcard — plus the prefixed wildcard nearly every
    /// pathname pattern ends with. They exist so that a typical pattern builds one expression, for its hostname,
    /// rather than eight.
    /// </remarks>
    private sealed class Component
    {
        /// <summary>The ECMAScript line terminators, which <c>.</c> does not match without the <c>s</c> flag.</summary>
        private static readonly SearchValues<char> _lineTerminators = SearchValues.Create("\n\r\u2028\u2029");

        /// <summary>https://urlpattern.spec.whatwg.org/#full-wildcard-regexp-value, as a .NET class.</summary>
        private const string FullWildcard = @"[^\n\r\u2028\u2029]*";

        private readonly ComponentKind _kind;
        private readonly string _text;
        private readonly Regex? _expression;

        private Component(ComponentKind kind, string text, Regex? expression)
        {
            _kind = kind;
            _text = text;
            _expression = expression;
        }

        internal static Component Create(UrlPatternComponent component)
        {
            var partList = component.PartList;

            if (partList.Length == 0)
            {
                return new Component(ComponentKind.Exact, string.Empty, expression: null);
            }

            if (partList.Length == 1)
            {
                var part = partList[0];

                if (part is { Type: UrlPatternPartType.FixedText, Modifier: UrlPatternModifier.None })
                {
                    return new Component(ComponentKind.Exact, part.Value, expression: null);
                }

                if (part is { Type: UrlPatternPartType.FullWildcard, Modifier: UrlPatternModifier.None, Suffix.Length: 0 })
                {
                    return part.Prefix.Length == 0
                        ? new Component(ComponentKind.Any, string.Empty, expression: null)
                        : new Component(ComponentKind.PrefixThenAny, part.Prefix, expression: null);
                }
            }

            // Linear-time matching needs no deadline: see the type's remarks. A deadline would be worse than none on
            // a transport thread, where the exception it raises cannot be reported anywhere.
            var expression = new Regex(
                GenerateExpression(partList, component.Options),
                RegexOptions.NonBacktracking | RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture,
                Regex.InfiniteMatchTimeout);

            return new Component(ComponentKind.Expression, string.Empty, expression);
        }

        internal bool Matches(string value) => _kind switch
        {
            ComponentKind.Any => !value.AsSpan().ContainsAny(_lineTerminators),
            ComponentKind.Exact => string.Equals(value, _text, StringComparison.Ordinal),
            ComponentKind.PrefixThenAny => value.StartsWith(_text, StringComparison.Ordinal)
                && !value.AsSpan(_text.Length).ContainsAny(_lineTerminators),
            _ => _expression!.IsMatch(value),
        };

        /// <summary>
        /// https://urlpattern.spec.whatwg.org/#generate-a-regular-expression-and-name-list, step for step as
        /// <see cref="UrlPatternGenerator.GenerateRegularExpressionAndNameList"/> takes it, written for .NET.
        /// </summary>
        /// <remarks>
        /// Four translations, none of which changes which strings match: <c>\A</c> and <c>\z</c> for <c>^</c> and
        /// <c>$</c>, because a .NET <c>$</c> also matches before a final line feed; a non-capturing group wherever
        /// the specification captures, since nothing is read out of a match; greedy rather than lazy repetition,
        /// which decides <i>which</i> match is found and never whether an anchored one exists; and the two
        /// wildcards spelled as the classes the "<c>v</c>" flag gives them — <c>[^]</c>, which .NET reads as an
        /// unterminated class, becomes <c>[\s\S]</c>.
        /// </remarks>
        private static string GenerateExpression(UrlPatternPart[] partList, UrlPatternCompileOptions options)
        {
            var segmentWildcard = options.DelimiterCodePoint == '\0'
                ? @"[\s\S]+"
                : @"[^\u" + ((int) options.DelimiterCodePoint).ToString("X4", CultureInfo.InvariantCulture) + "]+";

            var result = new StringBuilder(@"\A");

            foreach (var part in partList)
            {
                if (part.Type == UrlPatternPartType.FixedText)
                {
                    if (part.Modifier == UrlPatternModifier.None)
                    {
                        result.Append(Regex.Escape(part.Value));
                    }
                    else
                    {
                        result.Append("(?:").Append(Regex.Escape(part.Value)).Append(')').Append(Modifier(part.Modifier));
                    }

                    continue;
                }

                if (part.Type == UrlPatternPartType.Regexp)
                {
                    // TryCompile refuses a pattern with one before any component gets here.
                    Throw.InvalidOperationException("A regexp group cannot be matched without the engine.");
                }

                var value = part.Type == UrlPatternPartType.SegmentWildcard ? segmentWildcard : FullWildcard;

                if (part.Prefix.Length == 0 && part.Suffix.Length == 0)
                {
                    result.Append("(?:").Append(value).Append(')').Append(Modifier(part.Modifier));
                    continue;
                }

                var prefix = Regex.Escape(part.Prefix);
                var suffix = Regex.Escape(part.Suffix);

                if (part.Modifier is UrlPatternModifier.None or UrlPatternModifier.Optional)
                {
                    result.Append("(?:").Append(prefix).Append("(?:").Append(value).Append(')').Append(suffix).Append(')')
                        .Append(Modifier(part.Modifier));
                    continue;
                }

                // A repeating part excludes the first prefix and the last suffix but keeps both between repetitions.
                result.Append("(?:").Append(prefix)
                    .Append("(?:(?:").Append(value).Append(")(?:").Append(suffix).Append(prefix)
                    .Append("(?:").Append(value).Append("))*)")
                    .Append(suffix).Append(')');

                if (part.Modifier == UrlPatternModifier.ZeroOrMore)
                {
                    result.Append('?');
                }
            }

            return result.Append(@"\z").ToString();
        }

        /// <summary>https://urlpattern.spec.whatwg.org/#convert-a-modifier-to-a-string</summary>
        private static string Modifier(UrlPatternModifier modifier) => modifier switch
        {
            UrlPatternModifier.ZeroOrMore => "*",
            UrlPatternModifier.Optional => "?",
            UrlPatternModifier.OneOrMore => "+",
            _ => string.Empty,
        };
    }
}
#endif
