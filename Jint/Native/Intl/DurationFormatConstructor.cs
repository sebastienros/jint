using System.Globalization;
using Jint.Native.Function;
using Jint.Native.Intl.Data;
using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Descriptors;
using Jint.Runtime.Interop;

namespace Jint.Native.Intl;

/// <summary>
/// https://tc39.es/proposal-intl-duration-format/
/// </summary>
[JsObject(UseShape = true)]
internal sealed partial class DurationFormatConstructor : Constructor
{
    private static readonly JsString _functionName = new("DurationFormat");
    private static readonly string[] LocaleMatcherValues = ["lookup", "best fit"];
    private static readonly string[] StyleValues = ["long", "short", "narrow", "digital"];
    private static readonly string[] UnitStyleValues = ["long", "short", "narrow"];
    private static readonly string[] UnitStyleWithNumericValues = ["long", "short", "narrow", "numeric", "2-digit"];
    private static readonly string[] SubSecondStyleValues = ["long", "short", "narrow", "numeric"];
    private static readonly string[] DisplayValues = ["auto", "always"];

    public DurationFormatConstructor(
        Engine engine,
        Realm realm,
        FunctionPrototype functionPrototype,
        ObjectPrototype objectPrototype) : base(engine, realm, _functionName)
    {
        _prototype = functionPrototype;
        PrototypeObject = new DurationFormatPrototype(engine, realm, this, objectPrototype);
        _length = new PropertyDescriptor(JsNumber.PositiveZero, PropertyFlag.Configurable);
        _prototypeDescriptor = new PropertyDescriptor(PrototypeObject, PropertyFlag.AllForbidden);
    }

    protected override void Initialize() => CreateProperties_Generated();

    public DurationFormatPrototype PrototypeObject { get; }

    /// <summary>
    /// Called when Intl.DurationFormat is invoked without `new`.
    /// </summary>
    protected internal override JsValue Call(JsValue thisObject, JsCallArguments arguments)
    {
        Throw.TypeError(_realm, "Constructor Intl.DurationFormat requires 'new'");
        return Undefined;
    }

