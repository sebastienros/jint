using System.Buffers;
using System.Globalization;
using Jint.Native.Function;
using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Descriptors;
using Jint.Runtime.Interop;

namespace Jint.Native.Intl;

/// <summary>
/// https://tc39.es/ecma402/#sec-the-intl-collator-constructor
/// </summary>
internal sealed class CollatorConstructor : Constructor
{
    private static readonly JsString _functionName = new("Collator");
    private static readonly StringSearchValues LocaleMatcherValues = new(["lookup", "best fit"], StringComparison.Ordinal);
    private static readonly StringSearchValues UsageValues = new(["sort", "search"], StringComparison.Ordinal);
    private static readonly StringSearchValues SensitivityValues = new(["base", "accent", "case", "variant"], StringComparison.Ordinal);
    private static readonly StringSearchValues CaseFirstValues = new(["upper", "lower", "false"], StringComparison.Ordinal);

    // Valid collation types per CLDR (excluding "standard" and "search" which are special)
    private static readonly StringSearchValues ValidCollationTypes = new([
        "big5han", "compat", "dict", "direct", "ducet", "emoji", "eor",
        "gb2312", "phonebk", "phonebook", "phonetic", "pinyin", "reformed",
        "searchjl", "stroke", "trad", "unihan", "zhuyin", "default"
    ], StringComparison.Ordinal);

    // Locale-specific supported collation types (from CLDR)
    // Only locales with non-default collation support are listed
    private static readonly Dictionary<string, HashSet<string>> LocaleCollationSupport = new(StringComparer.OrdinalIgnoreCase)
    {
        ["de"] = new(StringComparer.Ordinal) { "default", "phonebk", "eor" },
        ["es"] = new(StringComparer.Ordinal) { "default", "trad", "eor" },
        ["zh"] = new(StringComparer.Ordinal) { "default", "big5han", "gb2312", "pinyin", "stroke", "unihan", "zhuyin", "eor" },
        ["ja"] = new(StringComparer.Ordinal) { "default", "unihan", "eor" },
        ["ko"] = new(StringComparer.Ordinal) { "default", "searchjl", "unihan", "eor" },
        ["sv"] = new(StringComparer.Ordinal) { "default", "reformed", "eor" },
        ["da"] = new(StringComparer.Ordinal) { "default", "eor" },
        ["ar"] = new(StringComparer.Ordinal) { "default", "compat", "eor" },
    };

    public CollatorConstructor(
        Engine engine,
        Realm realm,
        FunctionPrototype functionPrototype,
        ObjectPrototype objectPrototype) : base(engine, realm, _functionName)
    {
        _prototype = functionPrototype;
        PrototypeObject = new CollatorPrototype(engine, realm, this, objectPrototype);
        _length = new PropertyDescriptor(JsNumber.PositiveZero, PropertyFlag.Configurable);
        _prototypeDescriptor = new PropertyDescriptor(PrototypeObject, PropertyFlag.AllForbidden);
    }

    private CollatorPrototype PrototypeObject { get; }

    protected override void Initialize()
    {
        const PropertyFlag PropertyFlags = PropertyFlag.Configurable | PropertyFlag.Writable;
        var properties = new PropertyDictionary(1, checkExistingKeys: false)
        {
            ["supportedLocalesOf"] = new(new ClrFunction(Engine, "supportedLocalesOf", SupportedLocalesOf, 1, PropertyFlag.Configurable), PropertyFlags)
        };
        SetProperties(properties);
    }

    /// <summary>
    /// Called when Intl.Collator is invoked without `new`.
    /// </summary>
    protected internal override JsValue Call(JsValue thisObject, JsCallArguments arguments)
    {
        return Construct(arguments, this);
    }

