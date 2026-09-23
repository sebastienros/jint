using System.Text;

namespace Jint.HtmlParser.Css.Syntax;

// CSS Syntax Level 3, §4.3: https://drafts.csswg.org/css-syntax/#tokenizer-algorithms
internal sealed class CssTokenizer
{
    private readonly string _source;
    private readonly CancellationToken _cancellationToken;
    private readonly int _maxTokenCharacters;
    private readonly ParseDiagnosticCollector? _diagnostics;
    private int _position;
    private int _scanStart;
    private int _work;
    private bool _inComment;

    internal CssTokenizer(string source, int maxTokenCharacters,
        ParseDiagnosticCollector? diagnostics, CancellationToken cancellationToken)
    {
        _source = source;
        _maxTokenCharacters = maxTokenCharacters;
        _diagnostics = diagnostics;
        _cancellationToken = cancellationToken;
        _cancellationToken.ThrowIfCancellationRequested();
    }

    internal CssToken Next()
    {
        _cancellationToken.ThrowIfCancellationRequested();
        SkipComments();
        var start = _position;
        _scanStart = start;
        var c = Peek();
        if (c < 0)
        {
            return default;
        }

        if (IsWhitespace(c))
        {
            var text = new StringBuilder();
            do { AppendCodePoint(text, Consume()); } while (IsWhitespace(Peek()));
            return Make(CssTokenKind.Whitespace, start, text: text.ToString());
        }

        if (c is '\'' or '"')
        {
            Consume();
            return ConsumeString(start, c);
        }

        if (c == '#')
        {
            Consume();
            if (IsName(Peek()) || IsValidEscape(Peek(), Peek(1)))
            {
                var isId = WouldStartIdent(0);
                return Make(CssTokenKind.Hash, start, text: ConsumeName(), isIdHash: isId);
            }
            return Make(CssTokenKind.Delim, start, delimiter: '#');
        }

        if (c == '@')
        {
            Consume();
            return WouldStartIdent(0)
                ? Make(CssTokenKind.AtKeyword, start, text: ConsumeName())
                : Make(CssTokenKind.Delim, start, delimiter: '@');
        }

        if (c == '<' && Peek(1) == '!' && Peek(2) == '-' && Peek(3) == '-')
        {
            Consume(); Consume(); Consume(); Consume();
            return Make(CssTokenKind.Cdo, start);
        }

        if (c == '-' && Peek(1) == '-' && Peek(2) == '>')
        {
            Consume(); Consume(); Consume();
            return Make(CssTokenKind.Cdc, start);
        }

        if (WouldStartNumber(0))
        {
            return ConsumeNumeric(start);
        }

        if (WouldStartIdent(0))
        {
            return ConsumeIdentLike(start);
        }

        Consume();
        return c switch
        {
            ':' => Make(CssTokenKind.Colon, start, delimiter: ':'),
            ';' => Make(CssTokenKind.Semicolon, start, delimiter: ';'),
            ',' => Make(CssTokenKind.Comma, start, delimiter: ','),
            '(' => Make(CssTokenKind.OpenParenthesis, start, delimiter: '('),
            ')' => Make(CssTokenKind.CloseParenthesis, start, delimiter: ')'),
            '[' => Make(CssTokenKind.OpenSquareBracket, start, delimiter: '['),
            ']' => Make(CssTokenKind.CloseSquareBracket, start, delimiter: ']'),
            '{' => Make(CssTokenKind.OpenCurlyBracket, start, delimiter: '{'),
            '}' => Make(CssTokenKind.CloseCurlyBracket, start, delimiter: '}'),
            _ => Make(CssTokenKind.Delim, start, delimiter: c <= char.MaxValue ? (char) c : '\0')
        };
    }

    private CssToken ConsumeNumeric(int start)
    {
        var numberStart = _position;
        var isInteger = true;
        if (Peek() is '+' or '-') Consume();
        while (IsDigit(Peek())) Consume();
        if (Peek() == '.' && IsDigit(Peek(1)))
        {
            isInteger = false;
            Consume();
            while (IsDigit(Peek())) Consume();
        }
        if ((Peek() is 'e' or 'E') &&
            (IsDigit(Peek(1)) || ((Peek(1) is '+' or '-') && IsDigit(Peek(2)))))
        {
            isInteger = false;
            Consume();
            if (Peek() is '+' or '-') Consume();
            while (IsDigit(Peek())) Consume();
        }
        var number = _source.Substring(numberStart, _position - numberStart);
        if (WouldStartIdent(0))
        {
            return Make(CssTokenKind.Dimension, start, numberText: number,
                unit: ConsumeName(), isInteger: isInteger);
        }
        if (Peek() == '%')
        {
            Consume();
            return Make(CssTokenKind.Percentage, start, numberText: number, isInteger: isInteger);
        }
        return Make(CssTokenKind.Number, start, numberText: number, isInteger: isInteger);
    }

