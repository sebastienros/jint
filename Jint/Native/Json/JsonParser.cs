using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using Jint.Extensions;
using Jint.Native.Object;
using Jint.Pooling;
using Jint.Runtime;

namespace Jint.Native.Json;

/// <summary>
/// Represents a parse node for tracking source text positions.
/// Used for the JSON.parse source text access proposal.
/// </summary>
internal sealed class JsonParseNode
{
    public int Start { get; set; }
    public int End { get; set; }
    public bool IsPrimitive { get; set; }
    public JsValue? OriginalValue { get; set; }
    public List<JsonParseNode>? Elements { get; set; }
    public Dictionary<string, JsonParseNode>? Entries { get; set; }
}

/// <summary>
/// Result of parsing JSON with source information.
/// </summary>
internal readonly struct JsonParseResult
{
    public JsonParseResult(JsValue value, JsonParseNode? node)
    {
        Value = value;
        Node = node;
    }

    public JsValue Value { get; }
    public JsonParseNode? Node { get; }
}

public sealed class JsonParser
{
    private const int ConstraintCheckInterval = Engine.ConstraintCheckInterval;

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

    // Hidden-class shaping of parsed objects (see Shape): members route through the shared
    // per-prototype transition tree so an array of identically-laid-out records shares one interned
    // Shape and each record costs a single allocation instead of a property dictionary plus
    // per-property descriptors. _shapeBudget bounds how many NEW transition nodes one parse call may
    // intern — reused transitions (the identical-records case) cost nothing — because the tree is
    // pinned by its prototype for the prototype's lifetime, so an adversarial cold parse must not
    // grow it without bound. Once exhausted, objects that have not yet started shaping build
    // dictionaries for the rest of the call. The cached empty-root pair avoids a per-object
    // ConditionalWeakTable lookup and is revalidated by prototype identity (mirrors
    // ScriptFunction._ctorEmptyShape).
    private const int ShapeTransitionBudget = 1024;
    private int _shapeBudget;
    private Shape? _cachedEmptyRoot;
    private ObjectInstance? _cachedEmptyRootProto;

    // Property keys repeat across every record of a homogeneous array (the dominant JSON.parse payload):
    // an array of 500 identically-shaped records re-scans the same "id"/"name"/... key hundreds of times.
    // Interning object keys within a single parse lets those records share one key string (and its
    // JsString) instead of re-allocating both per record. Only property KEYS are interned, never string
    // values. The table is direct-mapped (slot = hash & mask, replace on collision): both hit and miss
    // cost a single compare, so key-diverse payloads (whose keys mostly miss) pay no probe tax, hot keys
    // of homogeneous payloads statistically keep their slots, and a hostile payload with millions of
    // distinct/long keys cannot grow the fixed-size table. The table is per-parser-instance and reset per
    // parse (no cross-parse or global state). _expectKey is set immediately before the Lex that scans a
    // key token (right after '{' or ',' inside an object) and consumed by the very next scan, so only
    // keys route through interning.
    private const int MaxInternedKeyLength = 64;
    private const int InternedKeySlots = 256; // power of two, indexed by hash & (InternedKeySlots - 1)
    private InternedKeyEntry[]? _internedKeys;
    private bool _expectKey;

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
            if (_index < _length && IsHexDigit(_source[_index]))
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