    /// <summary>
    /// https://tc39.es/ecma402/#sec-intl.collator
    /// </summary>
    public override ObjectInstance Construct(JsCallArguments arguments, JsValue newTarget)
    {
        var locales = arguments.At(0);
        var options = arguments.At(1);
        // (handled by runtime)

        // Get options object
        var optionsObj = IntlUtilities.CoerceOptionsToObject(_engine, options);

        // Validate localeMatcher option first (must be done before other processing)
        GetStringOption(optionsObj, "localeMatcher", LocaleMatcherValues, "best fit");

        // Resolve locale
        var requestedLocales = IntlUtilities.CanonicalizeLocaleList(_engine, locales);
        var availableLocales = IntlUtilities.GetAvailableLocales();
        var resolvedLocale = ResolveCollatorLocale(_engine, availableLocales, requestedLocales, optionsObj);

        // Parse Unicode extensions from the first requested locale (if any) since resolved locale may strip them
        string? uCollation = null;
        bool? uNumeric = null;
        string? uCaseFirst = null;
        if (requestedLocales.Count > 0)
        {
            ParseUnicodeExtensions(requestedLocales[0], out uCollation, out uNumeric, out uCaseFirst);
        }

        // Get options (options override unicode extensions)
        var usage = GetStringOption(optionsObj, "usage", UsageValues, "sort");
        var sensitivity = GetSensitivity(optionsObj);
        var collation = GetCollationOption(optionsObj, uCollation, resolvedLocale);
        var numeric = GetNumericOption(optionsObj, uNumeric);
        var caseFirst = GetCaseFirstOption(optionsObj, uCaseFirst);

        // Build locale with unicode extensions - include extension if it was present AND final value matches
        var finalLocale = BuildLocaleWithExtensions(resolvedLocale,
            collation, uCollation,
            numeric, uNumeric,
            caseFirst, uCaseFirst);

        // Get CompareInfo for the locale
        var culture = IntlUtilities.GetCultureInfo(resolvedLocale) ?? CultureInfo.InvariantCulture;

        // Get ignorePunctuation with locale-specific default
        // Thai (th) defaults to true, others default to false
        var ignorePunctuationDefault = resolvedLocale.StartsWith("th", StringComparison.OrdinalIgnoreCase);
        var ignorePunctuation = GetIgnorePunctuationOption(optionsObj, ignorePunctuationDefault);
        var compareInfo = culture.CompareInfo;

        // Map sensitivity to CompareOptions
        var compareOptions = MapSensitivityToCompareOptions(sensitivity, ignorePunctuation);

        // Get prototype from newTarget (for cross-realm construction)
        var proto = GetPrototypeFromConstructor(newTarget, static intrinsics => intrinsics.Collator.PrototypeObject);

        return new JsCollator(
            _engine,
            proto,
            finalLocale,
            usage,
            sensitivity,
            ignorePunctuation,
            collation,
            numeric,
            caseFirst,
            compareInfo,
            compareOptions);
    }

    private static string BuildLocaleWithExtensions(string baseLocale,
        string collation, string? uCollation,
        bool numeric, bool? uNumeric,
        string caseFirst, string? uCaseFirst)
    {
        // Include extension if: (1) extension was present in locale AND (2) final value equals extension value
        var extensions = new List<string>();

        // Add collation extension if extension was present and final value matches
        if (uCollation != null &&
            string.Equals(collation, uCollation, StringComparison.Ordinal) &&
            !string.Equals(collation, "default", StringComparison.Ordinal))
        {
            extensions.Add("co-" + collation);
        }

        // Add kn (numeric) extension if extension was present and final value matches extension value
        // Per spec: canonical form is just "kn" for true, don't include for false (default)
        if (uNumeric.HasValue && numeric == uNumeric.Value && numeric)
        {
            extensions.Add("kn");
        }

        // Add kf (caseFirst) extension if extension was present and final value matches
        if (uCaseFirst != null &&
            string.Equals(caseFirst, uCaseFirst, StringComparison.Ordinal) &&
            !string.Equals(caseFirst, "false", StringComparison.Ordinal))
        {
            extensions.Add("kf-" + caseFirst);
        }

        if (extensions.Count == 0)
        {
            return baseLocale;
        }

        // Sort extensions alphabetically (co, kf, kn order)
        extensions.Sort(StringComparer.Ordinal);
        return baseLocale + "-u-" + string.Join("-", extensions);
    }

