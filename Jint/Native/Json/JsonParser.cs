using System.Globalization;
using System.Runtime.CompilerServices;
using Esprima;
using Esprima.Ast;
using Jint.Native.Object;
using Jint.Pooling;
using Jint.Runtime;

using Range = Esprima.Range;

namespace Jint.Native.Json
{
    public class JsonParser
    {
        private readonly Engine _engine;

        public JsonParser(Engine engine)
        {
            _engine = engine;
        }

        private Extra _extra = null!;

        private int _index; // position in the stream
        private int _length; // length of the stream
        private int _lineNumber;
        private int _lineStart;
        private Location _location;
        private Token _lookahead = null!;
        private string _source = null!;

        private State _state;

        private static bool IsDecimalDigit(char ch)
        {
            return (ch >= '0' && ch <= '9');
        }

        private static bool IsHexDigit(char ch)
        {
            return
                ch >= '0' && ch <= '9' ||
                ch >= 'a' && ch <= 'f' ||
                ch >= 'A' && ch <= 'F'
                ;
        }

        private static bool IsOctalDigit(char ch)
        {
            return ch >= '0' && ch <= '7';
        }

        private static bool IsWhiteSpace(char ch)
        {
            return (ch == ' ')  ||
                   (ch == '\t') ||
                   (ch == '\n') ||
                   (ch == '\r');
        }

        private static bool IsLineTerminator(char ch)
        {
            return (ch == 10) || (ch == 13) || (ch == 0x2028) || (ch == 0x2029);
        }

        private static bool IsNullChar(char ch)
        {
            return ch == 'n'
                || ch == 'u'
                || ch == 'l'
                || ch == 'l'
                ;
        }

        private static bool IsTrueOrFalseChar(char ch)
        {
            return ch == 't'
                || ch == 'f'
                || ch == 'r'
                || ch == 'a'
                || ch == 'u'
                || ch == 'l'
                || ch == 'e'
                || ch == 's'
                ;
        }

        private char ScanHexEscape()
        {
            int code = char.MinValue;

            for (int i = 0; i < 4; ++i)
            {
                if (_index < _length + 1 && IsHexDigit(_source[_index]))
                {
                    char ch = _source[_index++];
                    code = code * 16 + "0123456789abcdef".IndexOf(ch.ToString(), StringComparison.OrdinalIgnoreCase);
                }
                else
                {
                    ThrowError(_index, Messages.ExpectedHexadecimalDigit);
                }
            }
            return (char) code;
        }

        private void SkipWhiteSpace()
        {
            while (_index < _length && IsWhiteSpace(_source[_index]))
            {
                ++_index;
            }
        }

        private Token ScanPunctuator()
        {
            int start = _index;
            char code = start < _source.Length ? _source[_index] : char.MinValue;

            switch ((int) code)
            {
                    // Check for most common single-character punctuators.
                case 46: // . dot
                case 40: // ( open bracket
                case 41: // ) close bracket
                case 59: // ; semicolon
                case 44: // , comma
                case 123: // { open curly brace
                case 125: // } close curly brace
                case 91: // [
                case 93: // ]
                case 58: // :
                case 63: // ?
                case 126: // ~
                    ++_index;

                    string value = TypeConverter.ToString(code);
                    return new Token
                    {
                        Type = Tokens.Punctuator,
                        Text = value,
                        Value = value,
                        LineNumber = _lineNumber,
                        LineStart = _lineStart,
                        Range = new[] { start, _index }
                    };
            }

            ThrowError(start, Messages.UnexpectedToken, code);
            return null!;
        }

