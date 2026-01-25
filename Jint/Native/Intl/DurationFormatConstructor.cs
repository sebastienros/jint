using System.Globalization;
using Jint.Native.Function;
using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Descriptors;
using Jint.Runtime.Interop;

namespace Jint.Native.Intl;

/// <summary>
/// https://tc39.es/proposal-intl-duration-format/
/// </summary>
internal sealed class DurationFormatConstructor : Constructor
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

    protected override void Initialize()
    {
        const PropertyFlag PropertyFlags = PropertyFlag.Configurable | PropertyFlag.Writable;
        var properties = new PropertyDictionary(1, checkExistingKeys: false)
        {
            ["supportedLocalesOf"] = new(new ClrFunction(Engine, "supportedLocalesOf", SupportedLocalesOf, 1, PropertyFlag.Configurable), PropertyFlags)
        };
        SetProperties(properties);
    }

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

        // Step 6: numberingSystem (must be read before style)
        var numberingSystem = GetNumberingSystemOption(optionsObj);

        // Step 13: style (must be read after localeMatcher and numberingSystem, before unit options)
        var style = GetStringOption(optionsObj, "style", StyleValues, "short");

        // Resolve locale (don't re-read localeMatcher from options)
        var requestedLocales = IntlUtilities.CanonicalizeLocaleList(_engine, locales);
        var availableLocales = IntlUtilities.GetAvailableLocales();
        var resolved = IntlUtilities.ResolveLocale(_engine, availableLocales, requestedLocales, localeMatcher, []);

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

        // Get unit style options with cascading defaults per spec (GetDurationUnitOptions)
        // Per Table 1: Years/months/weeks/days use baseStyle
        var yearsStyle = GetStringOption(optionsObj, "years", UnitStyleValues, baseStyle);
        var yearsDisplay = GetStringOption(optionsObj, "yearsDisplay", DisplayValues, "auto");

        var monthsStyle = GetStringOption(optionsObj, "months", UnitStyleValues, baseStyle);
        var monthsDisplay = GetStringOption(optionsObj, "monthsDisplay", DisplayValues, "auto");

        var weeksStyle = GetStringOption(optionsObj, "weeks", UnitStyleValues, baseStyle);
        var weeksDisplay = GetStringOption(optionsObj, "weeksDisplay", DisplayValues, "auto");

        var daysStyle = GetStringOption(optionsObj, "days", UnitStyleValues, baseStyle);
        var daysDisplay = GetStringOption(optionsObj, "daysDisplay", DisplayValues, "auto");

        // Per Table 1: Hours/minutes/seconds/sub-seconds use baseTimeStyle
        // Hours can use numeric/2-digit
        var hoursDefault = isDigital ? "numeric" : baseTimeStyle;
        var hoursStyle = GetStringOption(optionsObj, "hours", UnitStyleWithNumericValues, hoursDefault);
        var hoursDisplay = GetStringOption(optionsObj, "hoursDisplay", DisplayValues, "auto");
        var prevStyle = hoursStyle;

        // Minutes/seconds: default to 2-digit if previous is numeric-like
        // Per spec: if previous is numeric/2-digit, current cannot be long/short/narrow
        var minutesDefault = IsNumericLike(prevStyle) ? "2-digit" : (isDigital ? "numeric" : baseTimeStyle);
        var minutesStyle = GetStringOption(optionsObj, "minutes", UnitStyleWithNumericValues, minutesDefault);
        var minutesDisplay = GetStringOption(optionsObj, "minutesDisplay", DisplayValues, "auto");
        if (IsNumericLike(prevStyle) && !IsNumericLike(minutesStyle))
        {
            Throw.RangeError(_realm, "minutes style must be numeric or 2-digit when hours uses numeric or 2-digit");
        }
        prevStyle = minutesStyle;

        var secondsDefault = IsNumericLike(prevStyle) ? "2-digit" : (isDigital ? "numeric" : baseTimeStyle);
        var secondsStyle = GetStringOption(optionsObj, "seconds", UnitStyleWithNumericValues, secondsDefault);
        var secondsDisplay = GetStringOption(optionsObj, "secondsDisplay", DisplayValues, "auto");
        if (IsNumericLike(prevStyle) && !IsNumericLike(secondsStyle))
        {
            Throw.RangeError(_realm, "seconds style must be numeric or 2-digit when minutes uses numeric or 2-digit");
        }
        prevStyle = secondsStyle;

        // Sub-second units: default to numeric if previous is numeric-like
        var millisecondsDefault = IsNumericLike(prevStyle) ? "numeric" : (isDigital ? "numeric" : baseTimeStyle);
        var millisecondsStyle = GetStringOption(optionsObj, "milliseconds", SubSecondStyleValues, millisecondsDefault);
        var millisecondsDisplay = GetStringOption(optionsObj, "millisecondsDisplay", DisplayValues, "auto");
        if (IsNumericLike(prevStyle) && !IsNumericLikeOrFractional(millisecondsStyle))
        {
            Throw.RangeError(_realm, "milliseconds style must be numeric when seconds uses numeric or 2-digit");
        }
        prevStyle = millisecondsStyle;

        var microsecondsDefault = IsNumericLike(prevStyle) ? "numeric" : (isDigital ? "numeric" : baseTimeStyle);
        var microsecondsStyle = GetStringOption(optionsObj, "microseconds", SubSecondStyleValues, microsecondsDefault);
        var microsecondsDisplay = GetStringOption(optionsObj, "microsecondsDisplay", DisplayValues, "auto");
        if (IsNumericLikeOrFractional(prevStyle) && !IsNumericLikeOrFractional(microsecondsStyle))
        {
            Throw.RangeError(_realm, "microseconds style must be numeric when milliseconds uses numeric");
        }
        prevStyle = microsecondsStyle;

        var nanosecondsDefault = IsNumericLike(prevStyle) ? "numeric" : (isDigital ? "numeric" : baseTimeStyle);
        var nanosecondsStyle = GetStringOption(optionsObj, "nanoseconds", SubSecondStyleValues, nanosecondsDefault);
        var nanosecondsDisplay = GetStringOption(optionsObj, "nanosecondsDisplay", DisplayValues, "auto");
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
            resolved.Locale,
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

    private string GetNumberingSystemOption(ObjectInstance options)
    {
        var value = options.Get("numberingSystem");
        if (value.IsUndefined())
        {
            return "latn"; // Default
        }

        var stringValue = TypeConverter.ToString(value);

        // Validate against Unicode Locale Identifier type nonterminal
        // Pattern: (3*8alphanum) *("-" (3*8alphanum))
        if (!IntlUtilities.IsValidUnicodeExtensionValue(stringValue))
        {
            Throw.RangeError(_realm, $"Invalid numbering system: {stringValue}");
        }

        return stringValue;
    }

    /// <summary>
    /// https://tc39.es/proposal-intl-duration-format/#sec-intl.durationformat.supportedlocalesof
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
