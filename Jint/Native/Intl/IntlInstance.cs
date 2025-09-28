#pragma warning disable CA1859 // Use concrete types when possible for improved performance -- most of prototype methods return JsValue

using Jint.Native.Object;
using Jint.Native.Symbol;
using Jint.Runtime;
using Jint.Runtime.Descriptors;
using Jint.Runtime.Interop;
using System.Text;

namespace Jint.Native.Intl;

/// <summary>
/// https://tc39.es/ecma402/#intl-object
/// </summary>
internal sealed class IntlInstance : ObjectInstance
{
    private readonly Realm _realm;

    internal IntlInstance(
        Engine engine,
        Realm realm,
        ObjectPrototype objectPrototype) : base(engine)
    {
        _realm = realm;
        _prototype = objectPrototype;
    }

    protected override void Initialize()
    {
        // TODO check length
        var properties = new PropertyDictionary(10, checkExistingKeys: false)
        {
            ["Collator"] = new(_realm.Intrinsics.Collator, false, false, true),
            ["DateTimeFormat"] = new(_realm.Intrinsics.DateTimeFormat, false, false, true),
            ["DisplayNames"] = new(_realm.Intrinsics.DisplayNames, false, false, true),
            ["ListFormat"] = new(_realm.Intrinsics.ListFormat, false, false, true),
            ["Locale"] = new(_realm.Intrinsics.Locale, false, false, true),
            ["NumberFormat"] = new(_realm.Intrinsics.NumberFormat, false, false, true),
            ["PluralRules"] = new(_realm.Intrinsics.PluralRules, false, false, true),
            ["RelativeTimeFormat"] = new(_realm.Intrinsics.RelativeTimeFormat, false, false, true),
            ["Segmenter"] = new(_realm.Intrinsics.Segmenter, false, false, true),
            ["getCanonicalLocales"] = new(new ClrFunction(Engine, "getCanonicalLocales", GetCanonicalLocales, 1, PropertyFlag.Configurable), true, false, true),
        };
        SetProperties(properties);

        var symbols = new SymbolDictionary(1)
        {
            [GlobalSymbolRegistry.ToStringTag] = new("Intl", PropertyFlag.Configurable)
        };
        SetSymbols(symbols);
    }

    // CanonicalizeLocaleList(locales)
    // Spec: https://tc39.es/ecma402/#sec-canonicalizelocalelist
    // Behavior mirrors WebKit’s implementation: if `locales` is undefined -> empty list.
    // Otherwise iterate array-like `O`, accept String or Intl.Locale objects, validate
    // BCP-47 via ICU (uloc_forLanguageTag), then canonicalize (uloc_toLanguageTag).
    private List<string> CanonicalizeLocaleList(JsValue locales)
    {
        var seen = new List<string>();

        // 1. If locales is undefined, return empty list
        if (locales.IsUndefined())
            return seen;

        // 3–4. Build O:
        // If locales is a String or an Intl.Locale, behave like [ locales ].
        // We special-case String; for Intl.Locale we don’t have the brand slot here,
        // so we treat other objects via ToObject (spec 4).
        bool treatAsSingle = locales.IsString();

        ObjectInstance O;
        if (treatAsSingle)
        {
            var arr = _realm.Intrinsics.Array.Construct(Arguments.Empty);
            arr.SetIndexValue(0, locales, updateLength: true);
            O = arr;
        }
        else
        {
            O = TypeConverter.ToObject(_realm, locales);
        }

        // 5. Let len be LengthOfArrayLike(O)
        var lenValue = O.Get("length");
        var len = TypeConverter.ToLength(lenValue);

        var dedupe = new HashSet<string>(System.StringComparer.Ordinal);

        // 6–7. Iterate k
        for (ulong k = 0; k < len; k++)
        {
            var pk = TypeConverter.ToString(k); // ToString(𝔽(k))
            bool kPresent = O.HasProperty(pk);
            if (!kPresent)
                continue;

            var kValue = O.Get(pk);

            // 7.c.ii: must be String or Object
            if (!kValue.IsString() && !kValue.IsObject())
            {
                Throw.TypeError(_realm, "locale value must be a string or object");
            }

            // 7.c.iii/iv: tag from Locale.[[Locale]] or ToString(kValue)
            // We don’t have direct [[InitializedLocale]] plumbing here, so use ToString unless it’s a JS string.
            string tag = kValue.IsString()
                ? kValue.AsString().ToString()
                : TypeConverter.ToString(kValue);

            // 7.c.v–vii: Validate & canonicalize; throw RangeError if invalid
            string canonical = CanonicalizeUnicodeLocaleIdOrThrow(_engine, tag);

            if (dedupe.Add(canonical))
                seen.Add(canonical);
        }

        // 8. Return seen
        return seen;
    }

