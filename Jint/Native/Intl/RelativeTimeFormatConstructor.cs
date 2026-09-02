using System.Globalization;
using Jint.Native.Function;
using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Descriptors;
using Jint.Runtime.Interop;

namespace Jint.Native.Intl;

/// <summary>
/// https://tc39.es/ecma402/#sec-intl-relativetimeformat-constructor
/// </summary>
[JsObject(UseShape = true)]
internal sealed partial class RelativeTimeFormatConstructor : Constructor
{
    private static readonly JsString _functionName = new("RelativeTimeFormat");
    private static readonly string[] LocaleMatcherValues = ["lookup", "best fit"];
    private static readonly string[] StyleValues = ["long", "short", "narrow"];
    private static readonly string[] NumericValues = ["always", "auto"];

    public RelativeTimeFormatConstructor(
        Engine engine,
        Realm realm,
        FunctionPrototype functionPrototype,
        ObjectPrototype objectPrototype) : base(engine, realm, _functionName)
    {
        _prototype = functionPrototype;
        PrototypeObject = new RelativeTimeFormatPrototype(engine, realm, this, objectPrototype);
        _length = new PropertyDescriptor(JsNumber.PositiveZero, PropertyFlag.Configurable);
        _prototypeDescriptor = new PropertyDescriptor(PrototypeObject, PropertyFlag.AllForbidden);
    }

    protected override void Initialize() => CreateProperties_Generated();

    public RelativeTimeFormatPrototype PrototypeObject { get; }

    /// <summary>
    /// Called when Intl.RelativeTimeFormat is invoked without `new`.
    /// </summary>
    protected internal override JsValue Call(JsValue thisObject, JsCallArguments arguments)
    {
        Throw.TypeError(_realm, "Constructor Intl.RelativeTimeFormat requires 'new'");
        return Undefined;
    }

    /// <summary>
    /// https://tc39.es/ecma402/#sec-Intl.RelativeTimeFormat
    /// </summary>
    public override ObjectInstance Construct(JsCallArguments arguments, JsValue newTarget)
    {
        var locales = arguments.At(0);
        var options = arguments.At(1);

        // Get options object (lenient - converts to object)
        var optionsObj = IntlUtilities.CoerceOptionsToObject(_engine, options);

        // Per spec: Get options in the correct order
        // Step 5: localeMatcher
        var localeMatcher = GetStringOption(optionsObj, "localeMatcher", LocaleMatcherValues, "best fit");

        // Step 7: numberingSystem (read and validate)
        // Per spec, the value must be syntactically valid as a Unicode numbering system identifier
        // If not supported, we fall back to "latn" - we don't throw for valid-but-unsupported values
        var numberingSystemValue = optionsObj.Get("numberingSystem");
        string? numberingSystem = null;
        if (!numberingSystemValue.IsUndefined())
        {
            numberingSystem = TypeConverter.ToString(numberingSystemValue);
            if (!IntlUtilities.IsValidUnicodeExtensionValue(numberingSystem))
            {
                Throw.RangeError(_realm, $"Invalid numberingSystem: {numberingSystem}");
            }

            // 9.2.7 step 10. IsSupportedNumberingSystem below asks the provider, whose default answers
            // OrdinalIgnoreCase, so 'LATN' was accepted and then reported verbatim from resolvedOptions().
            numberingSystem = IntlUtilities.CanonicalizeUValue("nu", numberingSystem);
        }

        // Step 16: style
        var style = GetStringOption(optionsObj, "style", StyleValues, "long");

        // Step 18: numeric
        var numeric = GetStringOption(optionsObj, "numeric", NumericValues, "always");

        // Resolve locale (don't re-read localeMatcher from options)
        var requestedLocales = IntlUtilities.CanonicalizeLocaleList(_engine, locales);
        var availableLocales = IntlUtilities.GetAvailableLocales();
        var resolvedLocale = ResolveRelativeTimeFormatLocale(_engine, availableLocales, requestedLocales, localeMatcher);

        // Resolve numbering system per https://tc39.es/ecma402/#sec-resolvelocale (9.2.7) step 13 for the
        // relevant extension key "nu": the option wins, then the locale's -u-nu- extension, then the
        // locale's own default.
        string? localeNumberingSystem = null;
        foreach (var loc in requestedLocales)
        {
            localeNumberingSystem = UnicodeExtension.GetKeywordValue(loc, "nu");
            if (localeNumberingSystem != null)
            {
                break;
            }
        }

        string resolvedNumberingSystem;
        if (numberingSystem != null && IsSupportedNumberingSystem(numberingSystem))
        {
            // Options value is valid and supported - use it
            resolvedNumberingSystem = numberingSystem;
        }
        else if (localeNumberingSystem != null && IsSupportedNumberingSystem(localeNumberingSystem))
        {
            // Fall back to locale extension value
            resolvedNumberingSystem = localeNumberingSystem;
        }
        else
        {
            // keyLocaleData[0] — the locale's own default, which is Latin only when the provider says so
            resolvedNumberingSystem = IntlUtilities.GetLocaleDefaultNumberingSystem(_engine, resolvedLocale) ?? "latn";
        }

        // Adjust the resolved locale based on numbering system source
        // Per spec:
        // - If options.numberingSystem overrides locale extension with different value, remove nu from locale
        // - If options.numberingSystem matches locale extension, keep the extension
        // - If locale extension is used (no valid options value), keep the extension
        var finalResolvedLocale = resolvedLocale;
        var numberingSystemFromOptions = numberingSystem != null && IsSupportedNumberingSystem(numberingSystem);

        if (numberingSystemFromOptions)
        {
            // Check if the options value matches the locale extension
            if (localeNumberingSystem != null &&
                string.Equals(numberingSystem, localeNumberingSystem, StringComparison.OrdinalIgnoreCase))
            {
                // Options matches locale extension - keep the extension
                finalResolvedLocale = UnicodeExtension.WithKeyword(resolvedLocale, "nu", resolvedNumberingSystem);
            }
            else
            {
                // Options overrode locale extension with different value - remove nu from resolved locale
                finalResolvedLocale = UnicodeExtension.WithKeyword(resolvedLocale, "nu", null);
            }
        }
        else if (localeNumberingSystem != null && IsSupportedNumberingSystem(localeNumberingSystem))
        {
            // Locale extension is used - ensure it's in the resolved locale
            finalResolvedLocale = UnicodeExtension.WithKeyword(resolvedLocale, "nu", resolvedNumberingSystem);
        }
        else
        {
            // Default is used - remove any unsupported nu extension
            finalResolvedLocale = UnicodeExtension.WithKeyword(resolvedLocale, "nu", null);
        }

        // Get CultureInfo for the locale
        var culture = IntlUtilities.GetCultureInfo(finalResolvedLocale) ?? CultureInfo.InvariantCulture;

        // Get prototype from newTarget (for cross-realm construction)
        var proto = GetPrototypeFromConstructor(newTarget, static intrinsics => intrinsics.RelativeTimeFormat.PrototypeObject);

        // https://tc39.es/ecma402/#sec-InitializeRelativeTimeFormat steps 25-27: the NumberFormat this
        // formatter substitutes into its patterns is constructed with [[NumberingSystem]] written into its
        // options, not left to re-derive it from the locale. It has to be told: the resolved locale drops the
        // -u-nu- subtag whenever the numberingSystem option overrode one, so a NumberFormat built from the
        // locale alone would write the locale's digits where this formatter reports another system's.
        var numberFormatConstructor = (NumberFormatConstructor) _realm.Intrinsics.NumberFormat;
        var numberFormatOptions = OrdinaryObjectCreate(_engine, null);
        numberFormatOptions.CreateDataPropertyOrThrow("numberingSystem", resolvedNumberingSystem);
        var numberFormat = (JsNumberFormat) numberFormatConstructor.Construct([new JsString(finalResolvedLocale), numberFormatOptions], numberFormatConstructor);

        return new JsRelativeTimeFormat(
            _engine,
            proto,
            finalResolvedLocale,
            Data.ResolvedNumberingSystem.Resolve(_engine, resolvedNumberingSystem),
            style,
            numeric,
            culture,
            numberFormat);
    }