    private string GetStringOption(ObjectInstance options, string property, in StringSearchValues values, string fallback)
    {
        var value = options.Get(property);
        if (value.IsUndefined())
        {
            return fallback;
        }

        var stringValue = TypeConverter.ToString(value);

        if (!values.Contains(stringValue))
        {
            Throw.RangeError(_realm, $"Invalid value '{stringValue}' for option '{property}'");
        }

        return stringValue;
    }

    private string GetSensitivity(ObjectInstance options)
    {
        var value = options.Get("sensitivity");
        if (value.IsUndefined())
        {
            // Default depends on usage - "variant" for sort, "variant" for search
            return "variant";
        }

        var stringValue = TypeConverter.ToString(value);

        if (!SensitivityValues.Contains(stringValue))
        {
            Throw.RangeError(_realm, $"Invalid value '{stringValue}' for option 'sensitivity'");
        }

        return stringValue;
    }

    private static bool GetIgnorePunctuationOption(ObjectInstance options, bool fallback)
    {
        var value = options.Get("ignorePunctuation");
        if (value.IsUndefined())
        {
            return fallback;
        }

        return TypeConverter.ToBoolean(value);
    }

    private static void ParseUnicodeExtensions(string locale, out string? collation, out bool? numeric, out string? caseFirst)
    {
        collation = null;
        numeric = null;
        caseFirst = null;

        // Only search for -u- before the private-use section (-x-)
        var xIndex = locale.IndexOf("-x-", StringComparison.OrdinalIgnoreCase);
        var searchRange = xIndex >= 0 ? locale.Substring(0, xIndex) : locale;
        var uIndex = searchRange.IndexOf("-u-", StringComparison.Ordinal);
        if (uIndex < 0)
        {
            return;
        }

        // Extract the -u- extension content (up to private-use or next singleton)
        var extensionContent = (xIndex >= 0 ? locale.Substring(uIndex + 3, xIndex - uIndex - 3) : locale.Substring(uIndex + 3));
        var parts = extensionContent.Split('-');
        for (var i = 0; i < parts.Length; i++)
        {
            var key = parts[i];
            // Keys are exactly 2 characters, values are 3+ characters (or "true"/"false" which are special)
            // If the next part is also 2 characters, it's another key, not a value
            if (key.Length == 2)
            {
                // Check if there's a value (3+ chars or special 2-char values that aren't keys)
                string? value = null;
                if (i + 1 < parts.Length && parts[i + 1].Length >= 3)
                {
                    value = parts[i + 1];
                    i++;
                }

                switch (key)
                {
                    case "co":
                        collation = value;
                        break;
                    case "kn":
                        // -u-kn without value or with any non-false value means true
                        if (value == null)
                        {
                            numeric = true;
                        }
                        else
                        {
                            numeric = !string.Equals(value, "false", StringComparison.OrdinalIgnoreCase);
                        }
                        break;
                    case "kf":
                        caseFirst = value;
                        break;
                }
            }
        }
    }

    private static string GetCollationOption(ObjectInstance options, string? unicodeExtension, string resolvedLocale)
    {
        // Get the language code for locale-specific collation support
        var langCode = resolvedLocale;
        var dashIdx = resolvedLocale.IndexOf('-');
        if (dashIdx > 0)
        {
            langCode = resolvedLocale.Substring(0, dashIdx);
        }

        var value = options.Get("collation");
        if (!value.IsUndefined())
        {
            var collation = TypeConverter.ToString(value);

            // Per ECMA-402: "standard" and "search" collations are explicitly disallowed
            if (string.Equals(collation, "standard", StringComparison.Ordinal) ||
                string.Equals(collation, "search", StringComparison.Ordinal))
            {
                // Fall through to check unicode extension
            }
            // Validate against known collation types AND locale-specific support
            else if (ValidCollationTypes.Contains(collation) && IsCollationSupportedForLocale(langCode, collation))
            {
                return collation;
            }
            // Options value is not supported - fall through to check unicode extension
        }

        // Check unicode extension, but disallow "standard", "search", and invalid values
        if (unicodeExtension != null &&
            !string.Equals(unicodeExtension, "standard", StringComparison.Ordinal) &&
            !string.Equals(unicodeExtension, "search", StringComparison.Ordinal) &&
            ValidCollationTypes.Contains(unicodeExtension) &&
            IsCollationSupportedForLocale(langCode, unicodeExtension))
        {
            return unicodeExtension;
        }

        return "default";
    }

