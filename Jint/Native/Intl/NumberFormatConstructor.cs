using System.Buffers;
using System.Globalization;
using System.Text;
using Jint.Native.Function;
using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Descriptors;
using Jint.Runtime.Interop;

namespace Jint.Native.Intl;

/// <summary>
/// https://tc39.es/ecma402/#sec-intl-numberformat-constructor
/// </summary>
[JsObject(UseShape = true)]
internal sealed partial class NumberFormatConstructor : Constructor
{
    private static readonly JsString _functionName = new("NumberFormat");
    private static readonly string[] LocaleMatcherValues = ["lookup", "best fit"];
    private static readonly string[] StyleValues = ["decimal", "percent", "currency", "unit"];
    private static readonly string[] CurrencyDisplayValues = ["code", "symbol", "narrowSymbol", "name"];
    private static readonly string[] CurrencySignValues = ["standard", "accounting"];
    private static readonly string[] UnitDisplayValues = ["short", "narrow", "long"];
    private static readonly string[] NotationValues = ["standard", "scientific", "engineering", "compact"];
    private static readonly string[] CompactDisplayValues = ["short", "long"];
    private static readonly string[] SignDisplayValues = ["auto", "never", "always", "exceptZero", "negative"];
    private static readonly string[] RoundingModeValues = ["ceil", "floor", "expand", "trunc", "halfCeil", "halfFloor", "halfExpand", "halfTrunc", "halfEven"];
    private static readonly string[] RoundingPriorityValues = ["auto", "morePrecision", "lessPrecision"];
    private static readonly string[] TrailingZeroDisplayValues = ["auto", "stripIfInteger"];

    public NumberFormatConstructor(
        Engine engine,
        Realm realm,
        FunctionPrototype functionPrototype,
        ObjectPrototype objectPrototype) : base(engine, realm, _functionName)
    {
        _prototype = functionPrototype;
        PrototypeObject = new NumberFormatPrototype(engine, realm, this, objectPrototype);
        _length = new PropertyDescriptor(JsNumber.PositiveZero, PropertyFlag.Configurable);
        _prototypeDescriptor = new PropertyDescriptor(PrototypeObject, PropertyFlag.AllForbidden);
    }

    protected override void Initialize() => CreateProperties_Generated();

    public NumberFormatPrototype PrototypeObject { get; }

    /// <summary>
    /// https://tc39.es/ecma402/#sec-intl.numberformat
    /// Called when Intl.NumberFormat is invoked without `new`.
    /// </summary>
    protected internal override JsValue Call(JsValue thisObject, JsCallArguments arguments)
    {
        // Per spec: If NewTarget is undefined, let newTarget be the active function object
        var numberFormat = Construct(arguments, this);

        // Normative-optional ChainNumberFormat: when called as a plain function on a NumberFormat
        // receiver, stash the formatter under %Intl%.[[FallbackSymbol]] and return the receiver.
        return IntlUtilities.ChainLegacyConstructor(_realm, this, thisObject, numberFormat);
    }