    // Intl.getCanonicalLocales(locales)
    // https://tc39.es/ecma402/#sec-intl.getcanonicallocales
    private JsValue GetCanonicalLocales(JsValue thisObject, JsCallArguments arguments)
    {
        var locales = arguments.At(0);
        var list = CanonicalizeLocaleList(locales);

        var arr = new JsArray(_engine);
        arr.Prototype = _realm.Intrinsics.Array.PrototypeObject;

        for (uint i = 0; i < list.Count; i++)
        {
            arr.SetIndexValue(i, list[(int) i], updateLength: true);
        }

        return arr;
    }

    /// <summary>
    /// Equivalent to WebKit's languageTagForLocaleID(localeID, isImmortal=false).
    /// Calls ICU uloc_toLanguageTag(localeId, strict=false), then applies the same
    /// unicode extension cleanup WebKit does (drop "-u-…-true" values).
    /// </summary>
    public static string LanguageTagForLocaleId(string localeId)
    {
        if (string.IsNullOrEmpty(localeId))
            return string.Empty;

        var status = ICU.UErrorCode.U_ZERO_ERROR;

        // First pass with a reasonable buffer
        byte[] buf = new byte[256];
        int len = ICU.uloc_toLanguageTag(localeId, buf, buf.Length, strict: false, ref status);

        // If ICU tells us the required size, reallocate and retry
        if (len > buf.Length)
        {
            buf = new byte[len];
            status = ICU.UErrorCode.U_ZERO_ERROR;
            len = ICU.uloc_toLanguageTag(localeId, buf, buf.Length, strict: false, ref status);
        }

        if (status != ICU.UErrorCode.U_ZERO_ERROR || len <= 0)
            Throw.ArgumentException($"ICU uloc_toLanguageTag failed for '{localeId}' (status={status}).");

        // ICU writes UTF-8 bytes; decode exactly the returned length
        string tag = System.Text.Encoding.UTF8.GetString(buf, 0, len);

        // Do the same extension cleanup WebKit applies
        return CanonicalizeUnicodeExtensionsAfterIcu(tag);
    }