    private CssToken ConsumeIdentLike(int start)
    {
        var name = ConsumeName();
        if (Peek() != '(')
        {
            return Make(CssTokenKind.Ident, start, text: name);
        }
        Consume();
        if (!CssAscii.EqualsIgnoreCase(name, "url"))
        {
            return Make(CssTokenKind.Function, start, text: name);
        }

        while (IsWhitespace(Peek()) && IsWhitespace(Peek(1))) Consume();
        if (IsQuote(Peek()) || (IsWhitespace(Peek()) && IsQuote(Peek(1))))
        {
            return Make(CssTokenKind.Function, start, text: name);
        }
        return ConsumeUrl(start);
    }

    private CssToken ConsumeUrl(int start)
    {
        while (IsWhitespace(Peek())) Consume();
        var text = new StringBuilder();
        while (true)
        {
            var c = Peek();
            if (c < 0)
            {
                Report("css/unexpected-eof", _position);
                return Make(CssTokenKind.Url, start, text: text.ToString());
            }
            if (c == ')')
            {
                Consume();
                return Make(CssTokenKind.Url, start, text: text.ToString());
            }
            if (IsWhitespace(c))
            {
                do { Consume(); } while (IsWhitespace(Peek()));
                if (Peek() == ')')
                {
                    Consume();
                    return Make(CssTokenKind.Url, start, text: text.ToString());
                }
                if (Peek() < 0)
                {
                    Report("css/unexpected-eof", _position);
                    return Make(CssTokenKind.Url, start, text: text.ToString());
                }
                ConsumeBadUrlRemainder();
                Report("css/bad-url", start);
                return Make(CssTokenKind.BadUrl, start);
            }
            if (c is '"' or '\'' or '(' || IsNonPrintable(c))
            {
                ConsumeBadUrlRemainder();
                Report("css/bad-url", start);
                return Make(CssTokenKind.BadUrl, start);
            }
            if (c == '\\')
            {
                if (!IsValidEscape(c, Peek(1)))
                {
                    ConsumeBadUrlRemainder();
                    Report("css/bad-url", start);
                    return Make(CssTokenKind.BadUrl, start);
                }
                Consume();
                AppendCodePoint(text, ConsumeEscape());
            }
            else
            {
                AppendCodePoint(text, Consume());
            }
        }
    }

    private void ConsumeBadUrlRemainder()
    {
        while (Peek() >= 0)
        {
            if (Peek() == ')') { Consume(); return; }
            if (IsValidEscape(Peek(), Peek(1)))
            {
                Consume();
                ConsumeEscape();
            }
            else Consume();
        }
    }

    private CssToken ConsumeString(int start, int quote)
    {
        var text = new StringBuilder();
        while (true)
        {
            var c = Peek();
            if (c < 0)
            {
                Report("css/unexpected-eof", _position);
                return Make(CssTokenKind.String, start, text: text.ToString());
            }
            if (c == quote)
            {
                Consume();
                return Make(CssTokenKind.String, start, text: text.ToString());
            }
            if (c == '\n')
            {
                Report("css/bad-string", _position);
                return Make(CssTokenKind.BadString, start);
            }
            if (c == '\\')
            {
                Consume();
                if (Peek() < 0) continue;
                if (Peek() == '\n') { Consume(); continue; }
                AppendCodePoint(text, ConsumeEscape());
            }
            else AppendCodePoint(text, Consume());
        }
    }

    private string ConsumeName()
    {
        var text = new StringBuilder();
        while (true)
        {
            if (IsName(Peek())) AppendCodePoint(text, Consume());
            else if (IsValidEscape(Peek(), Peek(1)))
            {
                Consume();
                AppendCodePoint(text, ConsumeEscape());
            }
            else return text.ToString();
        }
    }

