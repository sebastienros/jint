using System.Text;
using System.Text.RegularExpressions;
using Jint.Native.Function;
using Jint.Native.Object;
using Jint.Native.Symbol;
using Jint.Runtime;
using Jint.Runtime.Descriptors;
using Jint.Runtime.Interop;
using Jint.Runtime.RegExp;

namespace Jint.Native.RegExp;

public sealed class RegExpConstructor : Constructor
{
    private static readonly JsString _functionName = new JsString("RegExp");
    private static readonly Regex DummyRegex = new("(?:)", RegexOptions.None, TimeSpan.FromSeconds(1));

    // B.2.4 RegExp Legacy Static Properties
    internal string _legacyInput = "";
    internal string _legacyLastMatch = "";
    internal string _legacyLastParen = "";
    internal string _legacyLeftContext = "";
    internal string _legacyRightContext = "";
    internal readonly string[] _legacyParens = new string[9];

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
        const PropertyFlag LengthFlags = PropertyFlag.Configurable;
        const PropertyFlag PropertyFlags = PropertyFlag.Configurable | PropertyFlag.Writable;

        // B.2.4 Legacy static accessor helpers
        JsValue LegacyGetter(Func<RegExpConstructor, string> getter, JsValue thisObj, JsCallArguments _)
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
                get: new ClrFunction(Engine, "", (thisObj, args) => LegacyGetter(getter, thisObj, args), 0, LengthFlags),
                set: Undefined,
                flags: PropertyFlag.Configurable);
        }

        // input/$_ - readable and writable
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

        // $1-$9 - readable, [[Set]]: undefined per B.2.4
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

        var properties = new PropertyDictionary(20, checkExistingKeys: false)
        {
            ["escape"] = new PropertyDescriptor(new ClrFunction(Engine, "escape", Escape, 1, LengthFlags), PropertyFlags),
            // B.2.4 Legacy static accessors
            ["input"] = inputAccessor,
            ["$_"] = inputAccessor,
            ["lastMatch"] = CreateLegacyReadOnlyAccessor(static c => c._legacyLastMatch),
            ["$&"] = CreateLegacyReadOnlyAccessor(static c => c._legacyLastMatch),
            ["lastParen"] = CreateLegacyReadOnlyAccessor(static c => c._legacyLastParen),
            ["$+"] = CreateLegacyReadOnlyAccessor(static c => c._legacyLastParen),
            ["leftContext"] = CreateLegacyReadOnlyAccessor(static c => c._legacyLeftContext),
            ["$`"] = CreateLegacyReadOnlyAccessor(static c => c._legacyLeftContext),
            ["rightContext"] = CreateLegacyReadOnlyAccessor(static c => c._legacyRightContext),
            ["$'"] = CreateLegacyReadOnlyAccessor(static c => c._legacyRightContext),
            ["$1"] = CreateParenAccessor(0),
            ["$2"] = CreateParenAccessor(1),
            ["$3"] = CreateParenAccessor(2),
            ["$4"] = CreateParenAccessor(3),
            ["$5"] = CreateParenAccessor(4),
            ["$6"] = CreateParenAccessor(5),
            ["$7"] = CreateParenAccessor(6),
            ["$8"] = CreateParenAccessor(7),
            ["$9"] = CreateParenAccessor(8),
        };
        SetProperties(properties);

        var symbols = new SymbolDictionary(1)
        {
            [GlobalSymbolRegistry.Species] = new GetSetPropertyDescriptor(get: new ClrFunction(_engine, "get [Symbol.species]", (thisObj, _) => thisObj, 0, PropertyFlag.Configurable), set: Undefined, PropertyFlag.Configurable)
        };
        SetSymbols(symbols);
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-regexp.escape
    /// </summary>
    private JsString Escape(JsValue thisObject, JsCallArguments arguments)
    {
        var s = arguments.At(0);

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
        var p = pattern.IsUndefined() ? "" : TypeConverter.ToString(pattern);
        if (string.IsNullOrEmpty(p))
        {
            p = "(?:)";
        }

        var f = flags.IsUndefined() ? "" : TypeConverter.ToString(flags);

        // Validate flags before attempting compilation - invalid flags should always be a SyntaxError
        ValidateFlags(f);

        var parserOptions = _engine.GetActiveParserOptions();
        try
        {
            var regExpParseResult = Tokenizer.AdaptRegExp(p, f, compiled: false, parserOptions.RegexTimeout,
                ecmaVersion: parserOptions.EcmaVersion,
                experimentalESFeatures: parserOptions.ExperimentalESFeatures);

            if (regExpParseResult.Success)
            {
                r.Value = regExpParseResult.Regex!;
                r.ParseResult = regExpParseResult;
                r.CustomEngine = null;
            }
            else
            {
                // Acornima could not convert the pattern to .NET Regex.
                // Try the custom engine (handles v-flag, unicode properties, forward backrefs, etc.)
                TryCompileWithCustomEngine(r, p, f);
            }
        }
        catch (Exception ex) when (ex is not Jint.Runtime.JavaScriptException)
        {
            // Acornima threw during parsing/conversion - try custom engine
            TryCompileWithCustomEngine(r, p, f);
        }

        r.Flags = f;
        r.Source = p;

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
    /// Validate regex flags per ECMAScript spec: only "dgimsuy" allowed, no duplicates,
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
    /// Attempt to compile the regex pattern using the custom Jint regex engine.
    /// Falls back to SyntaxError if the custom engine also fails.
    /// </summary>
    private void TryCompileWithCustomEngine(JsRegExp r, string pattern, string flags)
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

            r.CustomEngine = JintRegExpEngine.Compile(pattern, regExpFlags);
            // Set Value to a dummy regex to avoid null reference in code paths that read it
            // but won't actually use it (since UsesDotNetEngine will be false)
            r.Value = DummyRegex;
            r.ParseResult = default;
        }
        catch (RegExpSyntaxException ex)
        {
            Throw.SyntaxError(_realm, ex.Message);
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

    public JsRegExp Construct(Regex regExp, string source, string flags, RegExpParseResult regExpParseResult = default)
    {
        var r = RegExpAlloc(this);
        r.Value = regExp;
        r.Source = source;
        r.Flags = flags;
        r.ParseResult = regExpParseResult;

        RegExpInitialize(r);

        return r;
    }

    private static void RegExpInitialize(JsRegExp r)
    {
        r.SetOwnProperty(JsRegExp.PropertyLastIndex, new PropertyDescriptor(0, PropertyFlag.OnlyWritable));
    }
}
