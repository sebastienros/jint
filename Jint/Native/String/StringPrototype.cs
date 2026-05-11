#pragma warning disable CA1859 // Use concrete types when possible for improved performance -- most of prototype methods return JsValue

using System.Globalization;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using Jint.Native.Intl;
using Jint.Native.Json;
using Jint.Native.Object;
using Jint.Native.RegExp;
using Jint.Native.Symbol;
using Jint.Runtime;
using Jint.Runtime.Descriptors;
using Jint.Runtime.Descriptors.Specialized;
using Jint.Runtime.Interop;

namespace Jint.Native.String;

/// <summary>
/// https://tc39.es/ecma262/#sec-properties-of-the-string-prototype-object
/// </summary>
[JsObject(ExtraCapacity = 2)]
internal sealed partial class StringPrototype : StringInstance
{
    private readonly Realm _realm;

    [JsProperty(Name = "constructor", Flags = PropertyFlag.NonEnumerable)]
    private readonly StringConstructor _constructor;

    internal ClrFunction? _originalIteratorFunction;

    internal StringPrototype(
        Engine engine,
        Realm realm,
        StringConstructor constructor,
        ObjectPrototype objectPrototype)
        : base(engine, JsString.Empty)
    {
        _prototype = objectPrototype;
        _length = PropertyDescriptor.AllForbiddenDescriptor.NumberZero;
        _realm = realm;
        _constructor = constructor;
    }

    protected override void Initialize()
    {
        const PropertyFlag lengthFlags = PropertyFlag.Configurable;
        const PropertyFlag propertyFlags = lengthFlags | PropertyFlag.Writable;

        CreateProperties_Generated();

        // B.2.3: trimLeft/trimRight are aliases for trimStart/trimEnd. Aliasing the same descriptor
        // instance shares the same lazy-resolved Function reference. AddDangerous skips
        // duplicate-key probing and SetOwnProperty's validation pipeline; ExtraCapacity=2 on
        // [JsObject] presizes the dict so these adds don't trigger a resize.
        _properties!.TryGetValue("trimStart", out var trimStartDescriptor);
        _properties.TryGetValue("trimEnd", out var trimEndDescriptor);
        _properties.AddDangerous("trimLeft", trimStartDescriptor);
        _properties.AddDangerous("trimRight", trimEndDescriptor);

        // [Symbol.iterator] kept hand-written: needs to capture _originalIteratorFunction for
        // the HasOriginalIterator fast-path detection used by string iteration consumers.
        _originalIteratorFunction = new ClrFunction(_engine, "[Symbol.iterator]", Iterator, 0, lengthFlags);
        var symbols = new SymbolDictionary(1)
        {
            [GlobalSymbolRegistry.Iterator] = new PropertyDescriptor(_originalIteratorFunction, propertyFlags)
        };
        SetSymbols(symbols);
    }

    internal override bool HasOriginalIterator => ReferenceEquals(Get(GlobalSymbolRegistry.Iterator), _originalIteratorFunction);

    private ObjectInstance Iterator(JsValue thisObject, JsCallArguments arguments)
    {
        TypeConverter.RequireObjectCoercible(_engine, thisObject);
        var str = TypeConverter.ToString(thisObject);
        return _realm.Intrinsics.StringIteratorPrototype.Construct(str);
    }

    [JsFunction(Name = "toString")]
    private JsValue ToStringString(JsValue thisObject)
    {
        if (thisObject.IsString())
        {
            return thisObject;
        }

        var s = TypeConverter.ToObject(_realm, thisObject) as StringInstance;
        if (s is null)
        {
            Throw.TypeError(_realm, "String.prototype.toString requires that 'this' be a String");
        }

        return s.StringData;
    }

