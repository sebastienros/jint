using System.Buffers;
using System.Globalization;
using Jint.Native.Function;
using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Descriptors;

namespace Jint.Native.Intl;

/// <summary>
/// https://tc39.es/ecma402/#sec-intl-locale-constructor
/// </summary>
internal sealed class LocaleConstructor : Constructor
{
    private static readonly JsString _functionName = new("Locale");
    private static readonly StringSearchValues CaseFirstValues = new(["upper", "lower", "false"], StringComparison.Ordinal);
    private static readonly StringSearchValues HourCycleValues = new(["h11", "h12", "h23", "h24"], StringComparison.Ordinal);

    public LocaleConstructor(
        Engine engine,
        Realm realm,
        FunctionPrototype functionPrototype,
        ObjectPrototype objectPrototype) : base(engine, realm, _functionName)
    {
        _prototype = functionPrototype;
        PrototypeObject = new LocalePrototype(engine, realm, this, objectPrototype);
        _length = new PropertyDescriptor(1, PropertyFlag.Configurable);
        _prototypeDescriptor = new PropertyDescriptor(PrototypeObject, PropertyFlag.AllForbidden);
    }

    private LocalePrototype PrototypeObject { get; }

    /// <summary>
    /// https://tc39.es/ecma402/#sec-Intl.Locale
    /// </summary>
    public override ObjectInstance Construct(JsCallArguments arguments, JsValue newTarget)
    {
        if (newTarget.IsUndefined())
        {
            Throw.TypeError(_realm, "Intl.Locale must be called with 'new'");
        }

        var tag = arguments.At(0);
        var options = arguments.At(1);

        // 1. If tag is not a String and tag is not an Object, throw a TypeError.
        if (!tag.IsString() && !tag.IsObject())
        {
            Throw.TypeError(_realm, "First argument to Intl.Locale must be a string or Locale object");
        }

        string tagString;

        // 2. If Type(tag) is Object and tag has an [[InitializedLocale]] internal slot, then
        if (tag is JsLocale existingLocale)
        {
            tagString = existingLocale.Locale;
        }
        else
        {
            // 3. Else, let tag be ? ToString(tag).
            tagString = TypeConverter.ToString(tag);
        }

        // 4. If ! IsStructurallyValidLanguageTag(tag) is false, throw a RangeError.
        if (!IntlUtilities.IsStructurallyValidLanguageTag(tagString))
        {
            Throw.RangeError(_realm, $"Invalid language tag: {tagString}");
        }

        // Check for grandfathered tags before parsing
        tagString = CanonicalizeGrandfatheredTag(tagString);

        // 5. Let options be ? CoerceOptionsToObject(options).
        var optionsObj = IntlUtilities.CoerceOptionsToObject(_engine, options);

        // Parse the tag
        var parsedLocale = ParseLanguageTag(tagString);

        // Apply options - read in spec-defined order:
        // 1. Language ID components: language, script, region, variants
        // 2. Unicode extensions in key order: ca, co, fw, hc, kf, kn, nu
        var language = GetLanguageOption(optionsObj, parsedLocale.Language);
        var script = GetScriptOption(optionsObj, parsedLocale.Script);
        var region = GetRegionOption(optionsObj, parsedLocale.Region);
        var variants = GetVariantsOption(optionsObj, parsedLocale.Variants);

        // Check if the combination of language+variants forms a grandfathered tag
        // e.g., "cel" + variants=["gaulish"] should become "xtg"
        var combinedTag = BuildBaseName(language, script, region, variants);
        var canonicalizedCombined = CanonicalizeGrandfatheredTag(combinedTag);
        if (!string.Equals(combinedTag, canonicalizedCombined, StringComparison.Ordinal))
        {
            // The combined tag was a grandfathered tag, re-parse it
            var reparsed = ParseLanguageTag(canonicalizedCombined);
            language = reparsed.Language;
            script = reparsed.Script;
            region = reparsed.Region;
            variants = reparsed.Variants;
        }

        // Apply language+variant mappings for grandfathered variants (e.g., art+lojban → jbo)
        // This handles cases like "art-lojban-fonipa" which should become "jbo-fonipa"
        ApplyLanguageVariantMappings(ref language, ref variants);

        // Apply variant aliasing (e.g., arevela → language:hy, aaland → region:AX)
        ApplyVariantMappings(ref language, ref script, ref region, ref variants);

        var calendar = GetUnicodeExtensionOption(optionsObj, "calendar", "ca", parsedLocale.Calendar);
        var collation = GetUnicodeExtensionOption(optionsObj, "collation", "co", parsedLocale.Collation);
        var firstDayOfWeek = GetFirstDayOfWeekOption(optionsObj, parsedLocale.FirstDayOfWeek);
        var hourCycle = GetOptionString(optionsObj, "hourCycle", parsedLocale.HourCycle, in HourCycleValues);
        var caseFirst = GetOptionString(optionsObj, "caseFirst", parsedLocale.CaseFirst, in CaseFirstValues);
        var numericValue = IntlUtilities.GetOption(_engine, optionsObj, "numeric", IntlUtilities.OptionType.Boolean, null, JsValue.Undefined);
        bool? numeric = numericValue.IsUndefined() ? parsedLocale.Numeric : TypeConverter.ToBoolean(numericValue);
        var numberingSystem = GetUnicodeExtensionOption(optionsObj, "numberingSystem", "nu", parsedLocale.NumberingSystem);

        // Build the canonical locale string
        var canonicalLocale = BuildLocaleString(language, script, region, variants, parsedLocale.Attributes, calendar, caseFirst, collation, firstDayOfWeek, hourCycle, numberingSystem, numeric, parsedLocale.OtherUnicodeExtensions, parsedLocale.OtherExtensions);
        var baseName = BuildBaseName(language, script, region, variants);

        // Get CultureInfo (without variants for .NET compatibility)
        var cultureBaseName = BuildBaseName(language, script, region);
        var cultureInfo = IntlUtilities.GetCultureInfo(cultureBaseName) ?? CultureInfo.InvariantCulture;

        // Get prototype from newTarget (for cross-realm construction)
        var proto = GetPrototypeFromConstructor(newTarget, static intrinsics => intrinsics.Locale.PrototypeObject);

        return new JsLocale(
            _engine,
            proto,
            canonicalLocale,
            baseName,
            language!,
            script,
            region,
            variants.ToArray(),
            calendar,
            caseFirst,
            collation,
            hourCycle,
            numberingSystem,
            numeric,
            firstDayOfWeek,
            cultureInfo);
    }