    /// <summary>
    /// https://tc39.es/proposal-intl-duration-format/#sec-intl.durationformat
    /// </summary>
    public override ObjectInstance Construct(JsCallArguments arguments, JsValue newTarget)
    {
        var locales = arguments.At(0);
        var options = arguments.At(1);

        // Get options object (strict - throws TypeError for non-object)
        var optionsObj = IntlUtilities.GetOptionsObject(_engine, options);

        // Per spec steps 5, 6, 13: Get options in the correct order
        // Step 5: localeMatcher
        var localeMatcher = GetStringOption(optionsObj, "localeMatcher", LocaleMatcherValues, "best fit");

        // Step 6: numberingSystem option (must be read before style)
        var numberingSystemOption = GetNumberingSystemOption(optionsObj);

        // Step 13: style (must be read after localeMatcher and numberingSystem, before unit options)
        var style = GetStringOption(optionsObj, "style", StyleValues, "short");

        // Resolve locale (don't re-read localeMatcher from options)
        var requestedLocales = IntlUtilities.CanonicalizeLocaleList(_engine, locales);
        var availableLocales = IntlUtilities.GetAvailableLocales();
        var resolved = IntlUtilities.ResolveLocale(_engine, availableLocales, requestedLocales, localeMatcher, []);

        // Parse -u-nu- extension from the first requested locale
        string? extensionNu = null;
        if (requestedLocales.Count > 0)
        {
            extensionNu = UnicodeExtension.GetKeywordValue(requestedLocales[0], "nu");
        }

        // Resolve numbering system: option overrides extension; validate both
        var numberingSystem = ResolveNumberingSystem(numberingSystemOption, extensionNu);

        // Build resolved locale: include -u-nu- extension when resolved value matches extension value
        var resolvedLocale = resolved.Locale;
        if (extensionNu != null && string.Equals(numberingSystem, extensionNu, StringComparison.Ordinal))
        {
            resolvedLocale = UnicodeExtension.WithKeyword(resolved.Locale, "nu", numberingSystem);
        }

        // Determine base style based on overall style (for date units)
        var baseStyle = style switch
        {
            "digital" => "short",
            _ => style // "long", "short", "narrow"
        };

        // Per spec, when style is "short", all units default to "short"
        var baseTimeStyle = style switch
        {
            "long" => "long",
            "short" => "short",
            "narrow" => "narrow",
            "digital" => "numeric",
            _ => "short"
        };
        var isDigital = string.Equals(style, "digital", StringComparison.Ordinal);

        // Helper to check if a style is numeric-like
        bool IsNumericLike(string s) =>
            string.Equals(s, "numeric", StringComparison.Ordinal) ||
            string.Equals(s, "2-digit", StringComparison.Ordinal);

        // Helper to check if a style is numeric-like or fractional (for sub-second units)
        bool IsNumericLikeOrFractional(string s) =>
            IsNumericLike(s) ||
            string.Equals(s, "fractional", StringComparison.Ordinal);

        // https://tc39.es/ecma402/#sec-getdurationunitoptions steps 1-6, and step 1 of ValidateDurationUnitStyle.
        // The display default is "always" when the unit's own style option was given, for digital's hours,
        // minutes and seconds, and for the minutes and seconds after a numeric unit; otherwise "auto".
        // A numeric sub-second unit is "fractional" (step 4): it is folded into the unit above it, defaults
        // to "auto", and cannot be "always". Answers the style, and the display through an out parameter (net462 and
        // netstandard2.0 have no ValueTuple on 4.x).
        string GetUnitOptions(string unit, string displayField, string[] stylesList, string defaultStyle, string? prevStyle, out string display)
        {
            var styleGiven = TryGetStringOption(optionsObj, unit, stylesList, out var unitStyle);
            if (!styleGiven)
            {
                unitStyle = defaultStyle;
            }

            var isMinutesOrSeconds = unit is "minutes" or "seconds";
            var isFractional = unit is "milliseconds" or "microseconds" or "nanoseconds"
                && string.Equals(unitStyle, "numeric", StringComparison.Ordinal);

            string displayDefault;
            if (isFractional)
            {
                displayDefault = "auto";
            }
            else if (styleGiven)
            {
                displayDefault = "always";
            }
            else if (isDigital)
            {
                displayDefault = isMinutesOrSeconds || string.Equals(unit, "hours", StringComparison.Ordinal) ? "always" : "auto";
            }
            else if (prevStyle is not null && IsNumericLikeOrFractional(prevStyle))
            {
                displayDefault = isMinutesOrSeconds ? "always" : "auto";
            }
            else
            {
                displayDefault = "auto";
            }

            display = GetStringOption(optionsObj, displayField, DisplayValues, displayDefault);
            if (isFractional && string.Equals(display, "always", StringComparison.Ordinal))
            {
                Throw.RangeError(_realm, $"{displayField} cannot be 'always' when {unit} uses the numeric style");
            }

            return unitStyle;
        }

        // Get unit style options with cascading defaults per spec (GetDurationUnitOptions)
        // Per Table 1: Years/months/weeks/days use baseStyle
        var yearsStyle = GetUnitOptions("years", "yearsDisplay", UnitStyleValues, baseStyle, prevStyle: null, out var yearsDisplay);

        var monthsStyle = GetUnitOptions("months", "monthsDisplay", UnitStyleValues, baseStyle, prevStyle: null, out var monthsDisplay);

        var weeksStyle = GetUnitOptions("weeks", "weeksDisplay", UnitStyleValues, baseStyle, prevStyle: null, out var weeksDisplay);

        var daysStyle = GetUnitOptions("days", "daysDisplay", UnitStyleValues, baseStyle, prevStyle: null, out var daysDisplay);

        // Per Table 1: Hours/minutes/seconds/sub-seconds use baseTimeStyle
        // Hours can use numeric/2-digit
        var hoursDefault = isDigital ? "numeric" : baseTimeStyle;
        var hoursStyle = GetUnitOptions("hours", "hoursDisplay", UnitStyleWithNumericValues, hoursDefault, prevStyle: null, out var hoursDisplay);
        var prevStyle = hoursStyle;

        // Minutes/seconds: default to 2-digit if previous is numeric-like
        // Per spec: if previous is numeric/2-digit, current cannot be long/short/narrow
        var minutesDefault = IsNumericLike(prevStyle) ? "2-digit" : (isDigital ? "numeric" : baseTimeStyle);
        var minutesStyle = GetUnitOptions("minutes", "minutesDisplay", UnitStyleWithNumericValues, minutesDefault, prevStyle, out var minutesDisplay);
        if (IsNumericLike(prevStyle) && !IsNumericLike(minutesStyle))
        {
            Throw.RangeError(_realm, "minutes style must be numeric or 2-digit when hours uses numeric or 2-digit");
        }
        prevStyle = minutesStyle;

        var secondsDefault = IsNumericLike(prevStyle) ? "2-digit" : (isDigital ? "numeric" : baseTimeStyle);
        var secondsStyle = GetUnitOptions("seconds", "secondsDisplay", UnitStyleWithNumericValues, secondsDefault, prevStyle, out var secondsDisplay);
        if (IsNumericLike(prevStyle) && !IsNumericLike(secondsStyle))
        {
            Throw.RangeError(_realm, "seconds style must be numeric or 2-digit when minutes uses numeric or 2-digit");
        }
        prevStyle = secondsStyle;

        // Sub-second units: default to numeric if previous is numeric-like
        var millisecondsDefault = IsNumericLike(prevStyle) ? "numeric" : (isDigital ? "numeric" : baseTimeStyle);
        var millisecondsStyle = GetUnitOptions("milliseconds", "millisecondsDisplay", SubSecondStyleValues, millisecondsDefault, prevStyle, out var millisecondsDisplay);
        if (IsNumericLike(prevStyle) && !IsNumericLikeOrFractional(millisecondsStyle))
        {
            Throw.RangeError(_realm, "milliseconds style must be numeric when seconds uses numeric or 2-digit");
        }
        prevStyle = millisecondsStyle;

        var microsecondsDefault = IsNumericLike(prevStyle) ? "numeric" : (isDigital ? "numeric" : baseTimeStyle);
        var microsecondsStyle = GetUnitOptions("microseconds", "microsecondsDisplay", SubSecondStyleValues, microsecondsDefault, prevStyle, out var microsecondsDisplay);
        if (IsNumericLikeOrFractional(prevStyle) && !IsNumericLikeOrFractional(microsecondsStyle))
        {
            Throw.RangeError(_realm, "microseconds style must be numeric when milliseconds uses numeric");
        }
        prevStyle = microsecondsStyle;

        var nanosecondsDefault = IsNumericLike(prevStyle) ? "numeric" : (isDigital ? "numeric" : baseTimeStyle);
        var nanosecondsStyle = GetUnitOptions("nanoseconds", "nanosecondsDisplay", SubSecondStyleValues, nanosecondsDefault, prevStyle, out var nanosecondsDisplay);
        if (IsNumericLikeOrFractional(prevStyle) && !IsNumericLikeOrFractional(nanosecondsStyle))
        {
            Throw.RangeError(_realm, "nanoseconds style must be numeric when microseconds uses numeric");
        }

        // Get fractionalDigits option
        int? fractionalDigits = null;
        var fractionalDigitsValue = optionsObj.Get("fractionalDigits");
        if (!fractionalDigitsValue.IsUndefined())
        {
            var fd = TypeConverter.ToNumber(fractionalDigitsValue);
            if (double.IsNaN(fd) || fd < 0 || fd > 9)
            {
                Throw.RangeError(_realm, "fractionalDigits must be between 0 and 9");
            }
            fractionalDigits = (int) System.Math.Floor(fd);
        }

        // Get CultureInfo for the locale
        var culture = IntlUtilities.GetCultureInfo(resolved.Locale) ?? CultureInfo.InvariantCulture;

        // Get prototype from newTarget (for cross-realm construction)
        var proto = GetPrototypeFromConstructor(newTarget, static intrinsics => intrinsics.DurationFormat.PrototypeObject);

        return new JsDurationFormat(
            _engine,
            proto,
            resolvedLocale,
            style,
            numberingSystem,
            culture,
            yearsStyle,
            monthsStyle,
            weeksStyle,
            daysStyle,
            hoursStyle,
            minutesStyle,
            secondsStyle,
            millisecondsStyle,
            microsecondsStyle,
            nanosecondsStyle,
            yearsDisplay,
            monthsDisplay,
            weeksDisplay,
            daysDisplay,
            hoursDisplay,
            minutesDisplay,
            secondsDisplay,
            millisecondsDisplay,
            microsecondsDisplay,
            nanosecondsDisplay,
            fractionalDigits);
    }

