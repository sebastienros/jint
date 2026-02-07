using System.Globalization;
using System.Text.RegularExpressions;
using Jint.Native.Intl.Data;
using Jint.Native.Object;
using Jint.Runtime;

namespace Jint.Native.Intl;

/// <summary>
/// Shared utility functions for ECMA-402 Internationalization API.
/// https://tc39.es/ecma402/
/// </summary>
internal static class IntlUtilities
{
    // BCP 47 language tag pattern (permissive to accept Unicode extensions)
    // Accepts: language[-script][-region][-variant]*[-extension]*[-privateuse]
    // Extensions use single-character singletons like -u- for Unicode extensions
    // Full spec: https://www.rfc-editor.org/rfc/bcp/bcp47.txt
    private static readonly Regex LanguageTagPattern = new(
        "^[a-zA-Z]{2,8}(?:-[a-zA-Z0-9]{1,8})*$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant,
        TimeSpan.FromMilliseconds(100));

    /// <summary>
    /// Grandfathered tags that map to canonical forms.
    /// Only includes regular grandfathered tags that are valid per UTS35.
    /// See https://unicode.org/reports/tr35/#BCP_47_Conformance
    /// </summary>
    private static readonly Dictionary<string, string> GrandfatheredTags = new(StringComparer.OrdinalIgnoreCase)
    {
        // Regular grandfathered tags that are valid per UTS35
        { "art-lojban", "jbo" },
        { "cel-gaulish", "xtg" },
        { "zh-guoyu", "zh" },
        { "zh-hakka", "hak" },
        { "zh-xiang", "hsn" },
        // Sign language tags with single region code (sgn-XX -> valid language code)
        // These are in CLDR aliasData as type="language"
        { "sgn-BR", "bzs" },
        { "sgn-CO", "csn" },
        { "sgn-DE", "gsg" },
        { "sgn-DK", "dsl" },
        { "sgn-ES", "ssp" },
        { "sgn-FR", "fsl" },
        { "sgn-GB", "bfi" },
        { "sgn-GR", "gss" },
        { "sgn-IE", "isg" },
        { "sgn-IT", "ise" },
        { "sgn-JP", "jsl" },
        { "sgn-MX", "mfs" },
        { "sgn-NI", "ncs" },
        { "sgn-NL", "dse" },
        { "sgn-NO", "nsl" },
        { "sgn-PT", "psr" },
        { "sgn-SE", "swl" },
        { "sgn-US", "ase" },
        { "sgn-ZA", "sfs" },
        // Note: The following are NOT included because they are invalid per UTS35:
        // - no-bok, no-nyn, zh-min, zh-min-nan (regularGrandfatheredNonUTS35)
        // - All irregular grandfathered tags (i-*, en-gb-oed)
        // - Sign language grandfathered tags with compound regions (sgn-XX-XX like sgn-be-fr)
    };

    /// <summary>
    /// Language aliases from CLDR supplemental/languageAlias.xml.
    /// Maps deprecated/legacy language codes to their canonical replacements.
    /// </summary>
    private static readonly Dictionary<string, string> LanguageAliases = new(StringComparer.OrdinalIgnoreCase)
    {
        // ISO 639-3 to macrolanguage mappings
        { "cmn", "zh" },           // Mandarin Chinese -> Chinese
        { "arb", "ar" },           // Standard Arabic -> Arabic
        { "swh", "sw" },           // Swahili -> Swahili macrolanguage
        { "zsm", "ms" },           // Standard Malay -> Malay

        // Legacy/deprecated codes
        { "ji", "yi" },            // Yiddish (legacy)
        { "iw", "he" },            // Hebrew (legacy ISO 639-1)
        { "in", "id" },            // Indonesian (legacy ISO 639-1)
        { "jw", "jv" },            // Javanese (legacy)
        { "mo", "ro" },            // Moldavian (now Romanian)
        { "tl", "fil" },           // Tagalog -> Filipino (debatable, but per CLDR)
        { "sh", "sr-Latn" },       // Serbo-Croatian -> Serbian Latin

        // Other mappings
        { "aar", "aa" },
        { "abk", "ab" },
        { "afr", "af" },
        { "aka", "ak" },
        { "amh", "am" },
        { "ara", "ar" },
        { "aze", "az" },
        { "bel", "be" },
        { "ben", "bn" },
        { "bod", "bo" },
        { "bos", "bs" },
        { "bul", "bg" },
        { "cat", "ca" },
        { "ces", "cs" },
        { "cym", "cy" },
        { "dan", "da" },
        { "deu", "de" },
        { "ell", "el" },
        { "eng", "en" },
        { "est", "et" },
        { "eus", "eu" },
        { "fas", "fa" },
        { "fin", "fi" },
        { "fra", "fr" },
        { "gle", "ga" },
        { "glg", "gl" },
        { "guj", "gu" },
        { "heb", "he" },
        { "hin", "hi" },
        { "hrv", "hr" },
        { "hun", "hu" },
        { "hye", "hy" },
        { "ind", "id" },
        { "isl", "is" },
        { "ita", "it" },
        { "jav", "jv" },
        { "jpn", "ja" },
        { "kan", "kn" },
        { "kat", "ka" },
        { "kaz", "kk" },
        { "khm", "km" },
        { "kor", "ko" },
        { "lao", "lo" },
        { "lat", "la" },
        { "lav", "lv" },
        { "lit", "lt" },
        { "mal", "ml" },
        { "mar", "mr" },
        { "mkd", "mk" },
        { "mlt", "mt" },
        { "mon", "mn" },
        { "msa", "ms" },
        { "mya", "my" },
        { "nep", "ne" },
        { "nld", "nl" },
        { "nor", "no" },
        { "pan", "pa" },
        { "pol", "pl" },
        { "por", "pt" },
        { "pus", "ps" },
        { "ron", "ro" },
        { "rus", "ru" },
        { "sin", "si" },
        { "slk", "sk" },
        { "slv", "sl" },
        { "som", "so" },
        { "spa", "es" },
        { "sqi", "sq" },
        { "srp", "sr" },
        { "swa", "sw" },
        { "swe", "sv" },
        { "tam", "ta" },
        { "tel", "te" },
        { "tha", "th" },
        { "tur", "tr" },
        { "ukr", "uk" },
        { "urd", "ur" },
        { "uzb", "uz" },
        { "vie", "vi" },
        { "yid", "yi" },
        { "zho", "zh" },
        { "zul", "zu" },
    };

    /// <summary>
    /// Region aliases from CLDR supplemental/territoryAlias.xml.
    /// Maps deprecated/legacy region codes to their canonical replacements.
    /// </summary>
    private static readonly Dictionary<string, string> RegionAliases = new(StringComparer.OrdinalIgnoreCase)
    {
        // Historical region codes that have been replaced
        { "DD", "DE" },    // East Germany -> Germany
        { "YD", "YE" },    // South Yemen -> Yemen
        { "AN", "CW" },    // Netherlands Antilles -> Curacao (simplified)
        { "CS", "RS" },    // Serbia and Montenegro -> Serbia
        { "YU", "RS" },    // Yugoslavia -> Serbia
        { "TP", "TL" },    // East Timor (old) -> Timor-Leste
        { "ZR", "CD" },    // Zaire -> Democratic Republic of Congo
        { "BU", "MM" },    // Burma -> Myanmar
        { "SU", "RU" },    // Soviet Union -> Russia (simplified)
        { "FX", "FR" },    // Metropolitan France -> France
    };