    private string? GetOptionString(ObjectInstance options, string property, string? fallback, in StringSearchValues allowedValues)
    {
        var value = options.Get(property);
        if (value.IsUndefined())
        {
            return fallback;
        }

        var stringValue = TypeConverter.ToString(value);

        if (!allowedValues.Contains(stringValue))
        {
            Throw.RangeError(_realm, $"Invalid value '{stringValue}' for option '{property}'");
        }

        return stringValue;
    }

    /// <summary>
    /// Reads the option for a Unicode extension key, checks it against the <c>type</c> nonterminal and
    /// returns it in canonical form. https://tc39.es/ecma402/#sec-makelocalerecord (15.1.3) is where an
    /// option supersedes the tag's own keyword, and it does so through
    /// <c>CanonicalizeUValue(key, overrideValue)</c> — so the extension sequence Intl.Locale builds
    /// carries the canonical spelling of the value and never the one the caller happened to type.
    /// The local canonicalization this used to do covered only the CLDR alias substitution and never
    /// the case fold that precedes it, and its "collation" arm could not fire at all because
    /// <c>LocaleData.UnicodeMappings</c> has no "co" table.
    /// </summary>
    private string? GetUnicodeExtensionOption(ObjectInstance options, string property, string unicodeKey, string? fallback)
    {
        var value = options.Get(property);
        if (value.IsUndefined())
        {
            // The fallback comes from the parsed tag, which the parser has already lowercased and
            // aliased; running it through again is idempotent and keeps the two sources in one shape.
            return fallback != null ? IntlUtilities.CanonicalizeUValue(unicodeKey, fallback) : null;
        }

        var stringValue = TypeConverter.ToString(value);

        // Validate against pattern: (3*8alphanum) *("-" (3*8alphanum))
        if (!IsValidUnicodeExtensionValue(stringValue))
        {
            Throw.RangeError(_realm, $"Invalid value '{stringValue}' for option '{property}'");
        }

        return IntlUtilities.CanonicalizeUValue(unicodeKey, stringValue);
    }

    /// <summary>
    /// Validates that a string matches the Unicode extension value pattern: (3*8alphanum) *("-" (3*8alphanum))
    /// </summary>
    private static bool IsValidUnicodeExtensionValue(string value)
    {
        return IntlUtilities.IsValidUnicodeExtensionValue(value);
    }