    private bool IsSupportedNumberingSystem(string numberingSystem)
    {
        // A system is supported when the provider can supply its digits — the embedded table is the
        // default provider's answer, not the engine's own, so a host that adds a system is honoured here.
        return IntlUtilities.IsSupportedNumberingSystem(_engine, numberingSystem);
    }

    private string GetStringOption(ObjectInstance options, string property, string[]? values, string fallback)
    {
        var value = options.Get(property);
        if (value.IsUndefined())
        {
            return fallback;
        }

        var stringValue = TypeConverter.ToString(value);

        if (values != null && values.Length > 0)
        {
            var found = false;
            foreach (var allowed in values)
            {
                if (string.Equals(stringValue, allowed, StringComparison.Ordinal))
                {
                    found = true;
                    break;
                }
            }

            if (!found)
            {
                Throw.RangeError(_realm, $"Invalid value '{stringValue}' for option '{property}'");
            }
        }

        return stringValue;
    }

    private static string ResolveRelativeTimeFormatLocale(Engine engine, HashSet<string> availableLocales, List<string> requestedLocales, string localeMatcher)
    {
        var resolved = IntlUtilities.ResolveLocale(engine, availableLocales, requestedLocales, localeMatcher, []);
        return resolved.Locale;
    }

    /// <summary>
    /// https://tc39.es/ecma402/#sec-Intl.RelativeTimeFormat.supportedLocalesOf
    /// </summary>
    [JsFunction(Length = 1)]
    private JsArray SupportedLocalesOf(JsValue thisObject, JsValue locales, JsValue options)
    {

        var requestedLocales = IntlUtilities.CanonicalizeLocaleList(_engine, locales);
        var availableLocales = IntlUtilities.GetAvailableLocales();

        // Validate localeMatcher option
        var optionsObj = IntlUtilities.CoerceOptionsToObject(_engine, options);
        GetStringOption(optionsObj, "localeMatcher", LocaleMatcherValues, "best fit");

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