    private int ConsumeEscape()
    {
        if (Peek() < 0)
        {
            Report("css/unexpected-eof", _position);
            return 0xfffd;
        }
        if (!IsHexDigit(Peek())) return Consume();
        var value = 0;
        var digits = 0;
        while (digits < 6 && IsHexDigit(Peek()))
        {
            var c = Consume();
            value = (value << 4) + (c <= '9' ? c - '0' : (c | 0x20) - 'a' + 10);
            digits++;
        }
        if (IsWhitespace(Peek())) Consume();
        return value is 0 or > 0x10ffff || value is >= 0xd800 and <= 0xdfff ? 0xfffd : value;
    }

    private void SkipComments()
    {
        while (Peek() == '/' && Peek(1) == '*')
        {
            _inComment = true;
            Consume(); Consume();
            var closed = false;
            while (Peek() >= 0)
            {
                if (Peek() == '*' && Peek(1) == '/')
                {
                    Consume(); Consume();
                    closed = true;
                    break;
                }
                Consume();
            }
            _inComment = false;
            if (!closed) Report("css/unexpected-eof", _position);
        }
    }

    private CssToken Make(CssTokenKind kind, int start, string? text = null,
        string? numberText = null, string? unit = null, char delimiter = '\0',
        bool isInteger = false, bool isIdHash = false) =>
        new(kind, new CssSourceSpan(start, _position - start), text, numberText,
            unit, delimiter, isInteger, isIdHash);

    private int Peek(int offset = 0)
    {
        var index = _position;
        while (offset-- > 0)
        {
            if (index >= _source.Length) return -1;
            index += WidthAt(index);
        }
        return PeekAt(index);
    }

    private int PeekAt(int index)
    {
        if (index >= _source.Length) return -1;
        var c = _source[index];
        if (c == '\r' || c == '\f') return '\n';
        if (c == '\0') return 0xfffd;
        if (char.IsHighSurrogate(c))
        {
            return index + 1 < _source.Length && char.IsLowSurrogate(_source[index + 1])
                ? char.ConvertToUtf32(c, _source[index + 1]) : 0xfffd;
        }
        return char.IsLowSurrogate(c) ? 0xfffd : c;
    }

    private int WidthAt(int index)
    {
        var c = _source[index];
        if (c == '\r' && index + 1 < _source.Length && _source[index + 1] == '\n') return 2;
        return char.IsHighSurrogate(c) && index + 1 < _source.Length && char.IsLowSurrogate(_source[index + 1]) ? 2 : 1;
    }

    private int Consume()
    {
        var c = Peek();
        if (c >= 0)
        {
            _position += WidthAt(_position);
            if (!_inComment && _maxTokenCharacters > 0 && _position - _scanStart > _maxTokenCharacters)
            {
                throw new ParseLimitException(ParseLimitKind.TokenCharacters,
                    _maxTokenCharacters, _position - _scanStart);
            }
            if ((++_work & 1023) == 0) _cancellationToken.ThrowIfCancellationRequested();
        }
        return c;
    }

    private bool WouldStartNumber(int offset)
    {
        var a = Peek(offset);
        var b = Peek(offset + 1);
        var c = Peek(offset + 2);
        return a is '+' or '-' ? IsDigit(b) || b == '.' && IsDigit(c)
            : a == '.' ? IsDigit(b) : IsDigit(a);
    }

    private bool WouldStartIdent(int offset)
    {
        var a = Peek(offset);
        var b = Peek(offset + 1);
        var c = Peek(offset + 2);
        return a == '-' ? IsNameStart(b) || b == '-' || IsValidEscape(b, c)
            : IsNameStart(a) || IsValidEscape(a, b);
    }

    private static bool IsWhitespace(int c) => c is ' ' or '\t' or '\n';
    private static bool IsQuote(int c) => c is '\'' or '"';
    private static bool IsDigit(int c) => c is >= '0' and <= '9';
    private static bool IsHexDigit(int c) => IsDigit(c) || c is >= 'a' and <= 'f' or >= 'A' and <= 'F';
    private static bool IsNameStart(int c) => c is >= 'a' and <= 'z' or >= 'A' and <= 'Z' or '_' or >= 0x80;
    private static bool IsName(int c) => IsNameStart(c) || IsDigit(c) || c == '-';
    private static bool IsValidEscape(int first, int second) => first == '\\' && second != '\n';
    private static bool IsNonPrintable(int c) => c is >= 0 and <= 8 or 11 or >= 14 and <= 31 or 127;
    private static void AppendCodePoint(StringBuilder builder, int c)
    {
        if (c <= char.MaxValue) builder.Append((char) c);
        else builder.Append(char.ConvertFromUtf32(c));
    }

    private void Report(string code, int offset) => _diagnostics?.Add(code, offset);
}
