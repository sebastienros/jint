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
    private static readonly HashSet<string>? CaseFirstValues = ["upper", "lower", "false"];
    private static readonly HashSet<string>? HourCycleValues = ["h11", "h12", "h23", "h24"];

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
    /// https://tc39.es/ecma402/#sec-intl.locale
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
        var calendar = GetUnicodeExtensionOption(optionsObj, "calendar", parsedLocale.Calendar);
        var collation = GetUnicodeExtensionOption(optionsObj, "collation", parsedLocale.Collation);
        var firstDayOfWeek = GetFirstDayOfWeekOption(optionsObj, parsedLocale.FirstDayOfWeek);
        var hourCycle = GetOptionString(optionsObj, "hourCycle", parsedLocale.HourCycle, HourCycleValues);
        var caseFirst = GetOptionString(optionsObj, "caseFirst", parsedLocale.CaseFirst, CaseFirstValues);
        var numericValue = IntlUtilities.GetOption(_engine, optionsObj, "numeric", IntlUtilities.OptionType.Boolean, null, JsValue.Undefined);
        bool? numeric = numericValue.IsUndefined() ? parsedLocale.Numeric : TypeConverter.ToBoolean(numericValue);
        var numberingSystem = GetUnicodeExtensionOption(optionsObj, "numberingSystem", parsedLocale.NumberingSystem);

        // Build the canonical locale string
        var canonicalLocale = BuildLocaleString(language, script, region, variants, calendar, caseFirst, collation, firstDayOfWeek, hourCycle, numberingSystem, numeric, parsedLocale.OtherUnicodeExtensions, parsedLocale.OtherExtensions);
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

    private string? GetOptionString(ObjectInstance options, string property, string? fallback, HashSet<string>? allowedValues = null)
    {
        var value = options.Get(property);
        if (value.IsUndefined())
        {
            return fallback;
        }

        var stringValue = TypeConverter.ToString(value);

        if (allowedValues != null && allowedValues.Count > 0)
        {
            if (!allowedValues.Contains(stringValue))
            {
                Throw.RangeError(_realm, $"Invalid value '{stringValue}' for option '{property}'");
            }
        }

        return stringValue;
    }

    /// <summary>
    /// Gets a Unicode extension option value and validates it matches the pattern (3*8alphanum) *("-" (3*8alphanum))
    /// </summary>
    private string? GetUnicodeExtensionOption(ObjectInstance options, string property, string? fallback)
    {
        var value = options.Get(property);
        if (value.IsUndefined())
        {
            return fallback;
        }

        var stringValue = TypeConverter.ToString(value);

        // Validate against pattern: (3*8alphanum) *("-" (3*8alphanum))
        if (!IsValidUnicodeExtensionValue(stringValue))
        {
            Throw.RangeError(_realm, $"Invalid value '{stringValue}' for option '{property}'");
        }

        return stringValue;
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

        return stringValue.ToLowerInvariant();
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
            if (!((c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z')))
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
            return ((value[0] >= 'a' && value[0] <= 'z') || (value[0] >= 'A' && value[0] <= 'Z')) &&
                   ((value[1] >= 'a' && value[1] <= 'z') || (value[1] >= 'A' && value[1] <= 'Z'));
        }

        // 3 digits
        if (value.Length == 3)
        {
            return value[0] >= '0' && value[0] <= '9' &&
                   value[1] >= '0' && value[1] <= '9' &&
                   value[2] >= '0' && value[2] <= '9';
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
            if (!((c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z')))
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
        if (stringValue.Contains("--"))
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
    /// Gets and validates the firstDayOfWeek option.
    /// Numeric values (0-7) are converted to weekday strings.
    /// String values must be 3-8 alphanumeric characters (can be hyphenated).
    /// </summary>
    private string? GetFirstDayOfWeekOption(ObjectInstance options, string? fallback)
    {
        var value = options.Get("firstDayOfWeek");
        if (value.IsUndefined())
        {
            return fallback;
        }

        string stringValue;

        // Check if it's a boolean first (true means no value, like kn extension)
        if (value.IsBoolean())
        {
            if (TypeConverter.ToBoolean(value))
            {
                // Boolean true means just "fw" with no value
                return "";
            }
            else
            {
                // Boolean false becomes "false"
                stringValue = "false";
            }
        }
        // Check if it's a number
        else if (value.IsNumber())
        {
            var numValue = (int) TypeConverter.ToNumber(value);
            stringValue = WeekdayToString(numValue);
        }
        else
        {
            stringValue = TypeConverter.ToString(value);

            // Try to parse as integer string
            if (int.TryParse(stringValue, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var numValue) && numValue >= 0 && numValue <= 7)
            {
                stringValue = WeekdayToString(numValue);
            }
        }

        // Validate the string value matches type sequence pattern
        // Pattern: (3*8alphanum) *("-" (3*8alphanum))
        if (!IsValidFirstDayOfWeekValue(stringValue))
        {
            Throw.RangeError(_realm, $"Invalid value '{stringValue}' for option 'firstDayOfWeek'");
        }

        return stringValue.ToLowerInvariant();
    }

    /// <summary>
    /// Converts a weekday number to its string representation.
    /// 0 and 7 = sun, 1 = mon, 2 = tue, 3 = wed, 4 = thu, 5 = fri, 6 = sat
    /// </summary>
    private static string WeekdayToString(int day)
    {
        return day switch
        {
            1 => "mon",
            2 => "tue",
            3 => "wed",
            4 => "thu",
            5 => "fri",
            6 => "sat",
            0 or 7 => "sun",
            _ => day.ToString(System.Globalization.CultureInfo.InvariantCulture)
        };
    }

    /// <summary>
    /// Validates that a string matches the firstDayOfWeek value pattern.
    /// Pattern: (3*8alphanum) *("-" (3*8alphanum))
    /// Each part must be 3-8 alphanumeric characters.
    /// </summary>
    private static bool IsValidFirstDayOfWeekValue(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return false;
        }

        var parts = value.Split('-');
        foreach (var part in parts)
        {
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
        }

        return true;
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
            result.Language = parts[index].ToLowerInvariant();
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
            result.Region = parts[index].ToUpperInvariant();
            index++;
        }

        // Variants (optional, 5-8 alphanum or 4 chars starting with digit)
        while (index < parts.Length && IsVariantSubtag(parts[index]))
        {
            result.Variants.Add(parts[index].ToLowerInvariant());
            index++;
        }

        // Parse extensions
        var otherExtensionsParts = new List<string>();
        while (index < parts.Length)
        {
            if (parts[index].Length == 1)
            {
                var singleton = char.ToLowerInvariant(parts[index][0]);
                if (singleton == 'u')
                {
                    // Unicode extension
                    index++;
                    while (index < parts.Length && parts[index].Length != 1)
                    {
                        var key = parts[index].ToLowerInvariant();
                        index++;

                        // Check if this key has a value (next part is not a singleton and is 3+ chars for type values)
                        string? value = null;
                        if (index < parts.Length && parts[index].Length != 1 && parts[index].Length >= 3)
                        {
                            value = parts[index].ToLowerInvariant();
                            index++;
                        }

                        var handled = false;
                        switch (key)
                        {
                            case "ca":
                                if (value != null) { result.Calendar = value; handled = true; }
                                break;
                            case "co":
                                if (value != null) { result.Collation = value; handled = true; }
                                break;
                            case "fw":
                                // FirstDayOfWeek can have multi-part values (e.g., frank-yung-fong-tang)
                                if (value != null)
                                {
                                    // Collect additional value parts
                                    var fwParts = new List<string> { value };
                                    while (index < parts.Length && parts[index].Length >= 3 && parts[index].Length <= 8 && parts[index].Length != 1)
                                    {
                                        // Check if this looks like a key (2 chars) or next extension
                                        if (parts[index].Length == 2)
                                        {
                                            break;
                                        }
                                        fwParts.Add(parts[index].ToLowerInvariant());
                                        index++;
                                    }
                                    result.FirstDayOfWeek = string.Join("-", fwParts);
                                    handled = true;
                                }
                                break;
                            case "hc":
                                if (value != null) { result.HourCycle = value; handled = true; }
                                break;
                            case "kf":
                                // Per UTS35, "true" value is canonicalized to empty string (just "kf")
                                if (value != null)
                                {
                                    result.CaseFirst = string.Equals(value, "true", StringComparison.OrdinalIgnoreCase) ? "" : value;
                                    handled = true;
                                }
                                else
                                {
                                    // kf without value means "true" which canonicalizes to empty string
                                    result.CaseFirst = "";
                                    handled = true;
                                }
                                break;
                            case "kn":
                                if (value != null)
                                {
                                    result.Numeric = string.Equals(value, "true", StringComparison.OrdinalIgnoreCase);
                                }
                                else
                                {
                                    result.Numeric = true;
                                }
                                handled = true;
                                break;
                            case "nu":
                                if (value != null) { result.NumberingSystem = value; handled = true; }
                                break;
                        }

                        // Store unhandled unicode extension key/value pairs
                        if (!handled)
                        {
                            if (value != null)
                            {
                                result.OtherUnicodeExtensions.Add(key + "-" + value);
                            }
                            else
                            {
                                result.OtherUnicodeExtensions.Add(key);
                            }
                        }
                    }
                }
                else
                {
                    // Other extension (t, x, or other singleton)
                    var extStart = index;
                    index++;
                    while (index < parts.Length && parts[index].Length != 1)
                    {
                        index++;
                    }
                    // Collect all parts for this extension
                    for (var i = extStart; i < index; i++)
                    {
                        otherExtensionsParts.Add(parts[i].ToLowerInvariant());
                    }
                }
            }
            else
            {
                index++;
            }
        }

        if (otherExtensionsParts.Count > 0)
        {
            result.OtherExtensions = "-" + string.Join("-", otherExtensionsParts);
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

        return string.Join("-", parts);
    }

    private static string BuildLocaleString(
        string? language,
        string? script,
        string? region,
        List<string>? variants,
        string? calendar,
        string? caseFirst,
        string? collation,
        string? firstDayOfWeek,
        string? hourCycle,
        string? numberingSystem,
        bool? numeric,
        List<string>? otherUnicodeExtensions = null,
        string? otherExtensions = null)
    {
        var baseName = BuildBaseName(language, script, region, variants);

        // Build Unicode extension if any options are set
        // Order: ca, co, fw, hc, kf, kn, nu (alphabetical)
        var unicodeExtParts = new List<string>();

        if (!string.IsNullOrEmpty(calendar))
        {
            unicodeExtParts.Add("ca-" + calendar);
        }

        if (!string.IsNullOrEmpty(collation))
        {
            unicodeExtParts.Add("co-" + collation);
        }

        if (firstDayOfWeek != null)
        {
            if (firstDayOfWeek.Length == 0)
            {
                // Empty string means just "fw" with no value (boolean true)
                unicodeExtParts.Add("fw");
            }
            else
            {
                unicodeExtParts.Add("fw-" + firstDayOfWeek);
            }
        }

        if (!string.IsNullOrEmpty(hourCycle))
        {
            unicodeExtParts.Add("hc-" + hourCycle);
        }

        if (caseFirst != null)
        {
            if (caseFirst.Length == 0)
            {
                // Empty string means just "kf" (canonicalized from "kf-true")
                unicodeExtParts.Add("kf");
            }
            else
            {
                unicodeExtParts.Add("kf-" + caseFirst);
            }
        }

        if (numeric.HasValue)
        {
            if (numeric.Value)
            {
                // Per UTS35, "true" value is removed, so just "kn" without a value
                unicodeExtParts.Add("kn");
            }
            else
            {
                // Explicit false needs "kn-false"
                unicodeExtParts.Add("kn-false");
            }
        }

        if (!string.IsNullOrEmpty(numberingSystem))
        {
            unicodeExtParts.Add("nu-" + numberingSystem);
        }

        // Add other unicode extensions that were not recognized
        if (otherUnicodeExtensions != null && otherUnicodeExtensions.Count > 0)
        {
            unicodeExtParts.AddRange(otherUnicodeExtensions);
        }

        var result = baseName;
        if (unicodeExtParts.Count > 0)
        {
            result += "-u-" + string.Join("-", unicodeExtParts);
        }

        // Add other extensions (transformed, private use, etc.)
        if (!string.IsNullOrEmpty(otherExtensions))
        {
            result += otherExtensions;
        }

        return result;
    }

    private sealed class ParsedLocale
    {
        public string? Language { get; set; }
        public string? Script { get; set; }
        public string? Region { get; set; }
        public List<string> Variants { get; } = new();
        public string? Calendar { get; set; }
        public string? CaseFirst { get; set; }
        public string? Collation { get; set; }
        public string? FirstDayOfWeek { get; set; }
        public string? HourCycle { get; set; }
        public string? NumberingSystem { get; set; }
        public bool? Numeric { get; set; }
        /// <summary>
        /// Stores unrecognized unicode extension keys/values (e.g., "co" without value, "attr", etc.)
        /// </summary>
        public List<string> OtherUnicodeExtensions { get; } = new();
        /// <summary>
        /// Stores other extensions (transformed -t-, private use -x-, and other singletons)
        /// </summary>
        public string? OtherExtensions { get; set; }
    }
}