        private Token ScanNumericLiteral()
        {
            char ch = _source.CharCodeAt(_index);

            int start = _index;
            string number = "";

            // Number start with a -
            if (ch == '-')
            {
                number += _source.CharCodeAt(_index++).ToString();
                ch = _source.CharCodeAt(_index);
            }

            if (ch != '.')
            {
                number += _source.CharCodeAt(_index++).ToString();
                ch = _source.CharCodeAt(_index);

                // Hex number starts with '0x'.
                // Octal number starts with '0'.
                if (number == "0")
                {
                    // decimal number starts with '0' such as '09' is illegal.
                    if (ch > 0 && IsDecimalDigit(ch))
                    {
                        ThrowError(_index, Messages.UnexpectedToken, ch);
                    }
                }

                while (IsDecimalDigit(_source.CharCodeAt(_index)))
                {
                    number += _source.CharCodeAt(_index++).ToString();
                }
                ch = _source.CharCodeAt(_index);
            }

            if (ch == '.')
            {
                number += _source.CharCodeAt(_index++).ToString();
                while (IsDecimalDigit(_source.CharCodeAt(_index)))
                {
                    number += _source.CharCodeAt(_index++).ToString();
                }
                ch = _source.CharCodeAt(_index);
            }

            if (ch == 'e' || ch == 'E')
            {
                number += _source.CharCodeAt(_index++).ToString();

                ch = _source.CharCodeAt(_index);
                if (ch == '+' || ch == '-')
                {
                    number += _source.CharCodeAt(_index++).ToString();
                }
                if (IsDecimalDigit(_source.CharCodeAt(_index)))
                {
                    while (IsDecimalDigit(_source.CharCodeAt(_index)))
                    {
                        number += _source.CharCodeAt(_index++).ToString();
                    }
                }
                else
                {
                    ThrowError(_index, Messages.UnexpectedToken, _source.CharCodeAt(_index));
                }
            }

            return new Token
                {
                    Type = Tokens.Number,
                    Text = number,
                    Value = Double.Parse(number, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint | NumberStyles.AllowExponent, CultureInfo.InvariantCulture),
                    LineNumber = _lineNumber,
                    LineStart = _lineStart,
                    Range = new[] {start, _index}
                };
        }

        private Token ScanBooleanLiteral()
        {
            var start = _index;
            var s = "";

            var boolTrue = false;
            var boolFalse = false;
            if (ConsumeMatch("true"))
            {
                boolTrue = true;
                s = "true";
            }
            else if (ConsumeMatch("false"))
            {
                boolFalse = true;
                s = "false";
            }

            if (boolTrue || boolFalse)
            {
                return new Token
                {
                    Type = Tokens.BooleanLiteral,
                    Text = s,
                    Value = boolTrue,
                    LineNumber = _lineNumber,
                    LineStart = _lineStart,
                    Range = new[] { start, _index }
                };
            }

            ThrowError(start, Messages.UnexpectedTokenIllegal);
            return null!;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
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
            if (ConsumeMatch(Null.Text))
            {
                return new Token
                {
                    Type = Tokens.NullLiteral,
                    Text = Null.Text,
                    Value = Null.Instance,
                    LineNumber = _lineNumber,
                    LineStart = _lineStart,
                    Range = new[] { start, _index }
                };
            }

            ThrowError(start, Messages.UnexpectedTokenIllegal);
            return null!;
        }

        private Token ScanStringLiteral()
        {
            using var wrapper = StringBuilderPool.Rent();
            var sb = wrapper.Builder;

            char quote = _source[_index];

            int start = _index;
            ++_index;

            while (_index < _length)
            {
                char ch = _source.CharCodeAt(_index++);

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
                    sb.Append(ch.ToString());
                }
            }

            if (quote != 0)
            {
                // unterminated string literal
                ThrowError(_index, Messages.UnexpectedEOS);
            }

            string value = sb.ToString();
            return new Token
            {
                    Type = Tokens.String,
                    Text = value,
                    Value = value,
                    LineNumber = _lineNumber,
                    LineStart = _lineStart,
                    Range = new[] { start, _index }
                };
        }

