using System.Buffers;
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

    /// <summary>
    /// Documents up to this many UTF-8 bytes transcode into a stack buffer of the same char count (the
    /// char count can never exceed the byte count); longer ones rent from <see cref="ArrayPool{T}"/>.
    /// </summary>
    private const int Utf8TranscodeStackallocLimit = 256;

#if !SUPPORTS_UTF8_TRANSCODE
    /// <summary>
    /// Throws on malformed input instead of substituting U+FFFD, matching the strictness of
    /// <c>Utf8.ToUtf16(..., replaceInvalidSequences: false)</c> used on the newer runtimes.
    /// </summary>
    private static readonly UTF8Encoding StrictUtf8 = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);
#endif

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

    // String VALUES also repeat heavily across a homogeneous payload (status/type/category fields, enum-like
    // tokens, ...). The same direct-mapped, replace-on-collision discipline used for keys lets repeated values
    // share one string and one JsString instead of allocating both per occurrence; a cache hit allocates
    // nothing. Interning JsString identity is safe because === on strings is value-based, so a shared instance
    // is unobservable to script. Kept as a SEPARATE table from the keys so a value can never evict a hot key
    // (and vice versa). Long/unique payloads (length > MaxInternedValueLength) skip the table and are created
    // fresh via JsString.Create so they can never thrash the fixed-size table. Reset per parse like the keys.
    private const int MaxInternedValueLength = 64;
    private const int InternedValueSlots = 256; // power of two, indexed by hash & (InternedValueSlots - 1)
    private InternedValueEntry[]? _internedValues;

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

    private char ScanHexEscape(ReadOnlySpan<char> source)
    {
        int code = char.MinValue;

        for (int i = 0; i < 4; ++i)
        {
            if (_index < _length && IsHexDigit(source[_index]))
            {
                char ch = char.ToLower(source[_index++], CultureInfo.InvariantCulture);
                code = code * 16 + "0123456789abcdef".IndexOf(ch);
            }
            else
            {
                ThrowError(_index, Messages.ExpectedHexadecimalDigit);
            }
        }
        return (char) code;
    }

    private char ReadToNextSignificantCharacter(ReadOnlySpan<char> source)
    {
        char result = _index < _length ? source[_index] : char.MinValue;
        while (IsWhiteSpace(result))
        {
            if ((++_index) >= _length)
            {
                return char.MinValue;
            }
            result = source[_index];
        }
        return result;
    }

    private Token CreateToken(Tokens type, string? text, char firstCharacter, JsValue value, in TextRange range)
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

    private Token ScanPunctuator(ReadOnlySpan<char> source)
    {
        int start = _index;
        char code = start < source.Length ? source[_index] : char.MinValue;

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

    private Token ScanNumericLiteral(ReadOnlySpan<char> source)
    {
        using var sb = new ValueStringBuilder(stackalloc char[64]);
        var start = _index;
        var ch = source.CharCodeAt(_index);
        var canBeInteger = true;

        // Number start with a -
        if (ch == '-')
        {
            sb.Append(ch);
            ch = source.CharCodeAt(++_index);
        }

        if (ch != '.')
        {
            var firstCharacter = ch;
            sb.Append(ch);
            ch = source.CharCodeAt(++_index);

            // Hex number starts with '0x'.
            // Octal number starts with '0'.
            // This runs right after the first digit was appended (a possible sign does not matter),
            // so the leading-zero rule also rejects '-09' per the JSON grammar (int = zero / digit1-9 *DIGIT).
            if (firstCharacter == '0')
            {
                canBeInteger = false;
                // decimal number starts with '0' such as '09' is illegal.
                if (ch > 0 && IsDecimalDigit(ch))
                {
                    ThrowError(_index, Messages.UnexpectedToken, ch);
                }
            }

            while (IsDecimalDigit((ch = source.CharCodeAt(_index))))
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

            // the JSON grammar requires at least one digit after the decimal point ('1.' is illegal)
            if (!IsDecimalDigit(source.CharCodeAt(_index)))
            {
                ThrowError(_index, Messages.UnexpectedToken, source.CharCodeAt(_index));
            }

            while (IsDecimalDigit((ch = source.CharCodeAt(_index))))
            {
                sb.Append(ch);
                _index++;
            }
        }

        if (ch is 'e' or 'E')
        {
            canBeInteger = false;
            sb.Append(ch);
            ch = source.CharCodeAt(++_index);
            if (ch is '+' or '-')
            {
                sb.Append(ch);
                ch = source.CharCodeAt(++_index);
            }
            if (IsDecimalDigit(ch))
            {
                while (IsDecimalDigit(ch = source.CharCodeAt(_index)))
                {
                    sb.Append(ch);
                    _index++;
                }
            }
            else
            {
                ThrowError(_index, Messages.UnexpectedToken, source.CharCodeAt(_index));
            }
        }

        JsNumber value;
#if NET8_0_OR_GREATER
        // Parse straight off the scanned span so the common case never materializes the intermediate
        // number string. The raw text is only needed for the (rare) "unexpected trailing token"
        // diagnostic, which the token carries no eager copy of: TokenText reconstructs it from the token
        // range on demand. Both long.TryParse and double.Parse have span overloads on net8+.
        var number = sb.AsSpan();
        if (canBeInteger && long.TryParse(number, NumberStyles.Integer, CultureInfo.InvariantCulture, out var longResult) && longResult != -0)
        {
            value = JsNumber.Create(longResult);
        }
        else if (TryParseDecimalFast(number, out var fastValue))
        {
            // Bit-identical to the double.Parse fallback below for every shape it accepts (proven by
            // JsonTests.NumberFastPath*); the constructor keeps the Types.Number tagging the
            // double.Parse path produces. Gated to net8+ because only there is double.Parse guaranteed
            // to be IEEE correctly-rounded — on the legacy runtimes it can differ by an ULP, so we keep
            // deferring to it to stay byte-for-byte identical to the pre-existing behavior.
            value = new JsNumber(fastValue);
        }
        else
        {
            value = new JsNumber(double.Parse(number, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint | NumberStyles.AllowExponent, CultureInfo.InvariantCulture));
        }

        // Number tokens carry no eager Text (null); the trailing-token diagnostic rebuilds it from Range.
        return CreateToken(Tokens.Number, text: null, '\0', value, new TextRange(start, _index));
#else
        // Legacy runtimes have no span-based number parsing and no correctly-rounded double.Parse fast
        // path, so keep the original string-materializing behavior byte-for-byte.
        var number = sb.ToString();
        if (canBeInteger && long.TryParse(number, NumberStyles.Integer, CultureInfo.InvariantCulture, out var longResult) && longResult != -0)
        {
            value = JsNumber.Create(longResult);
        }
        else
        {
            value = new JsNumber(double.Parse(number, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint | NumberStyles.AllowExponent, CultureInfo.InvariantCulture));
        }

        return CreateToken(Tokens.Number, number, '\0', value, new TextRange(start, _index));
#endif
    }

#if NET8_0_OR_GREATER
    // Exact power-of-ten scaling factors for the fraction fast path. 10^0..10^15 are all exactly
    // representable doubles (each significand fits in 53 bits), so dividing an exact long numerator
    // (&lt; 10^15 &lt; 2^53) by one of these yields the correctly-rounded quotient — bit-identical to
    // double.Parse on the same text (see JsonTests.NumberFastPath*). The fraction path only ever indexes
    // 0..14 (at least one integer digit is required), the extra slots are headroom.
    private static readonly double[] PowersOf10 =
    {
        1e0, 1e1, 1e2, 1e3, 1e4, 1e5, 1e6, 1e7,
        1e8, 1e9, 1e10, 1e11, 1e12, 1e13, 1e14, 1e15,
    };

    /// <summary>
    /// Fast path for the dominant JSON number shape: an optional leading '-', integer digits, an
    /// optional '.fraction' and NO exponent, with at most 15 total digits. Every digit is accumulated
    /// into a single exact <see cref="long"/> numerator which is divided once by an exact power of ten,
    /// avoiding the general floating-point parse. Returns <see langword="false"/> — deferring to
    /// <c>double.Parse</c> — for anything outside that shape (an exponent, a 16th significant digit, or
    /// any unexpected trailing character). The result is bit-identical to <c>double.Parse</c> for every
    /// accepted input because both operands of the division are exactly representable.
    /// </summary>
    private static bool TryParseDecimalFast(ReadOnlySpan<char> text, out double result)
    {
        result = 0;
        var len = text.Length;
        var i = 0;

        var negative = false;
        if (len > 0 && text[0] == '-')
        {
            negative = true;
            i = 1;
        }

        long mantissa = 0;
        var totalDigits = 0;
        var intDigits = 0;
        while (i < len)
        {
            var c = text[i];
            if (!IsDecimalDigit(c))
            {
                break;
            }
            if (totalDigits == 15)
            {
                return false; // more significant digits than the exact long numerator can hold
            }
            mantissa = mantissa * 10 + (c - '0');
            totalDigits++;
            intDigits++;
            i++;
        }

        if (intDigits == 0)
        {
            return false; // no integer digit (e.g. a lone '.') — let the general path handle it
        }

        var fractionDigits = 0;
        if (i < len && text[i] == '.')
        {
            i++;
            while (i < len)
            {
                var c = text[i];
                if (!IsDecimalDigit(c))
                {
                    break;
                }
                if (totalDigits == 15)
                {
                    return false;
                }
                mantissa = mantissa * 10 + (c - '0');
                totalDigits++;
                fractionDigits++;
                i++;
            }
        }

        if (i != len)
        {
            return false; // exponent or other trailing character — not covered by the fast path
        }

        if (negative && mantissa == 0)
        {
            // Negative zero ("-0", "-0.0", ...): defer to double.Parse so the sign of zero always comes
            // from the platform parser rather than being synthesized here.
            return false;
        }

        var value = fractionDigits == 0 ? (double) mantissa : (double) mantissa / PowersOf10[fractionDigits];
        result = negative ? -value : value;
        return true;
    }
#endif

    private Token ScanBooleanLiteral(ReadOnlySpan<char> source)
    {
        var start = _index;
        if (ConsumeMatch(source, "true"))
        {
            return CreateToken(Tokens.BooleanLiteral, "true", '\t', JsBoolean.True, new TextRange(start, _index));
        }

        if (ConsumeMatch(source, "false"))
        {
            return CreateToken(Tokens.BooleanLiteral, "false", '\f', JsBoolean.False, new TextRange(start, _index));
        }

        ThrowError(start, Messages.UnexpectedTokenIllegal);
        return null!;
    }

    private bool ConsumeMatch(ReadOnlySpan<char> source, string text)
    {
        var start = _index;
        var length = text.Length;
        if (start + length - 1 < source.Length && source.Slice(start, length).SequenceEqual(text.AsSpan()))
        {
            _index += length;
            return true;
        }

        return false;
    }

    private Token ScanNullLiteral(ReadOnlySpan<char> source)
    {
        int start = _index;
        if (ConsumeMatch(source, "null"))
        {
            return CreateToken(Tokens.NullLiteral, "null", 'n', JsValue.Null, new TextRange(start, _index));
        }

        ThrowError(start, Messages.UnexpectedTokenIllegal);
        return null!;
    }

#if NET8_0_OR_GREATER
    // Characters that terminate a bulk string-content run: the closing quote, the escape backslash and
    // every control character (< 0x20, which JSON forbids unescaped). Any other code point \u2014 including
    // the Unicode line separators U+2028/U+2029, which the JSON grammar permits raw inside strings \u2014
    // is ordinary content. IndexOfAny over this set finds the first such character exactly where a
    // per-char loop would have stopped.
    private static readonly SearchValues<char> JsonStringStopChars = CreateStringStopChars();

    private static SearchValues<char> CreateStringStopChars()
    {
        Span<char> stops = stackalloc char[34];
        for (var i = 0; i < 32; i++)
        {
            stops[i] = (char) i;
        }
        stops[32] = '"';
        stops[33] = '\\';
        return SearchValues.Create(stops);
    }
#else
    // Portable fallback: the vectorized IndexOfAny locates the next quote/backslash, then we make sure
    // no control character (< 0x20) appears earlier, so the returned index matches the NET8
    // SearchValues path.
    //
    // Deliberately NOT routed through an IndexOfAny(SearchValues<char>) polyfill, even though one
    // exists. This keeps the vectorized IndexOfAny that every target framework has and only walks the
    // span up to the quote; a polyfilled SearchValues search would test every character one at a time
    // over the whole run, which is a real regression in the hottest parser here. The #if stays.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int IndexOfStringStop(ReadOnlySpan<char> span)
    {
        var qb = span.IndexOfAny('"', '\\');
        var limit = qb < 0 ? span.Length : qb;
        for (var i = 0; i < limit; i++)
        {
            if (span[i] < ' ')
            {
                return i;
            }
        }
        return qb;
    }
#endif

    private Token ScanStringLiteral(ref State state, bool isPropertyKey)
    {
        var source = state.Source;
        char quote = source[_index];
        int start = _index;
        ++_index;

        var length = _length;

        using var sb = new ValueStringBuilder(stackalloc char[64]);
        var scanned = 0;
        while (_index < length)
        {
            // Bulk fast path: copy the run of ordinary characters up to the next quote, backslash or
            // control character (< 0x20) in one shot. The search lands on exactly the character a
            // per-char loop would have stopped at, so escapes and every error position are preserved
            // byte-for-byte.
            var remaining = source.Slice(_index, length - _index);
#if NET8_0_OR_GREATER
            var stop = remaining.IndexOfAny(JsonStringStopChars);
#else
            var stop = IndexOfStringStop(remaining);
#endif
            if (stop < 0)
            {
                // No closing quote (and no special character) remains: unterminated literal, reported
                // below at position == length just like the original scanner.
                _index = length;
                break;
            }

            scanned += stop + 1;
            if (scanned >= ConstraintCheckInterval)
            {
                scanned = 0;
                _engine.Constraints.Check();
            }

            if (stop > 0)
            {
                sb.Append(remaining.Slice(0, stop));
            }

            var pos = _index + stop;
            char ch = source[pos];
            _index = pos + 1;

            if (ch == quote)
            {
                quote = char.MinValue;
                break;
            }

            if (ch <= 31)
            {
                ThrowError(pos, Messages.InvalidCharacter);
            }

            if (ch == '\\')
            {
                ch = source.CharCodeAt(_index++);

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
                        sb.Append(ScanHexEscape(source));
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

        // Intern the just-decoded string value so repeated values reuse one string/JsString; on a hit no
        // new string is materialized at all (the common homogeneous-payload case).
        var internedValue = InternStringValue(sb.AsSpan());
        return CreateToken(Tokens.String, internedValue.Text, '\"', internedValue.Value, new TextRange(start, _index));
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

    /// <summary>
    /// Interns a just-decoded string VALUE span for the lifetime of the current parse: identical values
    /// (regardless of how they were escaped in the source) return the same <see cref="string"/> and
    /// <see cref="JsString"/> instances, and on a cache hit nothing is allocated. The table is direct-mapped
    /// with replace-on-collision so hits and misses both cost one compare; values longer than
    /// <see cref="MaxInternedValueLength"/> skip the table and are created fresh via <see cref="JsString.Create(string)"/>
    /// (so empty/single-char values still hit its caches). The lookup uses the DECODED span, so
    /// <c>"abc"</c> and <c>"abc"</c> intern to the same instances.
    /// </summary>
    private InternedValue InternStringValue(ReadOnlySpan<char> span)
    {
        if (span.Length > MaxInternedValueLength)
        {
            var longText = span.ToString();
            return new InternedValue(longText, JsString.Create(longText));
        }

        var hash = Hash.GetFNVHashCode(span);
        var entries = _internedValues ??= new InternedValueEntry[InternedValueSlots];
        ref var entry = ref entries[hash & (InternedValueSlots - 1)];
        if (entry.Hash == hash && entry.Text is not null && span.SequenceEqual(entry.Text.AsSpan()))
        {
            return new InternedValue(entry.Text, entry.Value);
        }

        var text = span.ToString();
        var value = JsString.Create(text);
        entry = new InternedValueEntry(hash, text, value);
        return new InternedValue(text, value);
    }

    private Token Advance(ref State state)
    {
        // Consumed by exactly this scan: set immediately before the Lex that reads a key token.
        var isPropertyKey = _expectKey;
        _expectKey = false;

        var source = state.Source;
        char ch = ReadToNextSignificantCharacter(source);

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
            if (IsDecimalDigit(source.CharCodeAt(_index + 1)))
            {
                return ScanNumericLiteral(source);
            }
            return ScanPunctuator(source);
        }

        if (IsDecimalDigit(ch))
        {
            return ScanNumericLiteral(source);
        }

        if (ch == 't' || ch == 'f')
        {
            return ScanBooleanLiteral(source);
        }

        if (ch == 'n')
        {
            return ScanNullLiteral(source);
        }

        return ScanPunctuator(source);
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

    /// <summary>
    /// The token's display text for diagnostics. Number tokens carry no eager <see cref="Token.Text"/>
    /// (to avoid the per-number string allocation), so their raw source text is reconstructed from the
    /// token range here — byte-identical to the scanned text since numbers contain no escapes.
    /// </summary>
    private static string TokenText(ReadOnlySpan<char> source, Token token)
        => token.Text ?? source.Slice(token.Range.Start, token.Range.End - token.Range.Start).ToString();

    // Throw an exception because of the token.

    private void ThrowUnexpected(ReadOnlySpan<char> source, Token token)
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
        ThrowError(token, Messages.UnexpectedToken, TokenText(source, token));
    }

    /// <summary>
    /// Reports the closing punctuator sitting right after a ',' as the syntax error it is. The JSON
    /// grammar has no trailing comma — <c>JSONElementList : JSONElementList , JSONValue</c> and
    /// <c>JSONMemberList : JSONMemberList , JSONMember</c>
    /// (https://tc39.es/ecma262/#sec-json.parse) both require another element or member after the
    /// separator — so <c>[1,]</c> and <c>{"a":1,}</c> are as malformed as <c>[,]</c> already was. The
    /// element and member loops re-test for the closing punctuator at the top, which would otherwise
    /// read the ',' as harmless and finish the value.
    /// </summary>
    private void ThrowOnTrailingComma(ref State state, char closing)
    {
        if (Match(closing))
        {
            ThrowUnexpected(state.Source, _lookahead);
        }
    }

    // Expect the next token to match the specified punctuator.
    // If not, an exception will be thrown.
    private void Expect(ref State state, char value)
    {
        Token token = Lex(ref state);
        if (token.Type != Tokens.Punctuator || value != token.FirstCharacter)
        {
            ThrowUnexpected(state.Source, token);
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
                    ThrowOnTrailingComma(ref state, ']');
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
                ThrowUnexpected(state.Source, Lex(ref state));
            }

            var nameToken = Lex(ref state);
            var name = nameToken.Text!; // String tokens (keys) always carry non-null Text

            Expect(ref state, ':');
            var value = ParseJsonValue(ref state);
            AddJsonMember(obj, name, value, ref shaped, ref first);

            if (!Match('}'))
            {
                // The token right after ',' is the next key.
                _expectKey = true;
                Expect(ref state, ',');
                ThrowOnTrailingComma(ref state, '}');
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
        var digitLeading = Shape.IsIntegerIndexLikeKey(name);

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
                if (obj.ShapeOf.TryGetSlot(in key, out var slot))
                {
                    // Duplicate key: last value wins at the first occurrence's position, matching the
                    // dictionary representation's replace-in-place.
                    obj.SetSlot(slot, value);
                    return;
                }

                if (obj.TryShapeAdd(in key, value, out var created))
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
                ThrowUnexpected(state.Source, Lex(ref state));
                break;
        }

        ThrowUnexpected(state.Source, Lex(ref state));
        // can't be reached
        return JsValue.Null;
    }

    /// <summary>
    /// Parses a JSON document, throwing a <see cref="JavaScriptException"/> carrying a
    /// <c>SyntaxError</c> if it is malformed.
    /// </summary>
    /// <exception cref="ArgumentNullException"><paramref name="code"/> is <see langword="null"/>.</exception>
    public JsValue Parse(string code)
    {
        if (code is null)
        {
            Throw.ArgumentNullException(nameof(code));
        }

        return Parse(code.AsSpan());
    }

    /// <summary>
    /// Parses a JSON document held as UTF-16 characters, for callers that already have the document in a
    /// buffer and would otherwise materialize a <see cref="string"/> only to hand it to
    /// <see cref="Parse(string)"/>. The document itself is never copied: the scanner reads straight out of
    /// <paramref name="code"/>, so only the values the document actually produces are allocated.
    /// </summary>
    /// <remarks>
    /// Byte-for-byte equivalent to <see cref="Parse(string)"/> over the same characters, error positions
    /// included. In particular a leading U+FEFF is <em>not</em> skipped — it is not JSON whitespace, so it
    /// is a syntax error exactly as it is for the string overload. Only the UTF-8 overload strips a byte
    /// order mark.
    /// </remarks>
    public JsValue Parse(ReadOnlySpan<char> code)
    {
        // The one choke point: both other overloads funnel here, so this is where the engine is entered.
        // A parse runs no user code, but it builds objects and arrays into the engine's realm for the whole
        // document, which is engine-owned state — so it is a host entry in the sense both the concurrency
        // contract and the execution constraints mean. Its sibling JsonSerializer has always been bracketed;
        // this is that same bracket, reached through the span-taking overload because the document cannot be
        // captured by a closure. On the in-box callers — JSON.parse, a JSON module, response.json(), a JWK
        // import — the engine is already claimed by this thread, so this takes the nested branch and arms
        // nothing: the surrounding evaluation's budget goes on applying to the parse, which is what bounds a
        // 100 MB document a script hands to JSON.parse.
        return _engine.ExecuteWithConstraints(
            _engine.Options.Strict,
            code,
            this,
            static (source, parser) => parser.ParseCore(source));
    }

    private JsValue ParseCore(ReadOnlySpan<char> code)
    {
        State state = Reset(code);

        Peek(ref state);
        JsValue jsv = ParseJsonValue(ref state);

        Peek(ref state);

        if (_lookahead.Type != Tokens.EOF)
        {
            ThrowError(_lookahead, Messages.UnexpectedToken, TokenText(state.Source, _lookahead));
        }
        return jsv;
    }

    /// <summary>
    /// Parses a JSON document held as UTF-8 bytes, for callers such as network and storage layers that
    /// hold the document as bytes and would otherwise transcode it to a <see cref="string"/> only to hand
    /// it to <see cref="Parse(string)"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The engine's scanner is UTF-16, so the bytes are transcoded before parsing; what is avoided is the
    /// intermediate <see cref="string"/>, not the transcode. Short documents are converted on the stack
    /// and longer ones into a buffer rented from <see cref="ArrayPool{T}"/> and returned on every path,
    /// so no per-parse allocation is made for the document text itself. This is not a byte-level parser.
    /// </para>
    /// <para>
    /// A leading UTF-8 byte order mark (<c>EF BB BF</c>) is skipped, matching what callers of
    /// <c>JsonDocument.Parse</c> over UTF-8 expect. Only one leading mark is skipped; a U+FEFF anywhere
    /// else is a syntax error, as it is for the other overloads.
    /// </para>
    /// <para>
    /// <paramref name="utf8Json"/> must be well-formed UTF-8. An invalid sequence is reported the same way
    /// malformed JSON is — a <see cref="JavaScriptException"/> carrying a <c>SyntaxError</c>, catchable by
    /// script — rather than as an <see cref="ArgumentException"/> from the decoder. The position in the
    /// message is the byte offset of the offending sequence, counted after any byte order mark.
    /// </para>
    /// </remarks>
    public JsValue Parse(ReadOnlySpan<byte> utf8Json)
    {
        // Matches JsonDocument.Parse: a byte order mark is a legitimate part of a UTF-8 stream but not of
        // the JSON grammar, so exactly one leading mark is consumed here.
        if (utf8Json.Length >= 3 && utf8Json[0] == 0xEF && utf8Json[1] == 0xBB && utf8Json[2] == 0xBF)
        {
            utf8Json = utf8Json.Slice(3);
        }

        // A UTF-8 sequence never decodes to more UTF-16 code units than it has bytes (a 4-byte sequence
        // becomes a 2-char surrogate pair), so the byte count is always a sufficient char capacity.
        char[]? rented = null;
        Span<char> buffer = utf8Json.Length <= Utf8TranscodeStackallocLimit
            ? stackalloc char[Utf8TranscodeStackallocLimit]
            : (rented = ArrayPool<char>.Shared.Rent(utf8Json.Length));

        try
        {
            var length = TranscodeUtf8(utf8Json, buffer);
            return Parse(buffer.Slice(0, length));
        }
        finally
        {
            if (rented is not null)
            {
                ArrayPool<char>.Shared.Return(rented);
            }
        }
    }

    /// <summary>
    /// Decodes <paramref name="utf8Json"/> into <paramref name="destination"/>, which the caller has sized
    /// to at least the byte count. Decoding is strict: an invalid sequence raises the parser's own
    /// <c>SyntaxError</c> at its byte offset rather than being replaced with U+FFFD, so a corrupt document
    /// is reported as such instead of being parsed as if it contained replacement characters.
    /// </summary>
    private unsafe int TranscodeUtf8(ReadOnlySpan<byte> utf8Json, Span<char> destination)
    {
        if (utf8Json.IsEmpty)
        {
            return 0;
        }

#if SUPPORTS_UTF8_TRANSCODE
        // The destination cannot be too small (see the caller) and the whole document is present, so Done
        // is the only non-error outcome; everything else means the bytes are not well-formed UTF-8.
        var status = System.Text.Unicode.Utf8.ToUtf16(utf8Json, destination, out var bytesRead, out var charsWritten, replaceInvalidSequences: false);
        if (status != OperationStatus.Done)
        {
            ThrowError(bytesRead, Messages.InvalidUtf8);
        }

        return charsWritten;
#else
        // No span-based decoding on the legacy runtimes: a UTF8Encoding configured to throw gives the same
        // strictness through the pointer overloads, and reports the offending offset on the exception.
        try
        {
            fixed (byte* bytes = utf8Json)
            fixed (char* chars = destination)
            {
                return StrictUtf8.GetChars(bytes, utf8Json.Length, chars, destination.Length);
            }
        }
        catch (DecoderFallbackException ex)
        {
            ThrowError(ex.Index, Messages.InvalidUtf8);
            return 0;
        }
#endif
    }

    /// <summary>
    /// Resets the per-parse scanner state and returns the <see cref="State"/> carrying the document being
    /// parsed. The document lives in the state rather than in a field because a
    /// <see cref="ReadOnlySpan{T}"/> cannot be a field of a class, and <see cref="State"/> is a
    /// <c>ref struct</c> already threaded through the whole descent — which is what lets the scanner read
    /// the caller's buffer directly, whatever kind of buffer it is.
    /// </summary>
    private State Reset(ReadOnlySpan<char> code)
    {
        _index = 0;
        _length = code.Length;
        _lookahead = null!;
        _shapeBudget = ShapeTransitionBudget;
        if (_internedKeys is not null)
        {
            System.Array.Clear(_internedKeys, 0, _internedKeys.Length);
        }
        if (_internedValues is not null)
        {
            System.Array.Clear(_internedValues, 0, _internedValues.Length);
        }
        _expectKey = false;

        return new State(code);
    }

    /// <summary>
    /// Parses JSON and returns both the value and source tracking information.
    /// Used for the JSON.parse source text access proposal.
    /// </summary>
    internal JsonParseResult ParseWithSourceInfo(string code)
    {
        State state = Reset(code.AsSpan());

        Peek(ref state);
        var result = ParseJsonValueWithSourceInfo(ref state);

        Peek(ref state);

        if (_lookahead.Type != Tokens.EOF)
        {
            ThrowError(_lookahead, Messages.UnexpectedToken, TokenText(state.Source, _lookahead));
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
                ThrowUnexpected(state.Source, Lex(ref state));
                break;
        }

        ThrowUnexpected(state.Source, Lex(ref state));
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
                    ThrowOnTrailingComma(ref state, ']');
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
                ThrowUnexpected(state.Source, Lex(ref state));
            }

            var nameToken = Lex(ref state);
            var name = nameToken.Text!; // String tokens (keys) always carry non-null Text

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
                ThrowOnTrailingComma(ref state, '}');
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
        public State(ReadOnlySpan<char> source)
        {
            Source = source;
        }

        /// <summary>
        /// The document being parsed. It is carried here rather than in a field of the parser because a
        /// <see cref="ReadOnlySpan{T}"/> cannot be a field of a class, and this struct is already threaded
        /// by <c>ref</c> through the whole descent.
        /// </summary>
        public readonly ReadOnlySpan<char> Source;

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

        // Null only for Number tokens (see ScanNumericLiteral): their raw text is reconstructed from
        // Range on demand by TokenText. Every other token type carries a non-null Text.
        public string? Text;
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

    /// <summary>An interned string value: the deduplicated text and its (also deduplicated) <see cref="JsString"/>.</summary>
    [StructLayout(LayoutKind.Auto)]
    private readonly record struct InternedValue(string Text, JsString Value);

    /// <summary>One entry in the per-parse value-intern table; <see cref="Hash"/> is the FNV hash of <see cref="Text"/>.</summary>
    [StructLayout(LayoutKind.Auto)]
    private readonly record struct InternedValueEntry(int Hash, string Text, JsString Value);

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
        public const string InvalidUtf8 = "Invalid UTF-8 sequence in JSON";
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

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static char CharCodeAt(this ReadOnlySpan<char> source, int index)
    {
        if ((uint) index >= (uint) source.Length)
        {
            // char.MinValue is used as the null value
            return char.MinValue;
        }

        return source[index];
    }
}
