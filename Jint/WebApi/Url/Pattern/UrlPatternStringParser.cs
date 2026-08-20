#if NET8_0_OR_GREATER
using System.Globalization;
using System.Text;
using Jint.Runtime;

namespace Jint.WebApi.Url.Pattern;

/// <summary>
/// The pattern parser of https://urlpattern.spec.whatwg.org/#parsing — turns a pattern string into a part list.
/// </summary>
/// <remarks>
/// The pattern syntax is path-to-regexp's, so a part is "an optional prefix, at most one matching group, an
/// optional suffix, and a modifier". The parser reads the two shapes that can produce one: the bare sequence
/// <c>&lt;prefix char&gt;&lt;name&gt;&lt;regexp&gt;&lt;modifier&gt;</c>, and the braced
/// <c>{&lt;prefix&gt;&lt;name&gt;&lt;regexp&gt;&lt;suffix&gt;}&lt;modifier&gt;</c>. Anything else is fixed text,
/// buffered in <c>pending fixed value</c> so that a run of it becomes one part rather than one part per code
/// point — and run through the encoding callback, which is where a component's own URL canonicalization applies.
/// </remarks>
internal static class UrlPatternStringParser
{
    /// <summary>
    /// https://urlpattern.spec.whatwg.org/#parse-a-pattern-string
    /// </summary>
    internal static List<UrlPatternPart> Parse(
        Realm realm,
        string input,
        UrlPatternCompileOptions options,
        UrlPatternEncodingCallback encodingCallback)
    {
        var parser = new PatternParser(realm, UrlPatternTokenizer.Tokenize(realm, input, strict: true), options, encodingCallback);
        parser.Run();
        return parser.PartList;
    }

