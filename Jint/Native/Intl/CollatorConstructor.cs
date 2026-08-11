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
[JsObject(UseShape = true)]
internal sealed partial class CollatorConstructor : Constructor
{
    private static readonly JsString _functionName = new("Collator");
    private static readonly StringSearchValues LocaleMatcherValues = new(["lookup", "best fit"], StringComparison.Ordinal);
    private static readonly StringSearchValues UsageValues = new(["sort", "search"], StringComparison.Ordinal);
    private static readonly StringSearchValues SensitivityValues = new(["base", "accent", "case", "variant"], StringComparison.Ordinal);
    private static readonly StringSearchValues CaseFirstValues = new(["upper", "lower", "false"], StringComparison.Ordinal);

    // Locale-specific supported collation types (from CLDR)
    // Only locales with non-default collation support are listed
    private static readonly Dictionary<string, HashSet<string>> LocaleCollationSupport = new(StringComparer.OrdinalIgnoreCase)
    {
        ["ar"] = new(StringComparer.Ordinal) { "default", "compat", "eor" },
        ["da"] = new(StringComparer.Ordinal) { "default", "eor" },
        ["de"] = new(StringComparer.Ordinal) { "default", "phonebk", "eor" },
        ["en"] = new(StringComparer.Ordinal) { "default", "ducet", "emoji", "eor" },
        ["es"] = new(StringComparer.Ordinal) { "default", "trad", "eor" },
        ["hi"] = new(StringComparer.Ordinal) { "default", "direct", "eor" },
        ["ja"] = new(StringComparer.Ordinal) { "default", "unihan", "eor" },
        ["ko"] = new(StringComparer.Ordinal) { "default", "searchjl", "unihan", "eor" },
        ["ln"] = new(StringComparer.Ordinal) { "default", "phonetic", "eor" },
        ["si"] = new(StringComparer.Ordinal) { "default", "dict", "eor" },
        ["sv"] = new(StringComparer.Ordinal) { "default", "reformed", "eor" },
        ["zh"] = new(StringComparer.Ordinal) { "default", "big5han", "gb2312", "pinyin", "stroke", "unihan", "zhuyin", "eor" },
    };

    // The collations CLDR's root locale contributes to every locale. "standard" and "search" are
    // deliberately absent, for the reason IsReportableCollation gives.
    private static readonly string[] RootCollations = ["emoji", "eor"];

    // Each language's [[co]] list minus its leading null, in the lexicographic code unit order
    // https://tc39.es/ecma402/#sec-collationsoflocale reports it in. ECMA-402 gives a locale exactly
    // one such list: 15.5.10 step 3.c reports it and 9.2.7 step 10 — reached from 10.1.1 through
    // ResolveOptions — resolves a requested "co" against it, so deriving both views from this one
    // table is what keeps Intl.Locale.prototype.getCollations and Intl.Collator from disagreeing
    // about what a locale supports.
    private static readonly Dictionary<string, string[]> LocaleCollations = BuildLocaleCollations();

    private static Dictionary<string, string[]> BuildLocaleCollations()
    {
        var result = new Dictionary<string, string[]>(LocaleCollationSupport.Count, StringComparer.OrdinalIgnoreCase);
        foreach (var pair in LocaleCollationSupport)
        {
            var list = new List<string>(pair.Value.Count + RootCollations.Length);
            foreach (var collation in pair.Value)
            {
                if (IsReportableCollation(collation))
                {
                    list.Add(collation);
                }
            }

            foreach (var rootCollation in RootCollations)
            {
                if (!list.Contains(rootCollation))
                {
                    list.Add(rootCollation);
                }
            }

            list.Sort(StringComparer.Ordinal);
            result[pair.Key] = list.ToArray();
        }

        return result;
    }

    /// <summary>
    /// Whether an identifier may appear in a locale's [[co]] list. "standard" and "search" may not:
    /// https://tc39.es/ecma402/#sec-intl-collator-internal-slots (10.2.3) forbids either from being
    /// an element of any [[SortLocaleData]].[[&lt;locale&gt;]].[[co]] or
    /// [[SearchLocaleData]].[[&lt;locale&gt;]].[[co]] List. Keeping them out of the list itself —
    /// rather than special-casing them where a request is resolved — is what makes them unreportable
    /// and unrequestable by the same act. "default" is Jint's placeholder for a null resolved [[co]]
    /// and is not an identifier a locale can report either.
    /// </summary>
    private static bool IsReportableCollation(string collation)
    {
        return !string.Equals(collation, "default", StringComparison.Ordinal)
            && !string.Equals(collation, "standard", StringComparison.Ordinal)
            && !string.Equals(collation, "search", StringComparison.Ordinal);
    }

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

    protected override void Initialize() => CreateProperties_Generated();