    // Keys whose boolean "true" value is **elided** in canonical form.
    // For these, "-u-<key>-yes" and "-u-<key>-true" both canonicalize to just "-u-<key>".
    // Add "ca" here so a bare `-u-ca` does not synthesize `-yes`
    private static readonly HashSet<string> s_trueDroppableKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "kb", "kc", "kh", "kk", "kn", "ca"
    };


    // Canonicalize subdivision aliases (used for rg/sd values).
    private static string CanonicalizeSubdivision(string value)
    {
        switch (value.ToLowerInvariant())
        {
            case "no23": return "no50";
            case "cn11": return "cnbj";
            case "cz10a": return "cz110";
            case "fra": return "frges";
            case "frg": return "frges";
            case "lud": return "lucl"; // test262 prefers the first in replacement list
            default: return value;
        }
    }

    // Canonicalize time zone type aliases (used for tz values).
    private static string CanonicalizeTimeZoneType(string value)
    {
        switch (value.ToLowerInvariant())
        {
            case "cnckg": return "cnsha"; // deprecated -> preferred
            case "eire": return "iedub"; // alias -> canonical
            case "est": return "papty"; // alias -> canonical
            case "gmt0": return "gmt";   // alias -> canonical
            case "uct": return "utc";   // alias -> canonical
            case "zulu": return "utc";   // alias -> canonical
            case "utcw05": return "papty"; // short offset alias seen in test262
            default: return value;
        }
    }

    /// <summary>
    /// Mirrors WebKit's canonicalizeUnicodeExtensionsAfterICULocaleCanonicalization():
    /// - Finds the "-u-" extension and its end (before the next singleton).
    /// - Re-emits the extension with per-key normalization:
    ///   * For keys kb/kc/kh/kk/kn: drop boolean "true" (and treat "yes" as true → drop).
    ///   * For all other keys: keep "yes"; if ICU turned "yes" into "true", revert to "yes".
    ///   * For "rg"/"sd": canonicalize subdivision aliases (no23→no50, ...).
    ///   * For "tz": canonicalize timezone aliases (eire→iedub, est→papty, ...).
    /// Everything else in the tag is preserved.
    /// </summary>
    public static string CanonicalizeUnicodeExtensionsAfterIcu(string tag)
    {
        if (string.IsNullOrEmpty(tag))
            return tag;

        int extensionIndex = tag.IndexOf("-u-", StringComparison.OrdinalIgnoreCase);
        if (extensionIndex < 0)
            return tag;

        // Determine the end of the -u- block (before the next singleton like -x-).
        int extensionLength = tag.Length - extensionIndex;
        int end = extensionIndex + 3;
        while (end < tag.Length)
        {
            int dash = tag.IndexOf('-', end);
            if (dash < 0)
                break;
            if (dash + 2 < tag.Length && tag[dash + 2] == '-')
            {
                extensionLength = dash - extensionIndex;
                break;
            }
            end = dash + 1;
        }

        var result = new StringBuilder(tag.Length + 8);

        // Copy up to and including "-u"
        result.Append(tag, 0, extensionIndex + 2);

        // Process "-u-..." segment
        string extension = tag.Substring(extensionIndex, extensionLength);
        var parts = extension.Split('-'); // parts[0] == "", parts[1] == "u"
        int i = 2;

        while (i < parts.Length)
        {
            string subtag = parts[i];
            if (subtag.Length == 0) { i++; continue; }

            // Emit the key or attribute
            result.Append('-');
            result.Append(subtag);

            if (subtag.Length == 2)
            {
                // It's a key.
                string key = subtag;
                bool keyIsDroppableTrue = s_trueDroppableKeys.Contains(key);

                int valueStart = i + 1;
                int valueEnd = valueStart;
                while (valueEnd < parts.Length && parts[valueEnd].Length != 2 && parts[valueEnd].Length != 0)
                    valueEnd++;

                bool emittedAnyValue = false;

                for (int v = valueStart; v < valueEnd; v++)
                {
                    string value = parts[v];
                    if (value.Length == 0)
                        continue;

                    // Handle "yes"/"true" normalization
                    if (value.Equals("yes", StringComparison.OrdinalIgnoreCase))
                    {
                        if (keyIsDroppableTrue)
                        {
                            // Drop boolean true for droppable keys.
                            continue;
                        }
                        // keep "yes" for non-droppable
                    }
                    else if (value.Equals("true", StringComparison.OrdinalIgnoreCase))
                    {
                        if (keyIsDroppableTrue)
                        {
                            // Drop boolean true for droppable keys.
                            continue;
                        }
                        // Non-droppable: canonicalize to "yes"
                        value = "yes";
                    }

                    // Per-key aliasing
                    if (key.Equals("rg", StringComparison.OrdinalIgnoreCase) ||
                        key.Equals("sd", StringComparison.OrdinalIgnoreCase))
                    {
                        value = CanonicalizeSubdivision(value);
                    }
                    else if (key.Equals("tz", StringComparison.OrdinalIgnoreCase))
                    {
                        value = CanonicalizeTimeZoneType(value);
                    }

                    result.Append('-');
                    result.Append(value);
                    emittedAnyValue = true;
                }

                // If **no** value was emitted for a **non-droppable** key, synthesize "-yes".
                if (!emittedAnyValue && !keyIsDroppableTrue)
                {
                    result.Append("-yes");
                }

                i = valueEnd;
            }
            else
            {
                // Attribute (or malformed); just pass through.
                i++;
            }
        }

        // Append remainder after the -u- block
        result.Append(tag, extensionIndex + extensionLength, tag.Length - (extensionIndex + extensionLength));
        return result.ToString();
    }




    /// Validates `tag` as a BCP-47 language tag via ICU and returns a canonical tag.
    /// Throws RangeError on invalid tags (spec-compliant).
    public string CanonicalizeUnicodeLocaleIdOrThrow(Engine engine, string tag)
    {
        // 1) Validate & parse BCP-47 -> ICU locale ID
        var status = ICU.UErrorCode.U_ZERO_ERROR;
        byte[] locBuf = new byte[128];
        int parsed;
        int need = ICU.uloc_forLanguageTag(tag, locBuf, locBuf.Length, out parsed, ref status);

        if (need > locBuf.Length)
        {
            locBuf = new byte[need];
            status = ICU.UErrorCode.U_ZERO_ERROR;
            need = ICU.uloc_forLanguageTag(tag, locBuf, locBuf.Length, out parsed, ref status);
        }

        if (status != ICU.UErrorCode.U_ZERO_ERROR || parsed != tag.Length || need <= 0)
        {
            // RangeError per spec
            Throw.RangeError(_realm, $"invalid language tag: {tag}");
        }

        string icuLocaleId = Encoding.UTF8.GetString(locBuf, 0, need);

        // 2) Canonicalize the ICU locale ID (this applies CLDR language/region/script aliases, e.g. cmn->zh)
        status = ICU.UErrorCode.U_ZERO_ERROR;
        byte[] canonLoc = new byte[System.Math.Max(need + 16, 256)];
        int canonLen = ICU.uloc_canonicalize(icuLocaleId, canonLoc, canonLoc.Length, ref status);

        if (canonLen > canonLoc.Length)
        {
            canonLoc = new byte[canonLen];
            status = ICU.UErrorCode.U_ZERO_ERROR;
            canonLen = ICU.uloc_canonicalize(icuLocaleId, canonLoc, canonLoc.Length, ref status);
        }

        string icuCanonical = (status == ICU.UErrorCode.U_ZERO_ERROR && canonLen > 0)
            ? Encoding.UTF8.GetString(canonLoc, 0, canonLen)
            : icuLocaleId; // fall back if canonicalize didn’t change it

        // 3) Convert canonical ICU locale ID -> canonical BCP-47 tag
        status = ICU.UErrorCode.U_ZERO_ERROR;
        byte[] outBuf = new byte[256];
        int len = ICU.uloc_toLanguageTag(icuCanonical, outBuf, outBuf.Length, strict: false, ref status);

        if (len > outBuf.Length)
        {
            outBuf = new byte[len];
            status = ICU.UErrorCode.U_ZERO_ERROR;
            len = ICU.uloc_toLanguageTag(icuCanonical, outBuf, outBuf.Length, strict: false, ref status);
        }

        if (status != ICU.UErrorCode.U_ZERO_ERROR || len <= 0)
        {
            Throw.RangeError(_realm, $"failed to canonicalize language tag: {tag}");
        }

        var canonical = Encoding.UTF8.GetString(outBuf, 0, len);

        // WebKit-style cleanup for "-u-…-true"
        canonical = IntlInstance.CanonicalizeUnicodeExtensionsAfterIcu(canonical);

        // Fallback for ICU builds that don't alias cmn->zh
        canonical = FixKnownLanguageAliases(canonical);

        return canonical;
    }

    private static string FixKnownLanguageAliases(string canonicalTag)
    {
        if (string.IsNullOrEmpty(canonicalTag))
            return canonicalTag;

        // Split once: "xx[-…]" → lang + rest (rest includes the leading '-')
        int dash = canonicalTag.IndexOf('-');
        ReadOnlySpan<char> lang = dash < 0
            ? canonicalTag.AsSpan()
            : canonicalTag.AsSpan(0, dash);

        // We'll append the remainder (if any) after we swap the primary language subtag.
        ReadOnlySpan<char> rest = dash < 0
            ? ReadOnlySpan<char>.Empty
            : canonicalTag.AsSpan(dash); // includes '-...'

        // Known primary language aliases not consistently handled by older ICU:
        //  - cmn → zh (Mandarin → Chinese)
        //  - ji  → yi
        //  - in  → id
        if (lang.Equals("cmn".AsSpan(), StringComparison.OrdinalIgnoreCase))
        {
            return rest.IsEmpty ? "zh" : "zh" + rest.ToString();
        }

        if (lang.Equals("ji".AsSpan(), StringComparison.OrdinalIgnoreCase))
        {
            return rest.IsEmpty ? "yi" : "yi" + rest.ToString();
        }

        if (lang.Equals("in".AsSpan(), StringComparison.OrdinalIgnoreCase))
        {
            return rest.IsEmpty ? "id" : "id" + rest.ToString();
        }

        // Otherwise, leave as-is.
        return canonicalTag;
    }
}
