#if NET8_0_OR_GREATER
using System.Globalization;
using System.Runtime.InteropServices;
using Jint.Runtime;

namespace Jint.WebApi.Url.Pattern;

/// <summary>
/// https://urlpattern.spec.whatwg.org/#tokens — the lexical token kinds of a pattern string.
/// </summary>
internal enum UrlPatternTokenType
{
    /// <summary>A U+007B (<c>{</c>) code point.</summary>
    Open,

    /// <summary>A U+007D (<c>}</c>) code point.</summary>
    Close,

    /// <summary>A "<c>(&lt;regular expression&gt;)</c>" string, whose value excludes the parentheses.</summary>
    Regexp,

    /// <summary>A "<c>:&lt;name&gt;</c>" string, whose value excludes the colon.</summary>
    Name,

    /// <summary>A valid pattern code point with no special syntactical meaning.</summary>
    Char,

    /// <summary>A code point escaped with a backslash; the value is the escaped code point alone.</summary>
    EscapedChar,

    /// <summary>A U+003F (<c>?</c>) or U+002B (<c>+</c>) modifier.</summary>
    OtherModifier,

    /// <summary>A U+002A (<c>*</c>), which is either a wildcard group or a modifier.</summary>
    Asterisk,

    /// <summary>The end of the pattern string.</summary>
    End,

    /// <summary>A code point that is invalid in the pattern, either in itself or in this position.</summary>
    InvalidChar,
}

/// <summary>
/// https://urlpattern.spec.whatwg.org/#token — one lexical token, its position in the pattern string and the
/// code points it covers.
/// </summary>
[StructLayout(LayoutKind.Auto)]
internal readonly record struct UrlPatternToken(UrlPatternTokenType Type, int Index, string Value);

/// <summary>
/// The tokenizer of https://urlpattern.spec.whatwg.org/#tokenizing.
/// </summary>
/// <remarks>
/// <para>
/// The spec's tokenizer walks <i>code points</i> and this one walks UTF-16 code units, which is a difference of
/// bookkeeping only: "get the next code point" consumes both halves of a surrogate pair, so every position
/// this type records is the start of a code point and every "code point substring" is an ordinary substring.
/// The spec's recurring test "index is equal to input's code point length minus 1" — read as "the code point
/// just read is the last one" — becomes <c>_nextIndex == _input.Length</c>, which holds for an astral code point
/// as well.
/// </para>
/// <para>
/// The two policies differ only in what an error does. Under
/// "<a href="https://urlpattern.spec.whatwg.org/#tokenize-policy">strict</a>" it throws a <c>TypeError</c>, which
/// is how a malformed component pattern is rejected; under "lenient" it produces an
/// <see cref="UrlPatternTokenType.InvalidChar"/> token, which is what lets the constructor string parser see a
/// code point that is not valid pattern syntax and still treat it as a component separator.
/// </para>
/// </remarks>
internal static class UrlPatternTokenizer
{
    /// <summary>
    /// https://urlpattern.spec.whatwg.org/#tokenize
    /// </summary>
    internal static List<UrlPatternToken> Tokenize(Realm realm, string input, bool strict)
    {
        var tokenizer = new Tokenizer(realm, input, strict);
        tokenizer.Run();
        return tokenizer.Tokens;
    }

    /// <summary>
    /// https://urlpattern.spec.whatwg.org/#is-a-valid-name-code-point — whether <paramref name="codePoint"/> may
    /// appear in a "<c>:name</c>" group name, in the first position or a later one.
    /// </summary>
    /// <remarks>
    /// The spec defers to ECMAScript's <c>IdentifierStart</c> and <c>IdentifierPart</c>. Outside ASCII those are
    /// Unicode's <c>ID_Start</c> and <c>ID_Continue</c>, which the BCL does not expose as properties, so they are
    /// derived here from the general category plus the <c>Other_ID_Start</c> and <c>Other_ID_Continue</c> lists —
    /// the derivation the properties themselves are defined by. It agrees with the real properties up to the
    /// Unicode version of the platform's category table, the same version skew
    /// <see cref="Jint.WebApi.Url.Parsing.Idna"/> documents for IDNA.
    /// </remarks>
    internal static bool IsValidNameCodePoint(int codePoint, bool first)
    {
        if (codePoint < 0x80)
        {
            if (codePoint == '$' || codePoint == '_'
                || (codePoint >= 'a' && codePoint <= 'z')
                || (codePoint >= 'A' && codePoint <= 'Z'))
            {
                // U+005F (_) is IdentifierStartChar in its own right and is in ID_Continue.
                return true;
            }

            return !first && codePoint >= '0' && codePoint <= '9';
        }

        return first ? IsUnicodeIdStart(codePoint) : IsUnicodeIdContinue(codePoint);
    }