    /// <summary>
    /// Called when Intl.Collator is invoked without `new`.
    /// </summary>
    protected internal override JsValue Call(JsValue thisObject, JsCallArguments arguments)
    {
        // Intl.Collator has no normative-optional legacy-constructor (ChainCollator) behaviour — unlike
        // NumberFormat/DateTimeFormat it always returns a fresh instance and ignores the this value.
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
        GetStringOption(optionsObj, "localeMatcher", in LocaleMatcherValues, "best fit");

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
        var usage = GetStringOption(optionsObj, "usage", in UsageValues, "sort");
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
        return baseLocale + "-u-" + string.Join('-', extensions);
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

    private string GetCollationOption(ObjectInstance options, string? unicodeExtension, string resolvedLocale)
    {
        // Get the language code for locale-specific collation support
        var langCode = resolvedLocale;
        var dashIdx = resolvedLocale.IndexOf('-');
        if (dashIdx > 0)
        {
            langCode = resolvedLocale.Substring(0, dashIdx);
        }

        // Both requests are matched against the locale's [[co]] list, and the options value wins
        // when both resolve — https://tc39.es/ecma402/#sec-resolvelocale (9.2.7) step 10. "standard"
        // and "search" need no guard of their own: 10.2.3 keeps them out of every [[co]] list, and
        // IsReportableCollation is where Jint honours that.
        var value = options.Get("collation");
        if (!value.IsUndefined())
        {
            var collation = TypeConverter.ToString(value);

            // The syntax check https://tc39.es/ecma402/#sec-resolveoptions (9.2.8) step 6.d.ii makes
            // for every resolution option, here reached because 10.2.3 gives Intl.Collator the
            // descriptor { [[Key]]: "co", [[Property]]: "collation" }. It is a check on the shape of
            // the value alone, not on the [[co]] list below: an ill-formed value is a RangeError
            // whether or not the locale would have accepted a well-formed one.
            if (!IntlUtilities.IsValidUnicodeExtensionValue(collation))
            {
                Throw.RangeError(_realm, $"Invalid value '{collation}' for option 'collation'");
            }

            if (IsCollationSupportedForLocale(langCode, collation))
            {
                return collation;
            }

            // Options value is not supported - fall through to check unicode extension
        }

        if (unicodeExtension != null && IsCollationSupportedForLocale(langCode, unicodeExtension))
        {
            return unicodeExtension;
        }

        return "default";
    }

    /// <summary>
    /// The collation identifiers reported for <paramref name="language"/> by
    /// https://tc39.es/ecma402/#sec-collationsoflocale — the root collations plus whatever the
    /// language adds, in lexicographic code unit order. A language carrying no data of its own —
    /// including "und" and any tag matching no available Collator locale — gets exactly the root
    /// list, which is what the spec hardcodes for the unmatched case.
    /// </summary>
    internal static string[] GetCollationsForLanguage(string? language)
    {
        if (language is null || !LocaleCollations.TryGetValue(language, out var collations))
        {
            return RootCollations;
        }

        return collations;
    }

    /// <summary>
    /// Whether <paramref name="collation"/> can be resolved as the "co" value for
    /// <paramref name="language"/>. This is the acceptance side of the one [[co]] list a locale has,
    /// so it answers for exactly what <see cref="GetCollationsForLanguage"/> reports — anything else
    /// leaves Intl.Collator refusing a collation Intl.Locale.prototype.getCollations advertises.
    /// "default" is accepted on top of that list as a request and never appears in it: it is how
    /// Jint spells the null [[co]] that https://tc39.es/ecma402/#sec-intl.collator (10.1.1) turns
    /// into a resolved collation of "default" anyway.
    /// </summary>
    private static bool IsCollationSupportedForLocale(string language, string collation)
    {
        if (string.Equals(collation, "default", StringComparison.Ordinal))
        {
            return true;
        }

        foreach (var reported in GetCollationsForLanguage(language))
        {
            if (string.Equals(reported, collation, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
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
            // Ignore case and accents
            "base" => CompareOptions.IgnoreCase | CompareOptions.IgnoreNonSpace,
            // Ignore case but consider accents
            "accent" => CompareOptions.IgnoreCase,
            // Consider case but ignore accents
            "case" => CompareOptions.IgnoreNonSpace,
            // Consider both case and accents
            "variant" => CompareOptions.None,
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
    [JsFunction(Length = 1)]
    private JsArray SupportedLocalesOf(JsValue thisObject, JsValue locales, JsValue options)
    {

        var requestedLocales = IntlUtilities.CanonicalizeLocaleList(_engine, locales);
        var availableLocales = IntlUtilities.GetAvailableLocales();

        // Validate localeMatcher option
        var optionsObj = IntlUtilities.CoerceOptionsToObject(_engine, options);
        GetStringOption(optionsObj, "localeMatcher", in LocaleMatcherValues, "best fit");

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
