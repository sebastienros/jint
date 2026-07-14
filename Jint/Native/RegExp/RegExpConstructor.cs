using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using Jint.Collections;
using Jint.Native.Function;
using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Descriptors;
using Jint.Runtime.Interop;
using Jint.Runtime.RegExp;

namespace Jint.Native.RegExp;

[JsObject]
public sealed partial class RegExpConstructor : Constructor
{
    private static readonly JsString _functionName = new JsString("RegExp");
    private static readonly Regex DummyRegex = new("(?:)", RegexOptions.None, TimeSpan.FromSeconds(1));

    // B.2.4 RegExp Legacy Static Properties
    internal string _legacyInput = "";
    internal string _legacyLastMatch = "";
    internal string _legacyLastParen = "";
    internal readonly string[] _legacyParens = new string[9];
    // Left/right context computed lazily to avoid Substring allocations per exec()
    private string _legacyContextInput = "";
    private int _legacyMatchIndex;
    private int _legacyMatchEnd;
    internal string _legacyLeftContext => _legacyContextInput.Length > 0 ? _legacyContextInput.Substring(0, _legacyMatchIndex) : "";
    internal string _legacyRightContext => _legacyContextInput.Length > 0 ? _legacyContextInput.Substring(_legacyMatchEnd) : "";

    internal void SetLegacyContext(string input, int matchIndex, int matchLength)
    {
        _legacyContextInput = input;
        _legacyMatchIndex = matchIndex;
        _legacyMatchEnd = matchIndex + matchLength;
    }

    internal RegExpConstructor(
        Engine engine,
        Realm realm,
        FunctionPrototype functionPrototype,
        ObjectPrototype objectPrototype)
        : base(engine, realm, _functionName)
    {
        _prototype = functionPrototype;
        PrototypeObject = new RegExpPrototype(engine, realm, this, objectPrototype);
        _length = new PropertyDescriptor(2, PropertyFlag.Configurable);
        _prototypeDescriptor = new PropertyDescriptor(PrototypeObject, PropertyFlag.AllForbidden);
        for (var i = 0; i < 9; i++)
        {
            _legacyParens[i] = "";
        }
    }

    internal RegExpPrototype PrototypeObject { get; }

    protected override void Initialize()
    {
        CreateProperties_Generated();
        CreateSymbols_Generated();
        AddLegacyAccessors();
    }

    [JsSymbolAccessor("Species")]
    private static JsValue Species(JsValue thisObject) => thisObject;