    private Token ScanStringLiteral(ref State state, bool isPropertyKey)
    {
        char quote = _source[_index];
        int start = _index;
        ++_index;

        using var sb = new ValueStringBuilder(stackalloc char[64]);
        var scanned = 0;
        while (_index < _length)
        {
            if (++scanned % ConstraintCheckInterval == 0)
            {
                _engine.Constraints.Check();
            }

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

        if (isPropertyKey)
        {
            // Intern object keys straight off the scanned span so repeated keys reuse one string/JsString
            // and skip the span->string allocation entirely on a cache hit (the common homogeneous-record case).
            var interned = InternPropertyKey(sb.AsSpan());
            return CreateToken(Tokens.String, interned.Name, '\"', interned.Value, new TextRange(start, _index));
        }

        var value = sb.ToString();
        return CreateToken(Tokens.String, value, '\"', new JsString(value), new TextRange(start, _index));
    }

    /// <summary>
    /// Interns a just-scanned property-key span for the lifetime of the current parse: identical keys across
    /// records return the same <see cref="string"/> and <see cref="JsString"/> instances, and on a cache hit
    /// no new string is materialized at all. The table is direct-mapped with replace-on-collision, so hits
    /// and misses both cost one compare; keys longer than <see cref="MaxInternedKeyLength"/> are materialized
    /// fresh (current behavior) without touching the table.
    /// </summary>
    private InternedKey InternPropertyKey(ReadOnlySpan<char> span)
    {
        if (span.Length > MaxInternedKeyLength)
        {
            var longName = span.ToString();
            return new InternedKey(longName, new JsString(longName));
        }

        var hash = Hash.GetFNVHashCode(span);
        var entries = _internedKeys ??= new InternedKeyEntry[InternedKeySlots];
        ref var entry = ref entries[hash & (InternedKeySlots - 1)];
        if (entry.Hash == hash && entry.Name is not null && span.SequenceEqual(entry.Name.AsSpan()))
        {
            return new InternedKey(entry.Name, entry.Value);
        }

        var name = span.ToString();
        var value = new JsString(name);
        entry = new InternedKeyEntry(hash, name, value);
        return new InternedKey(name, value);
    }

    private Token Advance(ref State state)
    {
        // Consumed by exactly this scan: set immediately before the Lex that reads a key token.
        var isPropertyKey = _expectKey;
        _expectKey = false;

        char ch = ReadToNextSignificantCharacter();

        if (ch == char.MinValue)
        {
            return CreateToken(Tokens.EOF, string.Empty, '\0', JsValue.Undefined, new TextRange(_index, _index));
        }

        // String literal starts with double quote (#34).
        // Single quote (#39) are not allowed in JSON.
        if (ch == '"')
        {
            return ScanStringLiteral(ref state, isPropertyKey);
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
        Throw.SyntaxError(_engine.Realm, $"{msg} at position {position}");
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

        Expect(ref state, '[');

        // Elements accumulate in a pooled buffer and materialize as an exact-size dense
        // array; nested arrays rent their own builders during the recursion.
        var builder = new JsValueListBuilder(16);
        try
        {
            var elementCount = 0;
            while (!Match(']'))
            {
                if (++elementCount % ConstraintCheckInterval == 0)
                {
                    _engine.Constraints.Check();
                }

                builder.Add(ParseJsonValue(ref state));

                if (!Match(']'))
                {
                    Expect(ref state, ',');
                }
            }

            Expect(ref state, ']');
            state.CurrentDepth--;

            return _engine.Realm.Intrinsics.Array.ConstructFromBuilder(ref builder);
        }
        finally
        {
            builder.Dispose();
        }
    }

    private JsObject ParseJsonObject(ref State state)
    {
        if ((++state.CurrentDepth) > _maxDepth)
        {
            ThrowDepthLimitReached(_lookahead);
        }

        // The token right after '{' is the first key (or '}'): route it through key interning.
        _expectKey = true;
        Expect(ref state, '{');

        var obj = new JsObject(_engine);
        var shaped = false;
        var first = true;

        var memberCount = 0;
        while (!Match('}'))
        {
            if (++memberCount % ConstraintCheckInterval == 0)
            {
                _engine.Constraints.Check();
            }

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
            AddJsonMember(obj, name, value, ref shaped, ref first);

            if (!Match('}'))
            {
                // The token right after ',' is the next key.
                _expectKey = true;
                Expect(ref state, ',');
            }
        }

        Expect(ref state, '}');
        state.CurrentDepth--;

        return obj;
    }

    /// <summary>
    /// Adds one parsed member to <paramref name="obj"/>, routing through the hidden-class machinery
    /// (see <see cref="Shape"/>) so a run of identically-laid-out records — the dominant JSON payload —
    /// shares one interned shape and each record is a single allocation instead of a property dictionary
    /// plus per-property descriptors. <paramref name="shaped"/> and <paramref name="first"/> are the
    /// caller's per-object locals (each recursive object activation carries its own). Anything a shape
    /// cannot represent drops the object to dictionary mode, preserving the insertion order built so far.
    /// A raw own-property store like today's dictionary path: the prototype chain is never consulted,
    /// so an inherited setter (e.g. <c>__proto__</c>) is not invoked.
    /// </summary>
    private void AddJsonMember(JsObject obj, string name, JsValue value, ref bool shaped, ref bool first)
    {
        // Integer-like keys are pre-excluded from shapes (never build-then-deopt): own-key order puts
        // integer indices first (https://tc39.es/ecma262/#sec-ordinaryownpropertykeys), which the
        // slot (= insertion) order cannot express. Same conservative digit-leading classifier as the
        // other shape guards.
        var digitLeading = name.Length > 0 && char.IsDigit(name[0]);

        if (first)
        {
            first = false;
            // Start shaping lazily on the first member so `{}` stays a plain property-less JsObject.
            if (_shapeBudget > 0 && !digitLeading && obj.Prototype is { } proto)
            {
                if (!ReferenceEquals(_cachedEmptyRootProto, proto))
                {
                    _cachedEmptyRoot = _engine.GetEmptyShape(proto);
                    _cachedEmptyRootProto = proto;
                }

                obj.StartShapeBuilding(_cachedEmptyRoot!);
                shaped = true;
            }
        }

        if (shaped)
        {
            if (!digitLeading)
            {
                Key key = name;
                if (obj.ShapeOf.TryGetSlot(key, out var slot))
                {
                    // Duplicate key: last value wins at the first occurrence's position, matching the
                    // dictionary representation's replace-in-place.
                    obj.SetSlot(slot, value);
                    return;
                }

                if (obj.TryShapeAdd(key, value, out var created))
                {
                    if (created)
                    {
                        // Only newly interned transitions consume budget; an object mid-build may
                        // finish shaped after the budget hits zero (overshoot bounded by
                        // Shape.MaxShapeProperties).
                        _shapeBudget--;
                    }

                    return;
                }
            }

            // Integer-like key, or a megamorphic guard (own-property count / transition fan-out)
            // refused the add: finish this object as a dictionary.
            obj.ConvertToDictionaryMode();
            shaped = false;
        }

        obj.FastSetDataProperty(name, value);
    }

    private static bool PropertyNameContainsInvalidCharacters(string propertyName)
    {
        const char max = (char) 31;
        foreach (var c in propertyName)
        {
            if (c != '\t' && c != '\n' && c != '\r' && c != '\b' && c != '\f' && c <= max)
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
        _shapeBudget = ShapeTransitionBudget;
        if (_internedKeys is not null)
        {
            System.Array.Clear(_internedKeys, 0, _internedKeys.Length);
        }
        _expectKey = false;

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

    /// <summary>
    /// Parses JSON and returns both the value and source tracking information.
    /// Used for the JSON.parse source text access proposal.
    /// </summary>
    internal JsonParseResult ParseWithSourceInfo(string code)
    {
        _source = code;
        _index = 0;
        _length = _source.Length;
        _lookahead = null!;
        _shapeBudget = ShapeTransitionBudget;
        if (_internedKeys is not null)
        {
            System.Array.Clear(_internedKeys, 0, _internedKeys.Length);
        }
        _expectKey = false;

        State state = new State();

        Peek(ref state);
        var result = ParseJsonValueWithSourceInfo(ref state);

        Peek(ref state);

        if (_lookahead.Type != Tokens.EOF)
        {
            ThrowError(_lookahead, Messages.UnexpectedToken, _lookahead.Text);
        }
        return result;
    }

    private JsonParseResult ParseJsonValueWithSourceInfo(ref State state)
    {
        Tokens type = _lookahead.Type;
        switch (type)
        {
            case Tokens.NullLiteral:
            case Tokens.BooleanLiteral:
            case Tokens.String:
            case Tokens.Number:
                var token = Lex(ref state);
                var node = new JsonParseNode
                {
                    Start = token.Range.Start,
                    End = token.Range.End,
                    IsPrimitive = true,
                    OriginalValue = token.Value
                };
                return new JsonParseResult(token.Value, node);
            case Tokens.Punctuator:
                if (_lookahead.FirstCharacter == '[')
                {
                    return ParseJsonArrayWithSourceInfo(ref state);
                }
                if (_lookahead.FirstCharacter == '{')
                {
                    return ParseJsonObjectWithSourceInfo(ref state);
                }
                ThrowUnexpected(Lex(ref state));
                break;
        }

        ThrowUnexpected(Lex(ref state));
        return new JsonParseResult(JsValue.Null, null);
    }

    private JsonParseResult ParseJsonArrayWithSourceInfo(ref State state)
    {
        if ((++state.CurrentDepth) > _maxDepth)
        {
            ThrowDepthLimitReached(_lookahead);
        }

        var startPos = _lookahead.Range.Start;
        var elements = new List<JsonParseNode>();

        Expect(ref state, '[');

        var builder = new JsValueListBuilder(16);
        try
        {
            var elementCount = 0;
            while (!Match(']'))
            {
                if (++elementCount % ConstraintCheckInterval == 0)
                {
                    _engine.Constraints.Check();
                }

                var elementResult = ParseJsonValueWithSourceInfo(ref state);
                builder.Add(elementResult.Value);
                if (elementResult.Node != null)
                {
                    elements.Add(elementResult.Node);
                }

                if (!Match(']'))
                {
                    Expect(ref state, ',');
                }
            }

            Expect(ref state, ']');
            var endPos = _index;
            state.CurrentDepth--;

            var arrayValue = _engine.Realm.Intrinsics.Array.ConstructFromBuilder(ref builder);
            var arrayNode = new JsonParseNode
            {
                Start = startPos,
                End = endPos,
                IsPrimitive = false,
                Elements = elements
            };

            return new JsonParseResult(arrayValue, arrayNode);
        }
        finally
        {
            builder.Dispose();
        }
    }

    private JsonParseResult ParseJsonObjectWithSourceInfo(ref State state)
    {
        if ((++state.CurrentDepth) > _maxDepth)
        {
            ThrowDepthLimitReached(_lookahead);
        }

        var startPos = _lookahead.Range.Start;
        var entries = new Dictionary<string, JsonParseNode>(StringComparer.Ordinal);

        // The token right after '{' is the first key (or '}'): route it through key interning.
        _expectKey = true;
        Expect(ref state, '{');

        var obj = new JsObject(_engine);
        var shaped = false;
        var first = true;

        var memberCount = 0;
        while (!Match('}'))
        {
            if (++memberCount % ConstraintCheckInterval == 0)
            {
                _engine.Constraints.Check();
            }

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
            var valueResult = ParseJsonValueWithSourceInfo(ref state);
            AddJsonMember(obj, name, valueResult.Value, ref shaped, ref first);

            if (valueResult.Node != null)
            {
                entries[name] = valueResult.Node;
            }

            if (!Match('}'))
            {
                // The token right after ',' is the next key.
                _expectKey = true;
                Expect(ref state, ',');
            }
        }

        Expect(ref state, '}');
        var endPos = _index;
        state.CurrentDepth--;

        var objectNode = new JsonParseNode
        {
            Start = startPos,
            End = endPos,
            IsPrimitive = false,
            Entries = entries
        };

        return new JsonParseResult(obj, objectNode);
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

    /// <summary>An interned property key: the deduplicated name and its (also deduplicated) <see cref="JsString"/>.</summary>
    [StructLayout(LayoutKind.Auto)]
    private readonly record struct InternedKey(string Name, JsString Value);

    /// <summary>One entry in the per-parse key-intern table; <see cref="Hash"/> is the FNV hash of <see cref="Name"/>.</summary>
    [StructLayout(LayoutKind.Auto)]
    private readonly record struct InternedKeyEntry(int Hash, string Name, JsString Value);

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