    private static bool IsCollationSupportedForLocale(string language, string collation)
    {
        if (string.Equals(collation, "default", StringComparison.Ordinal))
        {
            return true;
        }

        // If we have explicit locale data, check against it
        if (LocaleCollationSupport.TryGetValue(language, out var supported))
        {
            return supported.Contains(collation);
        }

        // For unlisted locales, only "default" and "eor" are universally supported
        return string.Equals(collation, "eor", StringComparison.Ordinal);
    }

    private static bool GetNumericOption(ObjectInstance options, bool? unicodeExtension)
    {
        var value = options.Get("numeric");
        if (!value.IsUndefined())
        {
            return TypeConverter.ToBoolean(value);
        }

        return unicodeExtension ?? false;
    }

    private string GetCaseFirstOption(ObjectInstance options, string? unicodeExtension)
    {
        var value = options.Get("caseFirst");
        if (!value.IsUndefined())
        {
            var stringValue = TypeConverter.ToString(value);

            if (!CaseFirstValues.Contains(stringValue))
            {
                Throw.RangeError(_realm, $"Invalid value '{stringValue}' for option 'caseFirst'");
            }

            return stringValue;
        }

        if (unicodeExtension != null && CaseFirstValues.Contains(unicodeExtension))
        {
            return unicodeExtension;
        }

        return "false";
    }

    private static CompareOptions MapSensitivityToCompareOptions(string sensitivity, bool ignorePunctuation)
    {
        var options = sensitivity switch
        {
            "base" =>
                // Ignore case and accents
                CompareOptions.IgnoreCase | CompareOptions.IgnoreNonSpace,
            "accent" =>
                // Ignore case but consider accents
                CompareOptions.IgnoreCase,
            "case" =>
                // Consider case but ignore accents
                CompareOptions.IgnoreNonSpace,
            "variant" =>
                // Consider both case and accents
                CompareOptions.None,
            _ => CompareOptions.None,
        };

        if (ignorePunctuation)
        {
            options |= CompareOptions.IgnoreSymbols;
        }

        return options;
    }

    private static string ResolveCollatorLocale(Engine engine, HashSet<string> availableLocales, List<string> requestedLocales, ObjectInstance options)
    {
        var resolved = IntlUtilities.ResolveLocale(engine, availableLocales, requestedLocales, options, []);
        return resolved.Locale;
    }

    /// <summary>
    /// https://tc39.es/ecma402/#sec-intl.collator.supportedlocalesof
    /// </summary>
    private JsArray SupportedLocalesOf(JsValue thisObject, JsCallArguments arguments)
    {
        var locales = arguments.At(0);
        var options = arguments.At(1);

        var requestedLocales = IntlUtilities.CanonicalizeLocaleList(_engine, locales);
        var availableLocales = IntlUtilities.GetAvailableLocales();

        // Validate localeMatcher option
        var optionsObj = IntlUtilities.CoerceOptionsToObject(_engine, options);
        GetStringOption(optionsObj, "localeMatcher", LocaleMatcherValues, "best fit");

        // For now, return all requested locales that are available
        List<JsValue> supported = [];
        foreach (var locale in requestedLocales)
        {
            var bestAvailable = IntlUtilities.BestAvailableLocale(availableLocales, locale);
            if (bestAvailable != null)
            {
                supported.Add(locale);
            }
        }

        return new JsArray(_engine, supported.ToArray());
    }
}