    // B.2.4 Legacy static accessors. Kept hand-rolled (rather than [JsAccessor]) because the
    // accessor function names are non-identifier strings like "$&" / "$_" / "$1" / "$`" — when
    // the source generator names the underlying function "get $&", `Function.prototype.toString`
    // returns text that fails test262's native-function-syntax matcher. Building the accessors
    // here with empty-named ClrFunctions preserves the pre-source-gen behaviour.
    private void AddLegacyAccessors()
    {
        const PropertyFlag LengthFlags = PropertyFlag.Configurable;

        JsValue LegacyGetter(Func<RegExpConstructor, string> getter, JsValue thisObj)
        {
            if (!ReferenceEquals(thisObj, this))
            {
                Throw.TypeError(_realm, "Getter called on non-RegExp constructor");
            }
            return getter(this);
        }

        GetSetPropertyDescriptor CreateLegacyReadOnlyAccessor(Func<RegExpConstructor, string> getter)
        {
            return new GetSetPropertyDescriptor(
                get: new ClrFunction(Engine, "", (thisObj, _) => LegacyGetter(getter, thisObj), 0, LengthFlags),
                set: Undefined,
                flags: PropertyFlag.Configurable);
        }

        // input/$_ — readable and writable
        var inputGetter = new ClrFunction(Engine, "get input", (thisObj, _) =>
        {
            if (!ReferenceEquals(thisObj, this)) Throw.TypeError(_realm, "Getter called on non-RegExp constructor");
            return (JsValue) _legacyInput;
        }, 0, LengthFlags);
        var inputSetter = new ClrFunction(Engine, "set input", (thisObj, args) =>
        {
            if (!ReferenceEquals(thisObj, this)) Throw.TypeError(_realm, "Setter called on non-RegExp constructor");
            _legacyInput = TypeConverter.ToString(args.At(0));
            return Undefined;
        }, 1, LengthFlags);
        var inputAccessor = new GetSetPropertyDescriptor(get: inputGetter, set: inputSetter, flags: PropertyFlag.Configurable);

        // $1-$9 — readable, [[Set]]: undefined per B.2.4
        GetSetPropertyDescriptor CreateParenAccessor(int index)
        {
            var i = index;
            return new GetSetPropertyDescriptor(
                get: new ClrFunction(Engine, "", (thisObj, _) =>
                {
                    if (!ReferenceEquals(thisObj, this)) Throw.TypeError(_realm, "Getter called on non-RegExp constructor");
                    return (JsValue) _legacyParens[i];
                }, 0, LengthFlags),
                set: Undefined,
                flags: PropertyFlag.Configurable);
        }

        SetProperty("input", inputAccessor);
        SetProperty("$_", inputAccessor);
        SetProperty("lastMatch", CreateLegacyReadOnlyAccessor(static c => c._legacyLastMatch));
        SetProperty("$&", CreateLegacyReadOnlyAccessor(static c => c._legacyLastMatch));
        SetProperty("lastParen", CreateLegacyReadOnlyAccessor(static c => c._legacyLastParen));
        SetProperty("$+", CreateLegacyReadOnlyAccessor(static c => c._legacyLastParen));
        SetProperty("leftContext", CreateLegacyReadOnlyAccessor(static c => c._legacyLeftContext));
        SetProperty("$`", CreateLegacyReadOnlyAccessor(static c => c._legacyLeftContext));
        SetProperty("rightContext", CreateLegacyReadOnlyAccessor(static c => c._legacyRightContext));
        SetProperty("$'", CreateLegacyReadOnlyAccessor(static c => c._legacyRightContext));
        SetProperty("$1", CreateParenAccessor(0));
        SetProperty("$2", CreateParenAccessor(1));
        SetProperty("$3", CreateParenAccessor(2));
        SetProperty("$4", CreateParenAccessor(3));
        SetProperty("$5", CreateParenAccessor(4));
        SetProperty("$6", CreateParenAccessor(5));
        SetProperty("$7", CreateParenAccessor(6));
        SetProperty("$8", CreateParenAccessor(7));
        SetProperty("$9", CreateParenAccessor(8));
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-regexp.escape
    /// </summary>
    [JsFunction]
    private JsString Escape(JsValue thisObject, JsValue s)
    {
        // 1. If S is not a String, throw a TypeError exception.
        if (!s.IsString())
        {
            Throw.TypeError(_realm, "RegExp.escape requires a string argument");
            return null!;
        }

        var str = s.ToString();

        // 2. Let escaped be the empty String.
        // 3. Let cpList be StringToCodePoints(S).
        // 4. For each code point c in cpList, do
        if (string.IsNullOrEmpty(str))
        {
            return JsString.Empty;
        }

        var sb = new ValueStringBuilder(stackalloc char[64]);
        var isFirst = true;

        // Iterate by code points (handling surrogate pairs)
        for (var i = 0; i < str.Length; i++)
        {
            var c = str[i];
            int codePoint;

            // Handle surrogate pairs
            if (char.IsHighSurrogate(c) && i + 1 < str.Length && char.IsLowSurrogate(str[i + 1]))
            {
                codePoint = char.ConvertToUtf32(c, str[i + 1]);
                i++; // Skip the low surrogate
            }
            else
            {
                codePoint = c;
            }

            // a. If escaped is the empty String, and c is matched by DecimalDigit or AsciiLetter, then
            if (isFirst && IsDecimalDigitOrAsciiLetter(codePoint))
            {
                // Escape as \xHH (lowercase hex)
                sb.Append('\\');
                sb.Append('x');
                sb.Append(ToHexDigit((codePoint >> 4) & 0xF));
                sb.Append(ToHexDigit(codePoint & 0xF));
            }
            else
            {
                // b. Set escaped to the string-concatenation of escaped and EncodeForRegExpEscape(c).
                EncodeForRegExpEscape(ref sb, codePoint);
            }

            isFirst = false;
        }

        return new JsString(sb.ToString());
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-encodeforregexescape
    /// </summary>
    private static void EncodeForRegExpEscape(ref ValueStringBuilder sb, int codePoint)
    {
        // 1. If c is matched by SyntaxCharacter or c is U+002F (SOLIDUS), then
        //    a. Return the string-concatenation of 0x005C (REVERSE SOLIDUS) and UTF16EncodeCodePoint(c).
        if (IsSyntaxCharacterOrSolidus(codePoint))
        {
            sb.Append('\\');
            sb.Append((char) codePoint);
            return;
        }

        // 2. If c is the code point listed in some cell of the "Code Point" column of Table 64, then
        //    a. Return the string-concatenation of 0x005C (REVERSE SOLIDUS) and the ControlEscape.
        switch (codePoint)
        {
            case 0x09: // TAB
                sb.Append('\\');
                sb.Append('t');
                return;
            case 0x0A: // LF
                sb.Append('\\');
                sb.Append('n');
                return;
            case 0x0B: // VT
                sb.Append('\\');
                sb.Append('v');
                return;
            case 0x0C: // FF
                sb.Append('\\');
                sb.Append('f');
                return;
            case 0x0D: // CR
                sb.Append('\\');
                sb.Append('r');
                return;
        }

        // 3-5. If c is in otherPunctuators, WhiteSpace, LineTerminator, or surrogate, escape it
        if (ShouldEscape(codePoint))
        {
            if (codePoint <= 0xFF)
            {
                // \xHH format (2-digit lowercase hex)
                sb.Append('\\');
                sb.Append('x');
                sb.Append(ToHexDigit((codePoint >> 4) & 0xF));
                sb.Append(ToHexDigit(codePoint & 0xF));
            }
            else if (codePoint <= 0xFFFF)
            {
                // \uHHHH format (4-digit lowercase hex)
                sb.Append('\\');
                sb.Append('u');
                sb.Append(ToHexDigit((codePoint >> 12) & 0xF));
                sb.Append(ToHexDigit((codePoint >> 8) & 0xF));
                sb.Append(ToHexDigit((codePoint >> 4) & 0xF));
                sb.Append(ToHexDigit(codePoint & 0xF));
            }
            else
            {
                // Supplementary code point: encode as two \uHHHH escapes
                var highSurrogate = (char) (0xD800 + ((codePoint - 0x10000) >> 10));
                var lowSurrogate = (char) (0xDC00 + ((codePoint - 0x10000) & 0x3FF));

                sb.Append('\\');
                sb.Append('u');
                sb.Append(ToHexDigit((highSurrogate >> 12) & 0xF));
                sb.Append(ToHexDigit((highSurrogate >> 8) & 0xF));
                sb.Append(ToHexDigit((highSurrogate >> 4) & 0xF));
                sb.Append(ToHexDigit(highSurrogate & 0xF));

                sb.Append('\\');
                sb.Append('u');
                sb.Append(ToHexDigit((lowSurrogate >> 12) & 0xF));
                sb.Append(ToHexDigit((lowSurrogate >> 8) & 0xF));
                sb.Append(ToHexDigit((lowSurrogate >> 4) & 0xF));
                sb.Append(ToHexDigit(lowSurrogate & 0xF));
            }
            return;
        }

        // 6. Return UTF16EncodeCodePoint(c).
        if (codePoint <= 0xFFFF)
        {
            sb.Append((char) codePoint);
        }
        else
        {
            // Supplementary code point: encode as surrogate pair
            sb.Append((char) (0xD800 + ((codePoint - 0x10000) >> 10)));
            sb.Append((char) (0xDC00 + ((codePoint - 0x10000) & 0x3FF)));
        }
    }

    private static bool IsDecimalDigitOrAsciiLetter(int c)
    {
        return (c >= '0' && c <= '9') || (c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z');
    }

    private static bool IsSyntaxCharacterOrSolidus(int c)
    {
        // SyntaxCharacter: . * + ? ^ $ | ( ) [ ] { } \
        // Plus SOLIDUS: /
        return c switch
        {
            '.' or '*' or '+' or '?' or '^' or '$' or '|' or '(' or ')' or '[' or ']' or '{' or '}' or '\\' or '/' => true,
            _ => false
        };
    }

    private static bool ShouldEscape(int c)
    {
        // OtherPunctuators: ,-=<>#&!%:;@~'`"
        if (IsOtherPunctuator(c))
        {
            return true;
        }

        // WhiteSpace (excluding TAB, VT, FF which are handled as control escapes)
        if (IsWhiteSpace(c))
        {
            return true;
        }

        // LineTerminator (excluding LF, CR which are handled as control escapes)
        if (IsLineTerminator(c))
        {
            return true;
        }

        // Leading surrogate (0xD800-0xDBFF) or trailing surrogate (0xDC00-0xDFFF)
        if (c >= 0xD800 && c <= 0xDFFF)
        {
            return true;
        }

        return false;
    }

    private static bool IsOtherPunctuator(int c)
    {
        // ,-=<>#&!%:;@~'`"
        return c switch
        {
            ',' or '-' or '=' or '<' or '>' or '#' or '&' or '!' or '%' or ':' or ';' or '@' or '~' or '\'' or '`' or '"' => true,
            _ => false
        };
    }

    private static bool IsWhiteSpace(int c)
    {
        // WhiteSpace includes TAB, VT, FF, ZWNBSP, SPACE, NO-BREAK SPACE, and other USP
        // TAB (0x09), VT (0x0B), FF (0x0C) are handled separately as control escapes
        return c switch
        {
            0x0009 or 0x000B or 0x000C => true, // Already handled but include for completeness
            0x0020 => true, // SPACE
            0x00A0 => true, // NO-BREAK SPACE
            0xFEFF => true, // ZWNBSP
            0x1680 => true, // OGHAM SPACE MARK
            0x2000 or 0x2001 or 0x2002 or 0x2003 or 0x2004 or 0x2005 or 0x2006 or 0x2007 or 0x2008 or 0x2009 or 0x200A => true, // Various spaces
            0x202F => true, // NARROW NO-BREAK SPACE
            0x205F => true, // MEDIUM MATHEMATICAL SPACE
            0x3000 => true, // IDEOGRAPHIC SPACE
            _ => false
        };
    }

    private static bool IsLineTerminator(int c)
    {
        // LineTerminator: LF, CR, LS, PS
        // LF (0x0A) and CR (0x0D) are handled separately as control escapes
        return c switch
        {
            0x000A or 0x000D => true, // Already handled but include for completeness
            0x2028 => true, // LINE SEPARATOR
            0x2029 => true, // PARAGRAPH SEPARATOR
            _ => false
        };
    }

    private static char ToHexDigit(int value)
    {
        return (char) (value < 10 ? '0' + value : 'a' + (value - 10));
    }

    protected internal override JsValue Call(JsValue thisObject, JsCallArguments arguments)
    {
        return Construct(arguments, thisObject);
    }

    public ObjectInstance Construct(JsCallArguments arguments)
    {
        return Construct(arguments, this);
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-regexp-pattern-flags
    /// </summary>
    public override ObjectInstance Construct(JsCallArguments arguments, JsValue newTarget)
    {
        var pattern = arguments.At(0);
        var flags = arguments.At(1);

        var patternIsRegExp = pattern.IsRegExp();
        if (newTarget.IsUndefined())
        {
            newTarget = this;
            if (patternIsRegExp && flags.IsUndefined())
            {
                var patternConstructor = pattern.Get(CommonProperties.Constructor);
                if (ReferenceEquals(newTarget, patternConstructor))
                {
                    return (ObjectInstance) pattern;
                }
            }
        }

        JsValue p;
        JsValue f;
        if (pattern is JsRegExp regExpInstance)
        {
            p = regExpInstance.Source;
            f = flags.IsUndefined() ? regExpInstance.Flags : flags;
        }
        else if (patternIsRegExp)
        {
            p = pattern.Get(RegExpPrototype.PropertySource);
            f = flags.IsUndefined() ? pattern.Get(RegExpPrototype.PropertyFlags) : flags;
        }
        else
        {
            p = pattern;
            f = flags;
        }

        var r = RegExpAlloc(newTarget);
        return RegExpInitialize(r, p, f);
    }

    internal JsRegExp RegExpInitialize(JsRegExp r, JsValue pattern, JsValue flags, bool throwOnLastIndex = false)
    {
        // This method is called when a RegExp object is created at run-time, e.g.,
        // `new RegExp("abc", "i")`, `RegExp("abc", "i")`, `/x/.compile("abc", "i")`, `"x".match("abc")`, etc.

        var p = pattern.IsUndefined() ? "" : TypeConverter.ToString(pattern);
        if (string.IsNullOrEmpty(p))
        {
            p = JsRegExp.regExpForMatchingAllCharacters;
        }

        var f = flags.IsUndefined() ? "" : TypeConverter.ToString(flags);

        // Validate flags before attempting compilation - invalid flags should always be a SyntaxError

        if (!Tokenizer.ValidateRegExp(p, f, out var error, Engine.BaseParserOptions.EcmaVersion, Engine.BaseParserOptions.ExperimentalESFeatures))
        {
            Throw.SyntaxError(_realm, error!.Description);
        }

        RegExpParseResult regExpParseResult = default;

        var parserOptions = _engine.GetActiveParserOptions();

        if (!NeedCustomEngine(p, f))
        {
#pragma warning disable CS0618 // ParserOptions.RegexTimeout is obsolete but is the supported timeout source.
            regExpParseResult = RegExpParseCache.GetOrAdapt(p, f, compiled: false, parserOptions.RegexTimeout);
#pragma warning restore CS0618

            if (regExpParseResult.Success)
            {
                r.Value = regExpParseResult.Regex!;
                r.ParseResult = regExpParseResult;
            }
        }

        if (!regExpParseResult.Success)
        {
#pragma warning disable CS0618 // Type or member is obsolete
            var customEngine = TryCompileWithCustomEngine(_realm, p, f, parserOptions.RegexTimeout);
#pragma warning restore CS0618 // Type or member is obsolete

            regExpParseResult = RegExpParseResult.ForSuccess(customEngine);

            // Set Value to a dummy regex to avoid null reference in code paths that read it
            // but won't actually use it (since UsesDotNetEngine will be false)
            r.Value = DummyRegex;
            r.ParseResult = regExpParseResult;
        }

        // The pattern may contain characters that are not valid in a regexp literal ('/' and new line characters),
        // so such cases needs to be escaped.
        r.Source = EscapeRegExpSource(p);
        r.Flags = f;

        if (throwOnLastIndex)
        {
            // B.2.5.1: Use Set(O, "lastIndex", +0𝔽, true) which throws TypeError if non-writable.
            // Source/flags are already updated at this point per spec.
            r.Set(JsRegExp.PropertyLastIndex, 0, true);
        }
        else
        {
            RegExpInitialize(r);
        }

        return r;
    }

    /// <summary>
    /// Validate regex flags per ECMAScript spec: only "dgimsuvy" allowed, no duplicates,
    /// u and v are mutually exclusive.
    /// </summary>
    private void ValidateFlags(string flags)
    {
        var seen = 0;
        foreach (var c in flags)
        {
            var bit = c switch
            {
                'd' => 1,
                'g' => 2,
                'i' => 4,
                'm' => 8,
                's' => 16,
                'u' => 32,
                'v' => 64,
                'y' => 128,
                _ => -1,
            };

            if (bit < 0)
            {
                Throw.SyntaxError(_realm, $"Invalid regular expression flags: {flags}");
            }

            if ((seen & bit) != 0)
            {
                Throw.SyntaxError(_realm, $"Invalid regular expression flags: {flags}");
            }

            seen |= bit;
        }

        // u and v are mutually exclusive
        if ((seen & 32) != 0 && (seen & 64) != 0)
        {
            Throw.SyntaxError(_realm, $"Invalid regular expression flags: {flags}");
        }
    }

    /// <summary>
    /// Detect patterns where .NET Regex produces semantically incorrect results
    /// compared to ECMAScript specification. These patterns need the custom engine.
    /// </summary>
    internal static bool NeedCustomEngine(string pattern, string flags)
    {
        // Unicode modes: Although Acornima implements a best-effort conversion for
        // flag u patterns, it's not possible to produce standards-compliant results
        // in most of the cases. Conversion of flag v patterns is not supported at all.
        if (flags.Contains('u') || flags.Contains('v'))
        {
            return true;
        }

        // Case-insensitive with non-ASCII characters: .NET IgnoreCase applies broader
        // Unicode case folding than ES spec (e.g. U+212A KELVIN SIGN matches 'K',
        // U+017F LONG S matches 's'). In non-unicode mode, ES spec Canonicalize uses
        // toUpperCase only, not Unicode case folding. This applies to both literal
        // non-ASCII characters and \uXXXX escape sequences for non-ASCII code points.
        if (flags.Contains('i') && HasNonAsciiContent(pattern))
        {
            return true;
        }

        // Check for further non-compliant cases by scanning the pattern.
        //
        // The main hazard is repeated groups. ECMAScript clears the captures made inside a
        // quantified group at the start of every iteration (RepeatMatcher, step 4) whereas
        // .NET retains captures from earlier iterations, and .NET records an empty capture
        // when an iteration of a quantified capturing group matches the empty string whereas
        // ECMAScript rejects empty iterations. A quantified group is therefore only safe for
        // .NET when its body contains no capturing groups or lookaround assertions and, if
        // the group itself is capturing, when its body cannot match the empty string.

        const int nonCapturingGroup = 0;
        const int capturingGroup = 1;
        const int lookaheadAssertion = 2;
        const int lookbehindAssertion = 3;

        List<int>? capturingGroups = null;
        Dictionary<string, int>? namedGroupNumbers = null;
        var groupStack = new RefStack<GroupScanState>();

        for (int i = 0; i < pattern.Length; i++)
        {
            char ch = pattern[i], next;

            if (ch == '\\' && i + 1 < pattern.Length)
            {
                next = pattern[i + 1];
                if (next >= '1' && next <= '9')
                {
                    int refNum = 0;
                    int j = i + 1;
                    while (j < pattern.Length && pattern[j] >= '0' && pattern[j] <= '9')
                    {
                        refNum = refNum * 10 + (pattern[j] - '0');
                        j++;
                    }

                    if (capturingGroups is null || refNum > capturingGroups.Count || capturingGroups![refNum - 1] < 0)
                    {
                        // Forward or self backreference
                        return true;
                    }

                    foreach (var item in groupStack)
                    {
                        if (item.Type == lookbehindAssertion)
                        {
                            // Backreference in lookbehind
                            return true;
                        }
                    }

                    i = j - 1; // skip past parsed digits
                    // A backreference may match the empty string, so it doesn't affect nullability.
                    TryConsumeQuantifier(pattern, ref i, out _);
                    continue;
                }

                if (next == 'k' && i + 2 < pattern.Length && pattern[i + 2] == '<')
                {
                    int nameStart = i + 3;
                    int nameEnd = pattern.IndexOf('>', nameStart);
                    if (nameEnd > nameStart)
                    {
                        var name = pattern.Substring(nameStart, nameEnd - nameStart);
                        if (namedGroupNumbers is null || !namedGroupNumbers.TryGetValue(name, out var number) || capturingGroups![number - 1] < 0)
                        {
                            // Forward or self named backreference
                            return true;
                        }
                    }
                    else
                    {
                        nameEnd = nameStart + 1;
                    }

                    foreach (var item in groupStack)
                    {
                        if (item.Type == lookbehindAssertion)
                        {
                            // Named backreference in lookbehind
                            return true;
                        }
                    }

                    i = nameEnd;
                    // A backreference may match the empty string, so it doesn't affect nullability.
                    TryConsumeQuantifier(pattern, ref i, out _);
                    continue;
                }

                if (next is 'b' or 'B')
                {
                    // Zero-width assertion, doesn't affect nullability.
                    i++;
                    TryConsumeQuantifier(pattern, ref i, out _);
                    continue;
                }

                // A consuming escape atom; skip the whole escape sequence so that a quantifier
                // following it is attributed to the atom (e.g. in \u0041* the quantifier repeats U+0041).
                i++;
                switch (next)
                {
                    case 'x':
                        if (i + 2 < pattern.Length && HexDigitValue(pattern[i + 1]) >= 0 && HexDigitValue(pattern[i + 2]) >= 0)
                        {
                            i += 2;
                        }
                        break;
                    case 'u':
                        if (i + 4 < pattern.Length
                            && HexDigitValue(pattern[i + 1]) >= 0 && HexDigitValue(pattern[i + 2]) >= 0
                            && HexDigitValue(pattern[i + 3]) >= 0 && HexDigitValue(pattern[i + 4]) >= 0)
                        {
                            i += 4;
                        }
                        break;
                    case 'c':
                        if (i + 1 < pattern.Length && pattern[i + 1] is (>= 'a' and <= 'z') or (>= 'A' and <= 'Z'))
                        {
                            i++;
                        }
                        break;
                    case '0':
                        // Annex B legacy octal escape allows up to two more octal digits.
                        for (int digits = 0; digits < 2 && i + 1 < pattern.Length && pattern[i + 1] is >= '0' and <= '7'; digits++)
                        {
                            i++;
                        }
                        break;
                }

                HandleConsumingAtom(pattern, ref i, groupStack);
                continue;
            }

            if (ch == '[')
            {
                // A character class is a single consuming atom; skip to its unescaped ']'.
                int j = i + 1;
                while (j < pattern.Length)
                {
                    if (pattern[j] == '\\')
                    {
                        j++;
                    }
                    else if (pattern[j] == ']')
                    {
                        break;
                    }
                    j++;
                }

                i = j;
                HandleConsumingAtom(pattern, ref i, groupStack);
                continue;
            }

            if (ch == '(')
            {
                int groupNumber = 0;
                int groupType = nonCapturingGroup;

                if (i + 1 >= pattern.Length || pattern[i + 1] != '?')
                {
                    (capturingGroups ??= new()).Add(-1);
                    groupNumber = capturingGroups.Count;
                    groupType = capturingGroup;
                }
                else if (i + 2 < pattern.Length)
                {
                    next = pattern[i + 2];
                    if (next == '<')
                    {
                        int nameStart = i + 3;
                        if (nameStart < pattern.Length)
                        {
                            int nameEnd;
                            if (pattern[nameStart] is '=' or '!')
                            {
                                groupType = lookbehindAssertion;
                                i += 3; // skip "?<=" / "?<!"
                            }
                            else if ((nameEnd = pattern.IndexOf('>', nameStart)) > nameStart)
                            {
                                // Found (?<name>
                                (capturingGroups ??= new()).Add(-1);
                                groupNumber = capturingGroups.Count;
                                groupType = capturingGroup;

                                var name = pattern.Substring(nameStart, nameEnd - nameStart);
                                namedGroupNumbers ??= new(StringComparer.Ordinal);

#if NETSTANDARD2_1_OR_GREATER || NETCOREAPP2_0_OR_GREATER
                                if (!namedGroupNumbers.TryAdd(name, groupNumber))
                                {
                                    // Duplicate named group
                                    return true;
                                }
#else
                                if (namedGroupNumbers.ContainsKey(name))
                                {
                                    return true;
                                }
                                namedGroupNumbers.Add(name, groupNumber);
#endif

                                i = nameEnd;
                            }
                        }
                    }
                    else if (next is '=' or '!')
                    {
                        groupType = lookaheadAssertion;
                        i += 2; // skip "?=" / "?!"
                    }
                    else if (next == ':')
                    {
                        i += 2; // skip "?:"
                    }
                    else if (next is 'i' or 'm' or 's' or '-')
                    {
                        // Inline modifier group (?ims-ims: ... )
                        int j = i + 2;
                        do
                        {
                            if (pattern[j] == 'i')
                            {
                                // Case insensitive modifier
                                return true;
                            }
                            j++;
                        }
                        while (j < pattern.Length && pattern[j] is 'i' or 'm' or 's' or '-');

                        if (j < pattern.Length && pattern[j] == ':')
                        {
                            // Acornima adapts m/s modifier groups compliantly, skip the header.
                            i = j;
                        }
                    }
                }

                groupStack.Push(new GroupScanState { Number = groupNumber, Type = groupType, CurrentBranchNullable = true });
                continue;
            }

            if (ch == ')')
            {
                if (groupStack._size == 0)
                {
                    continue;
                }

                var group = groupStack.Pop();
                var groupNullable = group.AnyBranchNullable || group.CurrentBranchNullable;
                var hasQuantifier = TryConsumeQuantifier(pattern, ref i, out var minCanBeZero);

                if (hasQuantifier)
                {
                    if (group.Type is lookaheadAssertion or lookbehindAssertion)
                    {
                        // Quantified lookahead/lookbehind
                        return true;
                    }

                    if ((group.BodyFlags & (GroupBodyFlags.ContainsCapture | GroupBodyFlags.ContainsLookaround)) != GroupBodyFlags.None)
                    {
                        // Repeated group containing capturing groups or lookaround assertions:
                        // .NET retains captures from earlier iterations, ECMAScript resets them.
                        return true;
                    }

                    if (group.Type == capturingGroup && groupNullable)
                    {
                        // Repeated capturing group whose body may match the empty string: .NET records
                        // an empty capture for an empty iteration, ECMAScript rejects empty iterations.
                        return true;
                    }
                }

                if (group.Number > 0)
                {
                    capturingGroups![group.Number - 1] = i;
                }

                if (groupStack._size > 0)
                {
                    ref var parent = ref groupStack._array[groupStack._size - 1];
                    parent.BodyFlags |= group.BodyFlags;

                    if (group.Type is lookaheadAssertion or lookbehindAssertion)
                    {
                        // Lookarounds are zero-width, so they don't affect the parent's nullability.
                        parent.BodyFlags |= GroupBodyFlags.ContainsLookaround;
                    }
                    else
                    {
                        if (group.Type == capturingGroup)
                        {
                            parent.BodyFlags |= GroupBodyFlags.ContainsCapture;
                        }

                        if (!groupNullable && !(hasQuantifier && minCanBeZero))
                        {
                            parent.CurrentBranchNullable = false;
                        }
                    }
                }

                continue;
            }

            if (ch == '|')
            {
                if (groupStack._size > 0)
                {
                    ref var top = ref groupStack._array[groupStack._size - 1];
                    top.AnyBranchNullable |= top.CurrentBranchNullable;
                    top.CurrentBranchNullable = true;
                }
                continue;
            }

            if (ch is '^' or '$')
            {
                // Zero-width assertion, doesn't affect nullability.
                TryConsumeQuantifier(pattern, ref i, out _);
                continue;
            }

            // Any other character is a consuming atom (a literal, '.', a lone '{', ...).
            HandleConsumingAtom(pattern, ref i, groupStack);
        }

        return false;
    }

    /// <summary>
    /// Consumes an optional quantifier following a consuming atom and updates the nullability
    /// tracking of the innermost open group: the current alternative can no longer match the
    /// empty string unless the atom was made optional by a min-zero quantifier.
    /// </summary>
    private static void HandleConsumingAtom(string pattern, ref int i, RefStack<GroupScanState> groupStack)
    {
        var optional = TryConsumeQuantifier(pattern, ref i, out var minCanBeZero) && minCanBeZero;
        if (!optional && groupStack._size > 0)
        {
            groupStack._array[groupStack._size - 1].CurrentBranchNullable = false;
        }
    }

    /// <summary>
    /// Tries to consume a quantifier (*, +, ?, {n}, {n,}, {n,m}, each with an optional lazy '?'
    /// suffix) following position <paramref name="i"/>, advancing <paramref name="i"/> past it
    /// on success. A brace that doesn't form a well-formed interval quantifier is left in place
    /// (Annex B treats it as a literal).
    /// </summary>
    private static bool TryConsumeQuantifier(string pattern, ref int i, out bool minCanBeZero)
    {
        minCanBeZero = false;

        var j = i + 1;
        if ((uint) j >= (uint) pattern.Length)
        {
            return false;
        }

        switch (pattern[j])
        {
            case '*':
            case '?':
                minCanBeZero = true;
                break;

            case '+':
                break;

            case '{':
                var k = j + 1;
                var sawDigit = false;
                var minIsZero = true;
                while (k < pattern.Length && pattern[k] is >= '0' and <= '9')
                {
                    if (pattern[k] != '0')
                    {
                        minIsZero = false;
                    }
                    sawDigit = true;
                    k++;
                }

                if (!sawDigit)
                {
                    return false;
                }

                if (k < pattern.Length && pattern[k] == ',')
                {
                    k++;
                    while (k < pattern.Length && pattern[k] is >= '0' and <= '9')
                    {
                        k++;
                    }
                }

                if (k >= pattern.Length || pattern[k] != '}')
                {
                    return false;
                }

                minCanBeZero = minIsZero;
                j = k;
                break;

            default:
                return false;
        }

        if (j + 1 < pattern.Length && pattern[j + 1] == '?')
        {
            // Lazy quantifier suffix
            j++;
        }

        i = j;
        return true;
    }

    [Flags]
    private enum GroupBodyFlags : byte
    {
        None = 0,
        /// <summary>The group's body contains a capturing group (at any depth).</summary>
        ContainsCapture = 1,
        /// <summary>The group's body contains a lookaround assertion (at any depth).</summary>
        ContainsLookaround = 2,
    }

    /// <summary>Scan state for one open group in <see cref="NeedCustomEngine"/>.</summary>
    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
    private struct GroupScanState
    {
        /// <summary>1-based capturing group number, 0 for non-capturing groups and assertions.</summary>
        public int Number;
        /// <summary>One of the group type constants declared in <see cref="NeedCustomEngine"/>.</summary>
        public int Type;
        /// <summary>Hazards seen anywhere in the group's body so far.</summary>
        public GroupBodyFlags BodyFlags;
        /// <summary>Whether the current alternative may still match the empty string.</summary>
        public bool CurrentBranchNullable;
        /// <summary>Whether a completed alternative may match the empty string.</summary>
        public bool AnyBranchNullable;
    }

    /// <summary>
    /// Detect non-ASCII content in the pattern: either literal non-ASCII characters or
    /// \uXXXX / \u{XXXX} hex escapes for non-ASCII code points (&gt;0x7F).
    /// .NET IgnoreCase applies broader Unicode case folding than ES spec for these characters.
    /// ASCII-range escapes like \u0041 work correctly in .NET and don't need the custom engine.
    /// </summary>
    private static bool HasNonAsciiContent(string pattern)
    {
        for (int i = 0; i < pattern.Length; i++)
        {
            char ch = pattern[i];

            if (ch == '\\' && i + 1 < pattern.Length)
            {
                if (pattern[i + 1] == 'u' && i + 2 < pattern.Length)
                {
                    if (pattern[i + 2] == '{')
                    {
                        // \u{XXXX} syntax — parse hex value
                        int value = 0;
                        int j = i + 3;
                        while (j < pattern.Length && pattern[j] != '}')
                        {
                            int digit = HexDigitValue(pattern[j]);
                            if (digit < 0) break;
                            value = (value << 4) | digit;
                            j++;
                        }

                        if (j < pattern.Length && pattern[j] == '}' && value > 0x7F)
                        {
                            return true;
                        }

                        i = j; // skip past parsed escape
                    }
                    else if (i + 5 < pattern.Length)
                    {
                        // \uXXXX syntax — parse 4 hex digits
                        int d0 = HexDigitValue(pattern[i + 2]);
                        int d1 = HexDigitValue(pattern[i + 3]);
                        int d2 = HexDigitValue(pattern[i + 4]);
                        int d3 = HexDigitValue(pattern[i + 5]);
                        if (d0 >= 0 && d1 >= 0 && d2 >= 0 && d3 >= 0)
                        {
                            int value = (d0 << 12) | (d1 << 8) | (d2 << 4) | d3;
                            if (value > 0x7F)
                            {
                                return true;
                            }
                        }

                        i += 5; // skip past \uXXXX
                    }
                    else
                    {
                        i++; // skip escaped char
                    }
                }
                else
                {
                    i++; // skip escaped char
                }
            }
            else if (ch > 0x7F)
            {
                // Literal non-ASCII character in the pattern
                return true;
            }
        }

        return false;
    }

    private static int HexDigitValue(char c)
    {
        if (c >= '0' && c <= '9') return c - '0';
        if (c >= 'a' && c <= 'f') return c - 'a' + 10;
        if (c >= 'A' && c <= 'F') return c - 'A' + 10;
        return -1;
    }

    private static string EscapeRegExpSource(string source)
    {
        StringBuilder? sb = null;
        var inClass = false;
        var chunkStart = 0;
        for (var i = 0; i < source.Length; i++)
        {
            var ch = source[i];
            switch (ch)
            {
                case '[':
                    inClass = true;
                    break;
                case ']' when inClass:
                    inClass = false;
                    break;
                case '\\':
                    if (++i >= source.Length)
                    {
                        break;
                    }
                    ch = source[i];
                    // Backslash is omitted when a new line character follows.
                    if (ch is '\n' or '\r' or '\u2028' or '\u2029')
                    {
                        (sb ??= new()).Append(source, chunkStart, (i - 1) - chunkStart);
                        goto AppendEscapedNewLine;
                    }
                    break;
                case '/' when !inClass:
                    (sb ??= new()).Append(source, chunkStart, i - chunkStart);
                    sb.Append('\\').Append(ch);
                    chunkStart = i + 1;
                    break;
                case '\n':
                case '\r':
                case '\u2028':
                case '\u2029':
                    (sb ??= new()).Append(source, chunkStart, i - chunkStart);
AppendEscapedNewLine:
                    sb.Append(ch switch
                    {
                        '\n' => @"\n",
                        '\r' => @"\r",
                        '\u2028' => @"\u2028",
                        _ => @"\u2029"
                    });
                    chunkStart = i + 1;
                    break;
            }
        }

        if (sb is not null)
        {
            sb.Append(source, chunkStart, source.Length - chunkStart);
            source = sb.ToString();
        }

        return source;
    }

    /// <summary>
    /// Attempt to compile the regex pattern using the custom Jint regex engine.
    /// </summary>
    internal static JintRegExpEngine TryCompileWithCustomEngine(Realm realm, string pattern, string flags, TimeSpan regexTimeout)
    {
        try
        {
            var regExpFlags = RegExpFlags.None;
            foreach (var c in flags)
            {
                regExpFlags |= c switch
                {
                    'g' => RegExpFlags.Global,
                    'i' => RegExpFlags.IgnoreCase,
                    'm' => RegExpFlags.Multiline,
                    's' => RegExpFlags.DotAll,
                    'u' => RegExpFlags.Unicode,
                    'y' => RegExpFlags.Sticky,
                    'd' => RegExpFlags.Indices,
                    'v' => RegExpFlags.UnicodeSets,
                    _ => RegExpFlags.None,
                };
            }

            using var cts = regexTimeout.TotalMilliseconds > 0 && regexTimeout != Timeout.InfiniteTimeSpan
                ? new CancellationTokenSource(regexTimeout)
                : null;

            return JintRegExpEngine.Compile(pattern, regExpFlags, cts?.Token ?? default);
        }
        catch (OperationCanceledException)
        {
            throw new RegexMatchTimeoutException(string.Empty, pattern, regexTimeout);
        }
        catch (RegExpSyntaxException ex)
        {
            Throw.SyntaxError(realm, ex.Message);
            throw;
        }
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-regexpcreate
    /// RegExpCreate(P, F) - creates a new RegExp without calling IsRegExp on the pattern.
    /// Used by String.prototype.match/search/split per spec.
    /// </summary>
    internal JsRegExp RegExpCreate(JsValue pattern, JsValue flags)
    {
        var r = RegExpAlloc(this);
        return RegExpInitialize(r, pattern, flags);
    }

    private JsRegExp RegExpAlloc(JsValue newTarget)
    {
        if (ReferenceEquals(newTarget, this))
        {
            return new JsRegExp(_engine)
            {
                _prototype = PrototypeObject
            };
        }

        var r = OrdinaryCreateFromConstructor(
            newTarget,
            static intrinsics => intrinsics.RegExp.PrototypeObject,
            static (Engine engine, Realm _, object? _) => new JsRegExp(engine));
        return r;
    }

    internal JsRegExp Construct(RegExpParseResult parseResult, string source, string flags)
    {
        // This method is called to create a JsRegExp object from a regexp literal, e.g., `/abc/i`.

        var r = RegExpAlloc(this);
        r.Value = parseResult.Regex ?? DummyRegex;

        // Source is already a valid, properly escaped JS regexp pattern.
        r.Source = source;
        r.Flags = flags;

        r.ParseResult = parseResult;
        RegExpInitialize(r);
        return r;
    }

    public JsRegExp Construct(Regex regExp, string source = "?[native regex]", string flags = "")
    {
        // This method is called to create a JsRegExp object from a .NET Regex directly.

        var r = RegExpAlloc(this);
        r.Value = regExp;

        // We shouldn't return the .NET pattern as if it were a JS pattern since that would be
        // incorrect and misleading. We could try to convert the .NET pattern to a JS one,
        // but that would be a huge (if not impossible) task. So let's use an invalid pattern
        // giving a hint about the situation.
        r.Source = source;
        r.Flags = flags;

        RegExpInitialize(r);

        return r;
    }

    private static void RegExpInitialize(JsRegExp r)
    {
        r.SetOwnProperty(JsRegExp.PropertyLastIndex, new PropertyDescriptor(0, PropertyFlag.OnlyWritable));
    }
}