    // http://msdn.microsoft.com/en-us/library/system.char.iswhitespace(v=vs.110).aspx
    // http://en.wikipedia.org/wiki/Byte_order_mark
    const char BOM_CHAR = '\uFEFF';

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsWhiteSpaceEx(char c)
    {
        return char.IsWhiteSpace(c) || c == BOM_CHAR;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static string TrimEndEx(string s)
    {
        if (s.Length == 0)
            return string.Empty;

        if (!IsWhiteSpaceEx(s[s.Length - 1]))
            return s;

        return TrimEnd(s);
    }

    private static string TrimEnd(string s)
    {
        var i = s.Length - 1;
        while (i >= 0)
        {
            if (IsWhiteSpaceEx(s[i]))
                i--;
            else
                break;
        }

        return i >= 0 ? s.Substring(0, i + 1) : string.Empty;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static string TrimStartEx(string s)
    {
        if (s.Length == 0)
            return string.Empty;

        if (!IsWhiteSpaceEx(s[0]))
            return s;

        return TrimStart(s);
    }

    private static string TrimStart(string s)
    {
        var i = 0;
        while (i < s.Length)
        {
            if (IsWhiteSpaceEx(s[i]))
                i++;
            else
                break;
        }

        return i >= s.Length ? string.Empty : s.Substring(i);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static string TrimEx(string s)
    {
        return TrimEndEx(TrimStartEx(s));
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-string.prototype.trim
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [JsFunction]
    [RequireObjectCoercible]
    private static JsValue Trim(JsValue thisObject)
    {
        var s = TypeConverter.ToJsString(thisObject);
        if (s.Length == 0 || (!IsWhiteSpaceEx(s[0]) && !IsWhiteSpaceEx(s[s.Length - 1])))
        {
            return s;
        }
        return TrimEx(s.ToString());
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-string.prototype.trimstart
    /// </summary>
    [JsFunction]
    [RequireObjectCoercible]
    private static JsValue TrimStart(JsValue thisObject)
    {
        var s = TypeConverter.ToJsString(thisObject);
        if (s.Length == 0 || !IsWhiteSpaceEx(s[0]))
        {
            return s;
        }
        return TrimStartEx(s.ToString());
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-string.prototype.trimend
    /// </summary>
    [JsFunction]
    [RequireObjectCoercible]
    private static JsValue TrimEnd(JsValue thisObject)
    {
        var s = TypeConverter.ToJsString(thisObject);
        if (s.Length == 0 || !IsWhiteSpaceEx(s[s.Length - 1]))
        {
            return s;
        }
        return TrimEndEx(s.ToString());
    }

    [JsFunction]
    [RequireObjectCoercible]
    private JsValue ToLocaleUpperCase(JsValue thisObject, JsCallArguments arguments)
    {
        var s = TypeConverter.ToString(thisObject);

        // https://tc39.es/ecma402/#sup-string.prototype.tolocaleuppercase
        // 1. Let requestedLocales be ? CanonicalizeLocaleList(locales).
        var requestedLocales = IntlUtilities.CanonicalizeLocaleList(_engine, arguments.At(0));
        var culture = CultureInfo.InvariantCulture;
        if (requestedLocales.Count > 0)
        {
            culture = IntlUtilities.GetCultureInfo(requestedLocales[0]) ?? CultureInfo.InvariantCulture;
        }

        if (string.Equals("lt", culture.Name, StringComparison.OrdinalIgnoreCase))
        {
            s = StringInlHelper.LithuanianStringProcessor(s);
#if NET462
            // Code specific to .NET Framework 4.6.2.
            // For no good reason this verison does not upper case these characters correctly.
            return new JsString(ToUpperCaseWithSpecialCasing(s, culture)
                .Replace("ϳ", "Ϳ")
                .Replace("ʝ", "Ʝ"));
#endif
        }

        return new JsString(ToUpperCaseWithSpecialCasing(s, culture));
    }

    [JsFunction]
    [RequireObjectCoercible]
    private static JsValue ToUpperCase(JsValue thisObject)
    {
        var s = TypeConverter.ToString(thisObject);
        return new JsString(ToUpperCaseWithSpecialCasing(s, CultureInfo.InvariantCulture));
    }

    /// <summary>
    /// Converts string to uppercase with Unicode SpecialCasing.txt unconditional, locale-insensitive
    /// expansions (e.g. ß → SS, ﬀ → FF, Greek titlecase → upper + Ι).
    /// https://www.unicode.org/Public/UCD/latest/ucd/SpecialCasing.txt
    /// </summary>
    private static string ToUpperCaseWithSpecialCasing(string s, CultureInfo culture)
    {
        // Fast path: no codepoint in the string has a SpecialCasing expansion.
        if (!NeedsUpperSpecialCasing(s))
        {
            return s.ToUpper(culture);
        }

        // Stack buffer covers most strings; ValueStringBuilder rents from ArrayPool if it grows beyond.
        Span<char> stackBuffer = stackalloc char[128];
        var sb = new ValueStringBuilder(stackBuffer);
        for (var i = 0; i < s.Length; i++)
        {
            var c = s[i];
            var mapped = GetSpecialUpperCasing(c);
            if (mapped is not null)
            {
                sb.Append(mapped);
            }
            else if (char.IsHighSurrogate(c) && i + 1 < s.Length && char.IsLowSurrogate(s[i + 1]))
            {
                // No supplementary-plane special-cased uppercase mappings — pass through.
                sb.Append(c);
                sb.Append(s[i + 1]);
                i++;
            }
            else
            {
                sb.Append(char.ToUpper(c, culture));
            }
        }

        return sb.ToString();
    }

    private static bool NeedsUpperSpecialCasing(string s)
    {
        foreach (var c in s)
        {
            if (GetSpecialUpperCasing(c) is not null)
            {
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// Returns the SpecialCasing.txt unconditional uppercase expansion for a BMP code point,
    /// or <c>null</c> if no special mapping applies.
    /// </summary>
    private static string? GetSpecialUpperCasing(char c)
    {
        // ASCII has no SpecialCasing.txt expansions; the lowest mapped codepoint is U+00DF.
        if (c < '\u00DF')
        {
            return null;
        }

        return c switch
        {
            '\u00DF' => "\u0053\u0053", // LATIN SMALL LETTER SHARP S
            '\u0149' => "\u02BC\u004E", // LATIN SMALL LETTER N PRECEDED BY APOSTROPHE
            '\u01F0' => "\u004A\u030C", // LATIN SMALL LETTER J WITH CARON
            '\u0390' => "\u0399\u0308\u0301", // GREEK SMALL LETTER IOTA WITH DIALYTIKA AND TONOS
            '\u03B0' => "\u03A5\u0308\u0301", // GREEK SMALL LETTER UPSILON WITH DIALYTIKA AND TONOS
            '\u0587' => "\u0535\u0552", // ARMENIAN SMALL LIGATURE ECH YIWN
            '\u1E96' => "\u0048\u0331", // LATIN SMALL LETTER H WITH LINE BELOW
            '\u1E97' => "\u0054\u0308", // LATIN SMALL LETTER T WITH DIAERESIS
            '\u1E98' => "\u0057\u030A", // LATIN SMALL LETTER W WITH RING ABOVE
            '\u1E99' => "\u0059\u030A", // LATIN SMALL LETTER Y WITH RING ABOVE
            '\u1E9A' => "\u0041\u02BE", // LATIN SMALL LETTER A WITH RIGHT HALF RING
            '\u1F50' => "\u03A5\u0313", // GREEK SMALL LETTER UPSILON WITH PSILI
            '\u1F52' => "\u03A5\u0313\u0300", // GREEK SMALL LETTER UPSILON WITH PSILI AND VARIA
            '\u1F54' => "\u03A5\u0313\u0301", // GREEK SMALL LETTER UPSILON WITH PSILI AND OXIA
            '\u1F56' => "\u03A5\u0313\u0342", // GREEK SMALL LETTER UPSILON WITH PSILI AND PERISPOMENI
            '\u1F80' => "\u1F08\u0399", // GREEK SMALL LETTER ALPHA WITH PSILI AND YPOGEGRAMMENI
            '\u1F81' => "\u1F09\u0399", // GREEK SMALL LETTER ALPHA WITH DASIA AND YPOGEGRAMMENI
            '\u1F82' => "\u1F0A\u0399", // GREEK SMALL LETTER ALPHA WITH PSILI AND VARIA AND YPOGEGRAMMENI
            '\u1F83' => "\u1F0B\u0399", // GREEK SMALL LETTER ALPHA WITH DASIA AND VARIA AND YPOGEGRAMMENI
            '\u1F84' => "\u1F0C\u0399", // GREEK SMALL LETTER ALPHA WITH PSILI AND OXIA AND YPOGEGRAMMENI
            '\u1F85' => "\u1F0D\u0399", // GREEK SMALL LETTER ALPHA WITH DASIA AND OXIA AND YPOGEGRAMMENI
            '\u1F86' => "\u1F0E\u0399", // GREEK SMALL LETTER ALPHA WITH PSILI AND PERISPOMENI AND YPOGEGRAMMENI
            '\u1F87' => "\u1F0F\u0399", // GREEK SMALL LETTER ALPHA WITH DASIA AND PERISPOMENI AND YPOGEGRAMMENI
            '\u1F88' => "\u1F08\u0399", // GREEK CAPITAL LETTER ALPHA WITH PSILI AND PROSGEGRAMMENI
            '\u1F89' => "\u1F09\u0399", // GREEK CAPITAL LETTER ALPHA WITH DASIA AND PROSGEGRAMMENI
            '\u1F8A' => "\u1F0A\u0399", // GREEK CAPITAL LETTER ALPHA WITH PSILI AND VARIA AND PROSGEGRAMMENI
            '\u1F8B' => "\u1F0B\u0399", // GREEK CAPITAL LETTER ALPHA WITH DASIA AND VARIA AND PROSGEGRAMMENI
            '\u1F8C' => "\u1F0C\u0399", // GREEK CAPITAL LETTER ALPHA WITH PSILI AND OXIA AND PROSGEGRAMMENI
            '\u1F8D' => "\u1F0D\u0399", // GREEK CAPITAL LETTER ALPHA WITH DASIA AND OXIA AND PROSGEGRAMMENI
            '\u1F8E' => "\u1F0E\u0399", // GREEK CAPITAL LETTER ALPHA WITH PSILI AND PERISPOMENI AND PROSGEGRAMMENI
            '\u1F8F' => "\u1F0F\u0399", // GREEK CAPITAL LETTER ALPHA WITH DASIA AND PERISPOMENI AND PROSGEGRAMMENI
            '\u1F90' => "\u1F28\u0399", // GREEK SMALL LETTER ETA WITH PSILI AND YPOGEGRAMMENI
            '\u1F91' => "\u1F29\u0399", // GREEK SMALL LETTER ETA WITH DASIA AND YPOGEGRAMMENI
            '\u1F92' => "\u1F2A\u0399", // GREEK SMALL LETTER ETA WITH PSILI AND VARIA AND YPOGEGRAMMENI
            '\u1F93' => "\u1F2B\u0399", // GREEK SMALL LETTER ETA WITH DASIA AND VARIA AND YPOGEGRAMMENI
            '\u1F94' => "\u1F2C\u0399", // GREEK SMALL LETTER ETA WITH PSILI AND OXIA AND YPOGEGRAMMENI
            '\u1F95' => "\u1F2D\u0399", // GREEK SMALL LETTER ETA WITH DASIA AND OXIA AND YPOGEGRAMMENI
            '\u1F96' => "\u1F2E\u0399", // GREEK SMALL LETTER ETA WITH PSILI AND PERISPOMENI AND YPOGEGRAMMENI
            '\u1F97' => "\u1F2F\u0399", // GREEK SMALL LETTER ETA WITH DASIA AND PERISPOMENI AND YPOGEGRAMMENI
            '\u1F98' => "\u1F28\u0399", // GREEK CAPITAL LETTER ETA WITH PSILI AND PROSGEGRAMMENI
            '\u1F99' => "\u1F29\u0399", // GREEK CAPITAL LETTER ETA WITH DASIA AND PROSGEGRAMMENI
            '\u1F9A' => "\u1F2A\u0399", // GREEK CAPITAL LETTER ETA WITH PSILI AND VARIA AND PROSGEGRAMMENI
            '\u1F9B' => "\u1F2B\u0399", // GREEK CAPITAL LETTER ETA WITH DASIA AND VARIA AND PROSGEGRAMMENI
            '\u1F9C' => "\u1F2C\u0399", // GREEK CAPITAL LETTER ETA WITH PSILI AND OXIA AND PROSGEGRAMMENI
            '\u1F9D' => "\u1F2D\u0399", // GREEK CAPITAL LETTER ETA WITH DASIA AND OXIA AND PROSGEGRAMMENI
            '\u1F9E' => "\u1F2E\u0399", // GREEK CAPITAL LETTER ETA WITH PSILI AND PERISPOMENI AND PROSGEGRAMMENI
            '\u1F9F' => "\u1F2F\u0399", // GREEK CAPITAL LETTER ETA WITH DASIA AND PERISPOMENI AND PROSGEGRAMMENI
            '\u1FA0' => "\u1F68\u0399", // GREEK SMALL LETTER OMEGA WITH PSILI AND YPOGEGRAMMENI
            '\u1FA1' => "\u1F69\u0399", // GREEK SMALL LETTER OMEGA WITH DASIA AND YPOGEGRAMMENI
            '\u1FA2' => "\u1F6A\u0399", // GREEK SMALL LETTER OMEGA WITH PSILI AND VARIA AND YPOGEGRAMMENI
            '\u1FA3' => "\u1F6B\u0399", // GREEK SMALL LETTER OMEGA WITH DASIA AND VARIA AND YPOGEGRAMMENI
            '\u1FA4' => "\u1F6C\u0399", // GREEK SMALL LETTER OMEGA WITH PSILI AND OXIA AND YPOGEGRAMMENI
            '\u1FA5' => "\u1F6D\u0399", // GREEK SMALL LETTER OMEGA WITH DASIA AND OXIA AND YPOGEGRAMMENI
            '\u1FA6' => "\u1F6E\u0399", // GREEK SMALL LETTER OMEGA WITH PSILI AND PERISPOMENI AND YPOGEGRAMMENI
            '\u1FA7' => "\u1F6F\u0399", // GREEK SMALL LETTER OMEGA WITH DASIA AND PERISPOMENI AND YPOGEGRAMMENI
            '\u1FA8' => "\u1F68\u0399", // GREEK CAPITAL LETTER OMEGA WITH PSILI AND PROSGEGRAMMENI
            '\u1FA9' => "\u1F69\u0399", // GREEK CAPITAL LETTER OMEGA WITH DASIA AND PROSGEGRAMMENI
            '\u1FAA' => "\u1F6A\u0399", // GREEK CAPITAL LETTER OMEGA WITH PSILI AND VARIA AND PROSGEGRAMMENI
            '\u1FAB' => "\u1F6B\u0399", // GREEK CAPITAL LETTER OMEGA WITH DASIA AND VARIA AND PROSGEGRAMMENI
            '\u1FAC' => "\u1F6C\u0399", // GREEK CAPITAL LETTER OMEGA WITH PSILI AND OXIA AND PROSGEGRAMMENI
            '\u1FAD' => "\u1F6D\u0399", // GREEK CAPITAL LETTER OMEGA WITH DASIA AND OXIA AND PROSGEGRAMMENI
            '\u1FAE' => "\u1F6E\u0399", // GREEK CAPITAL LETTER OMEGA WITH PSILI AND PERISPOMENI AND PROSGEGRAMMENI
            '\u1FAF' => "\u1F6F\u0399", // GREEK CAPITAL LETTER OMEGA WITH DASIA AND PERISPOMENI AND PROSGEGRAMMENI
            '\u1FB2' => "\u1FBA\u0399", // GREEK SMALL LETTER ALPHA WITH VARIA AND YPOGEGRAMMENI
            '\u1FB3' => "\u0391\u0399", // GREEK SMALL LETTER ALPHA WITH YPOGEGRAMMENI
            '\u1FB4' => "\u0386\u0399", // GREEK SMALL LETTER ALPHA WITH OXIA AND YPOGEGRAMMENI
            '\u1FB6' => "\u0391\u0342", // GREEK SMALL LETTER ALPHA WITH PERISPOMENI
            '\u1FB7' => "\u0391\u0342\u0399", // GREEK SMALL LETTER ALPHA WITH PERISPOMENI AND YPOGEGRAMMENI
            '\u1FBC' => "\u0391\u0399", // GREEK CAPITAL LETTER ALPHA WITH PROSGEGRAMMENI
            '\u1FC2' => "\u1FCA\u0399", // GREEK SMALL LETTER ETA WITH VARIA AND YPOGEGRAMMENI
            '\u1FC3' => "\u0397\u0399", // GREEK SMALL LETTER ETA WITH YPOGEGRAMMENI
            '\u1FC4' => "\u0389\u0399", // GREEK SMALL LETTER ETA WITH OXIA AND YPOGEGRAMMENI
            '\u1FC6' => "\u0397\u0342", // GREEK SMALL LETTER ETA WITH PERISPOMENI
            '\u1FC7' => "\u0397\u0342\u0399", // GREEK SMALL LETTER ETA WITH PERISPOMENI AND YPOGEGRAMMENI
            '\u1FCC' => "\u0397\u0399", // GREEK CAPITAL LETTER ETA WITH PROSGEGRAMMENI
            '\u1FD2' => "\u0399\u0308\u0300", // GREEK SMALL LETTER IOTA WITH DIALYTIKA AND VARIA
            '\u1FD3' => "\u0399\u0308\u0301", // GREEK SMALL LETTER IOTA WITH DIALYTIKA AND OXIA
            '\u1FD6' => "\u0399\u0342", // GREEK SMALL LETTER IOTA WITH PERISPOMENI
            '\u1FD7' => "\u0399\u0308\u0342", // GREEK SMALL LETTER IOTA WITH DIALYTIKA AND PERISPOMENI
            '\u1FE2' => "\u03A5\u0308\u0300", // GREEK SMALL LETTER UPSILON WITH DIALYTIKA AND VARIA
            '\u1FE3' => "\u03A5\u0308\u0301", // GREEK SMALL LETTER UPSILON WITH DIALYTIKA AND OXIA
            '\u1FE4' => "\u03A1\u0313", // GREEK SMALL LETTER RHO WITH PSILI
            '\u1FE6' => "\u03A5\u0342", // GREEK SMALL LETTER UPSILON WITH PERISPOMENI
            '\u1FE7' => "\u03A5\u0308\u0342", // GREEK SMALL LETTER UPSILON WITH DIALYTIKA AND PERISPOMENI
            '\u1FF2' => "\u1FFA\u0399", // GREEK SMALL LETTER OMEGA WITH VARIA AND YPOGEGRAMMENI
            '\u1FF3' => "\u03A9\u0399", // GREEK SMALL LETTER OMEGA WITH YPOGEGRAMMENI
            '\u1FF4' => "\u038F\u0399", // GREEK SMALL LETTER OMEGA WITH OXIA AND YPOGEGRAMMENI
            '\u1FF6' => "\u03A9\u0342", // GREEK SMALL LETTER OMEGA WITH PERISPOMENI
            '\u1FF7' => "\u03A9\u0342\u0399", // GREEK SMALL LETTER OMEGA WITH PERISPOMENI AND YPOGEGRAMMENI
            '\u1FFC' => "\u03A9\u0399", // GREEK CAPITAL LETTER OMEGA WITH PROSGEGRAMMENI
            '\uFB00' => "\u0046\u0046", // LATIN SMALL LIGATURE FF
            '\uFB01' => "\u0046\u0049", // LATIN SMALL LIGATURE FI
            '\uFB02' => "\u0046\u004C", // LATIN SMALL LIGATURE FL
            '\uFB03' => "\u0046\u0046\u0049", // LATIN SMALL LIGATURE FFI
            '\uFB04' => "\u0046\u0046\u004C", // LATIN SMALL LIGATURE FFL
            '\uFB05' => "\u0053\u0054", // LATIN SMALL LIGATURE LONG S T
            '\uFB06' => "\u0053\u0054", // LATIN SMALL LIGATURE ST
            '\uFB13' => "\u0544\u0546", // ARMENIAN SMALL LIGATURE MEN NOW
            '\uFB14' => "\u0544\u0535", // ARMENIAN SMALL LIGATURE MEN ECH
            '\uFB15' => "\u0544\u053B", // ARMENIAN SMALL LIGATURE MEN INI
            '\uFB16' => "\u054E\u0546", // ARMENIAN SMALL LIGATURE VEW NOW
            '\uFB17' => "\u0544\u053D", // ARMENIAN SMALL LIGATURE MEN XEH
            _ => null
        };
    }

    [JsFunction]
    [RequireObjectCoercible]
    private JsValue ToLocaleLowerCase(JsValue thisObject, JsCallArguments arguments)
    {
        var s = TypeConverter.ToString(thisObject);

        // https://tc39.es/ecma402/#sup-string.prototype.tolocalelowercase
        // 1. Let requestedLocales be ? CanonicalizeLocaleList(locales).
        var requestedLocales = IntlUtilities.CanonicalizeLocaleList(_engine, arguments.At(0));
        var culture = CultureInfo.InvariantCulture;
        if (requestedLocales.Count > 0)
        {
            culture = IntlUtilities.GetCultureInfo(requestedLocales[0]) ?? CultureInfo.InvariantCulture;
        }

        return ToLowerCaseWithSpecialCasing(s, culture);
    }

    [JsFunction]
    [RequireObjectCoercible]
    private static JsValue ToLowerCase(JsValue thisObject)
    {
        var s = TypeConverter.ToString(thisObject);
        return ToLowerCaseWithSpecialCasing(s, CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Converts string to lowercase with Unicode special casing rules.
    /// Handles Final_Sigma, Turkish/Azeri I-dot, Lithuanian soft-dotted, and İ decomposition.
    /// https://unicode.org/reports/tr21/tr21-5.html#SpecialCasing
    /// </summary>
    private static string ToLowerCaseWithSpecialCasing(string s, CultureInfo culture)
    {
        var langName = culture.TwoLetterISOLanguageName;
        var isTurkishOrAzeri = string.Equals(langName, "tr", StringComparison.Ordinal) ||
                               string.Equals(langName, "az", StringComparison.Ordinal);
        var isLithuanian = string.Equals(langName, "lt", StringComparison.Ordinal);

        // Fast path: if no special characters, use standard lowercase (but not for Turkish/Azeri/Lithuanian)
        if (!isTurkishOrAzeri && !isLithuanian && !NeedsSpecialCasing(s))
        {
            return s.ToLower(culture);
        }

        // Stack buffer covers most strings; ValueStringBuilder rents from ArrayPool if it grows beyond.
        Span<char> stackBuffer = stackalloc char[128];
        var sb = new ValueStringBuilder(stackBuffer);

        for (var i = 0; i < s.Length; i++)
        {
            var c = s[i];

            // Greek Final_Sigma (all locales)
            if (c == '\u03A3')
            {
                sb.Append(IsFinalSigmaContext(s, i) ? '\u03C2' : '\u03C3');
                continue;
            }

            if (isTurkishOrAzeri)
            {
                // Turkish/Azeri: İ (U+0130) → i
                if (c == '\u0130')
                {
                    sb.Append('i');
                    continue;
                }

                // Turkish/Azeri: I + combining dot above (with only cc<230 in between) → i (remove dot above)
                if (c == 'I')
                {
                    if (FollowedByDotAbove(s, i))
                    {
                        sb.Append('i');
                        // Skip intervening cc<230 chars and the dot above
                        i++;
                        while (i < s.Length && s[i] != '\u0307')
                        {
                            sb.Append(char.ToLower(s[i], culture));
                            i++;
                        }
                        // i now points at U+0307, skip it
                        continue;
                    }

                    // Turkish/Azeri: I (not followed by dot above) → ı (dotless i)
                    sb.Append('\u0131');
                    continue;
                }
            }
            else if (isLithuanian)
            {
                // Lithuanian: Ì (U+00CC) → i + ̇ + ̀
                if (c == '\u00CC')
                {
                    sb.Append('i');
                    sb.Append('\u0307');
                    sb.Append('\u0300');
                    continue;
                }

                // Lithuanian: Í (U+00CD) → i + ̇ + ́
                if (c == '\u00CD')
                {
                    sb.Append('i');
                    sb.Append('\u0307');
                    sb.Append('\u0301');
                    continue;
                }

                // Lithuanian: Ĩ (U+0128) → i + ̇ + ̃
                if (c == '\u0128')
                {
                    sb.Append('i');
                    sb.Append('\u0307');
                    sb.Append('\u0303');
                    continue;
                }

                // Lithuanian: I, J, Į followed by combining class 230 mark → add U+0307 after lowercase
                if (c == 'I' || c == 'J' || c == '\u012E')
                {
                    if (FollowedByCombiningClass230(s, i))
                    {
                        sb.Append(char.ToLower(c, culture));
                        sb.Append('\u0307');
                        continue;
                    }
                }
            }
            else
            {
                // Default locale: İ (U+0130) → i + ̇ (U+0069 + U+0307)
                if (c == '\u0130')
                {
                    sb.Append('i');
                    sb.Append('\u0307');
                    continue;
                }
            }

            // Handle surrogate pairs
            if (char.IsHighSurrogate(c) && i + 1 < s.Length && char.IsLowSurrogate(s[i + 1]))
            {
                sb.Append(c);
                sb.Append(s[i + 1]);
                i++;
                continue;
            }

            sb.Append(char.ToLower(c, culture));
        }

        return sb.ToString();
    }

    /// <summary>
    /// Checks if the string needs special casing beyond standard ToLower.
    /// </summary>
    private static bool NeedsSpecialCasing(string s)
    {
        foreach (var c in s)
        {
            if (c == '\u03A3' || c == '\u0130')
            {
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// Checks if character at index i is followed by U+0307 (COMBINING DOT ABOVE)
    /// with only characters of combining class less than 230 in between.
    /// Used for Turkish/Azeri I + dot above handling.
    /// </summary>
    private static bool FollowedByDotAbove(string s, int i)
    {
        for (var j = i + 1; j < s.Length; j++)
        {
            // Handle surrogate pairs
            if (char.IsHighSurrogate(s[j]) && j + 1 < s.Length && char.IsLowSurrogate(s[j + 1]))
            {
                var cp = char.ConvertToUtf32(s[j], s[j + 1]);
                var cc = GetCombiningClass(cp);
                if (cc == 0 || cc >= 230)
                {
                    return false;
                }
                j++;
                continue;
            }

            var ch = s[j];
            if (ch == '\u0307')
            {
                return true;
            }

            var charCc = GetCombiningClass(ch);
            if (charCc == 0 || charCc >= 230)
            {
                return false;
            }
        }
        return false;
    }

    /// <summary>
    /// Checks if character at index i is followed by a combining mark with combining class 230.
    /// Used for Lithuanian soft-dotted character handling.
    /// </summary>
    private static bool FollowedByCombiningClass230(string s, int i)
    {
        for (var j = i + 1; j < s.Length; j++)
        {
            int cp;
            if (char.IsHighSurrogate(s[j]) && j + 1 < s.Length && char.IsLowSurrogate(s[j + 1]))
            {
                cp = char.ConvertToUtf32(s[j], s[j + 1]);
                j++;
            }
            else
            {
                cp = s[j];
            }

            var cc = GetCombiningClass(cp);
            if (cc == 0)
            {
                return false;
            }

            if (cc == 230)
            {
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// Returns the Unicode combining class for a code point.
    /// Covers the Combining Diacritical Marks block and common supplementary combining marks.
    /// </summary>
    private static int GetCombiningClass(int codePoint)
    {
        // Most characters have combining class 0
        if (codePoint < 0x0300)
        {
            return 0;
        }

        // Combining Diacritical Marks (U+0300-U+036F)
        return codePoint switch
        {
            // Class 230 (Above)
            >= 0x0300 and <= 0x0314 => 230,
            0x033D or 0x033E or 0x033F => 230,
            0x0340 or 0x0341 or 0x0342 or 0x0343 or 0x0344 or 0x0346 => 230,
            0x034A or 0x034B or 0x034C => 230,
            0x0350 or 0x0351 or 0x0352 => 230,
            0x0357 => 230,
            0x035B => 230,
            >= 0x0363 and <= 0x036F => 230,

            // Class 232 (Double Below)
            0x035C or 0x035F => 233,

            // Class 1 (Overlay)
            0x0334 or 0x0335 or 0x0336 or 0x0337 or 0x0338 => 1,

            // Class 220 (Below)
            >= 0x0316 and <= 0x0319 => 220,
            >= 0x031C and <= 0x0320 => 220,
            >= 0x0323 and <= 0x0326 => 220,
            >= 0x0329 and <= 0x0333 => 220,
            0x0339 or 0x033A or 0x033B or 0x033C => 220,
            0x0345 => 240, // Iota subscript
            0x0347 or 0x0348 or 0x0349 => 220,
            0x034D or 0x034E => 220,
            0x0353 or 0x0354 or 0x0355 or 0x0356 => 220,
            0x0359 or 0x035A => 220,

            // Class 202 (Attached Below Right)
            0x031A => 232,

            // Class 216 (Attached Above Right)
            0x0315 => 232,

            // Class 226 (Above Right)
            0x0358 => 232,

            // Combining marks outside the main block
            >= 0x0590 and <= 0x05CF => GetHebrewCombiningClass(codePoint),

            // Common supplementary combining marks
            0x101FD => 220, // PHAISTOS DISC SIGN COMBINING OBLIQUE STROKE
            >= 0x1D165 and <= 0x1D169 => 216, // MUSICAL SYMBOL COMBINING STEM etc. (attached)
            >= 0x1D16D and <= 0x1D172 => 216, // MUSICAL SYMBOL COMBINING AUGMENTATION DOT etc.
            >= 0x1D17B and <= 0x1D182 => 220, // MUSICAL SYMBOL COMBINING ACCENT etc.
            >= 0x1D185 and <= 0x1D189 => 230, // MUSICAL SYMBOL COMBINING DOIT etc.
            >= 0x1D18A and <= 0x1D18B => 220, // MUSICAL SYMBOL COMBINING DOWN BOW etc.

            _ => codePoint <= 0xFFFF && CharUnicodeInfo.GetUnicodeCategory((char) codePoint) == UnicodeCategory.NonSpacingMark ? 230 : 0
        };
    }

    private static int GetHebrewCombiningClass(int cp)
    {
        // Simplified: most Hebrew combining marks are below (220) or above (230)
        return cp switch
        {
            >= 0x0591 and <= 0x05AF => 220, // Hebrew accents (various, simplified)
            >= 0x05B0 and <= 0x05BD => 220, // Hebrew points
            0x05BF => 230,
            0x05C1 => 230,
            0x05C2 => 220,
            0x05C4 => 230,
            0x05C5 => 220,
            0x05C7 => 220,
            _ => 0
        };
    }

    /// <summary>
    /// Determines if the character at the given position is in a Final_Sigma context.
    /// Final_Sigma: C is preceded by a sequence consisting of a cased letter and then zero or more Case_Ignorable characters,
    /// and C is NOT followed by a sequence consisting of zero or more Case_Ignorable characters and then a cased letter.
    /// https://unicode.org/reports/tr21/tr21-5.html#Context
    /// </summary>
    private static bool IsFinalSigmaContext(string s, int index)
    {
        // Check backward: must find a cased letter (skipping Case_Ignorable)
        var foundCasedBefore = false;
        var i = index - 1;
        while (i >= 0)
        {
            int cp;
            int step;
            if (char.IsLowSurrogate(s[i]) && i - 1 >= 0 && char.IsHighSurrogate(s[i - 1]))
            {
                cp = char.ConvertToUtf32(s[i - 1], s[i]);
                step = 2;
            }
            else
            {
                cp = s[i];
                step = 1;
            }

            if (IsCased(cp))
            {
                foundCasedBefore = true;
                break;
            }
            if (!IsCaseIgnorable(cp))
            {
                break;
            }
            i -= step;
        }

        if (!foundCasedBefore)
        {
            return false;
        }

        // Check forward: must NOT find a cased letter (skipping Case_Ignorable)
        var j = index + 1;
        while (j < s.Length)
        {
            int cp;
            int step;
            if (char.IsHighSurrogate(s[j]) && j + 1 < s.Length && char.IsLowSurrogate(s[j + 1]))
            {
                cp = char.ConvertToUtf32(s[j], s[j + 1]);
                step = 2;
            }
            else
            {
                cp = s[j];
                step = 1;
            }

            if (IsCased(cp))
            {
                return false; // Found cased letter after, so NOT Final_Sigma
            }
            if (!IsCaseIgnorable(cp))
            {
                break;
            }
            j += step;
        }

        return true;
    }

    /// <summary>
    /// Checks if a character is "cased" (has uppercase or lowercase property).
    /// A character is cased if it has the Lowercase or Uppercase property, or has General_Category=Titlecase_Letter.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsCased(int cp)
    {
        // Cased = Lowercase OR Uppercase OR General_Category=Lt
        if (cp <= 0xFFFF)
        {
            var c = (char) cp;
            return char.IsLetter(c) && (char.IsLower(c) || char.IsUpper(c) || CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.TitlecaseLetter);
        }

#if SUPPORTS_UNICODE_CATEGORY_INT
        var category = CharUnicodeInfo.GetUnicodeCategory(cp);
#else
        // net462 / netstandard2.0 lack the int overload; the 2-char string allocation is unavoidable here.
        var category = CharUnicodeInfo.GetUnicodeCategory(char.ConvertFromUtf32(cp), 0);
#endif
        return category is UnicodeCategory.LowercaseLetter
            or UnicodeCategory.UppercaseLetter
            or UnicodeCategory.TitlecaseLetter;
    }

    /// <summary>
    /// Checks if a character is Case_Ignorable.
    /// Case_Ignorable characters include: Mn (Nonspacing_Mark), Me (Enclosing_Mark), Cf (Format),
    /// Lm (Modifier_Letter), Sk (Modifier_Symbol), and characters with Word_Break property MidLetter, MidNumLet, or Single_Quote.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsCaseIgnorable(int cp)
    {
#if SUPPORTS_UNICODE_CATEGORY_INT
        var category = CharUnicodeInfo.GetUnicodeCategory(cp);
#else
        // net462 / netstandard2.0 lack the int overload; supplementary-plane lookups go through a 2-char string.
        var category = cp <= 0xFFFF
            ? CharUnicodeInfo.GetUnicodeCategory((char) cp)
            : CharUnicodeInfo.GetUnicodeCategory(char.ConvertFromUtf32(cp), 0);
#endif
        return category == UnicodeCategory.NonSpacingMark ||      // Mn
               category == UnicodeCategory.EnclosingMark ||       // Me
               category == UnicodeCategory.Format ||              // Cf (includes U+180E Mongolian Vowel Separator)
               category == UnicodeCategory.ModifierLetter ||      // Lm
               category == UnicodeCategory.ModifierSymbol ||      // Sk
               cp == 0x0027 ||                                    // APOSTROPHE (Word_Break=Single_Quote)
               cp == 0x002E ||                                    // FULL STOP (Word_Break=MidNumLet)
               cp == 0x003A ||                                    // COLON (Word_Break=MidLetter)
               cp == 0x00B7 ||                                    // MIDDLE DOT (Word_Break=MidLetter)
               cp == 0x0387 ||                                    // GREEK ANO TELEIA (Word_Break=MidLetter)
               cp == 0x05F4 ||                                    // HEBREW PUNCTUATION GERSHAYIM (Word_Break=MidLetter)
               cp == 0x2018 ||                                    // LEFT SINGLE QUOTATION MARK (Word_Break=MidNumLet)
               cp == 0x2019 ||                                    // RIGHT SINGLE QUOTATION MARK (Word_Break=Single_Quote)
               cp == 0x2024 ||                                    // ONE DOT LEADER (Word_Break=MidNumLet)
               cp == 0x2027 ||                                    // HYPHENATION POINT (Word_Break=MidLetter)
               cp == 0xFE13 ||                                    // PRESENTATION FORM FOR VERTICAL COLON (Word_Break=MidLetter)
               cp == 0xFE52 ||                                    // SMALL FULL STOP (Word_Break=MidNumLet)
               cp == 0xFE55 ||                                    // SMALL COLON (Word_Break=MidLetter)
               cp == 0xFF07 ||                                    // FULLWIDTH APOSTROPHE (Word_Break=MidNumLet)
               cp == 0xFF0E ||                                    // FULLWIDTH FULL STOP (Word_Break=MidNumLet)
               cp == 0xFF1A;                                      // FULLWIDTH COLON (Word_Break=MidLetter)
    }

    private static int ToIntegerSupportInfinity(JsValue numberVal)
    {
        return numberVal._type == InternalTypes.Integer
            ? numberVal.AsInteger()
            : ToIntegerSupportInfinityUnlikely(numberVal);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static int ToIntegerSupportInfinityUnlikely(JsValue numberVal)
    {
        var doubleVal = TypeConverter.ToInteger(numberVal);
        int intVal;
        if (double.IsPositiveInfinity(doubleVal))
            intVal = int.MaxValue;
        else if (double.IsNegativeInfinity(doubleVal))
            intVal = int.MinValue;
        else
            intVal = (int) doubleVal;
        return intVal;
    }

    [JsFunction(Length = 2)]
    [RequireObjectCoercible]
    private static JsValue Substring(JsValue thisObject, JsCallArguments arguments)
    {
        var s = TypeConverter.ToString(thisObject);
        var start = TypeConverter.ToNumber(arguments.At(0));
        var end = TypeConverter.ToNumber(arguments.At(1));

        if (double.IsNaN(start) || start < 0)
        {
            start = 0;
        }

        if (double.IsNaN(end) || end < 0)
        {
            end = 0;
        }

        var len = s.Length;
        var intStart = ToIntegerSupportInfinity(start);

        var intEnd = arguments.At(1).IsUndefined() ? len : ToIntegerSupportInfinity(end);
        var finalStart = System.Math.Min(len, System.Math.Max(intStart, 0));
        var finalEnd = System.Math.Min(len, System.Math.Max(intEnd, 0));
        // Swap value if finalStart < finalEnd
        var from = System.Math.Min(finalStart, finalEnd);
        var to = System.Math.Max(finalStart, finalEnd);
        var length = to - from;

        if (length == 0)
        {
            return JsString.Empty;
        }

        if (length == 1)
        {
            return JsString.Create(s[from]);
        }

        return new JsString(s.Substring(from, length));
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-string.prototype.substr
    /// </summary>
    [JsFunction(Length = 2)]
    [RequireObjectCoercible]
    private static JsValue Substr(JsValue thisObject, JsCallArguments arguments)
    {
        var s = TypeConverter.ToString(thisObject);
        var start = TypeConverter.ToInteger(arguments.At(0));
        var length = arguments.At(1).IsUndefined()
            ? double.PositiveInfinity
            : TypeConverter.ToInteger(arguments.At(1));

        start = start >= 0 ? start : System.Math.Max(s.Length + start, 0);
        length = System.Math.Min(System.Math.Max(length, 0), s.Length - start);
        if (length <= 0)
        {
            return JsString.Empty;
        }

        var startIndex = TypeConverter.ToInt32(start);
        var l = TypeConverter.ToInt32(length);
        if (l == 1)
        {
            return TypeConverter.ToString(s[startIndex]);
        }
        return s.Substring(startIndex, l);
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-createhtml
    /// B.2.2.1
    /// </summary>
    private static JsValue CreateHTML(Engine engine, JsValue thisObject, string tag, string attribute, JsValue value)
    {
        TypeConverter.RequireObjectCoercible(engine, thisObject);
        var s = TypeConverter.ToString(thisObject);
        var p1 = "<" + tag;
        if (attribute.Length > 0)
        {
            var v = TypeConverter.ToString(value);
            var escapedV = v.Replace("\"", "&quot;");
            p1 += " " + attribute + "=\"" + escapedV + "\"";
        }
        return p1 + ">" + s + "</" + tag + ">";
    }

    [JsFunction(Length = 1)]
    private JsValue Anchor(JsValue thisObject, JsCallArguments arguments)
        => CreateHTML(_engine, thisObject, "a", "name", arguments.At(0));

    [JsFunction]
    private JsValue Big(JsValue thisObject, JsCallArguments arguments)
        => CreateHTML(_engine, thisObject, "big", "", Undefined);

    [JsFunction]
    private JsValue Blink(JsValue thisObject, JsCallArguments arguments)
        => CreateHTML(_engine, thisObject, "blink", "", Undefined);

    [JsFunction]
    private JsValue Bold(JsValue thisObject, JsCallArguments arguments)
        => CreateHTML(_engine, thisObject, "b", "", Undefined);

    [JsFunction]
    private JsValue Fixed(JsValue thisObject, JsCallArguments arguments)
        => CreateHTML(_engine, thisObject, "tt", "", Undefined);

    [JsFunction(Length = 1, Name = "fontcolor")]
    private JsValue FontColor(JsValue thisObject, JsCallArguments arguments)
        => CreateHTML(_engine, thisObject, "font", "color", arguments.At(0));

    [JsFunction(Length = 1, Name = "fontsize")]
    private JsValue FontSize(JsValue thisObject, JsCallArguments arguments)
        => CreateHTML(_engine, thisObject, "font", "size", arguments.At(0));

    [JsFunction]
    private JsValue Italics(JsValue thisObject, JsCallArguments arguments)
        => CreateHTML(_engine, thisObject, "i", "", Undefined);

    [JsFunction(Length = 1)]
    private JsValue Link(JsValue thisObject, JsCallArguments arguments)
        => CreateHTML(_engine, thisObject, "a", "href", arguments.At(0));

    [JsFunction]
    private JsValue Small(JsValue thisObject, JsCallArguments arguments)
        => CreateHTML(_engine, thisObject, "small", "", Undefined);

    [JsFunction]
    private JsValue Strike(JsValue thisObject, JsCallArguments arguments)
        => CreateHTML(_engine, thisObject, "strike", "", Undefined);

    [JsFunction]
    private JsValue Sub(JsValue thisObject, JsCallArguments arguments)
        => CreateHTML(_engine, thisObject, "sub", "", Undefined);

    [JsFunction]
    private JsValue Sup(JsValue thisObject, JsCallArguments arguments)
        => CreateHTML(_engine, thisObject, "sup", "", Undefined);

    /// <summary>
    /// https://tc39.es/ecma262/#sec-string.prototype.split
    /// </summary>
    [JsFunction(Length = 2)]
    [RequireObjectCoercible]
    private JsValue Split(JsValue thisObject, JsCallArguments arguments)
    {
        var separator = arguments.At(0);
        var limit = arguments.At(1);

        // fast path for empty regexp
        if (separator is JsRegExp R && string.Equals(R.Source, JsRegExp.regExpForMatchingAllCharacters, StringComparison.Ordinal))
        {
            separator = JsString.Empty;
        }

        if (separator is ObjectInstance oi)
        {
            var splitter = GetMethod(_realm, oi, GlobalSymbolRegistry.Split);
            if (splitter != null)
            {
                return splitter.Call(separator, thisObject, limit);
            }
        }

        var s = TypeConverter.ToString(thisObject);

        // Coerce into a number, true will become 1
        var lim = limit.IsUndefined() ? uint.MaxValue : TypeConverter.ToUint32(limit);

        // Per spec, if we got here the separator didn't have @@split, so just ToString it.
        // Don't call IsRegExp - it would be an observable extra property access (@@match).
        if (separator.IsNull())
        {
            separator = "null";
        }
        else if (!separator.IsUndefined())
        {
            if (separator is not JsRegExp)
            {
                separator = TypeConverter.ToJsString(separator); // Coerce into a string, for an object call toString()
            }
        }

        if (lim == 0)
        {
            return _realm.Intrinsics.Array.ArrayCreate(0);
        }

        if (separator.IsUndefined())
        {
            var arrayInstance = _realm.Intrinsics.Array.ArrayCreate(1);
            arrayInstance.SetIndexValue(0, s, updateLength: false);
            return arrayInstance;
        }

        return SplitWithStringSeparator(_realm, separator, s, lim);
    }

    internal static JsValue SplitWithStringSeparator(Realm realm, JsValue separator, string s, uint lim)
    {
        var segments = StringExecutionContext.Current.SplitSegmentList;
        segments.Clear();
        var sep = TypeConverter.ToString(separator);

        if (sep == string.Empty)
        {
            if (s.Length > segments.Capacity)
            {
                segments.Capacity = s.Length;
            }

            for (var i = 0; i < s.Length; i++)
            {
                segments.Add(TypeConverter.ToString(s[i]));
            }
        }
        else
        {
            var array = StringExecutionContext.Current.SplitArray1;
            array[0] = sep;
            segments.AddRange(s.Split(array, StringSplitOptions.None));
        }

        var length = (uint) System.Math.Min(segments.Count, lim);
        var a = realm.Intrinsics.Array.ArrayCreate(length);
        for (int i = 0; i < length; i++)
        {
            a.SetIndexValue((uint) i, segments[i], updateLength: false);
        }

        a.SetLength(length);
        return a;
    }

    /// <summary>
    /// https://tc39.es/proposal-relative-indexing-method/#sec-string-prototype-additions
    /// </summary>
    [JsFunction(Length = 1)]
    [RequireObjectCoercible]
    private static JsValue At(JsValue thisObject, JsCallArguments arguments)
    {
        var start = arguments.At(0);

        var o = thisObject.ToString();
        long len = o.Length;

        var relativeIndex = TypeConverter.ToInteger(start);
        int k;

        if (relativeIndex < 0)
        {
            k = (int) (len + relativeIndex);
        }
        else
        {
            k = (int) relativeIndex;
        }

        if (k < 0 || k >= len)
        {
            return Undefined;
        }

        return o[k];
    }

    [JsFunction(Length = 2)]
    [RequireObjectCoercible]
    private static JsValue Slice(JsValue thisObject, JsCallArguments arguments)
    {
        var start = TypeConverter.ToNumber(arguments.At(0));
        if (double.IsNegativeInfinity(start))
        {
            start = 0;
        }
        if (double.IsPositiveInfinity(start))
        {
            return JsString.Empty;
        }

        var s = TypeConverter.ToJsString(thisObject);
        var end = TypeConverter.ToNumber(arguments.At(1));
        if (double.IsPositiveInfinity(end))
        {
            end = s.Length;
        }

        var len = s.Length;
        var intStart = (int) start;
        var intEnd = arguments.At(1).IsUndefined() ? len : (int) TypeConverter.ToInteger(end);
        var from = intStart < 0 ? System.Math.Max(len + intStart, 0) : System.Math.Min(intStart, len);
        var to = intEnd < 0 ? System.Math.Max(len + intEnd, 0) : System.Math.Min(intEnd, len);
        var span = System.Math.Max(to - from, 0);

        if (span == 0)
        {
            return JsString.Empty;
        }

        if (span == 1)
        {
            return JsString.Create(s[from]);
        }

        return s.Substring(from, span);
    }

    [JsFunction(Length = 1)]
    [RequireObjectCoercible]
    private JsValue Search(JsValue thisObject, JsCallArguments arguments)
    {
        var regex = arguments.At(0);

        if (regex is ObjectInstance oi)
        {
            var searcher = GetMethod(_realm, oi, GlobalSymbolRegistry.Search);
            if (searcher != null)
            {
                return searcher.Call(regex, thisObject);
            }
        }

        var rx = _realm.Intrinsics.RegExp.RegExpCreate(regex, JsValue.Undefined);
        var s = TypeConverter.ToJsString(thisObject);
        return _engine.Invoke(rx, GlobalSymbolRegistry.Search, [s]);
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-string.prototype.replace
    /// </summary>
    [JsFunction(Length = 2)]
    [RequireObjectCoercible]
    private JsValue Replace(JsValue thisObject, JsCallArguments arguments)
    {
        var searchValue = arguments.At(0);
        var replaceValue = arguments.At(1);

        // 2. If searchValue is neither undefined nor null, then
        // Note: spec requires checking if searchValue IS an object, not just not-null/undefined
        if (searchValue is ObjectInstance)
        {
            var replacer = GetMethod(_realm, searchValue, GlobalSymbolRegistry.Replace);
            if (replacer != null)
            {
                return replacer.Call(searchValue, thisObject, replaceValue);
            }
        }

        var thisString = TypeConverter.ToJsString(thisObject);
        var searchString = TypeConverter.ToString(searchValue);
        var functionalReplace = replaceValue is ICallable;

        if (!functionalReplace)
        {
            replaceValue = TypeConverter.ToJsString(replaceValue);
        }

        var position = thisString.IndexOf(searchString);
        if (position < 0)
        {
            return thisString;
        }

        string replStr;
        if (functionalReplace)
        {
            var replValue = ((ICallable) replaceValue).Call(Undefined, searchString, position, thisString);
            replStr = TypeConverter.ToString(replValue);
        }
        else
        {
            var captures = System.Array.Empty<string>();
            replStr = RegExpPrototype.GetSubstitution(searchString, thisString.ToString(), position, captures, Undefined, TypeConverter.ToString(replaceValue));
        }

        var tailPos = position + searchString.Length;
        var newString = thisString.Substring(0, position) + replStr + thisString.Substring(tailPos);

        return newString;
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-string.prototype.replaceall
    /// </summary>
    [JsFunction(Length = 2)]
    [RequireObjectCoercible]
    private JsValue ReplaceAll(JsValue thisObject, JsCallArguments arguments)
    {
        var searchValue = arguments.At(0);
        var replaceValue = arguments.At(1);

        // 2. If searchValue is neither undefined nor null, then
        // Note: spec requires checking if searchValue IS an object, not just not-null/undefined
        if (searchValue is ObjectInstance)
        {
            if (searchValue.IsRegExp())
            {
                var flags = searchValue.Get(RegExpPrototype.PropertyFlags);
                TypeConverter.RequireObjectCoercible(_engine, flags);
                if (!TypeConverter.ToString(flags).Contains('g'))
                {
                    Throw.TypeError(_realm, "String.prototype.replaceAll called with a non-global RegExp argument");
                }
            }

            var replacer = GetMethod(_realm, searchValue, GlobalSymbolRegistry.Replace);
            if (replacer != null)
            {
                return replacer.Call(searchValue, thisObject, replaceValue);
            }
        }

        var thisString = TypeConverter.ToString(thisObject);
        var searchString = TypeConverter.ToString(searchValue);

        var functionalReplace = replaceValue is ICallable;

        if (!functionalReplace)
        {
            replaceValue = TypeConverter.ToJsString(replaceValue);

            // check fast case
            var newValue = replaceValue.ToString();
            if (!newValue.Contains('$') && searchString.Length > 0)
            {
                // just plain old string replace
                return thisString.Replace(searchString, newValue);
            }
        }

        // https://tc39.es/ecma262/#sec-stringindexof
        static int StringIndexOf(string s, string search, int fromIndex)
        {
            if (search.Length == 0 && fromIndex <= s.Length)
            {
                return fromIndex;
            }

            return fromIndex < s.Length
                ? s.IndexOf(search, fromIndex, StringComparison.Ordinal)
                : -1;
        }

        var searchLength = searchString.Length;
        var advanceBy = System.Math.Max(1, searchLength);

        var endOfLastMatch = 0;
        using var result = new ValueStringBuilder();

        var position = StringIndexOf(thisString, searchString, 0);
        while (position != -1)
        {
            string replacement;
            var preserved = thisString.Substring(endOfLastMatch, position - endOfLastMatch);
            if (functionalReplace)
            {
                var replValue = ((ICallable) replaceValue).Call(Undefined, searchString, position, thisString);
                replacement = TypeConverter.ToString(replValue);
            }
            else
            {
                var captures = System.Array.Empty<string>();
                replacement = RegExpPrototype.GetSubstitution(searchString, thisString, position, captures, Undefined, TypeConverter.ToString(replaceValue));
            }

            result.Append(preserved);
            result.Append(replacement);

            endOfLastMatch = position + searchLength;

            position = StringIndexOf(thisString, searchString, position + advanceBy);
        }

        if (endOfLastMatch < thisString.Length)
        {
            result.Append(thisString.AsSpan(endOfLastMatch));
        }

        return result.ToString();
    }

    [JsFunction(Length = 1)]
    [RequireObjectCoercible]
    private JsValue Match(JsValue thisObject, JsCallArguments arguments)
    {
        var regex = arguments.At(0);
        if (regex is ObjectInstance oi)
        {
            var matcher = GetMethod(_realm, oi, GlobalSymbolRegistry.Match);
            if (matcher != null)
            {
                return matcher.Call(regex, thisObject);
            }
        }

        var rx = _realm.Intrinsics.RegExp.RegExpCreate(regex, JsValue.Undefined);

        var s = TypeConverter.ToJsString(thisObject);
        return _engine.Invoke(rx, GlobalSymbolRegistry.Match, [s]);
    }

    [JsFunction(Length = 1)]
    [RequireObjectCoercible]
    private JsValue MatchAll(JsValue thisObject, JsCallArguments arguments)
    {
        var regex = arguments.At(0);
        // 2. If regexp is neither undefined nor null, then
        // Note: spec requires checking if regexp IS an object, not just not-null/undefined
        if (regex is ObjectInstance)
        {
            if (regex.IsRegExp())
            {
                var flags = regex.Get(RegExpPrototype.PropertyFlags);
                TypeConverter.RequireObjectCoercible(_engine, flags);
                if (!TypeConverter.ToString(flags).Contains('g'))
                {
                    Throw.TypeError(_realm, "String.prototype.matchAll called with a non-global RegExp argument");
                }
            }
            var matcher = GetMethod(_realm, regex, GlobalSymbolRegistry.MatchAll);
            if (matcher != null)
            {
                return matcher.Call(regex, thisObject);
            }
        }

        var s = TypeConverter.ToJsString(thisObject);
        var rx = (JsRegExp) _realm.Intrinsics.RegExp.Construct([regex, "g"]);

        return _engine.Invoke(rx, GlobalSymbolRegistry.MatchAll, [s]);
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-string.prototype.localecompare
    /// https://tc39.es/ecma402/#sup-string.prototype.localecompare
    /// </summary>
    [JsFunction(Length = 1)]
    [RequireObjectCoercible]
    private JsValue LocaleCompare(JsValue thisObject, JsCallArguments arguments)
    {
        var s = TypeConverter.ToString(thisObject);
        var that = TypeConverter.ToString(arguments.At(0));
        var locales = arguments.At(1);
        var options = arguments.At(2);

        // Use Intl.Collator for locale-aware comparison
        var collator = (JsCollator) Engine.Realm.Intrinsics.Collator.Construct([locales, options], Engine.Realm.Intrinsics.Collator);
        return collator.Compare(s, that);
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-string.prototype.lastindexof
    /// </summary>
    [JsFunction(Length = 1)]
    [RequireObjectCoercible]
    private static JsValue LastIndexOf(JsValue thisObject, JsCallArguments arguments)
    {
        var jsString = TypeConverter.ToJsString(thisObject);
        var searchStr = TypeConverter.ToString(arguments.At(0));
        double numPos = double.NaN;
        if (arguments.Length > 1 && !arguments[1].IsUndefined())
        {
            numPos = TypeConverter.ToNumber(arguments[1]);
        }

        var pos = double.IsNaN(numPos) ? double.PositiveInfinity : TypeConverter.ToInteger(numPos);

        var len = jsString.Length;
        var start = (int) System.Math.Min(System.Math.Max(pos, 0), len);
        var searchLen = searchStr.Length;

        if (searchLen > len)
        {
            return JsNumber.IntegerNegativeOne;
        }

        var s = jsString.ToString();
        var i = start;
        bool found;

        do
        {
            found = true;
            var j = 0;

            while (found && j < searchLen)
            {
                if (i + searchLen > len || s[i + j] != searchStr[j])
                {
                    found = false;
                }
                else
                {
                    j++;
                }
            }
            if (!found)
            {
                i--;
            }

        } while (!found && i >= 0);

        return i;
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-string.prototype.indexof
    /// </summary>
    [JsFunction(Length = 1)]
    [RequireObjectCoercible]
    private static JsValue IndexOf(JsValue thisObject, JsCallArguments arguments)
    {
        var s = TypeConverter.ToJsString(thisObject);
        var searchStr = TypeConverter.ToString(arguments.At(0));
        double pos = 0;
        if (arguments.Length > 1 && !arguments[1].IsUndefined())
        {
            pos = TypeConverter.ToInteger(arguments[1]);
        }

        if (pos > s.Length)
        {
            pos = s.Length;
        }

        if (pos < 0)
        {
            pos = 0;
        }

        return s.IndexOf(searchStr, (int) pos);
    }

    [JsFunction(Length = 1)]
    [RequireObjectCoercible]
    private static JsValue Concat(JsValue thisObject, JsCallArguments arguments)
    {
        if (thisObject is not JsString jsString)
        {
            jsString = new JsString.ConcatenatedString(TypeConverter.ToString(thisObject));
        }
        else
        {
            jsString = jsString.EnsureCapacity(0);
        }

        foreach (var argument in arguments)
        {
            jsString = jsString.Append(argument);
        }

        return jsString;
    }

    [JsFunction(Length = 1)]
    [RequireObjectCoercible]
    private static JsValue CharCodeAt(JsValue thisObject, JsCallArguments arguments)
    {
        JsValue pos = arguments.Length > 0 ? arguments[0] : 0;
        var s = TypeConverter.ToJsString(thisObject);
        var position = (int) TypeConverter.ToInteger(pos);
        if (position < 0 || position >= s.Length)
        {
            return JsNumber.DoubleNaN;
        }
        return (long) s[position];
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-string.prototype.codepointat
    /// </summary>
    [JsFunction(Length = 1)]
    [RequireObjectCoercible]
    private static JsValue CodePointAt(JsValue thisObject, JsCallArguments arguments)
    {
        JsValue pos = arguments.Length > 0 ? arguments[0] : 0;
        var s = TypeConverter.ToString(thisObject);
        var position = (int) TypeConverter.ToInteger(pos);
        if (position < 0 || position >= s.Length)
        {
            return Undefined;
        }

        return CodePointAt(s, position).CodePoint;
    }

    [StructLayout(LayoutKind.Auto)]
    private readonly record struct CodePointResult(int CodePoint, int CodeUnitCount, bool IsUnpairedSurrogate);

    private static CodePointResult CodePointAt(string s, int position)
    {
        var size = s.Length;
        var first = s.CharCodeAt(position);
        var cp = s.CharCodeAt(position);

        var firstIsLeading = char.IsHighSurrogate(first);
        var firstIsTrailing = char.IsLowSurrogate(first);
        if (!firstIsLeading && !firstIsTrailing)
        {
            return new CodePointResult(cp, 1, false);
        }

        if (firstIsTrailing || position + 1 == size)
        {
            return new CodePointResult(cp, 1, true);
        }

        var second = s.CharCodeAt(position + 1);
        if (!char.IsLowSurrogate(second))
        {
            return new CodePointResult(cp, 1, true);
        }

        return new CodePointResult(char.ConvertToUtf32(first, second), 2, false);
    }

    [JsFunction(Length = 1)]
    [RequireObjectCoercible]
    private static JsValue CharAt(JsValue thisObject, JsCallArguments arguments)
    {
        var s = TypeConverter.ToJsString(thisObject);
        var position = TypeConverter.ToInteger(arguments.At(0));
        var size = s.Length;
        if (position >= size || position < 0)
        {
            return JsString.Empty;
        }
        return JsString.Create(s[(int) position]);
    }

    [JsFunction]
    private JsValue ValueOf(JsValue thisObject)
    {
        if (thisObject is StringInstance si)
        {
            return si.StringData;
        }

        if (thisObject is JsString)
        {
            return thisObject;
        }

        Throw.TypeError(_realm, "String.prototype.valueOf requires that 'this' be a String");
        return Undefined;
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-string.prototype.padstart
    /// </summary>
    [JsFunction(Length = 1)]
    [RequireObjectCoercible]
    private static JsValue PadStart(JsValue thisObject, JsCallArguments arguments)
    {
        return StringPad(thisObject, arguments, true);
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-string.prototype.padend
    /// </summary>
    [JsFunction(Length = 1)]
    [RequireObjectCoercible]
    private static JsValue PadEnd(JsValue thisObject, JsCallArguments arguments)
    {
        return StringPad(thisObject, arguments, false);
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-stringpad
    /// </summary>
    private static JsValue StringPad(JsValue thisObject, JsCallArguments arguments, bool padStart)
    {
        var s = TypeConverter.ToJsString(thisObject);

        var targetLength = TypeConverter.ToInt32(arguments.At(0));
        var padStringValue = arguments.At(1);

        var padString = padStringValue.IsUndefined()
            ? " "
            : TypeConverter.ToString(padStringValue);

        if (s.Length > targetLength || padString.Length == 0)
        {
            return s;
        }

        targetLength -= s.Length;
        if (targetLength > padString.Length)
        {
            padString = string.Join("", System.Linq.Enumerable.Repeat(padString, (targetLength / padString.Length) + 1));
        }

        return padStart
            ? $"{padString.Substring(0, targetLength)}{s}"
            : $"{s}{padString.Substring(0, targetLength)}";
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-string.prototype.startswith
    /// </summary>
    [JsFunction(Length = 1)]
    [RequireObjectCoercible]
    private JsValue StartsWith(JsValue thisObject, JsCallArguments arguments)
    {
        var s = TypeConverter.ToJsString(thisObject);

        var searchString = arguments.At(0);
        if (ReferenceEquals(searchString, Null))
        {
            searchString = "null";
        }
        else
        {
            if (searchString.IsRegExp())
            {
                Throw.TypeError(_realm, "First argument to String.prototype.startsWith must not be a regular expression");
            }
        }

        var searchStr = TypeConverter.ToString(searchString);

        var pos = TypeConverter.ToInt32(arguments.At(1));

        var len = s.Length;
        var start = System.Math.Min(System.Math.Max(pos, 0), len);

        return s.StartsWith(searchStr, start);
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-string.prototype.endswith
    /// </summary>
    [JsFunction(Length = 1)]
    [RequireObjectCoercible]
    private JsValue EndsWith(JsValue thisObject, JsCallArguments arguments)
    {
        var s = TypeConverter.ToJsString(thisObject);

        var searchString = arguments.At(0);
        if (ReferenceEquals(searchString, Null))
        {
            searchString = "null";
        }
        else
        {
            if (searchString.IsRegExp())
            {
                Throw.TypeError(_realm, "First argument to String.prototype.endsWith must not be a regular expression");
            }
        }

        var searchStr = TypeConverter.ToString(searchString);

        var len = s.Length;
        var pos = TypeConverter.ToInt32(arguments.At(1, len));
        var end = System.Math.Min(System.Math.Max(pos, 0), len);

        return s.EndsWith(searchStr, end);
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-string.prototype.includes
    /// </summary>
    [JsFunction(Length = 1)]
    [RequireObjectCoercible]
    private JsValue Includes(JsValue thisObject, JsCallArguments arguments)
    {
        var s = TypeConverter.ToJsString(thisObject);
        var searchString = arguments.At(0);

        if (searchString.IsRegExp())
        {
            Throw.TypeError(_realm, "First argument to String.prototype.includes must not be a regular expression");
        }

        var searchStr = TypeConverter.ToString(searchString);
        double pos = 0;
        if (arguments.Length > 1 && !arguments[1].IsUndefined())
        {
            pos = TypeConverter.ToInteger(arguments[1]);
        }

        if (searchStr.Length == 0)
        {
            return JsBoolean.True;
        }

        if (pos < 0)
        {
            pos = 0;
        }

        return s.IndexOf(searchStr, (int) pos) > -1;
    }

    [JsFunction]
    [RequireObjectCoercible]
    private JsValue Normalize(JsValue thisObject, JsCallArguments arguments)
    {
        var str = TypeConverter.ToString(thisObject);

        var param = arguments.At(0);

        var form = "NFC";
        if (!param.IsUndefined())
        {
            form = TypeConverter.ToString(param);
        }

        var nf = NormalizationForm.FormC;
        switch (form)
        {
            case "NFC":
                nf = NormalizationForm.FormC;
                break;
            case "NFD":
                nf = NormalizationForm.FormD;
                break;
            case "NFKC":
                nf = NormalizationForm.FormKC;
                break;
            case "NFKD":
                nf = NormalizationForm.FormKD;
                break;
            default:
                Throw.RangeError(
                    _realm,
                    "The normalization form should be one of NFC, NFD, NFKC, NFKD.");
                break;
        }

        return str.Normalize(nf);
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-string.prototype.repeat
    /// </summary>
    [JsFunction(Length = 1)]
    [RequireObjectCoercible]
    private JsValue Repeat(JsValue thisObject, JsCallArguments arguments)
    {
        var s = TypeConverter.ToString(thisObject);
        var count = arguments.At(0);

        var n = TypeConverter.ToIntegerOrInfinity(count);

        if (n < 0 || double.IsPositiveInfinity(n))
        {
            Throw.RangeError(_realm, "Invalid count value");
        }

        if (n == 0 || s.Length == 0)
        {
            return JsString.Empty;
        }

        var resultLength = n * s.Length;
        if (resultLength > ClrLimits.MaxArrayLength)
        {
            Throw.RangeError(_realm, "Invalid string length");
        }

        if (s.Length == 1)
        {
            return new string(s[0], (int) n);
        }

        var sb = new ValueStringBuilder((int) resultLength);
        for (var i = 0; i < n; ++i)
        {
            sb.Append(s);
        }

        return sb.ToString();
    }

    [JsFunction]
    [RequireObjectCoercible]
    private static JsValue IsWellFormed(JsValue thisObject)
    {
        var s = TypeConverter.ToString(thisObject);

        return IsStringWellFormedUnicode(s);
    }

    [JsFunction]
    [RequireObjectCoercible]
    private static JsValue ToWellFormed(JsValue thisObject)
    {
        var s = TypeConverter.ToString(thisObject);

        var strLen = s.Length;
        var k = 0;

        var result = new ValueStringBuilder();
        while (k < strLen)
        {
            var cp = CodePointAt(s, k);
            if (cp.IsUnpairedSurrogate)
            {
                // \uFFFD
                result.Append('�');
            }
            else
            {
                result.Append(s.AsSpan(k, cp.CodeUnitCount));
            }
            k += cp.CodeUnitCount;
        }

        return result.ToString();
    }

    private static bool IsStringWellFormedUnicode(string s)
    {
        for (var i = 0; i < s.Length; ++i)
        {
            var isSurrogate = (s.CharCodeAt(i) & 0xF800) == 0xD800;
            if (!isSurrogate)
            {
                continue;
            }

            var isLeadingSurrogate = s.CharCodeAt(i) < 0xDC00;
            if (!isLeadingSurrogate)
            {
                return false; // unpaired trailing surrogate
            }

            var isFollowedByTrailingSurrogate = i + 1 < s.Length && (s.CharCodeAt(i + 1) & 0xFC00) == 0xDC00;
            if (!isFollowedByTrailingSurrogate)
            {
                return false; // unpaired leading surrogate
            }

            ++i;
        }

        return true;
    }
}