    /// <summary>
    /// Gets and validates the language option.
    /// Language must be 2-3 letters, 4 letters (reserved), or 5-8 letters.
    /// </summary>
    private string? GetLanguageOption(ObjectInstance options, string? fallback)
    {
        var value = options.Get("language");
        if (value.IsUndefined())
        {
            return fallback;
        }

        var stringValue = TypeConverter.ToString(value);

        // Validate language production:
        // language = 2*3ALPHA / 4ALPHA / 5*8ALPHA
        // Must be pure ASCII letters only, no hyphens
        if (!IsValidLanguageSubtag(stringValue))
        {
            Throw.RangeError(_realm, $"Invalid value '{stringValue}' for option 'language'");
        }

        return CanonicalizeLanguage(stringValue.ToLowerInvariant());
    }

    /// <summary>
    /// Gets and validates the region option.
    /// Region must be 2 letters or 3 digits.
    /// Applies region aliasing (e.g., 554 → NZ).
    /// </summary>
    private string? GetRegionOption(ObjectInstance options, string? fallback)
    {
        var value = options.Get("region");
        if (value.IsUndefined())
        {
            return fallback;
        }

        var stringValue = TypeConverter.ToString(value);

        // Validate region production: 2ALPHA / 3DIGIT
        if (!IsValidRegionSubtag(stringValue))
        {
            Throw.RangeError(_realm, $"Invalid value '{stringValue}' for option 'region'");
        }

        var region = stringValue.ToUpperInvariant();

        // Apply region aliasing (e.g., 554 → NZ)
        if (Data.LocaleData.RegionMappings.TryGetValue(region, out var replacement))
        {
            region = replacement;
        }

        return region;
    }

    /// <summary>
    /// Gets and validates the script option.
    /// Script must be exactly 4 letters.
    /// </summary>
    private string? GetScriptOption(ObjectInstance options, string? fallback)
    {
        var value = options.Get("script");
        if (value.IsUndefined())
        {
            return fallback;
        }

        var stringValue = TypeConverter.ToString(value);

        // Validate script production: 4ALPHA
        if (!IsValidScriptSubtag(stringValue))
        {
            Throw.RangeError(_realm, $"Invalid value '{stringValue}' for option 'script'");
        }

        // Title case: first letter uppercase, rest lowercase
        return char.ToUpperInvariant(stringValue[0]) + stringValue.Substring(1).ToLowerInvariant();
    }

    /// <summary>
    /// Validates a language subtag per UTS 35: 2-3 letters or 5-8 letters.
    /// 4-letter subtags are reserved and NOT valid for the language option.
    /// "root" is not a valid Unicode BCP 47 language subtag.
    /// </summary>
    private static bool IsValidLanguageSubtag(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return false;
        }

        // Per UTS 35: unicode_language_subtag = alpha{2,3} | alpha{5,8}
        // 4-letter subtags are reserved and not valid
        if (value.Length < 2 || value.Length == 4 || value.Length > 8)
        {
            return false;
        }

        // Must be all ASCII letters (no hyphens, digits, etc.)
        foreach (var c in value)
        {
            if (!char.IsAsciiLetter(c))
            {
                return false;
            }
        }

        // "root" is not a valid Unicode BCP 47 language subtag
        if (string.Equals(value, "root", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return true;
    }

    /// <summary>
    /// Validates a region subtag: 2 letters or 3 digits.
    /// </summary>
    private static bool IsValidRegionSubtag(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return false;
        }

        // 2 letters
        if (value.Length == 2)
        {
            return char.IsAsciiLetter(value[0]) && char.IsAsciiLetter(value[1]);
        }

        // 3 digits
        if (value.Length == 3)
        {
            return char.IsAsciiDigit(value[0]) && char.IsAsciiDigit(value[1]) && char.IsAsciiDigit(value[2]);
        }

        return false;
    }