    private sealed class PatternParser(
        Realm realm,
        List<UrlPatternToken> tokenList,
        UrlPatternCompileOptions options,
        UrlPatternEncodingCallback encodingCallback)
    {
        private readonly Realm _realm = realm;
        private readonly List<UrlPatternToken> _tokenList = tokenList;
        private readonly UrlPatternCompileOptions _options = options;
        private readonly UrlPatternEncodingCallback _encodingCallback = encodingCallback;
        private readonly string _segmentWildcardRegexp = UrlPatternGenerator.GenerateSegmentWildcardRegexp(options);
        private string _pendingFixedValue = string.Empty;
        private int _index;
        private int _nextNumericName;

        internal List<UrlPatternPart> PartList { get; } = [];

        internal void Run()
        {
            while (_index < _tokenList.Count)
            {
                // The first shape: <prefix char><name><regexp><modifier>, any of which may be absent.
                var charToken = TryConsume(UrlPatternTokenType.Char);
                var nameToken = TryConsume(UrlPatternTokenType.Name);
                var regexpOrWildcardToken = TryConsumeRegexpOrWildcard(nameToken.HasValue);

                if (nameToken.HasValue || regexpOrWildcardToken.HasValue)
                {
                    var prefix = charToken?.Value ?? string.Empty;
                    if (prefix.Length != 0 && !_options.IsPrefix(prefix))
                    {
                        // Only the component's own prefix code point is an automatic prefix; anything else is
                        // fixed text that happens to sit in front of the group.
                        _pendingFixedValue += prefix;
                        prefix = string.Empty;
                    }

                    MaybeAddPartFromPendingFixedValue();
                    AddPart(prefix, nameToken, regexpOrWildcardToken, string.Empty, TryConsumeModifier());
                    continue;
                }

                var fixedToken = charToken ?? TryConsume(UrlPatternTokenType.EscapedChar);
                if (fixedToken is { } fixedText)
                {
                    _pendingFixedValue += fixedText.Value;
                    continue;
                }

                // The second shape: {<prefix><name><regexp><suffix>}<modifier>.
                if (TryConsume(UrlPatternTokenType.Open).HasValue)
                {
                    var prefix = ConsumeText();
                    nameToken = TryConsume(UrlPatternTokenType.Name);
                    regexpOrWildcardToken = TryConsumeRegexpOrWildcard(nameToken.HasValue);
                    var suffix = ConsumeText();
                    ConsumeRequired(UrlPatternTokenType.Close);
                    AddPart(prefix, nameToken, regexpOrWildcardToken, suffix, TryConsumeModifier());
                    continue;
                }

                MaybeAddPartFromPendingFixedValue();
                ConsumeRequired(UrlPatternTokenType.End);
            }
        }

        /// <summary>https://urlpattern.spec.whatwg.org/#try-to-consume-a-token</summary>
        private UrlPatternToken? TryConsume(UrlPatternTokenType type)
        {
            var next = _tokenList[_index];
            if (next.Type != type)
            {
                return null;
            }

            _index++;
            return next;
        }

        /// <summary>https://urlpattern.spec.whatwg.org/#try-to-consume-a-modifier-token</summary>
        private UrlPatternToken? TryConsumeModifier()
            => TryConsume(UrlPatternTokenType.OtherModifier) ?? TryConsume(UrlPatternTokenType.Asterisk);

        /// <summary>https://urlpattern.spec.whatwg.org/#try-to-consume-a-regexp-or-wildcard-token</summary>
        private UrlPatternToken? TryConsumeRegexpOrWildcard(bool hasNameToken)
        {
            var token = TryConsume(UrlPatternTokenType.Regexp);
            if (!hasNameToken && token is null)
            {
                // A "*" right after a name is the name's modifier, not a wildcard of its own.
                token = TryConsume(UrlPatternTokenType.Asterisk);
            }

            return token;
        }

        /// <summary>https://urlpattern.spec.whatwg.org/#consume-a-required-token</summary>
        private void ConsumeRequired(UrlPatternTokenType type)
        {
            if (TryConsume(type) is null)
            {
                Throw.TypeError(_realm, $"Invalid URLPattern syntax at index {_tokenList[_index].Index}");
            }
        }

        /// <summary>https://urlpattern.spec.whatwg.org/#consume-text</summary>
        private string ConsumeText()
        {
            var result = new ValueStringBuilder(stackalloc char[32]);
            try
            {
                while (true)
                {
                    var token = TryConsume(UrlPatternTokenType.Char) ?? TryConsume(UrlPatternTokenType.EscapedChar);
                    if (token is not { } consumed)
                    {
                        break;
                    }

                    result.Append(consumed.Value);
                }

                return result.AsSpan().ToString();
            }
            finally
            {
                result.Dispose();
            }
        }

        /// <summary>https://urlpattern.spec.whatwg.org/#maybe-add-a-part-from-the-pending-fixed-value</summary>
        private void MaybeAddPartFromPendingFixedValue()
        {
            if (_pendingFixedValue.Length == 0)
            {
                return;
            }

            var encodedValue = _encodingCallback(_realm, _pendingFixedValue);
            _pendingFixedValue = string.Empty;
            PartList.Add(new UrlPatternPart(
                UrlPatternPartType.FixedText,
                encodedValue,
                UrlPatternModifier.None,
                string.Empty,
                string.Empty,
                string.Empty));
        }

        /// <summary>https://urlpattern.spec.whatwg.org/#add-a-part</summary>
        private void AddPart(
            string prefix,
            UrlPatternToken? nameToken,
            UrlPatternToken? regexpOrWildcardToken,
            string suffix,
            UrlPatternToken? modifierToken)
        {
            var modifier = UrlPatternModifier.None;
            if (modifierToken is { } modifierValue)
            {
                modifier = modifierValue.Value switch
                {
                    "?" => UrlPatternModifier.Optional,
                    "*" => UrlPatternModifier.ZeroOrMore,
                    "+" => UrlPatternModifier.OneOrMore,
                    _ => UrlPatternModifier.None,
                };
            }

            if (nameToken is null && regexpOrWildcardToken is null && modifier == UrlPatternModifier.None)
            {
                // A "{foo}" grouping: it can be merged with the text on either side of it.
                _pendingFixedValue += prefix;
                return;
            }

            MaybeAddPartFromPendingFixedValue();

            if (nameToken is null && regexpOrWildcardToken is null)
            {
                // A "{foo}?" grouping: the modifier makes it a part of its own.
                if (prefix.Length == 0)
                {
                    return;
                }

                PartList.Add(new UrlPatternPart(
                    UrlPatternPartType.FixedText,
                    _encodingCallback(_realm, prefix),
                    modifier,
                    string.Empty,
                    string.Empty,
                    string.Empty));
                return;
            }

            // Everything becomes a regular expression first, so that an explicitly written "([^/]+?)" is treated
            // exactly like the ":foo" that would have generated it.
            string regexpValue;
            if (regexpOrWildcardToken is not { } token)
            {
                regexpValue = _segmentWildcardRegexp;
            }
            else if (token.Type == UrlPatternTokenType.Asterisk)
            {
                regexpValue = UrlPatternGenerator.FullWildcardRegexpValue;
            }
            else
            {
                regexpValue = token.Value;
            }

            var type = UrlPatternPartType.Regexp;
            if (string.Equals(regexpValue, _segmentWildcardRegexp, StringComparison.Ordinal))
            {
                type = UrlPatternPartType.SegmentWildcard;
                regexpValue = string.Empty;
            }
            else if (string.Equals(regexpValue, UrlPatternGenerator.FullWildcardRegexpValue, StringComparison.Ordinal))
            {
                type = UrlPatternPartType.FullWildcard;
                regexpValue = string.Empty;
            }

            string name;
            if (nameToken is { } named)
            {
                name = named.Value;
            }
            else if (regexpOrWildcardToken is not null)
            {
                name = _nextNumericName.ToString(CultureInfo.InvariantCulture);
                _nextNumericName++;
            }
            else
            {
                name = string.Empty;
            }

            if (IsDuplicateName(name))
            {
                Throw.TypeError(_realm, $"Duplicate URLPattern group name \"{name}\"");
            }

            PartList.Add(new UrlPatternPart(
                type,
                regexpValue,
                modifier,
                name,
                _encodingCallback(_realm, prefix),
                _encodingCallback(_realm, suffix)));
        }

        /// <summary>https://urlpattern.spec.whatwg.org/#is-a-duplicate-name</summary>
        private bool IsDuplicateName(string name)
        {
            foreach (var part in PartList)
            {
                if (string.Equals(part.Name, name, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
#endif
