using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using Jint.Runtime;

namespace Jint.Native.Json;

public sealed class JsonParser
{
    private readonly Engine _engine;
    private readonly int _maxDepth;

    /// <summary>
    /// Creates a new parser using the recursion depth specified in <see cref="Options.JsonOptions.MaxParseDepth"/>.
    /// </summary>
    public JsonParser(Engine engine)
        : this(engine, engine.Options.Json.MaxParseDepth)
    {
    }

    public JsonParser(Engine engine, int maxDepth)
    {
        if (maxDepth < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxDepth), $"Max depth must be greater or equal to zero");
        }
        _maxDepth = maxDepth;
        _engine = engine;
        // Two tokens are "live" during parsing,
        // lookahead and the current one on the stack
        // To add a safety boundary to not overwrite
        // "still in use" stuff, the buffer contains 5
        // instead of 2 tokens.
        _tokenBuffer = new Token[5];
        for (int i = 0; i < _tokenBuffer.Length; i++)
        {
            _tokenBuffer[i] = new Token();
        }
        _tokenBufferIndex = 0;
    }

    private int _index; // position in the stream
    private int _length; // length of the stream
    private Token _lookahead = null!;
    private string _source = null!;
    private readonly Token[] _tokenBuffer;
    private int _tokenBufferIndex;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsDecimalDigit(char ch)
    {
        // * For characters, which are before the '0', the equation will be negative and then wrap
        //   around because of the unsigned short cast
        // * For characters, which are after the '9', the equation will be positive, but >  9
        // * For digits, the equation will be between int(0) and int(9)
        return ((uint) (ch - '0')) <= 9;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsLowerCaseHexAlpha(char ch)
    {
        return ((uint) (ch - 'a')) <= 5;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsUpperCaseHexAlpha(char ch)
    {
        return ((uint) (ch - 'A')) <= 5;
    }

    private static bool IsHexDigit(char ch)
    {
        return
            IsDecimalDigit(ch) ||
            IsLowerCaseHexAlpha(ch) ||
            IsUpperCaseHexAlpha(ch)
            ;
    }

    private static bool IsWhiteSpace(char ch)
    {
        return (ch == ' ') ||
               (ch == '\t') ||
               (ch == '\n') ||
               (ch == '\r');
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsLineTerminator(char ch)
    {
        return (ch == 10) || (ch == 13) || (ch == 0x2028) || (ch == 0x2029);
    }

    private char ScanHexEscape()
    {
        int code = char.MinValue;

        for (int i = 0; i < 4; ++i)
        {
            if (_index < _length + 1 && IsHexDigit(_source[_index]))
            {
                char ch = char.ToLower(_source[_index++], CultureInfo.InvariantCulture);
                code = code * 16 + "0123456789abcdef".IndexOf(ch);
            }
            else
            {
                ThrowError(_index, Messages.ExpectedHexadecimalDigit);
            }
        }
        return (char) code;
    }

    private char ReadToNextSignificantCharacter()
    {
        char result = _index < _length ? _source[_index] : char.MinValue;
        while (IsWhiteSpace(result))
        {
            if ((++_index) >= _length)
            {
                return char.MinValue;
            }
            result = _source[_index];
        }
        return result;
    }

    private Token CreateToken(Tokens type, string text, char firstCharacter, JsValue value, in TextRange range)
    {
        Token result = _tokenBuffer[_tokenBufferIndex++];
        if (_tokenBufferIndex >= _tokenBuffer.Length)
        {
            _tokenBufferIndex = 0;
        }
        result.Type = type;
        result.Text = text;
        result.FirstCharacter = firstCharacter;
        result.Value = value;
        result.Range = range;
        return result;
    }

    private Token ScanPunctuator()
    {
        int start = _index;
        char code = start < _source.Length ? _source[_index] : char.MinValue;

        string value = ScanPunctuatorValue(start, code);
        ++_index;
        return CreateToken(Tokens.Punctuator, value, code, JsValue.Undefined, new TextRange(start, _index));
    }

    private string ScanPunctuatorValue(int start, char code)
    {
        switch (code)
        {
            case '.': return ".";
            case ',': return ",";
            case '{': return "{";
            case '}': return "}";
            case '[': return "[";
            case ']': return "]";
            case ':': return ":";
            default:
                ThrowError(start, Messages.UnexpectedToken, code);
                return null!;
        }
    }

    private Token ScanNumericLiteral()
    {
        using var sb = new ValueStringBuilder(stackalloc char[64]);
        var start = _index;
        var ch = _source.CharCodeAt(_index);
        var canBeInteger = true;

        // Number start with a -
        if (ch == '-')
        {
            sb.Append(ch);
            ch = _source.CharCodeAt(++_index);
        }

        if (ch != '.')
        {
            var firstCharacter = ch;
            sb.Append(ch);
            ch = _source.CharCodeAt(++_index);

            // Hex number starts with '0x'.
            // Octal number starts with '0'.
            if (sb.Length == 1 && firstCharacter == '0')
            {
                canBeInteger = false;
                // decimal number starts with '0' such as '09' is illegal.
                if (ch > 0 && IsDecimalDigit(ch))
                {
                    ThrowError(_index, Messages.UnexpectedToken, ch);
                }
            }

            while (IsDecimalDigit((ch = _source.CharCodeAt(_index))))
            {
                sb.Append(ch);
                _index++;
            }
        }

        if (ch == '.')
        {
            canBeInteger = false;
            sb.Append(ch);
            _index++;

            while (IsDecimalDigit((ch = _source.CharCodeAt(_index))))
            {
                sb.Append(ch);
                _index++;
            }
        }

        if (ch is 'e' or 'E')
        {
            canBeInteger = false;
            sb.Append(ch);
            ch = _source.CharCodeAt(++_index);
            if (ch is '+' or '-')
            {
                sb.Append(ch);
                ch = _source.CharCodeAt(++_index);
            }
            if (IsDecimalDigit(ch))
            {
                while (IsDecimalDigit(ch = _source.CharCodeAt(_index)))
                {
                    sb.Append(ch);
                    _index++;
                }
            }
            else
            {
                ThrowError(_index, Messages.UnexpectedToken, _source.CharCodeAt(_index));
            }
        }

        var number = sb.ToString();

        JsNumber value;
        if (canBeInteger && long.TryParse(number, NumberStyles.Integer, CultureInfo.InvariantCulture, out var longResult) && longResult != -0)
        {
            value = JsNumber.Create(longResult);
        }
        else
        {
            value = new JsNumber(double.Parse(number, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint | NumberStyles.AllowExponent, CultureInfo.InvariantCulture));
        }

        return CreateToken(Tokens.Number, number, '\0', value, new TextRange(start, _index));
    }

    private Token ScanBooleanLiteral()
    {
        var start = _index;
        if (ConsumeMatch("true"))
        {
            return CreateToken(Tokens.BooleanLiteral, "true", '\t', JsBoolean.True, new TextRange(start, _index));
        }

        if (ConsumeMatch("false"))
        {
            return CreateToken(Tokens.BooleanLiteral, "false", '\f', JsBoolean.False, new TextRange(start, _index));
        }

        ThrowError(start, Messages.UnexpectedTokenIllegal);
        return null!;
    }

    private bool ConsumeMatch(string text)
    {
        var start = _index;
        var length = text.Length;
        if (start + length - 1 < _source.Length && _source.AsSpan(start, length).SequenceEqual(text.AsSpan()))
        {
            _index += length;
            return true;
        }

        return false;
    }

    private Token ScanNullLiteral()
    {
        int start = _index;
        if (ConsumeMatch("null"))
        {
            return CreateToken(Tokens.NullLiteral, "null", 'n', JsValue.Null, new TextRange(start, _index));
        }

        ThrowError(start, Messages.UnexpectedTokenIllegal);
        return null!;
    }

    private Token ScanStringLiteral(ref State state)
    {
        char quote = _source[_index];
        int start = _index;
        ++_index;

        using var sb = new ValueStringBuilder(stackalloc char[64]);
        while (_index < _length)
        {
            char ch = _source[_index++];

            if (ch == quote)
            {
                quote = char.MinValue;
                break;
            }

            if (ch <= 31)
            {
                ThrowError(_index - 1, Messages.InvalidCharacter);
            }

            if (ch == '\\')
            {
                ch = _source.CharCodeAt(_index++);

                switch (ch)
                {
                    case '"':
                        sb.Append('"');
                        break;
                    case '\\':
                        sb.Append('\\');
                        break;
                    case '/':
                        sb.Append('/');
                        break;
                    case 'n':
                        sb.Append('\n');
                        break;
                    case 'r':
                        sb.Append('\r');
                        break;
                    case 't':
                        sb.Append('\t');
                        break;
                    case 'u':
                        sb.Append(ScanHexEscape());
                        break;
                    case 'b':
                        sb.Append('\b');
                        break;
                    case 'f':
                        sb.Append('\f');
                        break;
                    default:
                        ThrowError(_index - 1, Messages.UnexpectedToken, ch);
                        break;
                }
            }
            else if (IsLineTerminator(ch))
            {
                break;
            }
            else
            {
                sb.Append(ch);
            }
        }

        if (quote != 0)
        {
            // unterminated string literal
            ThrowError(_index, Messages.UnexpectedEOS);
        }

        var value = sb.ToString();
        return CreateToken(Tokens.String, value, '\"', new JsString(value), new TextRange(start, _index));
    }

    private Token Advance(ref State state)
    {
        char ch = ReadToNextSignificantCharacter();

        if (ch == char.MinValue)
        {
            return CreateToken(Tokens.EOF, string.Empty, '\0', JsValue.Undefined, new TextRange(_index, _index));
        }

        // String literal starts with double quote (#34).
        // Single quote (#39) are not allowed in JSON.
        if (ch == '"')
        {
            return ScanStringLiteral(ref state);
        }

        if (ch == '-') // Negative Number
        {
            if (IsDecimalDigit(_source.CharCodeAt(_index + 1)))
            {
                return ScanNumericLiteral();
            }
            return ScanPunctuator();
        }

        if (IsDecimalDigit(ch))
        {
            return ScanNumericLiteral();
        }

        if (ch == 't' || ch == 'f')
        {
            return ScanBooleanLiteral();
        }

        if (ch == 'n')
        {
            return ScanNullLiteral();
        }

        return ScanPunctuator();
    }

    private Token Lex(ref State state)
    {
        Token token = _lookahead;
        _index = token.Range.End;
        _lookahead = Advance(ref state);
        _index = token.Range.End;
        return token;
    }

    private void Peek(ref State state)
    {
        int pos = _index;
        _lookahead = Advance(ref state);
        _index = pos;
    }

    [DoesNotReturn]
    private void ThrowDepthLimitReached(Token token)
    {
        ThrowError(token.Range.Start, Messages.MaxDepthLevelReached);
    }

    [DoesNotReturn]
    private void ThrowError(Token token, string messageFormat, params object[] arguments)
    {
        ThrowError(token.Range.Start, messageFormat, arguments);
    }

    [DoesNotReturn]
    private void ThrowError(int position, string messageFormat, params object[] arguments)
    {
        var msg = string.Format(CultureInfo.InvariantCulture, messageFormat, arguments);
        ExceptionHelper.ThrowSyntaxError(_engine.Realm, $"{msg} at position {position}");
    }

    // Throw an exception because of the token.

    private void ThrowUnexpected(Token token)
    {
        if (token.Type == Tokens.EOF)
        {
            ThrowError(token, Messages.UnexpectedEOS);
        }

        if (token.Type == Tokens.Number)
        {
            ThrowError(token, Messages.UnexpectedNumber);
        }

        if (token.Type == Tokens.String)
        {
            ThrowError(token, Messages.UnexpectedString);
        }

        // BooleanLiteral, NullLiteral, or Punctuator.
        ThrowError(token, Messages.UnexpectedToken, token.Text);
    }

    // Expect the next token to match the specified punctuator.
    // If not, an exception will be thrown.
    private void Expect(ref State state, char value)
    {
        Token token = Lex(ref state);
        if (token.Type != Tokens.Punctuator || value != token.FirstCharacter)
        {
            ThrowUnexpected(token);
        }
    }

    // Return true if the next token matches the specified punctuator.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Match(char value)
    {
        return _lookahead.Type == Tokens.Punctuator && value == _lookahead.FirstCharacter;
    }

    private JsArray ParseJsonArray(ref State state)
    {
        if ((++state.CurrentDepth) > _maxDepth)
        {
            ThrowDepthLimitReached(_lookahead);
        }

        /*
         To speed up performance, the list allocation is deferred.

         First the elements are stored within an array received
         from the .NET array pool.

         If a list contains less elements that the size that array,
         a Jint array is constructed with the values stored in that
         array.

         When the number of elements exceed the buffer size,
         The elements-array gets created and filled with the content
         of the array. The array will then turn into an
         intermediate buffer which gets flushed to the list
         when its full.
        */
        List<JsValue>? elements = null;

        Expect(ref state, '[');

        int bufferIndex = 0;
        JsArray? result = null;

        JsValue[] buffer = ArrayPool<JsValue>.Shared.Rent(16);
        try
        {
            while (!Match(']'))
            {
                buffer[bufferIndex++] = ParseJsonValue(ref state);

                if (!Match(']'))
                {
                    Expect(ref state, ',');
                }

                if (bufferIndex >= buffer.Length)
                {
                    if (elements is null)
                    {
                        elements = new List<JsValue>(buffer);
                    }
                    else
                    {
                        elements.AddRange(buffer);
                    }
                    bufferIndex = 0;
                }
            }

            // BufferIndex = 0 has two meanings
            // * Empty JSON array (elements will be null)
            // * The buffer array has just been flushed (elements will NOT be null)
            if (bufferIndex > 0)
            {
                if (elements is null)
                {
                    // No element list has been created, all values did fit into the array.
                    // The Jint-Array can get constructed from that array.
                    var data = new JsValue[bufferIndex];
                    System.Array.Copy(buffer, data, length: bufferIndex);
                    result = new JsArray(_engine, data);
                }
                else
                {
                    // An element list has been created. Flush the
                    // remaining added items within the array to that list.
                    for (var i = 0; i < bufferIndex; ++i)
                    {
                        elements.Add(buffer[i]);
                    }
                }
            }
            else if (elements is null)
            {
                // the JSON array did not have any elements
                // aka: []
                result = new JsArray(_engine);
            }
        }
        finally
        {
            ArrayPool<JsValue>.Shared.Return(buffer);
        }

        Expect(ref state, ']');
        state.CurrentDepth--;

        return result ?? new JsArray(_engine, elements!.ToArray());
    }

    private JsObject ParseJsonObject(ref State state)
    {
        if ((++state.CurrentDepth) > _maxDepth)
        {
            ThrowDepthLimitReached(_lookahead);
        }

        Expect(ref state, '{');

        var obj = new JsObject(_engine);

        while (!Match('}'))
        {
            Tokens type = _lookahead.Type;
            if (type != Tokens.String)
            {
                ThrowUnexpected(Lex(ref state));
            }

            var nameToken = Lex(ref state);
            var name = nameToken.Text;
            if (PropertyNameContainsInvalidCharacters(name))
            {
                ThrowError(nameToken, Messages.InvalidCharacter);
            }

            Expect(ref state, ':');
            var value = ParseJsonValue(ref state);
            obj.FastSetDataProperty(name, value);

            if (!Match('}'))
            {
                Expect(ref state, ',');
            }
        }

        Expect(ref state, '}');
        state.CurrentDepth--;

        return obj;
    }

    private static bool PropertyNameContainsInvalidCharacters(string propertyName)
    {
        const char max = (char) 31;
        foreach (var c in propertyName)
        {
            if (c != '\t' && c <= max)
            {
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// Optimization.
    /// By calling Lex().Value for each type, we parse the token twice.
    /// It was already parsed by the peek() method.
    /// _lookahead.Value already contain the value.
    /// </summary>
    private JsValue ParseJsonValue(ref State state)
    {
        Tokens type = _lookahead.Type;
        switch (type)
        {
            case Tokens.NullLiteral:
            case Tokens.BooleanLiteral:
            case Tokens.String:
            case Tokens.Number:
                return Lex(ref state).Value;
            case Tokens.Punctuator:
                if (_lookahead.FirstCharacter == '[')
                {
                    return ParseJsonArray(ref state);
                }
                if (_lookahead.FirstCharacter == '{')
                {
                    return ParseJsonObject(ref state);
                }
                ThrowUnexpected(Lex(ref state));
                break;
        }

        ThrowUnexpected(Lex(ref state));
        // can't be reached
        return JsValue.Null;
    }

    public JsValue Parse(string code)
    {
        _source = code;
        _index = 0;
        _length = _source.Length;
        _lookahead = null!;

        State state = new State();

        Peek(ref state);
        JsValue jsv = ParseJsonValue(ref state);

        Peek(ref state);

        if (_lookahead.Type != Tokens.EOF)
        {
            ThrowError(_lookahead, Messages.UnexpectedToken, _lookahead.Text);
        }
        return jsv;
    }

    [StructLayout(LayoutKind.Auto)]
    private ref struct State
    {
        /// <summary>
        /// The current recursion depth
        /// </summary>
        public int CurrentDepth { get; set; }
    }

    private enum Tokens
    {
        NullLiteral,
        BooleanLiteral,
        String,
        Number,
        Punctuator,
        EOF,
    };

    private sealed class Token
    {
        public Tokens Type;
        public char FirstCharacter;
        public JsValue Value = JsValue.Undefined;
        public string Text = null!;
        public TextRange Range;
    }

    [StructLayout(LayoutKind.Auto)]
    private readonly struct TextRange
    {
        public TextRange(int start, int end)
        {
            Start = start;
            End = end;
        }

        public int Start { get; }
        public int End { get; }
    }

    static class Messages
    {
        public const string InvalidCharacter = "Invalid character in JSON";
        public const string ExpectedHexadecimalDigit = "Expected hexadecimal digit in JSON";
        public const string UnexpectedToken = "Unexpected token '{0}' in JSON";
        public const string UnexpectedTokenIllegal = "Unexpected token ILLEGAL in JSON";
        public const string UnexpectedNumber = "Unexpected number in JSON";
        public const string UnexpectedString = "Unexpected string in JSON";
        public const string UnexpectedEOS = "Unexpected end of JSON input";
        public const string MaxDepthLevelReached = "Max. depth level of JSON reached";
    };
}

internal static class StringExtensions
{
    internal static char CharCodeAt(this string source, int index)
    {
        if (index > source.Length - 1)
        {
            // char.MinValue is used as the null value
            return char.MinValue;
        }

        return source[index];
    }
}