    private static bool IsUnicodeIdStart(int codePoint)
    {
        // Other_ID_Start: code points kept in ID_Start for stability although their category no longer implies it.
        if (codePoint is 0x1885 or 0x1886 or 0x2118 or 0x212E or 0x309B or 0x309C)
        {
            return true;
        }

        return CharUnicodeInfo.GetUnicodeCategory(codePoint) is UnicodeCategory.UppercaseLetter
            or UnicodeCategory.LowercaseLetter
            or UnicodeCategory.TitlecaseLetter
            or UnicodeCategory.ModifierLetter
            or UnicodeCategory.OtherLetter
            or UnicodeCategory.LetterNumber;
    }

    private static bool IsUnicodeIdContinue(int codePoint)
    {
        // IdentifierPartChar adds the two joiners on top of ID_Continue.
        if (codePoint is 0x200C or 0x200D)
        {
            return true;
        }

        // Other_ID_Continue.
        if (codePoint is 0x00B7 or 0x0387 or 0x19DA || (codePoint >= 0x1369 && codePoint <= 0x1371))
        {
            return true;
        }

        if (IsUnicodeIdStart(codePoint))
        {
            return true;
        }

        return CharUnicodeInfo.GetUnicodeCategory(codePoint) is UnicodeCategory.NonSpacingMark
            or UnicodeCategory.SpacingCombiningMark
            or UnicodeCategory.DecimalDigitNumber
            or UnicodeCategory.ConnectorPunctuation;
    }

    private sealed class Tokenizer(Realm realm, string input, bool strict)
    {
        private readonly Realm _realm = realm;
        private readonly string _input = input;
        private readonly bool _strict = strict;
        private int _index;
        private int _nextIndex;
        private int _codePoint = -1;

        internal List<UrlPatternToken> Tokens { get; } = [];

        internal void Run()
        {
            while (_index < _input.Length)
            {
                SeekAndGetNextCodePoint(_index);

                switch (_codePoint)
                {
                    case '*':
                        AddTokenWithDefaultPositionAndLength(UrlPatternTokenType.Asterisk);
                        continue;
                    case '+':
                    case '?':
                        AddTokenWithDefaultPositionAndLength(UrlPatternTokenType.OtherModifier);
                        continue;
                    case '\\':
                        ReadEscapedChar();
                        continue;
                    case '{':
                        AddTokenWithDefaultPositionAndLength(UrlPatternTokenType.Open);
                        continue;
                    case '}':
                        AddTokenWithDefaultPositionAndLength(UrlPatternTokenType.Close);
                        continue;
                    case ':':
                        ReadName();
                        continue;
                    case '(':
                        ReadRegexp();
                        continue;
                    default:
                        AddTokenWithDefaultPositionAndLength(UrlPatternTokenType.Char);
                        continue;
                }
            }

            AddTokenWithDefaultLength(UrlPatternTokenType.End, _index, _index);
        }

        private void ReadEscapedChar()
        {
            if (_nextIndex == _input.Length)
            {
                // A trailing backslash escapes nothing.
                ProcessTokenizingError(_nextIndex, _index);
                return;
            }

            var escapedIndex = _nextIndex;
            GetNextCodePoint();
            AddTokenWithDefaultLength(UrlPatternTokenType.EscapedChar, _nextIndex, escapedIndex);
        }

        private void ReadName()
        {
            var namePosition = _nextIndex;
            var nameStart = namePosition;

            while (namePosition < _input.Length)
            {
                SeekAndGetNextCodePoint(namePosition);
                if (!IsValidNameCodePoint(_codePoint, first: namePosition == nameStart))
                {
                    break;
                }

                namePosition = _nextIndex;
            }

            if (namePosition <= nameStart)
            {
                // A colon that names nothing.
                ProcessTokenizingError(nameStart, _index);
                return;
            }

            AddTokenWithDefaultLength(UrlPatternTokenType.Name, namePosition, nameStart);
        }

