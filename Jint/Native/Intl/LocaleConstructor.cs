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

        // Apply options
        var calendar = GetUnicodeExtensionOption(optionsObj, "calendar", parsedLocale.Calendar);
        var caseFirst = GetOptionString(optionsObj, "caseFirst", parsedLocale.CaseFirst, CaseFirstValues);
        var collation = GetUnicodeExtensionOption(optionsObj, "collation", parsedLocale.Collation);
        var hourCycle = GetOptionString(optionsObj, "hourCycle", parsedLocale.HourCycle, HourCycleValues);
        var language = GetLanguageOption(optionsObj, parsedLocale.Language);
        var numberingSystem = GetUnicodeExtensionOption(optionsObj, "numberingSystem", parsedLocale.NumberingSystem);
        var numericValue = IntlUtilities.GetOption(_engine, optionsObj, "numeric", IntlUtilities.OptionType.Boolean, null, JsValue.Undefined);
        bool? numeric = numericValue.IsUndefined() ? parsedLocale.Numeric : TypeConverter.ToBoolean(numericValue);
        var region = GetRegionOption(optionsObj, parsedLocale.Region);
        var script = GetScriptOption(optionsObj, parsedLocale.Script);

        // Build the canonical locale string
        var canonicalLocale = BuildLocaleString(language, script, region, calendar, caseFirst, collation, hourCycle, numberingSystem, numeric);
        var baseName = BuildBaseName(language, script, region);

        // Get CultureInfo
        var cultureInfo = IntlUtilities.GetCultureInfo(baseName) ?? CultureInfo.InvariantCulture;

        return new JsLocale(
            _engine,
            PrototypeObject,
            canonicalLocale,
            baseName,
            language!,
            script,
            region,
            calendar,
            caseFirst,
            collation,
            hourCycle,
            numberingSystem,
            numeric,
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

        return stringValue.ToUpperInvariant();
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
    /// Validates a language subtag: 2-3 letters, 4 letters (reserved), or 5-8 letters.
    /// "root" is not a valid Unicode BCP 47 language subtag.
    /// </summary>
    private static bool IsValidLanguageSubtag(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return false;
        }

        // Must be 2-8 letters
        if (value.Length < 2 || value.Length > 8)
        {
            return false;
        }

        // Must be all ASCII letters
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

        // Parse extensions (starting with -u- for Unicode)
        while (index < parts.Length)
        {
            if (parts[index].Length == 1 && char.ToLowerInvariant(parts[index][0]) == 'u')
            {
                // Unicode extension
                index++;
                while (index < parts.Length && parts[index].Length != 1)
                {
                    var key = parts[index].ToLowerInvariant();
                    index++;

                    if (index < parts.Length && parts[index].Length != 1 && parts[index].Length >= 2)
                    {
                        var value = parts[index].ToLowerInvariant();
                        index++;

                        switch (key)
                        {
                            case "ca":
                                result.Calendar = value;
                                break;
                            case "co":
                                result.Collation = value;
                                break;
                            case "hc":
                                result.HourCycle = value;
                                break;
                            case "kf":
                                result.CaseFirst = value;
                                break;
                            case "kn":
                                result.Numeric = string.Equals(value, "true", StringComparison.OrdinalIgnoreCase);
                                break;
                            case "nu":
                                result.NumberingSystem = value;
                                break;
                        }
                    }
                    else if (string.Equals(key, "kn", StringComparison.Ordinal))
                    {
                        // -u-kn without value means true
                        result.Numeric = true;
                    }
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

    private static string BuildBaseName(string? language, string? script, string? region)
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

        return string.Join("-", parts);
    }

    private static string BuildLocaleString(
        string? language,
        string? script,
        string? region,
        string? calendar,
        string? caseFirst,
        string? collation,
        string? hourCycle,
        string? numberingSystem,
        bool? numeric)
    {
        var baseName = BuildBaseName(language, script, region);

        // Build Unicode extension if any options are set
        var extensions = new List<string>();

        if (!string.IsNullOrEmpty(calendar))
        {
            extensions.Add("ca-" + calendar);
        }

        if (!string.IsNullOrEmpty(collation))
        {
            extensions.Add("co-" + collation);
        }

        if (!string.IsNullOrEmpty(hourCycle))
        {
            extensions.Add("hc-" + hourCycle);
        }

        if (!string.IsNullOrEmpty(caseFirst))
        {
            extensions.Add("kf-" + caseFirst);
        }

        if (numeric.HasValue)
        {
            extensions.Add("kn-" + (numeric.Value ? "true" : "false"));
        }

        if (!string.IsNullOrEmpty(numberingSystem))
        {
            extensions.Add("nu-" + numberingSystem);
        }

        if (extensions.Count > 0)
        {
            return baseName + "-u-" + string.Join("-", extensions);
        }

        return baseName;
    }

    private sealed class ParsedLocale
    {
        public string? Language { get; set; }
        public string? Script { get; set; }
        public string? Region { get; set; }
        public string? Calendar { get; set; }
        public string? CaseFirst { get; set; }
        public string? Collation { get; set; }
        public string? HourCycle { get; set; }
        public string? NumberingSystem { get; set; }
        public bool? Numeric { get; set; }
    }
}