    /// <summary>
    /// Likely script for common languages (from CLDR likelySubtags).
    /// Used for complex region subtag replacement when no explicit script is present.
    /// </summary>
    private static readonly Dictionary<string, string> LikelyScripts = new(StringComparer.OrdinalIgnoreCase)
    {
        { "aa", "Latn" }, { "ab", "Cyrl" }, { "af", "Latn" }, { "am", "Ethi" },
        { "ar", "Arab" }, { "as", "Beng" }, { "az", "Latn" }, { "be", "Cyrl" },
        { "bg", "Cyrl" }, { "bn", "Beng" }, { "bs", "Latn" }, { "ca", "Latn" },
        { "cs", "Latn" }, { "cy", "Latn" }, { "da", "Latn" }, { "de", "Latn" },
        { "el", "Grek" }, { "en", "Latn" }, { "es", "Latn" }, { "et", "Latn" },
        { "eu", "Latn" }, { "fa", "Arab" }, { "fi", "Latn" }, { "fr", "Latn" },
        { "ga", "Latn" }, { "gl", "Latn" }, { "gu", "Gujr" }, { "he", "Hebr" },
        { "hi", "Deva" }, { "hr", "Latn" }, { "hu", "Latn" }, { "hy", "Armn" },
        { "id", "Latn" }, { "is", "Latn" }, { "it", "Latn" }, { "ja", "Jpan" },
        { "ka", "Geor" }, { "kk", "Cyrl" }, { "km", "Khmr" }, { "kn", "Knda" },
        { "ko", "Kore" }, { "ky", "Cyrl" }, { "lo", "Laoo" }, { "lt", "Latn" },
        { "lv", "Latn" }, { "mk", "Cyrl" }, { "ml", "Mlym" }, { "mn", "Cyrl" },
        { "mr", "Deva" }, { "ms", "Latn" }, { "my", "Mymr" }, { "nb", "Latn" },
        { "ne", "Deva" }, { "nl", "Latn" }, { "nn", "Latn" }, { "no", "Latn" },
        { "or", "Orya" }, { "pa", "Guru" }, { "pl", "Latn" }, { "ps", "Arab" },
        { "pt", "Latn" }, { "ro", "Latn" }, { "ru", "Cyrl" }, { "si", "Sinh" },
        { "sk", "Latn" }, { "sl", "Latn" }, { "sq", "Latn" }, { "sr", "Cyrl" },
        { "sv", "Latn" }, { "sw", "Latn" }, { "ta", "Taml" }, { "te", "Telu" },
        { "tg", "Cyrl" }, { "th", "Thai" }, { "tk", "Latn" }, { "tr", "Latn" },
        { "uk", "Cyrl" }, { "und", "Latn" }, { "ur", "Arab" }, { "uz", "Latn" },
        { "vi", "Latn" }, { "zh", "Hans" },
    };

    /// <summary>
    /// T extension value aliases for deprecated values.
    /// From CLDR supplemental/alias.xml (tvalueAlias).
    /// </summary>
    private static readonly Dictionary<string, string> TValueAliases = new(StringComparer.OrdinalIgnoreCase)
    {
        { "names", "prprname" },  // m0-names -> m0-prprname
    };

