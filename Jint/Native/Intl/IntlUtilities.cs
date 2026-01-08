using System.Globalization;
using System.Text.RegularExpressions;
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
        @"^[a-zA-Z]{2,8}(?:-[a-zA-Z0-9]{1,8})*$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant,
        TimeSpan.FromMilliseconds(100));

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

        // 2. If Type(locales) is String, then let O be CreateArrayFromList(« locales »)
        ObjectInstance o;
        if (locales.IsString())
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
    /// </summary>
    internal static bool IsStructurallyValidLanguageTag(string locale)
    {
        if (string.IsNullOrEmpty(locale))
        {
            return false;
        }

        // Simple validation - accept valid BCP 47 tags
        // For production, this should be more comprehensive
        return LanguageTagPattern.IsMatch(locale);
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
        // Extract and preserve unicode extensions (-u-...)
        string? unicodeExtension = null;
        var uIndex = locale.IndexOf("-u-", StringComparison.OrdinalIgnoreCase);
        if (uIndex >= 0)
        {
            // Find where the extension ends (at the next singleton or end of string)
            var extensionEnd = locale.Length;
            for (var i = uIndex + 3; i < locale.Length - 1; i++)
            {
                if (locale[i] == '-' && i + 2 < locale.Length && locale[i + 2] == '-')
                {
                    // Found another singleton (single char followed by -)
                    extensionEnd = i;
                    break;
                }
            }
            unicodeExtension = locale.Substring(uIndex, extensionEnd - uIndex);
            var basePart = locale.Substring(0, uIndex);
            var suffixPart = extensionEnd < locale.Length ? locale.Substring(extensionEnd) : "";
            locale = basePart + suffixPart;
        }

        // Try to map to a CultureInfo and back to get canonical form
        string canonicalBase;
        try
        {
            var culture = GetCultureInfo(locale);
            if (culture != null && !string.IsNullOrEmpty(culture.Name))
            {
                // Use the culture's name which is the canonical BCP 47 tag
                canonicalBase = culture.Name;
            }
            else
            {
                canonicalBase = CanonicalizeLanguageTag(locale);
            }
        }
        catch
        {
            // Fall through to manual canonicalization
            canonicalBase = CanonicalizeLanguageTag(locale);
        }

        // Re-append unicode extension if present
        if (unicodeExtension != null)
        {
            return canonicalBase + unicodeExtension.ToLowerInvariant();
        }

        return canonicalBase;
    }

    /// <summary>
    /// Manually canonicalize a language tag according to BCP 47 rules.
    /// </summary>
    private static string CanonicalizeLanguageTag(string tag)
    {
        var parts = tag.Split('-');
        if (parts.Length == 0)
        {
            return tag;
        }

        var result = new List<string>();

        // Language subtag (first part) - lowercase
        result.Add(parts[0].ToLowerInvariant());

        for (var i = 1; i < parts.Length; i++)
        {
            var part = parts[i];

            if (part.Length == 4 && char.IsLetter(part[0]))
            {
                // Script subtag - title case
                result.Add(char.ToUpperInvariant(part[0]) + part.Substring(1).ToLowerInvariant());
            }
            else if (part.Length == 2 && char.IsLetter(part[0]))
            {
                // Region subtag (2 letters) - uppercase
                result.Add(part.ToUpperInvariant());
            }
            else if (part.Length == 3 && char.IsDigit(part[0]))
            {
                // Region subtag (3 digits) - as is
                result.Add(part);
            }
            else
            {
                // Singleton, variant, or extension subtag - lowercase
                result.Add(part.ToLowerInvariant());
            }
        }

        return string.Join("-", result);
    }

    /// <summary>
    /// https://tc39.es/ecma402/#sec-resolvelocale
    /// </summary>
    internal static ResolvedLocale ResolveLocale(
        Engine engine,
        IReadOnlyCollection<string> availableLocales,
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
        IReadOnlyCollection<string> availableLocales,
        List<string> requestedLocales,
        string localeMatcher,
        string[] relevantExtensionKeys)
    {
        return ResolveLocaleCore(engine, availableLocales, requestedLocales, localeMatcher, relevantExtensionKeys);
    }

    private static ResolvedLocale ResolveLocaleCore(
        Engine engine,
        IReadOnlyCollection<string> availableLocales,
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
    internal static MatcherResult LookupMatcher(Engine engine, IReadOnlyCollection<string> availableLocales, List<string> requestedLocales)
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
    internal static MatcherResult BestFitMatcher(Engine engine, IReadOnlyCollection<string> availableLocales, List<string> requestedLocales)
    {
        // For now, use the same algorithm as LookupMatcher
        // A production implementation would use locale distance algorithms
        return LookupMatcher(engine, availableLocales, requestedLocales);
    }

    /// <summary>
    /// https://tc39.es/ecma402/#sec-bestavailablelocale
    /// </summary>
    internal static string? BestAvailableLocale(IReadOnlyCollection<string> availableLocales, string locale)
    {
        // 1. Let candidate be locale.
        var candidate = locale;

        // 2. Repeat
        while (true)
        {
            // a. If availableLocales contains candidate, return candidate.
            if (ContainsLocale(availableLocales, candidate))
            {
                return candidate;
            }

            // Also try matching via CultureInfo
            var culture = GetCultureInfo(candidate);
            if (culture != null)
            {
                var cultureName = culture.Name;
                if (ContainsLocale(availableLocales, cultureName))
                {
                    return cultureName;
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

    private static bool ContainsLocale(IReadOnlyCollection<string> locales, string locale)
    {
        foreach (var l in locales)
        {
            if (string.Equals(l, locale, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
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
    internal static HashSet<string> GetAvailableLocales()
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
    }

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
            return CultureInfo.GetCultureInfo(cultureTag);
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
        switch (type)
        {
            case OptionType.Boolean:
                value = TypeConverter.ToBoolean(value) ? JsBoolean.True : JsBoolean.False;
                break;

            case OptionType.String:
                value = TypeConverter.ToJsString(value);
                break;
        }

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