    /// <summary>
    /// Validates a script subtag: exactly 4 letters.
    /// </summary>
    private static bool IsValidScriptSubtag(string value)
    {
        if (string.IsNullOrEmpty(value) || value.Length != 4)
        {
            return false;
        }

        foreach (var c in value)
        {
            if (!char.IsAsciiLetter(c))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Gets and validates the variants option.
    /// The option value is a string of hyphen-separated variant subtags.
    /// Variants are sorted alphabetically for canonicalization.
    /// </summary>
    private List<string> GetVariantsOption(ObjectInstance options, List<string> fallback)
    {
        var value = options.Get("variants");
        if (value.IsUndefined())
        {
            // Sort fallback variants for canonicalization
            var sortedFallback = new List<string>(fallback);
            sortedFallback.Sort(StringComparer.Ordinal);
            return sortedFallback;
        }

        var stringValue = TypeConverter.ToString(value);

        // Empty string is invalid
        if (string.IsNullOrEmpty(stringValue))
        {
            Throw.RangeError(_realm, "Invalid variants option: empty string");
        }

        // Check for leading/trailing dashes
        if (stringValue[0] == '-' || stringValue[stringValue.Length - 1] == '-')
        {
            Throw.RangeError(_realm, $"Invalid variants option: {stringValue}");
        }

        // Check for double dashes
        if (stringValue.Contains("--", StringComparison.Ordinal))
        {
            Throw.RangeError(_realm, $"Invalid variants option: {stringValue}");
        }

        // Parse and validate variants
        var variants = new List<string>();
        var seenVariants = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var parts = stringValue.Split('-');

        foreach (var part in parts)
        {
            if (string.IsNullOrEmpty(part))
            {
                Throw.RangeError(_realm, $"Invalid variants option: {stringValue}");
            }

            var lowerPart = part.ToLowerInvariant();

            // Validate variant: 5-8 alphanumeric or 4 chars starting with digit
            if (!IsVariantSubtag(lowerPart))
            {
                Throw.RangeError(_realm, $"Invalid variant subtag: {part}");
            }

            // Check for duplicates (case-insensitive)
            if (seenVariants.Contains(lowerPart))
            {
                Throw.RangeError(_realm, $"Duplicate variant subtag: {part}");
            }

            seenVariants.Add(lowerPart);
            variants.Add(lowerPart);
        }

        // Sort variants alphabetically for canonicalization
        variants.Sort(StringComparer.Ordinal);

        return variants;
    }

    /// <summary>
    /// https://tc39.es/ecma402/#sec-Intl.Locale steps 22-24 — the <c>firstDayOfWeek</c> option is read
    /// with <c>GetOption(options, "firstDayOfWeek", string, empty, undefined)</c>, so every value is
    /// coerced with ToString before anything inspects it; the result is mapped by
    /// https://tc39.es/ecma402/#sec-weekdaytouvalue and then checked against the <c>type</c>
    /// Unicode locale nonterminal.
    /// </summary>
    /// <remarks>
    /// Reading the Number before the String was the defect. A numeric option was truncated to an
    /// <c>int</c>, so <c>0.5</c> and <c>Number.MIN_VALUE</c> both resolved to <c>"sun"</c> where the
    /// spec rejects them, <c>NaN</c> resolved to <c>"sun"</c> rather than to <c>"nan"</c>, and
    /// <c>Infinity</c> resolved to the digits of <see cref="int.MaxValue"/> rather than to
    /// <c>"infinity"</c> — all of which fall out correctly once ToString runs first. The boolean arm
    /// went the same way: <c>true</c> is now the ordinary string <c>"true"</c>, which UTS #35 Annex C
    /// removes in <see cref="IntlUtilities.CanonicalizeUValue"/>, so it needs no special case and
    /// <c>"TRUE"</c> reaches the same answer.
    /// </remarks>
    private string? GetFirstDayOfWeekOption(ObjectInstance options, string? fallback)
    {
        var value = options.Get("firstDayOfWeek");
        if (value.IsUndefined())
        {
            return fallback;
        }

        var stringValue = WeekdayToUValue(TypeConverter.ToString(value));

        // The type nonterminal: (3*8alphanum) *("-" (3*8alphanum)), ASCII-only. The local copy this
        // replaces used char.IsLetterOrDigit, which answers for the whole of Unicode, so a value of
        // three accented Latin letters, or of a CJK ideograph followed by "bc", was accepted as
        // well-formed instead of raising a RangeError.
        if (!IntlUtilities.IsValidUnicodeExtensionValue(stringValue))
        {
            Throw.RangeError(_realm, $"Invalid value '{stringValue}' for option 'firstDayOfWeek'");
        }

        return IntlUtilities.CanonicalizeUValue("fw", stringValue);
    }

    /// <summary>
    /// https://tc39.es/ecma402/#sec-weekdaytouvalue — the Unicode First Day Identifier for a
    /// String that numerically names a day of the week, and every other String unmodified.
    /// </summary>
    private static string WeekdayToUValue(string fw)
    {
        return fw switch
        {
            "1" => "mon",
            "2" => "tue",
            "3" => "wed",
            "4" => "thu",
            "5" => "fri",
            "6" => "sat",
            "0" or "7" => "sun",
            _ => fw
        };
    }

    private static ParsedLocale ParseLanguageTag(string tag)
    {
        var result = new ParsedLocale();
        var parts = tag.Split('-');

        if (parts.Length == 0)
        {
            return result;
        }

        var index = 0;

        // Language (required, 2-3 or 4-8 letters)
        if (index < parts.Length && parts[index].Length >= 2 && parts[index].Length <= 8 && IsAllLetters(parts[index]))
        {
            result.Language = CanonicalizeLanguage(parts[index].ToLowerInvariant());
            index++;
        }

        // Script (optional, 4 letters)
        if (index < parts.Length && parts[index].Length == 4 && IsAllLetters(parts[index]))
        {
            result.Script = char.ToUpperInvariant(parts[index][0]) + parts[index].Substring(1).ToLowerInvariant();
            index++;
        }

        // Region (optional, 2 letters or 3 digits)
        if (index < parts.Length && ((parts[index].Length == 2 && IsAllLetters(parts[index])) ||
                                     (parts[index].Length == 3 && IsAllDigits(parts[index]))))
        {
            var region = parts[index].ToUpperInvariant();

            // Apply script-sensitive region aliasing first (e.g., Armn + SU → AM)
            var scriptRegionResolved = false;
            if (result.Script != null)
            {
                var scriptRegionKey = result.Script + "+" + region;
                if (Data.LocaleData.ScriptRegionMappings.TryGetValue(scriptRegionKey, out var scriptRegionReplacement))
                {
                    region = scriptRegionReplacement;
                    scriptRegionResolved = true;
                }
            }

            // Fall back to simple region aliasing (e.g., numeric codes like 554 → NZ)
            if (!scriptRegionResolved && Data.LocaleData.RegionMappings.TryGetValue(region, out var regionReplacement))
            {
                region = regionReplacement;
            }

            result.Region = region;
            index++;
        }

        // Variants (optional, 5-8 alphanum or 4 chars starting with digit)
        while (index < parts.Length && IsVariantSubtag(parts[index]))
        {
            result.Variants.Add(parts[index].ToLowerInvariant());
            index++;
        }

        // Parse extensions
        while (index < parts.Length)
        {
            if (parts[index].Length == 1)
            {
                var singleton = char.ToLowerInvariant(parts[index][0]);
                if (singleton == 'u')
                {
                    // Unicode extension
                    index++;

                    // First, collect any attributes (3-8 alphanumeric parts before any key)
                    while (index < parts.Length
                           && !UnicodeExtension.IsKey(parts[index].AsSpan())
                           && parts[index].Length >= 3
                           && parts[index].Length <= 8)
                    {
                        result.Attributes.Add(parts[index].ToLowerInvariant());
                        index++;
                    }

                    // Then process key-value pairs
                    while (index < parts.Length && parts[index].Length != 1)
                    {
                        // UnicodeExtension owns what a key looks like, so this parser and the readers the
                        // formatters use cannot drift apart about where one keyword ends and the next begins.
                        if (!UnicodeExtension.IsKey(parts[index].AsSpan()))
                        {
                            // Unexpected format - skip
                            index++;
                            continue;
                        }

                        var key = parts[index].ToLowerInvariant();
                        index++;

                        // A keyword's value runs to the next key, so every subtag until then belongs to
                        // it. That rule is UnicodeExtension's, and it now applies to every key alike:
                        // "hc", "kf" and "kn" used to read a single subtag while "ca", "co" and "nu"
                        // collected the whole run.
                        string? value = null;
                        if (index < parts.Length && parts[index].Length >= 3 && parts[index].Length <= 8)
                        {
                            var firstValuePart = parts[index].ToLowerInvariant();
                            index++;
                            value = CollectMultiPartValue(parts, ref index, firstValuePart);
                        }

                        // https://tc39.es/ecma402/#sec-Intl.Locale step 13 canonicalizes the whole tag
                        // before a single option is read, and that carries UTS #35 Annex C down to each
                        // keyword's value: the bcp47 aliases, and the removal of a value of "true". This
                        // parser only ASCII-lowercased, so "en-u-kb-yes" kept its "yes" and every
                        // "-true" survived on every key the switch below does not name. A key written
                        // with no value at all is already in that form, which is the empty string.
                        var canonicalValue = value is null ? "" : IntlUtilities.CanonicalizeUValue(key, value);

                        // Duplicate keys: the first occurrence wins, so every assignment below is
                        // conditional on the field still being absent — and absent is null, because the
                        // empty string is now a value a tag can legitimately carry.
                        var handled = true;
                        switch (key)
                        {
                            case "ca":
                                result.Calendar ??= canonicalValue;
                                break;
                            case "co":
                                result.Collation ??= canonicalValue;
                                break;
                            case "fw":
                                result.FirstDayOfWeek ??= canonicalValue;
                                break;
                            case "hc":
                                result.HourCycle ??= canonicalValue;
                                break;
                            case "kf":
                                result.CaseFirst ??= canonicalValue;
                                break;
                            case "kn":
                                // https://tc39.es/ecma402/#sec-Intl.Locale sets [[Numeric]] to true
                                // when the record's [[kn]] is "true" or the empty String. The
                                // canonicalization above has already turned "true" — and the "yes" the
                                // bcp47 data aliases to it — into the empty string.
                                result.Numeric ??= canonicalValue.Length == 0;
                                break;
                            case "nu":
                                result.NumberingSystem ??= canonicalValue;
                                break;
                            default:
                                handled = false;
                                break;
                        }

                        // Store unhandled unicode extension key/value pairs
                        if (!handled)
                        {
                            result.OtherUnicodeExtensions.Add(
                                canonicalValue.Length == 0 ? key : key + "-" + canonicalValue);
                        }
                    }
                }
                else
                {
                    // Other extension (a-w, y) or private use (x)
                    index++; // Skip the singleton

                    var extParts = new List<string>();
                    if (singleton == 'x')
                    {
                        // Private use extension: consume ALL remaining parts
                        // (in private use, single-char parts are data, not singletons)
                        while (index < parts.Length)
                        {
                            extParts.Add(parts[index].ToLowerInvariant());
                            index++;
                        }
                    }
                    else
                    {
                        // Regular extension: collect until next singleton
                        while (index < parts.Length && parts[index].Length != 1)
                        {
                            extParts.Add(parts[index].ToLowerInvariant());
                            index++;
                        }
                    }

                    // Store this extension as (singleton, content)
                    result.OtherExtensions.Add(new ExtensionEntry(singleton, string.Join('-', extParts)));
                }
            }
            else
            {
                index++;
            }
        }

        return result;
    }

    private static bool IsAllLetters(string s)
    {
        foreach (var c in s)
        {
            if (!char.IsLetter(c))
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsAllDigits(string s)
    {
        foreach (var c in s)
        {
            if (!char.IsDigit(c))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Checks if a string is a valid variant subtag.
    /// Variant subtags are 5-8 alphanumerics OR 4 chars starting with a digit.
    /// </summary>
    private static bool IsVariantSubtag(string s)
    {
        if (string.IsNullOrEmpty(s))
        {
            return false;
        }

        // Single character is an extension singleton, not a variant
        if (s.Length == 1)
        {
            return false;
        }

        // 4 characters starting with digit
        if (s.Length == 4 && char.IsDigit(s[0]))
        {
            return IsAllAlphanumeric(s);
        }

        // 5-8 alphanumeric characters
        if (s.Length >= 5 && s.Length <= 8)
        {
            return IsAllAlphanumeric(s);
        }

        return false;
    }

    private static bool IsAllAlphanumeric(string s)
    {
        foreach (var c in s)
        {
            if (!char.IsLetterOrDigit(c))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Gets the key (first 2-char subtag) from a Unicode extension part like "ca-gregory" or "kn".
    /// </summary>
    /// <summary>
    /// Appends one Unicode extension keyword, as the bare key when its value is present but empty.
    /// </summary>
    private static void AddKeyword(List<string> unicodeExtParts, string key, string? value)
    {
        if (value is null)
        {
            return;
        }

        unicodeExtParts.Add(value.Length == 0 ? key : key + "-" + value);
    }

    private static string GetUnicodeExtensionKey(string part)
    {
        var dashIndex = part.IndexOf('-');
        return dashIndex > 0 ? part.Substring(0, dashIndex) : part;
    }

    /// <summary>
    /// Collects multi-part Unicode extension values (e.g., "islamic-civil" for calendar).
    /// Values are 3-8 alphanumeric characters, and 2-char parts indicate a new key.
    /// </summary>
    private static string CollectMultiPartValue(string[] parts, ref int index, string firstValue)
    {
        var valueParts = new List<string> { firstValue };

        while (index < parts.Length && parts[index].Length >= 3 && parts[index].Length <= 8)
        {
            // A 2-char part would be a new key, so stop
            if (parts[index].Length == 2)
            {
                break;
            }
            // A 1-char part is a singleton (next extension), so stop
            if (parts[index].Length == 1)
            {
                break;
            }
            valueParts.Add(parts[index].ToLowerInvariant());
            index++;
        }

        return string.Join('-', valueParts);
    }

    private static string BuildBaseName(string? language, string? script, string? region, List<string>? variants = null)
    {
        var parts = new List<string>();

        if (!string.IsNullOrEmpty(language))
        {
            parts.Add(language!);
        }

        if (!string.IsNullOrEmpty(script))
        {
            parts.Add(script!);
        }

        if (!string.IsNullOrEmpty(region))
        {
            parts.Add(region!);
        }

        if (variants != null)
        {
            parts.AddRange(variants);
        }

        return string.Join('-', parts);
    }

    private static string BuildLocaleString(
        string? language,
        string? script,
        string? region,
        List<string>? variants,
        List<string>? attributes,
        string? calendar,
        string? caseFirst,
        string? collation,
        string? firstDayOfWeek,
        string? hourCycle,
        string? numberingSystem,
        bool? numeric,
        List<string>? otherUnicodeExtensions = null,
        List<ExtensionEntry>? otherExtensions = null)
    {
        var baseName = BuildBaseName(language, script, region, variants);

        // Collect all extensions for sorting
        var allExtensions = new List<ExtensionEntry>();

        // Build Unicode extension content if any options are set
        var unicodeExtParts = new List<string>();

        // Add sorted attributes first (they come before key-value pairs)
        if (attributes != null && attributes.Count > 0)
        {
            var sortedAttributes = new List<string>(attributes);
            sortedAttributes.Sort(StringComparer.Ordinal);
            unicodeExtParts.AddRange(sortedAttributes);
        }

        // Add key-value pairs. https://tc39.es/ecma402/#sec-insert-unicode-extension-and-canonicalize
        // (9.2.9) appends a keyword's value only when that value is not the empty String, so a key
        // whose value UTS #35 Annex C removed - "en-u-ca-true", or "en-u-kb-yes" through its alias -
        // is written as the bare key. Absent is null; present-and-empty is the empty string, which is
        // why none of these test IsNullOrEmpty.
        AddKeyword(unicodeExtParts, "ca", calendar);
        AddKeyword(unicodeExtParts, "co", collation);
        AddKeyword(unicodeExtParts, "fw", firstDayOfWeek);
        AddKeyword(unicodeExtParts, "hc", hourCycle);
        AddKeyword(unicodeExtParts, "kf", caseFirst);

        if (numeric.HasValue)
        {
            if (numeric.Value)
            {
                unicodeExtParts.Add("kn");
            }
            else
            {
                unicodeExtParts.Add("kn-false");
            }
        }

        AddKeyword(unicodeExtParts, "nu", numberingSystem);

        // Add other unicode extensions that were not recognized
        if (otherUnicodeExtensions != null && otherUnicodeExtensions.Count > 0)
        {
            unicodeExtParts.AddRange(otherUnicodeExtensions);
        }

        // Sort Unicode extension key-value pairs alphabetically by key
        var attrCount = (attributes?.Count ?? 0);
        if (unicodeExtParts.Count > attrCount)
        {
            var keyValuePairs = unicodeExtParts.GetRange(attrCount, unicodeExtParts.Count - attrCount);
            keyValuePairs.Sort((a, b) =>
            {
                var keyA = GetUnicodeExtensionKey(a);
                var keyB = GetUnicodeExtensionKey(b);
                return string.Compare(keyA, keyB, StringComparison.Ordinal);
            });
            unicodeExtParts.RemoveRange(attrCount, unicodeExtParts.Count - attrCount);
            unicodeExtParts.AddRange(keyValuePairs);
        }

        // Add the Unicode extension to the list if it has content
        if (unicodeExtParts.Count > 0)
        {
            allExtensions.Add(new ExtensionEntry('u', string.Join('-', unicodeExtParts)));
        }

        // Add other extensions
        if (otherExtensions != null)
        {
            allExtensions.AddRange(otherExtensions);
        }

        // Sort extensions: alphabetically by singleton, but 'x' (private use) always comes last
        allExtensions.Sort((a, b) =>
        {
            if (a.Singleton == 'x' && b.Singleton != 'x') return 1;
            if (b.Singleton == 'x' && a.Singleton != 'x') return -1;
            return a.Singleton.CompareTo(b.Singleton);
        });

        // Build the result
        var result = baseName;
        foreach (var ext in allExtensions)
        {
            result += "-" + ext.Singleton;
            if (!string.IsNullOrEmpty(ext.Content))
            {
                result += "-" + ext.Content;
            }
        }

        return result;
    }

    private sealed class ParsedLocale
    {
        public string? Language { get; set; }
        public string? Script { get; set; }
        public string? Region { get; set; }
        public List<string> Variants { get; } = new();
        /// <summary>
        /// Unicode extension attributes (3-8 alphanumeric subtags before any keys).
        /// </summary>
        public List<string> Attributes { get; } = new();
        public string? Calendar { get; set; }
        public string? CaseFirst { get; set; }
        public string? Collation { get; set; }
        public string? FirstDayOfWeek { get; set; }
        public string? HourCycle { get; set; }
        public string? NumberingSystem { get; set; }
        public bool? Numeric { get; set; }
        /// <summary>
        /// Stores unrecognized unicode extension keys/values (e.g., "cu-eur", etc.)
        /// </summary>
        public List<string> OtherUnicodeExtensions { get; } = new();
        /// <summary>
        /// Stores other extensions as (singleton, content) pairs for sorting.
        /// e.g., ('a', "bar"), ('x', "u-foo")
        /// </summary>
        public List<ExtensionEntry> OtherExtensions { get; } = new();
    }

    private readonly record struct ExtensionEntry(char Singleton, string Content);

    /// <summary>
    /// Canonicalizes grandfathered tags using CLDR data.
    /// </summary>
    private static string CanonicalizeGrandfatheredTag(string tag)
    {
        if (Data.LocaleData.TagMappings.TryGetValue(tag, out var replacement))
        {
            return replacement;
        }
        return tag;
    }

    /// <summary>
    /// Canonicalizes a language subtag using CLDR data.
    /// </summary>
    private static string CanonicalizeLanguage(string language)
    {
        // Check simple language mappings (e.g., "mo" → "ro", "cmn" → "zh")
        if (Data.LocaleData.LanguageMappings.TryGetValue(language, out var replacement))
        {
            return replacement;
        }
        return language;
    }

    /// <summary>
    /// Applies variant aliasing per CLDR variantAlias rules.
    /// For example:
    /// - arevela → changes language to "hy" (and removes the variant)
    /// - aaland → changes region to "AX" (and removes the variant)
    /// - heploc → changes to variant "alalc97"
    /// </summary>
    private static void ApplyVariantMappings(ref string? language, ref string? script, ref string? region, ref List<string> variants)
    {
        if (variants.Count == 0)
        {
            return;
        }

        var changed = false;
        var newVariants = new List<string>(variants.Count);

        foreach (var variant in variants)
        {
            if (Data.LocaleData.VariantMappings.TryGetValue(variant, out var mapping))
            {
                changed = true;
                switch (mapping.Type)
                {
                    case "language":
                        language = mapping.Replacement;
                        // Variant is removed (not added to newVariants)
                        break;
                    case "region":
                        region = mapping.Replacement;
                        // Variant is removed (not added to newVariants)
                        break;
                    case "variant":
                        // Replace with the new variant name
                        newVariants.Add(mapping.Replacement);
                        break;
                }
            }
            else
            {
                newVariants.Add(variant);
            }
        }

        if (changed)
        {
            // Sort variants again after substitutions
            newVariants.Sort(StringComparer.Ordinal);
            variants = newVariants;
        }
    }

    /// <summary>
    /// Applies language+variant mappings for grandfathered variants.
    /// For example, "art" + "lojban" → "jbo" (with "lojban" removed from variants).
    /// </summary>
    private static void ApplyLanguageVariantMappings(ref string? language, ref List<string> variants)
    {
        if (language == null || variants.Count == 0)
        {
            return;
        }

        // Check each variant to see if language+variant forms a grandfathered pattern
        for (var i = 0; i < variants.Count; i++)
        {
            var key = language + "+" + variants[i];
            if (Data.LocaleData.LanguageVariantMappings.TryGetValue(key, out var newLanguage))
            {
                // Found a match - update language and remove the variant
                var newVariants = new List<string>(variants);
                newVariants.RemoveAt(i);
                language = newLanguage;
                variants = newVariants;
                // Recursively check for more mappings (unlikely but spec-compliant)
                ApplyLanguageVariantMappings(ref language, ref variants);
                return;
            }
        }
    }
}