    private static readonly Lazy<HashSet<string>> AllCultures = new(() =>
    {
        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var culture in CultureInfo.GetCultures(CultureTypes.AllCultures))
        {
            if (!string.IsNullOrEmpty(culture.Name))
            {
                result.Add(culture.Name);
            }
        }

        return result;
    });

    internal static readonly Lazy<CultureInfo[]> SpecificCultures = new(() => CultureInfo.GetCultures(CultureTypes.SpecificCultures));

    /// <summary>
    /// https://tc39.es/ecma402/#sec-canonicalizelocalelist
    /// </summary>
    internal static List<string> CanonicalizeLocaleList(Engine engine, JsValue locales)
    {
        // 1. If locales is undefined, return a new empty List.
        if (locales.IsUndefined())
        {
            return [];
        }

        var seen = new List<string>();

        // 2. If Type(locales) is String or locales has an [[InitializedLocale]] internal slot, then
        //    let O be CreateArrayFromList(« locales »)
        ObjectInstance o;
        if (locales.IsString() || locales is JsLocale)
        {
            o = new JsArray(engine, [locales]);
        }
        // 2b. If locales is null, throw a TypeError (null is not a valid locales argument)
        else if (locales.IsNull())
        {
            Throw.TypeError(engine.Realm, "Locales argument must not be null");
            return null!;
        }
        else
        {
            // 3. Else let O be ? ToObject(locales).
            o = TypeConverter.ToObject(engine.Realm, locales);
        }

        // 4. Let len be ? ToLength(? Get(O, "length")).
        var len = TypeConverter.ToLength(o.Get(CommonProperties.Length));

        // 5. For each integer k from 0 to len, do
        for (ulong k = 0; k < len; k++)
        {
            var pk = k.ToString(CultureInfo.InvariantCulture);

            // a. Let kPresent be ? HasProperty(O, Pk).
            if (!o.HasProperty(pk))
            {
                continue;
            }

            // b. If kPresent is true, then
            var kValue = o.Get(pk);

            // i. If Type(kValue) is not String or Object, throw a TypeError exception.
            // Note: null and undefined are neither String nor Object, so they throw TypeError
            if (!kValue.IsString() && !kValue.IsObject())
            {
                Throw.TypeError(engine.Realm, "Locale should be a string or object");
            }

            // Note: null is an Object in JS, so we need an explicit check
            if (kValue.IsNull())
            {
                Throw.TypeError(engine.Realm, "Locale should be a string or object");
            }

            string tag;

            // ii. If Type(kValue) is Object and kValue has an [[InitializedLocale]] internal slot, then
            if (kValue is JsLocale jsLocale)
            {
                tag = jsLocale.Locale;
            }
            else
            {
                // iii. Else, let tag be ? ToString(kValue).
                tag = TypeConverter.ToString(kValue);
            }

            // iv. If ! IsStructurallyValidLanguageTag(tag) is false, throw a RangeError exception.
            if (!IsStructurallyValidLanguageTag(tag))
            {
                Throw.RangeError(engine.Realm, $"Invalid language tag: {tag}");
            }

            // v. Let canonicalizedTag be ! CanonicalizeUnicodeLocaleId(tag).
            var canonicalizedTag = CanonicalizeUnicodeLocaleId(tag);

            // vi. If canonicalizedTag is not an element of seen, append canonicalizedTag to seen.
            if (!seen.Contains(canonicalizedTag))
            {
                seen.Add(canonicalizedTag);
            }
        }

        // 6. Return seen.
        return seen;
    }

    /// <summary>
    /// https://tc39.es/ecma402/#sec-isstructurallyvalidlanguagetag
    /// Per UTS 35, validates Unicode BCP 47 Locale Identifiers.
    /// </summary>
    internal static bool IsStructurallyValidLanguageTag(string locale)
    {
        if (string.IsNullOrEmpty(locale))
        {
            return false;
        }

        // Check for invalid characters (non-ASCII, null, whitespace, underscore)
        foreach (var c in locale)
        {
            if (c > 127 || c == '\0' || char.IsWhiteSpace(c) || c == '_')
            {
                return false;
            }
        }

        // Basic check: only ASCII letters, digits, and hyphens
        if (!LanguageTagPattern.IsMatch(locale))
        {
            return false;
        }

        var parts = locale.Split('-');
        if (parts.Length == 0 || parts[0].Length == 0)
        {
            return false;
        }

        // First part must be a valid language subtag (2-3 or 5-8 alpha) or grandfathered
        var firstPart = parts[0];

        // Check for private use only tags ("x-...")
        if (string.Equals(firstPart, "x", StringComparison.OrdinalIgnoreCase))
        {
            // Private use only is invalid in UTS35
            return false;
        }

        // Check for extension singleton in first place ("u", "t", etc.)
        if (firstPart.Length == 1)
        {
            return false;
        }

        // Check for region code in first place (3 digits like "419")
        if (firstPart.Length == 3 && char.IsDigit(firstPart[0]))
        {
            return false;
        }

        // First part must be all letters (language subtag)
        foreach (var c in firstPart)
        {
            if (!char.IsLetter(c))
            {
                return false;
            }
        }

        // Language subtag length: 2-3 or 5-8 letters (4 is script, not allowed as first)
        if (firstPart.Length == 4 || firstPart.Length > 8)
        {
            return false;
        }

        // Track seen singletons for duplicate detection
        var seenSingletons = new HashSet<char>();
        var seenVariants = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var inExtension = false;
        var extensionType = '\0';
        var extensionHasSubtag = false;
        var hasScript = false;
        var hasRegion = false;

        for (var i = 1; i < parts.Length; i++)
        {
            var part = parts[i];

            if (part.Length == 0)
            {
                return false; // Empty subtag (double hyphen)
            }

            // Check for singleton (extension marker)
            if (part.Length == 1)
            {
                var singleton = char.ToLowerInvariant(part[0]);

                // If we're in private use extension (x-), single chars are just content, not new singletons
                if (inExtension && extensionType == 'x')
                {
                    extensionHasSubtag = true;
                    continue;
                }

                // Extension must have at least one subtag after it
                if (inExtension && !extensionHasSubtag)
                {
                    return false;
                }

                // Check for duplicate singleton
                if (seenSingletons.Contains(singleton))
                {
                    return false;
                }
                seenSingletons.Add(singleton);

                inExtension = true;
                extensionType = singleton;
                extensionHasSubtag = false;
                continue;
            }

            if (inExtension)
            {
                extensionHasSubtag = true;

                // Private use extension (x-) accepts any subtags, no format validation needed
                if (extensionType == 'x')
                {
                    continue;
                }

                if (extensionType == 'u')
                {
                    // Unicode extension: ukey must be 2 chars (alphanum + alpha)
                    if (part.Length == 2)
                    {
                        if (!char.IsLetterOrDigit(part[0]) || !char.IsLetter(part[1]))
                        {
                            return false;
                        }
                    }
                }
                else if (extensionType == 't')
                {
                    // Transformed extension validation is complex, handled separately below
                }
            }
            else
            {
                // Not in extension - check for script, region, variant

                if (part.Length == 4 && char.IsLetter(part[0]))
                {
                    // Could be script subtag (4 letters) or variant (4 chars starting with digit)
                    var isAllLetters = true;
                    foreach (var c in part)
                    {
                        if (!char.IsLetter(c))
                        {
                            isAllLetters = false;
                            break;
                        }
                    }

                    if (isAllLetters)
                    {
                        // Script subtag
                        if (hasScript || hasRegion || seenVariants.Count > 0)
                        {
                            // Script must come before region and variants
                            // And only one script allowed
                            return false;
                        }
                        hasScript = true;
                    }
                    else if (char.IsDigit(part[0]))
                    {
                        // 4-char variant starting with digit
                        var partLower = part.ToLowerInvariant();
                        if (seenVariants.Contains(partLower))
                        {
                            return false; // Duplicate variant
                        }
                        seenVariants.Add(partLower);
                    }
                    else
                    {
                        // 4-char alphanumeric but not script and not starting with digit - invalid
                        return false;
                    }
                }
                else if ((part.Length == 2 && char.IsLetter(part[0])) ||
                         (part.Length == 3 && char.IsDigit(part[0])))
                {
                    // Region subtag (2 letters or 3 digits)
                    if (hasRegion)
                    {
                        // Only one region allowed
                        return false;
                    }
                    hasRegion = true;
                }
                else if (part.Length == 4 && char.IsDigit(part[0]))
                {
                    // Variant subtag (4 chars starting with digit, e.g., "1996", "1994")
                    var partLower = part.ToLowerInvariant();
                    if (seenVariants.Contains(partLower))
                    {
                        return false; // Duplicate variant
                    }
                    seenVariants.Add(partLower);
                }
                else if (part.Length >= 5 && part.Length <= 8)
                {
                    // Variant subtag (5-8 alphanumeric)
                    var partLower = part.ToLowerInvariant();
                    if (seenVariants.Contains(partLower))
                    {
                        return false; // Duplicate variant
                    }
                    seenVariants.Add(partLower);
                }
                else
                {
                    // Invalid subtag length for non-extension position
                    // 3-letter alpha subtags would be extlang (not allowed in UTS35)
                    // Other lengths are invalid
                    return false;
                }
            }
        }

        // Extension must have at least one subtag
        if (inExtension && !extensionHasSubtag)
        {
            return false;
        }

        // Validate T extension structure if present
        if (!ValidateTransformedExtension(locale))
        {
            return false;
        }

        return true;
    }

    /// <summary>
    /// Validates the structure of a transformed extension (-t-).
    /// </summary>
    private static bool ValidateTransformedExtension(string locale)
    {
        var tIndex = locale.IndexOf("-t-", StringComparison.OrdinalIgnoreCase);
        if (tIndex < 0)
        {
            return true; // No T extension
        }

        // Find the end of the T extension (next singleton or end of string)
        var endIndex = locale.Length;
        for (var i = tIndex + 3; i < locale.Length - 1; i++)
        {
            if (locale[i] == '-' && i + 2 < locale.Length && locale[i + 2] == '-' && char.IsLetterOrDigit(locale[i + 1]))
            {
                var nextChar = locale[i + 1];
                if (char.IsLetter(nextChar) && nextChar != 'x' && nextChar != 'X')
                {
                    // Found another singleton (not x)
                    endIndex = i;
                    break;
                }
                else if (nextChar == 'x' || nextChar == 'X')
                {
                    // Private use starts
                    endIndex = i;
                    break;
                }
            }
        }

        var tExtension = locale.Substring(tIndex + 3, endIndex - tIndex - 3);
        if (string.IsNullOrEmpty(tExtension))
        {
            return false; // Empty T extension
        }

        var parts = tExtension.Split('-');
        if (parts.Length == 0 || parts[0].Length == 0)
        {
            return false;
        }

        // Parse T extension: [tlang] [tfield]*
        // tlang = unicode_language_subtag (2-3 or 5-8 alpha) ["-" unicode_script_subtag] ["-" unicode_region_subtag] *("-" unicode_variant_subtag)
        // tfield = tkey tvalue+
        // tkey = alpha digit
        // tvalue = 3-8 alphanum

        var index = 0;
        var inTlang = true;
        var tlangHasLanguage = false;
        var tlangHasScript = false;
        var tlangHasRegion = false;
        var currentTKeyHasValue = true; // Start true since we don't have a key yet
        HashSet<string>? tlangSeenVariants = null;

        while (index < parts.Length)
        {
            var part = parts[index];

            // Check if this is a tkey (alpha + digit, 2 chars)
            if (part.Length == 2 && char.IsLetter(part[0]) && char.IsDigit(part[1]))
            {
                // Entering tfield
                if (!currentTKeyHasValue)
                {
                    return false; // Previous tkey had no tvalue
                }
                inTlang = false;
                currentTKeyHasValue = false;
                index++;
                continue;
            }

            if (inTlang)
            {
                // Validate tlang component
                if (!tlangHasLanguage)
                {
                    // First part must be language subtag (2-3 or 5-8 alpha)
                    if (!IsValidTLangLanguage(part))
                    {
                        return false;
                    }
                    tlangHasLanguage = true;
                }
                else if (!tlangHasScript && part.Length == 4 && IsAllLetters(part))
                {
                    // Script subtag (4 alpha)
                    tlangHasScript = true;
                }
                else if (part.Length == 4 && char.IsDigit(part[0]))
                {
                    // 4-char variant starting with digit (e.g., "1994")
                    // Must come after script/region checks
                    tlangSeenVariants ??= new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    if (!tlangSeenVariants.Add(part))
                    {
                        return false; // Duplicate variant in tlang
                    }
                }
                else if (!tlangHasRegion && ((part.Length == 2 && IsAllLetters(part)) || (part.Length == 3 && IsAllDigits(part))))
                {
                    // Region subtag (2 alpha or 3 digit)
                    tlangHasRegion = true;
                }
                else if (IsValidVariant(part))
                {
                    // Variant subtag (5-8 alphanum or 4 starting with digit)
                    tlangSeenVariants ??= new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    if (!tlangSeenVariants.Add(part))
                    {
                        return false; // Duplicate variant in tlang
                    }
                }
                else
                {
                    // Invalid tlang component (could be extlang which is not allowed)
                    return false;
                }
            }
            else
            {
                // Validate tvalue (3-8 alphanum)
                if (part.Length < 3 || part.Length > 8)
                {
                    return false;
                }
                foreach (var c in part)
                {
                    if (!char.IsLetterOrDigit(c))
                    {
                        return false;
                    }
                }
                currentTKeyHasValue = true;
            }

            index++;
        }

        // Final check: if we ended with a tkey, it must have had a tvalue
        if (!inTlang && !currentTKeyHasValue)
        {
            return false;
        }

        return true;
    }

    private static bool IsValidTLangLanguage(string part)
    {
        // Language subtag must be 2-3 or 5-8 alpha characters
        // 4-letter would be a script, not language
        if (part.Length < 2 || part.Length == 4 || part.Length > 8)
        {
            return false;
        }
        return IsAllLetters(part);
    }

    private static bool IsValidVariant(string part)
    {
        // Variant is 5-8 alphanum, or 4 chars starting with digit
        if (part.Length >= 5 && part.Length <= 8)
        {
            foreach (var c in part)
            {
                if (!char.IsLetterOrDigit(c)) return false;
            }
            return true;
        }
        if (part.Length == 4 && char.IsDigit(part[0]))
        {
            foreach (var c in part)
            {
                if (!char.IsLetterOrDigit(c)) return false;
            }
            return true;
        }
        return false;
    }

    private static bool IsAllLetters(string part)
    {
        foreach (var c in part)
        {
            if (!char.IsLetter(c)) return false;
        }
        return true;
    }

    private static bool IsAllDigits(string part)
    {
        foreach (var c in part)
        {
            if (!char.IsDigit(c)) return false;
        }
        return true;
    }

    /// <summary>
    /// Validates that a string matches the Unicode extension value pattern: (3*8alphanum) *("-" (3*8alphanum))
    /// Only ASCII alphanumeric characters are allowed.
    /// </summary>
    internal static bool IsValidUnicodeExtensionValue(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return false;
        }

        var parts = value.Split('-');
        foreach (var part in parts)
        {
            // Each segment must be 3-8 ASCII alphanumeric characters
            if (part.Length < 3 || part.Length > 8)
            {
                return false;
            }

            foreach (var c in part)
            {
                // Must be ASCII alphanumeric only (a-z, A-Z, 0-9)
                if (!((c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9')))
                {
                    return false;
                }
            }
        }

        return true;
    }

    /// <summary>
    /// https://tc39.es/ecma402/#sec-canonicalizeunicodelocaleid
    /// Canonicalizes a Unicode locale identifier.
    /// </summary>
    private static string CanonicalizeUnicodeLocaleId(string locale)
    {
        // 1. Check grandfathered tags first (highest priority)
        // Use LocaleData first, then fallback to hardcoded dictionary
        if (LocaleData.TagMappings.TryGetValue(locale, out var grandfatheredReplacement))
        {
            return grandfatheredReplacement;
        }

        if (GrandfatheredTags.TryGetValue(locale, out grandfatheredReplacement))
        {
            return grandfatheredReplacement;
        }

        // 2. Parse the locale into components
        var parsed = ParseLanguageTag(locale);

        // 3. Apply language aliasing
        if (parsed.Language != null)
        {
            // First try complex language mappings (may add script/region)
            if (LocaleData.ComplexLanguageMappings.TryGetValue(parsed.Language, out var complexMapping))
            {
                parsed.Language = complexMapping.Language;
                if (parsed.Script == null && complexMapping.Script != null)
                {
                    parsed.Script = complexMapping.Script;
                }

                if (parsed.Region == null && complexMapping.Region != null)
                {
                    parsed.Region = complexMapping.Region;
                }
            }
            // Then try simple language mappings from LocaleData
            else if (LocaleData.LanguageMappings.TryGetValue(parsed.Language, out var langReplacement))
            {
                parsed.Language = langReplacement;
            }
            // Fallback to hardcoded aliases
            else if (LanguageAliases.TryGetValue(parsed.Language, out langReplacement))
            {
                // Handle complex replacements like "sh" -> "sr-Latn"
                if (langReplacement.Contains('-'))
                {
                    var replacementParts = langReplacement.Split('-');
                    parsed.Language = replacementParts[0];
                    // Add script from replacement if not already present
                    if (replacementParts.Length > 1 && parsed.Script == null)
                    {
                        parsed.Script = replacementParts[1];
                    }
                }
                else
                {
                    parsed.Language = langReplacement;
                }
            }
        }

        // 4. Apply region aliasing with script-aware replacement for multi-territory regions
        if (parsed.Region != null)
        {
            // First try script-aware replacement (for deprecated regions with multiple replacements)
            var script = parsed.Script;
            if (script == null && parsed.Language != null)
            {
                LikelyScripts.TryGetValue(parsed.Language, out script);
            }

            if (script != null)
            {
                var scriptRegionKey = script + "+" + parsed.Region;
                if (LocaleData.ScriptRegionMappings.TryGetValue(scriptRegionKey, out var scriptRegionReplacement))
                {
                    parsed.Region = scriptRegionReplacement;
                }
                else if (LocaleData.RegionMappings.TryGetValue(parsed.Region, out var regionReplacement))
                {
                    parsed.Region = regionReplacement;
                }
                else if (RegionAliases.TryGetValue(parsed.Region, out regionReplacement))
                {
                    parsed.Region = regionReplacement;
                }
            }
            else if (LocaleData.RegionMappings.TryGetValue(parsed.Region, out var regionReplacement))
            {
                parsed.Region = regionReplacement;
            }
            else if (RegionAliases.TryGetValue(parsed.Region, out regionReplacement))
            {
                parsed.Region = regionReplacement;
            }
        }

        // 5. Apply variant aliasing from CLDR data
        if (parsed.Variants != null && parsed.Variants.Count > 0)
        {
            for (var i = parsed.Variants.Count - 1; i >= 0; i--)
            {
                if (LocaleData.VariantMappings.TryGetValue(parsed.Variants[i], out var variantMapping))
                {
                    if (string.Equals(variantMapping.Type, "language", StringComparison.Ordinal))
                    {
                        // Variant maps to a language replacement - remove variant and update language
                        parsed.Language = variantMapping.Replacement;
                        parsed.Variants.RemoveAt(i);
                    }
                    else if (string.Equals(variantMapping.Type, "region", StringComparison.Ordinal))
                    {
                        // Variant maps to a region - remove variant and set region
                        if (parsed.Region == null)
                        {
                            parsed.Region = variantMapping.Replacement;
                        }
                        parsed.Variants.RemoveAt(i);
                    }
                    else
                    {
                        // Type is "variant" - simple variant replacement
                        parsed.Variants[i] = variantMapping.Replacement;

                        // Remove prefix variants if specified
                        if (variantMapping.Prefix != null)
                        {
                            for (var j = parsed.Variants.Count - 1; j >= 0; j--)
                            {
                                if (j != i && string.Equals(parsed.Variants[j], variantMapping.Prefix, StringComparison.OrdinalIgnoreCase))
                                {
                                    parsed.Variants.RemoveAt(j);
                                    if (j < i) i--;
                                }
                            }
                        }
                    }
                }
            }
        }

        // 5b. Apply language+variant mappings (e.g., "art+lojban" → "jbo")
        if (parsed.Language != null && parsed.Variants != null && parsed.Variants.Count > 0)
        {
            for (var i = parsed.Variants.Count - 1; i >= 0; i--)
            {
                var key = parsed.Language + "+" + parsed.Variants[i].ToLowerInvariant();
                if (LocaleData.LanguageVariantMappings.TryGetValue(key, out var newLanguage))
                {
                    parsed.Language = newLanguage;
                    parsed.Variants.RemoveAt(i);
                }
            }
        }

        // 6. Sort variant subtags alphabetically (per ECMA-402)
        if (parsed.Variants != null && parsed.Variants.Count > 1)
        {
            parsed.Variants.Sort(StringComparer.OrdinalIgnoreCase);
        }

        // 7. Canonicalize extensions
        CanonicalizeExtensions(parsed);

        // 8. Build canonical tag
        return BuildCanonicalTag(parsed);
    }

    /// <summary>
    /// Parses a BCP 47 language tag into its components.
    /// </summary>
    private static ParsedLanguageTag ParseLanguageTag(string tag)
    {
        var result = new ParsedLanguageTag();
        var parts = tag.Split('-');
        var index = 0;

        if (parts.Length == 0)
        {
            return result;
        }

        // Language subtag (first part)
        result.Language = parts[index++].ToLowerInvariant();

        // Subsequent parts
        while (index < parts.Length)
        {
            var part = parts[index];
            var partLower = part.ToLowerInvariant();

            // Check for singleton (extension indicator)
            if (part.Length == 1)
            {
                // Start of extension sequence
                var extensionType = partLower[0];
                var extensionParts = new List<string> { partLower };
                index++;

                if (extensionType == 'x')
                {
                    // Private use extension: collect ALL remaining parts
                    while (index < parts.Length)
                    {
                        extensionParts.Add(parts[index].ToLowerInvariant());
                        index++;
                    }
                }
                else
                {
                    // Other extensions: collect until next singleton or end
                    while (index < parts.Length && parts[index].Length != 1)
                    {
                        extensionParts.Add(parts[index].ToLowerInvariant());
                        index++;
                    }
                }

                result.Extensions ??= new List<ExtensionSubtag>();
                result.Extensions.Add(new ExtensionSubtag { Type = extensionType, Parts = extensionParts });
            }
            else if (part.Length == 4 && char.IsLetter(part[0]) && result.Script == null && result.Region == null && (result.Variants == null || result.Variants.Count == 0))
            {
                // Script subtag (4 letters, title case)
                result.Script = char.ToUpperInvariant(part[0]) + partLower.Substring(1);
                index++;
            }
            else if ((part.Length == 2 && char.IsLetter(part[0])) || (part.Length == 3 && char.IsDigit(part[0])))
            {
                // Region subtag (2 letters uppercase or 3 digits)
                if (result.Region == null && (result.Variants == null || result.Variants.Count == 0))
                {
                    result.Region = part.Length == 2 ? part.ToUpperInvariant() : part;
                    index++;
                }
                else
                {
                    // It's a variant
                    result.Variants ??= new List<string>();
                    result.Variants.Add(partLower);
                    index++;
                }
            }
            else
            {
                // Variant subtag (5-8 alphanumeric, 4 starting with digit, or unknown)
                result.Variants ??= new List<string>();
                result.Variants.Add(partLower);
                index++;
            }
        }

        return result;
    }

    /// <summary>
    /// Canonicalizes extensions (u, t, x, etc.).
    /// </summary>
    private static void CanonicalizeExtensions(ParsedLanguageTag parsed)
    {
        if (parsed.Extensions == null)
        {
            return;
        }

        for (var i = 0; i < parsed.Extensions.Count; i++)
        {
            var ext = parsed.Extensions[i];
            var type = ext.Type;
            var parts = ext.Parts;

            if (type == 't' && parts.Count > 1)
            {
                // T extension: canonicalize tlang and tfield subtags
                var newParts = new List<string> { "t" };
                var tfields = new List<KeyValueParts>();
                string? currentKey = null;
                var currentValues = new List<string>();
                var tlangParts = new List<string>();
                var inTlang = true;

                for (var j = 1; j < parts.Count; j++)
                {
                    var part = parts[j];

                    // tkey is exactly 2 chars, first is alpha, second is digit
                    if (part.Length == 2 && char.IsLetter(part[0]) && char.IsDigit(part[1]))
                    {
                        // This is a tkey
                        inTlang = false;

                        if (currentKey != null)
                        {
                            tfields.Add(new KeyValueParts { Key = currentKey, Values = currentValues });
                            currentValues = new List<string>();
                        }
                        currentKey = part;
                    }
                    else if (inTlang)
                    {
                        // Part of tlang
                        tlangParts.Add(part);
                    }
                    else
                    {
                        // Part of tvalue - apply value aliasing
                        if (TValueAliases.TryGetValue(part, out var alias))
                        {
                            currentValues.Add(alias);
                        }
                        else
                        {
                            currentValues.Add(part);
                        }
                    }
                }

                // Save last tfield
                if (currentKey != null)
                {
                    tfields.Add(new KeyValueParts { Key = currentKey, Values = currentValues });
                }

                // Canonicalize tlang if present
                if (tlangParts.Count > 0)
                {
                    // Apply language aliasing to tlang (try LocaleData first, then fallback)
                    if (LocaleData.LanguageMappings.TryGetValue(tlangParts[0], out var tlangReplacement))
                    {
                        tlangParts[0] = tlangReplacement;
                    }
                    else if (LanguageAliases.TryGetValue(tlangParts[0], out tlangReplacement))
                    {
                        tlangParts[0] = tlangReplacement;
                    }

                    // Parse tlang structure: language[-script][-region][-variant]*
                    // and sort variant subtags alphabetically
                    var tlangPrefix = new List<string>();
                    var tlangVariants = new List<string>();

                    for (var k = 0; k < tlangParts.Count; k++)
                    {
                        var part = tlangParts[k];
                        if (k == 0)
                        {
                            // Language subtag
                            tlangPrefix.Add(part);
                        }
                        else if (part.Length == 4 && char.IsLetter(part[0]) && tlangVariants.Count == 0)
                        {
                            // Script subtag (4 letters before any variants)
                            tlangPrefix.Add(part);
                        }
                        else if ((part.Length == 2 && char.IsLetter(part[0])) || (part.Length == 3 && char.IsDigit(part[0])))
                        {
                            // Region subtag (2 alpha or 3 digit)
                            if (tlangVariants.Count == 0)
                            {
                                tlangPrefix.Add(part);
                            }
                            else
                            {
                                // Treat as variant if we already have variants
                                tlangVariants.Add(part);
                            }
                        }
                        else
                        {
                            // Variant subtag
                            tlangVariants.Add(part);
                        }
                    }

                    // Sort variants alphabetically
                    tlangVariants.Sort(StringComparer.Ordinal);

                    // Add prefix parts
                    foreach (var p in tlangPrefix)
                    {
                        newParts.Add(p);
                    }

                    // Add sorted variants
                    foreach (var v in tlangVariants)
                    {
                        newParts.Add(v);
                    }
                }

                // Sort tfields alphabetically by tkey
                tfields.Sort((a, b) => string.Compare(a.Key, b.Key, StringComparison.Ordinal));

                // Add sorted tfields
                foreach (var kv in tfields)
                {
                    newParts.Add(kv.Key);
                    newParts.AddRange(kv.Values);
                }

                parsed.Extensions[i] = new ExtensionSubtag { Type = type, Parts = newParts };
            }
            else if (type == 'u')
            {
                // U extension: sort keywords alphabetically
                var newParts = new List<string> { "u" };
                var attributes = new List<string>();
                var keywords = new List<KeyValueParts>();
                string? currentKey = null;
                var currentValues = new List<string>();

                for (var j = 1; j < parts.Count; j++)
                {
                    var part = parts[j];

                    // ukey is exactly 2 chars, both alpha
                    if (part.Length == 2 && char.IsLetter(part[0]) && char.IsLetter(part[1]) && currentKey == null && keywords.Count == 0 && attributes.Count == 0 && j == 1)
                    {
                        // Could be first keyword key
                        currentKey = part;
                    }
                    else if (part.Length == 2 && char.IsLetter(part[0]) && char.IsLetter(part[1]))
                    {
                        // This is a ukey
                        if (currentKey != null)
                        {
                            keywords.Add(new KeyValueParts { Key = currentKey, Values = currentValues });
                            currentValues = new List<string>();
                        }
                        currentKey = part;
                    }
                    else if (currentKey == null)
                    {
                        // Attribute (before any keywords)
                        attributes.Add(part);
                    }
                    else
                    {
                        // Part of uvalue
                        currentValues.Add(part);
                    }
                }

                // Save last keyword
                if (currentKey != null)
                {
                    keywords.Add(new KeyValueParts { Key = currentKey, Values = currentValues });
                }

                // Apply Unicode value aliasing from CLDR data
                foreach (var kw in keywords)
                {
                    if (LocaleData.UnicodeMappings.TryGetValue(kw.Key, out var valueAliases))
                    {
                        // First try looking up the full joined value (for hyphenated aliases like "ethiopic-amete-alem")
                        var fullValue = string.Join("-", kw.Values);
                        if (valueAliases.TryGetValue(fullValue, out var aliasedValue))
                        {
                            // Replace all parts with the new value (may also be hyphenated)
                            kw.Values.Clear();
                            foreach (var part in aliasedValue.Split('-'))
                            {
                                kw.Values.Add(part);
                            }
                        }
                        else
                        {
                            // Fall back to looking up individual parts
                            for (var k = 0; k < kw.Values.Count; k++)
                            {
                                if (valueAliases.TryGetValue(kw.Values[k], out aliasedValue))
                                {
                                    kw.Values[k] = aliasedValue;
                                }
                            }
                        }
                    }

                    // Per UTS 35 §3.2.1: Any type value "true" is removed
                    // This means if the only value is "true", just keep the key
                    kw.Values.RemoveAll(v => string.Equals(v, "true", StringComparison.OrdinalIgnoreCase));
                }

                // Add sorted attributes
                attributes.Sort(StringComparer.Ordinal);
                newParts.AddRange(attributes);

                // Sort keywords alphabetically by key
                keywords.Sort((a, b) => string.Compare(a.Key, b.Key, StringComparison.Ordinal));

                // Add sorted keywords
                foreach (var kv in keywords)
                {
                    newParts.Add(kv.Key);
                    newParts.AddRange(kv.Values);
                }

                parsed.Extensions[i] = new ExtensionSubtag { Type = type, Parts = newParts };
            }
        }

        // Sort extensions by singleton (t before u before x, etc.)
        parsed.Extensions.Sort((a, b) => a.Type.CompareTo(b.Type));
    }

    /// <summary>
    /// Builds a canonical BCP 47 tag from parsed components.
    /// </summary>
    private static string BuildCanonicalTag(ParsedLanguageTag parsed)
    {
        var result = new List<string>();

        // Language
        if (parsed.Language != null)
        {
            result.Add(parsed.Language);
        }

        // Script
        if (parsed.Script != null)
        {
            result.Add(parsed.Script);
        }

        // Region
        if (parsed.Region != null)
        {
            result.Add(parsed.Region);
        }

        // Variants (already sorted)
        if (parsed.Variants != null)
        {
            result.AddRange(parsed.Variants);
        }

        // Extensions (already sorted)
        if (parsed.Extensions != null)
        {
            foreach (var ext in parsed.Extensions)
            {
                result.AddRange(ext.Parts);
            }
        }

        return string.Join("-", result);
    }

    private sealed class ParsedLanguageTag
    {
        public string? Language { get; set; }
        public string? Script { get; set; }
        public string? Region { get; set; }
        public List<string>? Variants { get; set; }
        public List<ExtensionSubtag>? Extensions { get; set; }
    }

    private sealed class ExtensionSubtag
    {
        public char Type { get; set; }
        public List<string> Parts { get; set; } = new List<string>();
    }

    private sealed class KeyValueParts
    {
        public string Key { get; set; } = "";
        public List<string> Values { get; set; } = new List<string>();
    }

    /// <summary>
    /// https://tc39.es/ecma402/#sec-resolvelocale
    /// </summary>
    internal static ResolvedLocale ResolveLocale(
        Engine engine,
        HashSet<string> availableLocales,
        List<string> requestedLocales,
        JsValue options,
        string[] relevantExtensionKeys)
    {
        // 1. Let matcher be options.[[localeMatcher]].
        var matcher = options.IsObject() ? options.Get("localeMatcher").ToString() : "best fit";
        return ResolveLocaleCore(engine, availableLocales, requestedLocales, matcher, relevantExtensionKeys);
    }

    /// <summary>
    /// https://tc39.es/ecma402/#sec-resolvelocale
    /// Overload that accepts pre-read localeMatcher to avoid reading the option twice.
    /// </summary>
    internal static ResolvedLocale ResolveLocale(
        Engine engine,
        HashSet<string> availableLocales,
        List<string> requestedLocales,
        string localeMatcher,
        string[] relevantExtensionKeys)
    {
        return ResolveLocaleCore(engine, availableLocales, requestedLocales, localeMatcher, relevantExtensionKeys);
    }

    private static ResolvedLocale ResolveLocaleCore(
        Engine engine,
        HashSet<string> availableLocales,
        List<string> requestedLocales,
        string matcher,
        string[] relevantExtensionKeys)
    {

        // 2. If matcher is "lookup", let r be LookupMatcher(availableLocales, requestedLocales).
        // 3. Else let r be BestFitMatcher(availableLocales, requestedLocales).
        var matcherResult = string.Equals(matcher, "lookup", StringComparison.Ordinal)
            ? LookupMatcher(engine, availableLocales, requestedLocales)
            : BestFitMatcher(engine, availableLocales, requestedLocales);

        // 4. Let foundLocale be r.[[locale]].
        var foundLocale = matcherResult.Locale;

        // For now, return a simplified result
        // Full implementation would process extension keys
        return new ResolvedLocale(
            foundLocale,
            foundLocale, // dataLocale
            null // numberingSystem
        );
    }

    /// <summary>
    /// https://tc39.es/ecma402/#sec-lookupmatcher
    /// </summary>
    internal static MatcherResult LookupMatcher(Engine engine, HashSet<string> availableLocales, List<string> requestedLocales)
    {
        // 1. For each element locale of requestedLocales, do
        foreach (var locale in requestedLocales)
        {
            // a. Let noExtensionsLocale be the String value that is locale with any Unicode locale extension sequences removed.
            var noExtensionsLocale = RemoveUnicodeExtensions(locale);

            // b. Let availableLocale be BestAvailableLocale(availableLocales, noExtensionsLocale).
            var availableLocale = BestAvailableLocale(availableLocales, noExtensionsLocale);

            // c. If availableLocale is not undefined, return the Record { [[locale]]: availableLocale, [[extension]]: extension }.
            if (availableLocale != null)
            {
                return new MatcherResult(availableLocale, ExtractUnicodeExtension(locale));
            }
        }

        // 2. Return the Record { [[locale]]: defaultLocale }.
        return new MatcherResult(DefaultLocale(engine), null);
    }

    /// <summary>
    /// https://tc39.es/ecma402/#sec-bestfitmatcher
    /// For now, this delegates to LookupMatcher. A proper implementation would use more sophisticated matching.
    /// </summary>
    internal static MatcherResult BestFitMatcher(Engine engine, HashSet<string> availableLocales, List<string> requestedLocales)
    {
        // For now, use the same algorithm as LookupMatcher
        // A production implementation would use locale distance algorithms
        return LookupMatcher(engine, availableLocales, requestedLocales);
    }

    /// <summary>
    /// https://tc39.es/ecma402/#sec-bestavailablelocale
    /// </summary>
    internal static string? BestAvailableLocale(HashSet<string> availableLocales, string locale)
    {
        // 1. Let candidate be locale.
        var candidate = locale;

        // 2. Repeat
        while (true)
        {
            // a. If availableLocales contains candidate, return candidate.
            if (availableLocales.Contains(candidate))
            {
                return candidate;
            }

            // Also try matching via CultureInfo
            var culture = GetCultureInfo(candidate);
            if (culture != null)
            {
                var cultureName = culture.Name;
                if (availableLocales.Contains(cultureName))
                {
                    return cultureName;
                }
            }

            // Try expanded script form for Chinese regions
            // zh-TW → zh-Hant-TW, zh-HK → zh-Hant-HK, zh-CN → zh-Hans-CN, etc.
            if (candidate.StartsWith("zh-", StringComparison.OrdinalIgnoreCase) &&
                !candidate.Contains("-Hant", StringComparison.OrdinalIgnoreCase) &&
                !candidate.Contains("-Hans", StringComparison.OrdinalIgnoreCase))
            {
                var region = candidate.Substring(3);
                var isTraditional = string.Equals(region, "TW", StringComparison.OrdinalIgnoreCase) ||
                                    string.Equals(region, "HK", StringComparison.OrdinalIgnoreCase) ||
                                    string.Equals(region, "MO", StringComparison.OrdinalIgnoreCase);
                var script = isTraditional ? "Hant" : "Hans";
                var expandedCandidate = $"zh-{script}-{region}";
                if (availableLocales.Contains(expandedCandidate))
                {
                    return expandedCandidate;
                }
            }

            // b. Let pos be the character index of the last occurrence of "-" in candidate.
            var pos = candidate.LastIndexOf('-');

            // c. If pos is undefined, return undefined.
            if (pos == -1)
            {
                return null;
            }

            // d. If pos >= 2 and the character at index pos - 2 of candidate is "-", decrease pos by 2.
            if (pos >= 2 && candidate[pos - 2] == '-')
            {
                pos -= 2;
            }

            // e. Let candidate be the substring of candidate from position 0 to position pos.
            candidate = candidate.Substring(0, pos);
        }
    }

    /// <summary>
    /// Returns the default locale for the current environment.
    /// Uses the engine's configured culture if available, otherwise falls back to system culture.
    /// </summary>
    internal static string DefaultLocale(Engine? engine = null)
    {
        var culture = engine?.Options.Culture ?? CultureInfo.CurrentCulture;
        return string.IsNullOrEmpty(culture.Name) ? "en" : culture.Name;
    }

    /// <summary>
    /// Gets a set of available locales from .NET's CultureInfo.
    /// </summary>
    internal static HashSet<string> GetAvailableLocales() => AllCultures.Value;

    /// <summary>
    /// Removes Unicode locale extension sequences from a language tag.
    /// </summary>
    private static string RemoveUnicodeExtensions(string locale)
    {
        // Unicode extensions start with "-u-"
        var extensionIndex = locale.IndexOf("-u-", StringComparison.OrdinalIgnoreCase);
        if (extensionIndex == -1)
        {
            return locale;
        }

        // Find end of extension (next singleton or end of string)
        var endIndex = locale.Length;
        for (var i = extensionIndex + 3; i < locale.Length - 1; i++)
        {
            if (locale[i] == '-' && i + 2 < locale.Length && locale[i + 2] == '-')
            {
                // Found another singleton
                endIndex = i;
                break;
            }
        }

        if (endIndex < locale.Length)
        {
#if NET6_0_OR_GREATER
            return string.Concat(locale.AsSpan(0, extensionIndex), locale.AsSpan(endIndex));
#else
            return locale.Substring(0, extensionIndex) + locale.Substring(endIndex);
#endif
        }

        return locale.Substring(0, extensionIndex);
    }

    /// <summary>
    /// Extracts the Unicode extension from a locale tag.
    /// </summary>
    private static string? ExtractUnicodeExtension(string locale)
    {
        var extensionIndex = locale.IndexOf("-u-", StringComparison.OrdinalIgnoreCase);
        if (extensionIndex == -1)
        {
            return null;
        }

        var endIndex = locale.Length;
        for (var i = extensionIndex + 3; i < locale.Length - 1; i++)
        {
            if (locale[i] == '-' && i + 2 < locale.Length && locale[i + 2] == '-')
            {
                endIndex = i;
                break;
            }
        }

        return locale.Substring(extensionIndex + 1, endIndex - extensionIndex - 1);
    }

    /// <summary>
    /// Converts a BCP 47 language tag to a .NET CultureInfo.
    /// Returns null if the tag cannot be mapped.
    /// </summary>
    internal static CultureInfo? GetCultureInfo(string locale)
    {
        if (string.IsNullOrEmpty(locale))
        {
            return null;
        }

        try
        {
            // Remove Unicode extensions before creating CultureInfo
            var cultureTag = RemoveUnicodeExtensions(locale);
            // Use new CultureInfo() instead of GetCultureInfo() to get correct locale-specific
            // data. GetCultureInfo returns a cached read-only culture that may have different
            // NumberFormatInfo patterns (e.g., CurrencyNegativePattern).
            return new CultureInfo(cultureTag);
        }
        catch (CultureNotFoundException)
        {
            // Try parent locale (remove last subtag)
            var hyphenIndex = locale.LastIndexOf('-');
            if (hyphenIndex > 0)
            {
                return GetCultureInfo(locale.Substring(0, hyphenIndex));
            }

            return null;
        }
    }

    /// <summary>
    /// https://tc39.es/ecma402/#sec-coerceoptionstoobject
    /// </summary>
    internal static ObjectInstance CoerceOptionsToObject(Engine engine, JsValue options)
    {
        if (options.IsUndefined())
        {
            return ObjectInstance.OrdinaryObjectCreate(engine, null);
        }

        return TypeConverter.ToObject(engine.Realm, options);
    }

    /// <summary>
    /// https://tc39.es/ecma402/#sec-getoptionsobject
    /// Stricter than CoerceOptionsToObject - throws TypeError for non-object values.
    /// </summary>
    internal static ObjectInstance GetOptionsObject(Engine engine, JsValue options)
    {
        // 1. If options is undefined, return OrdinaryObjectCreate(null).
        if (options.IsUndefined())
        {
            return ObjectInstance.OrdinaryObjectCreate(engine, null);
        }

        // 2. If options is an Object, return options.
        if (options.IsObject())
        {
            return options.AsObject();
        }

        // 3. Throw a TypeError exception.
        Throw.TypeError(engine.Realm, "Options must be an object or undefined");
        return null!;
    }

    /// <summary>
    /// https://tc39.es/ecma402/#sec-getoption
    /// </summary>
    internal static JsValue GetOption(
        Engine engine,
        JsValue options,
        string property,
        OptionType type,
        JsValue[]? values,
        JsValue fallback)
    {
        // 1. Let value be ? Get(options, property).
        var value = options.Get(property);

        // 2. If value is undefined, return fallback.
        if (value.IsUndefined())
        {
            return fallback;
        }

        // 3. Convert value based on type
        value = type switch
        {
            OptionType.Boolean => TypeConverter.ToBoolean(value) ? JsBoolean.True : JsBoolean.False,
            OptionType.String => TypeConverter.ToJsString(value),
            _ => value
        };

        // 4. If values is not empty and value is not in values, throw RangeError
        if (values != null && values.Length > 0)
        {
            var found = false;
            foreach (var v in values)
            {
                if (JsValue.SameValue(value, v) || string.Equals(value.ToString(), v.ToString(), StringComparison.Ordinal))
                {
                    found = true;
                    value = v; // Use the canonical value from the list
                    break;
                }
            }

            if (!found)
            {
                Throw.RangeError(engine.Realm, $"Invalid value '{value}' for option '{property}'");
            }
        }

        return value;
    }

    /// <summary>
    /// https://tc39.es/ecma402/#sec-getbooleanoption
    /// </summary>
    internal static bool GetBooleanOption(Engine engine, JsValue options, string property, bool fallback)
    {
        var value = GetOption(engine, options, property, OptionType.Boolean, null, fallback ? JsBoolean.True : JsBoolean.False);
        return TypeConverter.ToBoolean(value);
    }

    /// <summary>
    /// https://tc39.es/ecma402/#sec-getstringorbooleanoption
    /// </summary>
    internal static JsValue GetStringOrBooleanOption(
        Engine engine,
        JsValue options,
        string property,
        JsValue[]? values,
        JsValue trueValue,
        JsValue falsyValue,
        JsValue fallback)
    {
        var value = options.Get(property);

        if (value.IsUndefined())
        {
            return fallback;
        }

        if (value.IsBoolean())
        {
            return TypeConverter.ToBoolean(value) ? trueValue : falsyValue;
        }

        var stringValue = TypeConverter.ToJsString(value);

        if (JsValue.SameValue(stringValue, new JsString("true")) || JsValue.SameValue(stringValue, new JsString("false")))
        {
            return fallback;
        }

        if (values != null && values.Length > 0)
        {
            var found = false;
            foreach (var v in values)
            {
                if (JsValue.SameValue(v, stringValue))
                {
                    found = true;
                    break;
                }
            }

            if (!found)
            {
                Throw.RangeError(engine.Realm, $"Invalid value '{stringValue}' for option '{property}'");
            }
        }

        return stringValue;
    }

    internal enum OptionType
    {
        Boolean,
        String
    }

    internal record struct MatcherResult(string Locale, string? Extension);

    internal record struct ResolvedLocale(string Locale, string DataLocale, string? NumberingSystem);
}