        private Token Advance()
        {
            SkipWhiteSpace();

            if (_index >= _length)
            {
                return new Token
                    {
                        Type = Tokens.EOF,
                        LineNumber = _lineNumber,
                        LineStart = _lineStart,
                        Range = new[] {_index, _index}
                    };
            }

            char ch = _source.CharCodeAt(_index);

            // Very common: ( and ) and ;
            if (ch == 40 || ch == 41 || ch == 58)
            {
                return ScanPunctuator();
            }

            // String literal starts with double quote (#34).
            // Single quote (#39) are not allowed in JSON.
            if (ch == 34)
            {
                return ScanStringLiteral();
            }

            // Dot (.) char #46 can also start a floating-point number, hence the need
            // to check the next character.
            if (ch == 46)
            {
                if (IsDecimalDigit(_source.CharCodeAt(_index + 1)))
                {
                    return ScanNumericLiteral();
                }
                return ScanPunctuator();
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

        private Token CollectToken()
        {
            var start = Position.From(
                line: _lineNumber,
                column: _index - _lineStart);

            Token token = Advance();

            var end = Position.From(
                line: _lineNumber,
                column: _index - _lineStart);

            _location = Location.From(start, end, _source);

            if (token.Type != Tokens.EOF)
            {
                var range = new[] {token.Range[0], token.Range[1]};
                var value = _source.Substring(token.Range[0], token.Range[1]);
                _extra.Tokens.Add(new Token
                    {
                        Type = token.Type,
                        Text = value,
                        Value = value,
                        Range = range,
                    });
            }

            return token;
        }

        private Token Lex()
        {
            Token token = _lookahead;
            _index = token.Range[1];
            _lineNumber = token.LineNumber.HasValue ? token.LineNumber.Value : 0;
            _lineStart = token.LineStart;

            _lookahead = (_extra.Tokens != null) ? CollectToken() : Advance();

            _index = token.Range[1];
            _lineNumber = token.LineNumber.HasValue ? token.LineNumber.Value : 0;
            _lineStart = token.LineStart;

            return token;
        }

        private void Peek()
        {
            int pos = _index;
            int line = _lineNumber;
            int start = _lineStart;
            _lookahead = (_extra.Tokens != null) ? CollectToken() : Advance();
            _index = pos;
            _lineNumber = line;
            _lineStart = start;
        }

        private void MarkStart()
        {
            if (_extra.Loc.HasValue)
            {
                _state.MarkerStack.Push(_index - _lineStart);
                _state.MarkerStack.Push(_lineNumber);
            }
            if (_extra.Range != null)
            {
                _state.MarkerStack.Push(_index);
            }
        }

        private T MarkEnd<T>(T node) where T : Node
        {
            if (_extra.Range != null)
            {
                node.Range = Range.From(_state.MarkerStack.Pop(), _index);
            }
            if (_extra.Loc.HasValue)
            {
                var start = Position.From(line: _state.MarkerStack.Pop(), column: _state.MarkerStack.Pop());
                var end = Position.From(line: _lineNumber, column: _index - _lineStart);
                node.Location = Location.From(start: start, end: end, source: _source);
                PostProcess(node);
            }
            return node;
        }

        public T MarkEndIf<T>(T node) where T : Node
        {
            if (node.Range != default || node.Location != default)
            {
                if (_extra.Loc.HasValue)
                {
                    _state.MarkerStack.Pop();
                    _state.MarkerStack.Pop();
                }
                if (_extra.Range != null)
                {
                    _state.MarkerStack.Pop();
                }
            }
            else
            {
                MarkEnd(node);
            }
            return node;
        }

        public Node PostProcess(Node node)
        {
            //if (_extra.Source != null)
            //{
            //    node.Location.Source = _extra.Source;
            //}

            return node;
        }

        private void ThrowError(Token token, string messageFormat, params object[] arguments)
        {
            ThrowError(token.Range[0], messageFormat, arguments);
        }

        private void ThrowError(int position, string messageFormat, params object[] arguments)
        {
            string msg = System.String.Format(messageFormat, arguments);
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

        private void Expect(string value)
        {
            Token token = Lex();
            if (token.Type != Tokens.Punctuator || !value.Equals(token.Value))
            {
                ThrowUnexpected(token);
            }
        }

        // Return true if the next token matches the specified punctuator.

        private bool Match(string value)
        {
            return _lookahead.Type == Tokens.Punctuator && value.Equals(_lookahead.Value);
        }

        private ObjectInstance ParseJsonArray()
        {
            var elements = new List<JsValue>();

            Expect("[");

            while (!Match("]"))
            {
                if (Match(","))
                {
                    Lex();
                    elements.Add(Null.Instance);
                }
                else
                {
                    elements.Add(ParseJsonValue());

                    if (!Match("]"))
                    {
                        Expect(",");
                    }
                }
            }

            Expect("]");

            return _engine.Realm.Intrinsics.Array.ConstructFast(elements);
        }

        public ObjectInstance ParseJsonObject()
        {
            Expect("{");

            var obj = _engine.Realm.Intrinsics.Object.Construct(Arguments.Empty);

            while (!Match("}"))
            {
                Tokens type = _lookahead.Type;
                if (type != Tokens.String)
                {
                    ThrowUnexpected(Lex());
                }

                var nameToken = Lex();
                var name = nameToken.Value.ToString();
                if (PropertyNameContainsInvalidCharacters(name))
                {
                    ThrowError(nameToken, Messages.InvalidCharacter);
                }

                Expect(":");
                var value = ParseJsonValue();

                obj.FastAddProperty(name, value, true, true, true);

                if (!Match("}"))
                {
                    Expect(",");
                }
            }

            Expect("}");

            return obj;
        }

        private static bool PropertyNameContainsInvalidCharacters(string propertyName)
        {
            const char max = (char) 31;
            foreach (var c in propertyName)
            {
                if (c != '\t' && c <= max)
                    return true;
            }
            return false;
        }

        /// <summary>
        /// Optimization.
        /// By calling Lex().Value for each type, we parse the token twice.
        /// It was already parsed by the peek() method.
        /// _lookahead.Value already contain the value.
        /// </summary>
        /// <returns></returns>
        private JsValue ParseJsonValue()
        {
            Tokens type = _lookahead.Type;
            MarkStart();

            switch (type)
            {
                case Tokens.NullLiteral:
                    var v = Lex().Value;
                    return Null.Instance;
                case Tokens.BooleanLiteral:
                    // implicit conversion operator goes through caching
                    return (bool) Lex().Value ? JsBoolean.True : JsBoolean.False;
                case Tokens.String:
                    // implicit conversion operator goes through caching
                    return new JsString((string) Lex().Value);
                case Tokens.Number:
                    return (double) Lex().Value;
            }

            if (Match("["))
            {
                return ParseJsonArray();
            }

            if (Match("{"))
            {
                return ParseJsonObject();
            }

            ThrowUnexpected(Lex());

            // can't be reached
            return Null.Instance;
        }

        public JsValue Parse(string code)
        {
            return Parse(code, null);
        }

        public JsValue Parse(string code, ParserOptions? options)
        {
            _source = code;
            _index = 0;
            _lineNumber = 1;
            _lineStart = 0;
            _length = _source.Length;
            _lookahead = null!;
            _state = new State
            {
                AllowIn = true,
                LabelSet = new HashSet<string>(),
                InFunctionBody = false,
                InIteration = false,
                InSwitch = false,
                LastCommentStart = -1,
                MarkerStack = new Stack<int>()
            };

            _extra = new Extra
                {
                    Range = new int[0],
                    Loc = 0,

                };

            if (options != null)
            {
                if (options.Tokens)
                {
                    _extra.Tokens = new List<Token>();
                }

            }

            try
            {
                MarkStart();
                Peek();
                JsValue jsv = ParseJsonValue();

                Peek();

                if(_lookahead.Type != Tokens.EOF)
                {
                    ThrowError(_lookahead, Messages.UnexpectedToken, _lookahead.Text);
                }
                return jsv;
            }
            finally
            {
                _extra = new Extra();
            }
        }

        private sealed class Extra
        {
            public int? Loc;
            public int[]? Range;

            public List<Token> Tokens = null!;
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

        class Token
        {
            public Tokens Type;
            public object Value = null!;
            public string Text = null!;
            public int[] Range = null!;
            public int? LineNumber;
            public int LineStart;
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
        };

        struct State
        {
            public int LastCommentStart;
            public bool AllowIn;
            public HashSet<string> LabelSet;
            public bool InFunctionBody;
            public bool InIteration;
            public bool InSwitch;
            public Stack<int> MarkerStack;
        }
    }

    internal static class StringExtensions
    {
        public static char CharCodeAt(this string source, int index)
        {
            if (index > source.Length - 1)
            {
                // char.MinValue is used as the null value
                return char.MinValue;
            }

            return source[index];
        }
    }
}
