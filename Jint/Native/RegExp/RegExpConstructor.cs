using System.Diagnostics.CodeAnalysis;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using Jint.Collections;
using Jint.Extensions;
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

    // RegExp legacy static properties ($1-$9, lastMatch, lastParen, leftContext, rightContext, input).
    // These are NOT Annex B — they come from the TC39 "RegExp Legacy Features" proposal
    // (https://github.com/tc39/proposal-regexp-legacy-features); ECMA-262's B.2.4 is the
    // Date.prototype additions.
    internal string _legacyInput = "";
    internal string _legacyLastParen = "";
    internal readonly string[] _legacyParens = new string[9];
    // Last match and left/right context are computed lazily from the match position, so those three
    // cost nothing beyond the subject reference and two offsets stored per successful exec().
    // Materializing them eagerly cost a Substring per exec() — for a match that spans most of a large
    // subject that is a copy of the whole subject, paid whether or not anything ever reads them.
    // The fields above are not lazy: $1-$9 and lastParen are copied out of the Match on every
    // successful exec() (see RegExpPrototype.UpdateLegacyStaticProperties).
    private string _legacyContextInput = "";
    private int _legacyMatchIndex;
    private int _legacyMatchEnd;
    internal string _legacyLastMatch => _legacyContextInput.Length > 0 ? _legacyContextInput.Substring(_legacyMatchIndex, _legacyMatchEnd - _legacyMatchIndex) : "";
    internal string _legacyLeftContext => _legacyContextInput.Length > 0 ? _legacyContextInput.Substring(0, _legacyMatchIndex) : "";
    internal string _legacyRightContext => _legacyContextInput.Length > 0 ? _legacyContextInput.Substring(_legacyMatchEnd) : "";

    internal void SetLegacyContext(string input, int matchIndex, int matchLength)
    {
        _legacyContextInput = input;
        _legacyMatchIndex = matchIndex;
        _legacyMatchEnd = matchIndex + matchLength;
    }

    /// <summary>
    /// Returns the legacy statics above to their initial (empty) state. Every successful match writes them,
    /// so without this a later evaluation reading <c>RegExp.$1</c> or <c>RegExp.input</c> would see the
    /// previous evaluation's subject text.
    /// </summary>
    internal void ResetLegacyProperties()
    {
        _legacyInput = "";
        _legacyLastParen = "";
        var parens = _legacyParens;
        for (var i = 0; i < parens.Length; i++)
        {
            parens[i] = "";
        }

        _legacyContextInput = "";
        _legacyMatchIndex = 0;
        _legacyMatchEnd = 0;
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

    // Legacy static accessors (RegExp Legacy Features proposal). Kept hand-rolled rather than
    // [JsAccessor] because the accessor function names are non-identifier strings like
    // "$&" / "$_" / "$1" / "$`" — when the source generator names the underlying function "get $&",
    // `Function.prototype.toString` returns text that fails test262's native-function-syntax matcher.
    // Building the accessors here with empty-named ClrFunctions preserves the pre-source-gen behaviour.
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

        // $1-$9 — readable, [[Set]]: undefined per the RegExp Legacy Features proposal
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
    /// https://tc39.es/ecma262/#sec-encodeforregexpescape
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

    // TAB, VT, FF, LF and CR are already spelled as control escapes above and reach neither of these; the
    // rest of both productions comes from the one definition in Character, so a supplementary code point
    // — which is in neither — is out of range for it rather than missing from a list.
    private static bool IsWhiteSpace(int c) => c <= 0xFFFF && ((char) c).IsJsWhiteSpaceExceptLineTerminator();

    private static bool IsLineTerminator(int c) => c <= 0xFFFF && ((char) c).IsJsLineTerminator();

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

        // A previously host-supplied Regex is re-initialized from a JavaScript pattern here (compile),
        // so it stops being a host regex and gets a compiled matcher below.
        r.HostRegex = null;

        var p = pattern.IsUndefined() ? "" : TypeConverter.ToString(pattern);
        if (string.IsNullOrEmpty(p))
        {
            p = JsRegExp.regExpForMatchingAllCharacters;
        }

        var f = flags.IsUndefined() ? "" : TypeConverter.ToString(flags);

        // The match timeout comes off the one conversion-options instance Jint binds its OnRegExp handler to,
        // which resolved an explicit ScriptParsingOptions/ModuleParsingOptions RegexTimeout against
        // Options.Constraints.RegexTimeout when the active script or module was parsed (sebastienros/jint#3431).
        // A null instance - parser options that did not come from Jint's handler, or a preparation, which has no
        // engine to resolve a timeout against (sebastienros/jint#3442) - leaves it on the engine's constraint,
        // which RegExpPrototype.GetMatchTimeout reads at match time.
        var conversionOptions = _engine.GetActiveParserOptions().OnRegExp?.Target as Engine.RegexConversionOptions;

        // Compiling is also what validates: the flags and the pattern's early errors are both the matcher's
        // to report, so a pattern is parsed once rather than once by Acornima and again here.
        r.Matcher = RegExpParseCache.GetOrCompile(_realm, p, f);
        r.ConversionOptions = conversionOptions;

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
    /// Compiles a JavaScript pattern, throwing the <c>SyntaxError</c> the specification requires for invalid
    /// flags or an early error in the pattern.
    /// https://tc39.es/ecma262/#sec-regexpinitialize
    /// </summary>
    internal static JintRegExpEngine Compile(Realm realm, string pattern, string flags)
    {
        var regExpFlags = ParseFlags(flags);
        if (regExpFlags is not null)
        {
            try
            {
                return JintRegExpEngine.Compile(pattern, regExpFlags.Value);
            }
            catch (RegExpSyntaxException ex)
            {
                ThrowSyntaxError(realm, pattern, flags, ex.Message);
            }
        }

        ThrowSyntaxError(realm, pattern, flags, $"Invalid regular expression flags '{flags}'");
        return null!;
    }

    [DoesNotReturn]
    private static void ThrowSyntaxError(Realm realm, string pattern, string flags, string fallbackMessage)
    {
        // The matcher's diagnostics are structured codes rather than prose, so the message comes from
        // Acornima's validator, which reads the same grammar; it is only consulted on this failure path.
        var message = !Tokenizer.ValidateRegExp(pattern, flags, out var error, Engine.BaseParserOptions.EcmaVersion, Engine.BaseParserOptions.ExperimentalESFeatures)
            ? error!.Description
            : fallbackMessage;
        Throw.SyntaxError(realm, message);
    }

    /// <summary>
    /// Parses a flags string per https://tc39.es/ecma262/#sec-regexpinitialize step 5: only <c>dgimsuvy</c>,
    /// none repeated, and not both <c>u</c> and <c>v</c>. Returns null when the string is invalid.
    /// </summary>
    internal static RegExpFlags? ParseFlags(string flags)
    {
        var result = RegExpFlags.None;
        foreach (var c in flags)
        {
            var flag = c switch
            {
                'd' => RegExpFlags.Indices,
                'g' => RegExpFlags.Global,
                'i' => RegExpFlags.IgnoreCase,
                'm' => RegExpFlags.Multiline,
                's' => RegExpFlags.DotAll,
                'u' => RegExpFlags.Unicode,
                'v' => RegExpFlags.UnicodeSets,
                'y' => RegExpFlags.Sticky,
                _ => RegExpFlags.None,
            };

            if (flag == RegExpFlags.None || (result & flag) != RegExpFlags.None)
            {
                return null;
            }

            result |= flag;
        }

        const RegExpFlags UnicodeModes = RegExpFlags.Unicode | RegExpFlags.UnicodeSets;
        return (result & UnicodeModes) == UnicodeModes ? null : result;
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

    internal JsRegExp Construct(JintRegExpEngine matcher, Engine.RegexConversionOptions? conversionOptions, string source, string flags)
    {
        // This method is called to create a JsRegExp object from a regexp literal, e.g., `/abc/i`.

        var r = RegExpAlloc(this);
        r.Matcher = matcher;
        r.ConversionOptions = conversionOptions;

        // Source is already a valid, properly escaped JS regexp pattern.
        r.Source = source;
        r.Flags = flags;

        RegExpInitialize(r);
        return r;
    }

    /// <summary>
    /// Creates a <c>RegExp</c> from a host-supplied .NET <see cref="Regex"/>, as
    /// <see cref="Options.InteropOptions.RegexConversion"/> directs.
    /// </summary>
    public JsRegExp Construct(Regex regExp, string source = "?[native regex]", string flags = "")
    {
        if (_engine.Options.Interop.RegexConversion == RegexConversionMode.EcmaScript)
        {
            return ConstructFromHostRegexAsEcmaScript(regExp);
        }

        var r = RegExpAlloc(this);

        // The host's own .NET pattern keeps .NET semantics: the prototype lanes route a host regex to the
        // .NET engine and read its capture-group metadata off the Regex.
        r.HostRegex = regExp;

        // We shouldn't return the .NET pattern as if it were a JS pattern since that would be
        // incorrect and misleading, so the source is a deliberately invalid pattern giving a hint about the
        // situation. RegexConversionMode.EcmaScript is the opt-in that reads the pattern as ECMAScript instead.
        r.Source = source;
        r.Flags = flags;

        RegExpInitialize(r);

        return r;
    }

    private JsRegExp ConstructFromHostRegexAsEcmaScript(Regex regExp)
    {
        const RegexOptions Translatable = RegexOptions.IgnoreCase | RegexOptions.Multiline | RegexOptions.Singleline
                                          | RegexOptions.Compiled | RegexOptions.CultureInvariant;
        var options = regExp.Options;
        if ((options & ~Translatable) != RegexOptions.None)
        {
            Throw.SyntaxError(_realm, $"The .NET regex option(s) '{options & ~Translatable}' have no ECMAScript equivalent");
        }

        var flags = string.Concat(
            (options & RegexOptions.IgnoreCase) != RegexOptions.None ? "i" : "",
            (options & RegexOptions.Multiline) != RegexOptions.None ? "m" : "",
            (options & RegexOptions.Singleline) != RegexOptions.None ? "s" : "");

        var pattern = regExp.ToString();
        var r = RegExpAlloc(this);
        r.Matcher = RegExpParseCache.GetOrCompile(_realm, pattern, flags);
        r.Source = EscapeRegExpSource(pattern.Length == 0 ? JsRegExp.regExpForMatchingAllCharacters : pattern);
        r.Flags = flags;
        RegExpInitialize(r);
        return r;
    }

    private static void RegExpInitialize(JsRegExp r)
    {
        r.SetOwnProperty(JsRegExp.PropertyLastIndex, new PropertyDescriptor(0, PropertyFlag.OnlyWritable));
    }
}