    /// <summary>
    /// https://tc39.es/ecma402/#sec-intl.numberformat
    /// </summary>
    public override ObjectInstance Construct(JsCallArguments arguments, JsValue newTarget)
    {
        var locales = arguments.At(0);
        var options = arguments.At(1);

        // Get options object
        var optionsObj = IntlUtilities.CoerceOptionsToObject(_engine, options);

        // 1. Read options in spec-defined order
        // Read localeMatcher first (per spec order)
        var localeMatcher = GetStringOption(optionsObj, "localeMatcher", LocaleMatcherValues, "best fit");

        // Read numberingSystem option (and validate it)
        var numberingSystemValue = optionsObj.Get("numberingSystem");
        string? numberingSystem = null;
        if (!numberingSystemValue.IsUndefined())
        {
            numberingSystem = TypeConverter.ToString(numberingSystemValue);

            // https://tc39.es/ecma402/#sec-resolveoptions (9.2.8) step 6.d.ii: the check is the "type"
            // Unicode locale nonterminal, alphanum{3,8} (sep alphanum{3,8})*, and nothing narrower.
            // The hand-rolled check this replaces was narrower in two ways that both misreported:
            // char.IsAsciiLetterLower made 'LATN' a RangeError where the nonterminal matches it, and no
            // hyphen was admitted at all, so a well-formed multi-subtag value threw where it should
            // simply have matched no numbering system and left the default in place.
            if (!IntlUtilities.IsValidUnicodeExtensionValue(numberingSystem))
            {
                Throw.RangeError(_realm, $"Invalid numberingSystem: {numberingSystem}");
            }

            // 9.2.7 step 10, immediately before the keyLocaleData lookup below.
            numberingSystem = IntlUtilities.CanonicalizeUValue("nu", numberingSystem);
        }

        // Resolve locale using already-read localeMatcher
        var requestedLocales = IntlUtilities.CanonicalizeLocaleList(_engine, locales);
        var availableLocales = IntlUtilities.GetAvailableLocales();
        var resolvedLocale = ResolveNumberFormatLocale(_engine, availableLocales, requestedLocales, localeMatcher);

        // SetNumberFormatUnitOptions - read style, currency, currencyDisplay, currencySign, unit, unitDisplay
        var style = GetStringOption(optionsObj, "style", StyleValues, "decimal");

        // Read currency option (always read, validate if style is currency)
        var currencyValue = optionsObj.Get("currency");
        string? currency = null;
        if (!currencyValue.IsUndefined())
        {
            currency = TypeConverter.ToString(currencyValue).ToUpperInvariant();
            if (!IsWellFormedCurrencyCode(currency))
            {
                Throw.RangeError(_realm, $"Invalid currency code: {currency}");
            }
        }
        else if (string.Equals(style, "currency", StringComparison.Ordinal))
        {
            Throw.TypeError(_realm, "Currency code is required with currency style");
        }

        // Read currencyDisplay option (always read)
        var currencyDisplay = GetStringOption(optionsObj, "currencyDisplay", CurrencyDisplayValues, "symbol");

        // Read currencySign option (always read)
        var currencySign = GetStringOption(optionsObj, "currencySign", CurrencySignValues, "standard");

        // Read unit option (always read, validate if style is unit)
        var unitValue = optionsObj.Get("unit");
        string? unit = null;
        if (!unitValue.IsUndefined())
        {
            unit = TypeConverter.ToString(unitValue);
            if (!IsWellFormedUnitIdentifier(unit))
            {
                Throw.RangeError(_realm, $"Invalid unit: {unit}");
            }
        }
        else if (string.Equals(style, "unit", StringComparison.Ordinal))
        {
            Throw.TypeError(_realm, "Unit is required with unit style");
        }

        // Read unitDisplay option (always read)
        var unitDisplay = GetStringOption(optionsObj, "unitDisplay", UnitDisplayValues, "short");

        // Read notation option
        var notation = GetStringOption(optionsObj, "notation", NotationValues, "standard");

        var isCurrency = string.Equals(style, "currency", StringComparison.Ordinal);
        var isPercent = string.Equals(style, "percent", StringComparison.Ordinal);
        var isCompact = string.Equals(notation, "compact", StringComparison.Ordinal);

        // SetNumberFormatDigitOptions - digit options in spec order
        var minimumIntegerDigits = GetNumberOption(optionsObj, "minimumIntegerDigits", 1, 21, 1);

        // Determine default fraction digits based on style and notation
        // Per spec: currency-specific digits only apply when notation is "standard"
        var isStandardNotation = string.Equals(notation, "standard", StringComparison.Ordinal);
        int minFracDefault, maxFracDefault;
        if (isCurrency && isStandardNotation)
        {
            // Only use currency-specific digits for standard notation
            minFracDefault = maxFracDefault = GetCurrencyDigits(currency!);
        }
        else if (isPercent)
        {
            minFracDefault = maxFracDefault = 0;
        }
        else if (isCompact)
        {
            // Compact notation uses 0 fraction digits by default
            minFracDefault = maxFracDefault = 0;
        }
        else
        {
            // Scientific, engineering, and other notations (even with currency style)
            minFracDefault = 0;
            maxFracDefault = 3;
        }

        // Per spec: fractionDigits range is 0-100
        var minFracDigitsValue = optionsObj.Get("minimumFractionDigits");
        var maxFracDigitsValue = optionsObj.Get("maximumFractionDigits");
        var hasFractionDigits = !minFracDigitsValue.IsUndefined() || !maxFracDigitsValue.IsUndefined();
        var minimumFractionDigits = GetNumberOptionFromValue(minFracDigitsValue, "minimumFractionDigits", 0, 100, minFracDefault);
        // Per spec: maximumFractionDigits minimum is 0, fallback is max(mnfd, mxfdDefault)
        var maximumFractionDigits = GetNumberOptionFromValue(maxFracDigitsValue, "maximumFractionDigits", 0, 100, System.Math.Max(minimumFractionDigits, maxFracDefault));

        // Per spec: if mnfd > mxfd, adjust mnfd down to mxfd
        if (minimumFractionDigits > maximumFractionDigits)
        {
            minimumFractionDigits = maximumFractionDigits;
        }

        // Significant digits options - read in order (must only read each property once)
        var minSigDigitsValue = optionsObj.Get("minimumSignificantDigits");
        var maxSigDigitsValue = optionsObj.Get("maximumSignificantDigits");

        int? minimumSignificantDigits = null;
        int? maximumSignificantDigits = null;
        var minimumSignificantDigitsExplicit = !minSigDigitsValue.IsUndefined();
        var maximumSignificantDigitsExplicit = !maxSigDigitsValue.IsUndefined();

        // If either significant digits option is present, use significant digits mode
        if (!minSigDigitsValue.IsUndefined() || !maxSigDigitsValue.IsUndefined())
        {
            minimumSignificantDigits = GetNumberOptionFromValue(minSigDigitsValue, "minimumSignificantDigits", 1, 21, 1);
            maximumSignificantDigits = GetNumberOptionFromValue(maxSigDigitsValue, "maximumSignificantDigits", minimumSignificantDigits.Value, 21, 21);
        }

        // Rounding options in spec order
        var roundingIncrement = GetRoundingIncrementOption(optionsObj);
        var roundingMode = GetStringOption(optionsObj, "roundingMode", RoundingModeValues, "halfExpand");
        var roundingPriority = GetStringOption(optionsObj, "roundingPriority", RoundingPriorityValues, "auto");
        var trailingZeroDisplay = GetStringOption(optionsObj, "trailingZeroDisplay", TrailingZeroDisplayValues, "auto");

        // Remaining options in spec order
        var compactDisplay = GetStringOption(optionsObj, "compactDisplay", CompactDisplayValues, "short");
        var useGrouping = GetUseGroupingOption(optionsObj, notation);
        var signDisplay = GetStringOption(optionsObj, "signDisplay", SignDisplayValues, "auto");

        // https://tc39.es/ecma402/#sec-setnumberformatdigitoptions: compact notation asked for no digit
        // option at all is the one case that leaves both roundings unconfigured, and the algorithm gives
        // it a rounding of its own — one to two significant digits, at more-precision against no fraction
        // digits. That is the rule that writes "1.2K" for 1234 and "12K" for 12345, and the compact lane
        // now reads it here instead of open-coding a rounding of its own.
        if (isCompact
            && !minimumSignificantDigits.HasValue
            && !maximumSignificantDigits.HasValue
            && !hasFractionDigits
            && string.Equals(roundingPriority, "auto", StringComparison.Ordinal))
        {
            minimumFractionDigits = 0;
            maximumFractionDigits = 0;
            minimumSignificantDigits = 1;
            maximumSignificantDigits = 2;
            roundingPriority = "morePrecision";
        }

        // Validate roundingIncrement constraints
        if (roundingIncrement > 1)
        {
            // roundingIncrement > 1 with roundingPriority != "auto" is TypeError
            if (!string.Equals(roundingPriority, "auto", StringComparison.Ordinal))
            {
                Throw.TypeError(_realm, "roundingIncrement cannot be used with roundingPriority other than 'auto'");
            }

            // roundingIncrement > 1 with significantDigits is TypeError
            if (minimumSignificantDigits.HasValue || maximumSignificantDigits.HasValue)
            {
                Throw.TypeError(_realm, "roundingIncrement cannot be used with significantDigits");
            }

            // roundingIncrement > 1 requires min == max fraction digits
            if (minimumFractionDigits != maximumFractionDigits)
            {
                Throw.RangeError(_realm, "roundingIncrement requires minimumFractionDigits to equal maximumFractionDigits");
            }
        }

        // Get NumberFormatInfo for the locale
        // Try the first requested locale first (e.g., "zh-TW") before falling back to resolved locale (e.g., "zh")
        // This ensures we get locale-specific data like NaN symbols from the exact requested locale
        CultureInfo culture;
        if (requestedLocales.Count > 0)
        {
            culture = IntlUtilities.GetCultureInfo(requestedLocales[0])
                      ?? IntlUtilities.GetCultureInfo(resolvedLocale)
                      ?? CultureInfo.InvariantCulture;
        }
        else
        {
            culture = IntlUtilities.GetCultureInfo(resolvedLocale) ?? CultureInfo.InvariantCulture;
        }
        var numberFormatInfo = (NumberFormatInfo) culture.NumberFormat.Clone();

        // Apply digit options - .NET NumberFormatInfo only supports 0-99, so clamp
        var clampedMaxFractionDigits = System.Math.Min(maximumFractionDigits, 99);
        numberFormatInfo.NumberDecimalDigits = clampedMaxFractionDigits;
        numberFormatInfo.CurrencyDecimalDigits = clampedMaxFractionDigits;
        numberFormatInfo.PercentDecimalDigits = clampedMaxFractionDigits;

        // Apply currency symbol based on currencyDisplay option. "code" is the currency code by
        // specification, so it is the one display the provider is not asked about.
        if (currency != null)
        {
            var currencyData = currencyDisplay is "code"
                ? null
                : _engine.Options.Intl.CldrProvider.GetCurrencyData(resolvedLocale, currency);

            numberFormatInfo.CurrencySymbol = currencyDisplay switch
            {
                "code" => currency, // e.g., "USD"
                "narrowSymbol" => currencyData?.NarrowSymbol ?? currencyData?.Symbol ?? currency, // e.g., "$"
                "name" => currencyData?.DisplayName ?? currency, // e.g., "US dollars"
                _ => currencyData?.Symbol ?? currency // "symbol": "$", or "US$" depending on locale
            };
        }

        // Resolve numbering system per https://tc39.es/ecma402/#sec-resolvelocale (9.2.7) step 13 for the
        // relevant extension key "nu":
        // 1. If options.numberingSystem is a supported system, use it
        // 2. Otherwise, fall back to locale extension if supported
        // 3. Otherwise, use the locale's own default, and Latin only when that has no opinion either
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
        bool numberingSystemFromOptions = false;

        if (numberingSystem != null && IsSupportedNumberingSystem(numberingSystem))
        {
            // Options value is valid and supported - use it
            resolvedNumberingSystem = numberingSystem;
            numberingSystemFromOptions = true;
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
        var finalResolvedLocale = resolvedLocale;
        if (localeNumberingSystem != null)
        {
            if (numberingSystemFromOptions && !string.Equals(numberingSystem, localeNumberingSystem, StringComparison.OrdinalIgnoreCase))
            {
                // Options overrode locale extension - remove nu from resolved locale
                finalResolvedLocale = UnicodeExtension.WithKeyword(resolvedLocale, "nu", null);
            }
            else if (!IsSupportedNumberingSystem(localeNumberingSystem))
            {
                // Locale extension was invalid - remove it
                finalResolvedLocale = UnicodeExtension.WithKeyword(resolvedLocale, "nu", null);
            }
            else
            {
                // Locale extension is used - ensure it's in the resolved locale
                finalResolvedLocale = UnicodeExtension.WithKeyword(resolvedLocale, "nu", resolvedNumberingSystem);
            }
        }

        // Get prototype from newTarget (for cross-realm construction)
        var proto = GetPrototypeFromConstructor(newTarget, static intrinsics => intrinsics.NumberFormat.PrototypeObject);

        return new JsNumberFormat(
            _engine,
            proto,
            finalResolvedLocale,
            Data.ResolvedNumberingSystem.Resolve(_engine, resolvedNumberingSystem),
            style,
            currency,
            currencyDisplay,
            currencySign,
            unit,
            unitDisplay,
            notation,
            compactDisplay,
            signDisplay,
            useGrouping,
            minimumIntegerDigits,
            minimumFractionDigits,
            maximumFractionDigits,
            minimumSignificantDigits,
            maximumSignificantDigits,
            minimumSignificantDigitsExplicit,
            maximumSignificantDigitsExplicit,
            roundingMode,
            roundingPriority,
            roundingIncrement,
            trailingZeroDisplay,
            numberFormatInfo,
            culture);
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

    private string GetUseGroupingOption(ObjectInstance options, string notation)
    {
        var value = options.Get("useGrouping");
        if (value.IsUndefined())
        {
            // Default depends on notation: compact => "min2", otherwise => "auto"
            return string.Equals(notation, "compact", StringComparison.Ordinal) ? "min2" : "auto";
        }

        // Handle boolean values
        if (value.IsBoolean())
        {
            return TypeConverter.ToBoolean(value) ? "always" : "false";
        }

        // Handle string values
        var stringValue = TypeConverter.ToString(value);

        // Per spec: strings "true" and "false" should return default ("auto")
        if (string.Equals(stringValue, "true", StringComparison.Ordinal) ||
            string.Equals(stringValue, "false", StringComparison.Ordinal))
        {
            return "auto";
        }

        // Handle falsy values (0, null, "") - they all convert to "false"
        if (value.IsNull() || (value.IsNumber() && TypeConverter.ToNumber(value) == 0) ||
            (value.IsString() && stringValue.Length == 0))
        {
            return "false";
        }

        // Validate against allowed string values (auto, always, min2)
        if (string.Equals(stringValue, "auto", StringComparison.Ordinal) ||
            string.Equals(stringValue, "always", StringComparison.Ordinal) ||
            string.Equals(stringValue, "min2", StringComparison.Ordinal))
        {
            return stringValue;
        }

        Throw.RangeError(_realm, $"Invalid value '{stringValue}' for option 'useGrouping'");
        return "auto"; // Never reached
    }

    private static readonly int[] ValidRoundingIncrements = [1, 2, 5, 10, 20, 25, 50, 100, 200, 250, 500, 1000, 2000, 2500, 5000];

    private int GetRoundingIncrementOption(ObjectInstance options)
    {
        var value = options.Get("roundingIncrement");
        if (value.IsUndefined())
        {
            return 1;
        }

        var number = TypeConverter.ToNumber(value);
        if (!double.IsFinite(number))
        {
            Throw.RangeError(_realm, "roundingIncrement must be a finite number");
        }

        var intValue = (int) System.Math.Floor(number);

        // Must be one of the valid values
        if (!System.Array.Exists(ValidRoundingIncrements, v => v == intValue))
        {
            Throw.RangeError(_realm, $"Invalid roundingIncrement value: {intValue}");
        }

        return intValue;
    }

    private int GetNumberOption(ObjectInstance options, string property, int minimum, int maximum, int fallback)
    {
        var value = options.Get(property);
        return GetNumberOptionFromValue(value, property, minimum, maximum, fallback);
    }

    private int GetNumberOptionFromValue(JsValue value, string property, int minimum, int maximum, int fallback)
    {
        if (value.IsUndefined())
        {
            return fallback;
        }

        var number = TypeConverter.ToNumber(value);
        if (double.IsNaN(number))
        {
            Throw.RangeError(_realm, $"Invalid number for option '{property}'");
        }

        var intValue = (int) System.Math.Floor(number);
        if (intValue < minimum || intValue > maximum)
        {
            Throw.RangeError(_realm, $"Value {intValue} for option '{property}' is out of range [{minimum}, {maximum}]");
        }

        return intValue;
    }

    private static int GetCurrencyDigits(string currency)
    {
        // Most currencies use 2 decimal places
        // Some exceptions:
        return currency switch
        {
            "BHD" or "IQD" or "JOD" or "KWD" or "LYD" or "OMR" or "TND" => 3, // 3 decimal places
            "BIF" or "CLP" or "DJF" or "GNF" or "ISK" or "JPY" or "KMF" or
            "KRW" or "PYG" or "RWF" or "UGX" or "UYI" or "VND" or "VUV" or
            "XAF" or "XOF" or "XPF" => 0, // 0 decimal places
            _ => 2 // Default 2 decimal places
        };
    }

    private static bool IsWellFormedCurrencyCode(string currency)
    {
        // Per spec: currency code must be exactly 3 ASCII letters (A-Z, a-z)
        return currency.Length == 3 &&
               char.IsAsciiLetter(currency[0]) &&
               char.IsAsciiLetter(currency[1]) &&
               char.IsAsciiLetter(currency[2]);
    }

    private static readonly StringSearchValues SanctionedUnits = new(
    [
        "acre", "bit", "byte", "celsius", "centimeter", "day", "degree",
        "fahrenheit", "fluid-ounce", "foot", "gallon", "gigabit", "gigabyte",
        "gram", "hectare", "hour", "inch", "kilobit", "kilobyte", "kilogram",
        "kilometer", "liter", "megabit", "megabyte", "meter", "microsecond",
        "mile", "mile-scandinavian", "milliliter", "millimeter", "millisecond",
        "minute", "month", "nanosecond", "ounce", "percent", "petabyte",
        "pound", "second", "stone", "terabit", "terabyte", "week", "yard", "year"
    ], StringComparison.Ordinal);

    private static bool IsWellFormedUnitIdentifier(string unit)
    {
        if (SanctionedUnits.Contains(unit))
        {
            return true;
        }

        // Check for compound unit (e.g., "mile-per-hour")
        var perIndex = unit.IndexOf("-per-", StringComparison.Ordinal);
        if (perIndex > 0)
        {
            var numerator = unit.Substring(0, perIndex);
            var denominator = unit.Substring(perIndex + 5);
            return SanctionedUnits.Contains(numerator) && SanctionedUnits.Contains(denominator);
        }

        return false;
    }

    private static string ResolveNumberFormatLocale(Engine engine, HashSet<string> availableLocales, List<string> requestedLocales, string localeMatcher)
    {
        var resolved = IntlUtilities.ResolveLocale(engine, availableLocales, requestedLocales, localeMatcher, []);
        return resolved.Locale;
    }

    /// <summary>
    /// https://tc39.es/ecma402/#sec-intl.numberformat.supportedlocalesof
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