    private string GetStringOption(ObjectInstance options, string property, string[]? values, string fallback)
    {
        return TryGetStringOption(options, property, values, out var value) ? value : fallback;
    }

    private bool TryGetStringOption(ObjectInstance options, string property, string[]? values, out string result)
    {
        var value = options.Get(property);
        if (value.IsUndefined())
        {
            result = string.Empty;
            return false;
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

        result = stringValue;
        return true;
    }

    private string? GetNumberingSystemOption(ObjectInstance options)
    {
        var value = options.Get("numberingSystem");
        if (value.IsUndefined())
        {
            return null;
        }

        var stringValue = TypeConverter.ToString(value);

        // Validate against Unicode Locale Identifier type nonterminal
        // Pattern: (3*8alphanum) *("-" (3*8alphanum))
        if (!IntlUtilities.IsValidUnicodeExtensionValue(stringValue))
        {
            Throw.RangeError(_realm, $"Invalid numbering system: {stringValue}");
        }

        // 9.2.7 step 10. ResolveNumberingSystem below probes NumberingSystemData.Digits, which is keyed
        // OrdinalIgnoreCase, so an uncanonicalized 'LATN' was accepted and then reported back verbatim
        // from resolvedOptions() as a numbering system identifier that does not exist.
        return IntlUtilities.CanonicalizeUValue("nu", stringValue);
    }

    /// <summary>
    /// https://tc39.es/ecma402/#sec-resolvelocale (9.2.7) step 13 for <c>nu</c>, which
    /// https://tc39.es/proposal-intl-duration-format/#sec-Intl.DurationFormat-internal-slots lists as a
    /// relevant extension key: the option wins, then the locale's <c>-u-nu-</c> extension, then Latin.
    /// </summary>
    private static string ResolveNumberingSystem(string? optionValue, string? extensionValue)
    {
        // Options override extension
        if (optionValue != null && NumberingSystemData.Digits.ContainsKey(optionValue))
        {
            return optionValue;
        }

        // Extension fallback
        if (extensionValue != null && NumberingSystemData.Digits.ContainsKey(extensionValue))
        {
            return extensionValue;
        }

        // Default
        return "latn";
    }

    /// <summary>
    /// https://tc39.es/proposal-intl-duration-format/#sec-intl.durationformat.supportedlocalesof
    /// </summary>
    [JsFunction(Length = 1)]
    private JsArray SupportedLocalesOf(JsValue thisObject, JsValue locales, JsValue options)
    {

        var requestedLocales = IntlUtilities.CanonicalizeLocaleList(_engine, locales);
        var availableLocales = IntlUtilities.GetAvailableLocales();

        // Validate localeMatcher option
        var optionsObj = IntlUtilities.CoerceOptionsToObject(_engine, options);
        GetStringOption(optionsObj, "localeMatcher", LocaleMatcherValues, "best fit");

        var supported = new List<JsValue>();
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