        private void ReadRegexp()
        {
            var depth = 1;
            var regexpPosition = _nextIndex;
            var regexpStart = regexpPosition;
            var error = false;

            while (regexpPosition < _input.Length)
            {
                SeekAndGetNextCodePoint(regexpPosition);

                if (_codePoint > 0x7F)
                {
                    // "The regular expression is required to consist of only ASCII code points."
                    error = FailRegexp(regexpStart);
                    break;
                }

                if (regexpPosition == regexpStart && _codePoint == '?')
                {
                    // A leading "?" would make the group non-capturing or a lookaround.
                    error = FailRegexp(regexpStart);
                    break;
                }

                if (_codePoint == '\\')
                {
                    if (_nextIndex == _input.Length)
                    {
                        error = FailRegexp(regexpStart);
                        break;
                    }

                    GetNextCodePoint();
                    if (_codePoint > 0x7F)
                    {
                        error = FailRegexp(regexpStart);
                        break;
                    }

                    regexpPosition = _nextIndex;
                    continue;
                }

                if (_codePoint == ')')
                {
                    depth--;
                    if (depth == 0)
                    {
                        regexpPosition = _nextIndex;
                        break;
                    }
                }
                else if (_codePoint == '(')
                {
                    depth++;
                    if (_nextIndex == _input.Length)
                    {
                        error = FailRegexp(regexpStart);
                        break;
                    }

                    var temporaryPosition = _nextIndex;
                    GetNextCodePoint();
                    if (_codePoint != '?')
                    {
                        // Only non-capturing groups and assertions may nest inside a regexp group.
                        error = FailRegexp(regexpStart);
                        break;
                    }

                    _nextIndex = temporaryPosition;
                }

                regexpPosition = _nextIndex;
            }

            if (error)
            {
                return;
            }

            if (depth != 0)
            {
                ProcessTokenizingError(regexpStart, _index);
                return;
            }

            var regexpLength = regexpPosition - regexpStart - 1;
            if (regexpLength == 0)
            {
                ProcessTokenizingError(regexpStart, _index);
                return;
            }

            AddToken(UrlPatternTokenType.Regexp, regexpPosition, regexpStart, regexpLength);
        }

        private bool FailRegexp(int regexpStart)
        {
            ProcessTokenizingError(regexpStart, _index);
            return true;
        }

        /// <summary>https://urlpattern.spec.whatwg.org/#get-the-next-code-point</summary>
        private void GetNextCodePoint()
        {
            var c = _input[_nextIndex];
            if (char.IsHighSurrogate(c) && _nextIndex + 1 < _input.Length && char.IsLowSurrogate(_input[_nextIndex + 1]))
            {
                _codePoint = char.ConvertToUtf32(c, _input[_nextIndex + 1]);
                _nextIndex += 2;
                return;
            }

            _codePoint = c;
            _nextIndex++;
        }

        /// <summary>https://urlpattern.spec.whatwg.org/#seek-and-get-the-next-code-point</summary>
        private void SeekAndGetNextCodePoint(int index)
        {
            _nextIndex = index;
            GetNextCodePoint();
        }

        /// <summary>https://urlpattern.spec.whatwg.org/#add-a-token</summary>
        private void AddToken(UrlPatternTokenType type, int nextPosition, int valuePosition, int valueLength)
        {
            Tokens.Add(new UrlPatternToken(type, _index, _input.Substring(valuePosition, valueLength)));
            _index = nextPosition;
        }

        /// <summary>https://urlpattern.spec.whatwg.org/#add-a-token-with-default-length</summary>
        private void AddTokenWithDefaultLength(UrlPatternTokenType type, int nextPosition, int valuePosition)
            => AddToken(type, nextPosition, valuePosition, nextPosition - valuePosition);

        /// <summary>https://urlpattern.spec.whatwg.org/#add-a-token-with-default-position-and-length</summary>
        private void AddTokenWithDefaultPositionAndLength(UrlPatternTokenType type)
            => AddTokenWithDefaultLength(type, _nextIndex, _index);

        /// <summary>https://urlpattern.spec.whatwg.org/#process-a-tokenizing-error</summary>
        private void ProcessTokenizingError(int nextPosition, int valuePosition)
        {
            if (_strict)
            {
                Throw.TypeError(_realm, $"Invalid URLPattern syntax at index {valuePosition}");
            }

            AddTokenWithDefaultLength(UrlPatternTokenType.InvalidChar, nextPosition, valuePosition);
        }
    }
}
#endif
