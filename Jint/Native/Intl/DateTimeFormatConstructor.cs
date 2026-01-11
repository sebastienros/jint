using System.Globalization;
using Jint.Native.Function;
using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Descriptors;
using Jint.Runtime.Interop;

namespace Jint.Native.Intl;

/// <summary>
/// https://tc39.es/ecma402/#sec-intl-datetimeformat-constructor
/// </summary>
internal sealed class DateTimeFormatConstructor : Constructor
{
    private static readonly JsString _functionName = new("DateTimeFormat");
    private static readonly HashSet<string> LocaleMatcherValues = ["lookup", "best fit"];
    private static readonly HashSet<string> FormatMatcherValues = ["basic", "best fit"];
    private static readonly HashSet<string> WeekdayValues = ["long", "short", "narrow"];
    private static readonly HashSet<string> EraValues = ["long", "short", "narrow"];
    private static readonly HashSet<string> YearValues = ["numeric", "2-digit"];
    private static readonly HashSet<string> MonthValues = ["numeric", "2-digit", "long", "short", "narrow"];
    private static readonly HashSet<string> DayValues = ["numeric", "2-digit"];
    private static readonly HashSet<string> DayPeriodValues = ["long", "short", "narrow"];
    private static readonly HashSet<string> HourValues = ["numeric", "2-digit"];
    private static readonly HashSet<string> MinuteValues = ["numeric", "2-digit"];
    private static readonly HashSet<string> SecondValues = ["numeric", "2-digit"];
    private static readonly HashSet<string> TimeZoneNameValues = ["long", "short", "shortOffset", "longOffset", "shortGeneric", "longGeneric"];
    private static readonly HashSet<string> HourCycleValues = ["h11", "h12", "h23", "h24"];
    private static readonly HashSet<string> DateStyleValues = ["full", "long", "medium", "short"];
    private static readonly HashSet<string> TimeStyleValues = ["full", "long", "medium", "short"];

    public DateTimeFormatConstructor(
        Engine engine,
        Realm realm,
        FunctionPrototype functionPrototype,
        ObjectPrototype objectPrototype) : base(engine, realm, _functionName)
    {
        _prototype = functionPrototype;
        PrototypeObject = new DateTimeFormatPrototype(engine, realm, this, objectPrototype);
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

    public DateTimeFormatPrototype PrototypeObject { get; }

    /// <summary>
    /// Called when Intl.DateTimeFormat is invoked without `new`.
    /// </summary>
    protected internal override JsValue Call(JsValue thisObject, JsCallArguments arguments)
    {
        return Construct(arguments, this);
    }

    /// <summary>
    /// https://tc39.es/ecma402/#sec-intl.datetimeformat
    /// </summary>
    public override ObjectInstance Construct(JsCallArguments arguments, JsValue newTarget)
    {
        var locales = arguments.At(0);
        var options = arguments.At(1);

        // Get options object
        var optionsObj = IntlUtilities.CoerceOptionsToObject(_engine, options);

        // Validate localeMatcher option first (must be done before other processing)
        GetStringOption(optionsObj, "localeMatcher", LocaleMatcherValues, null);

        // Resolve locale
        var requestedLocales = IntlUtilities.CanonicalizeLocaleList(_engine, locales);
        var availableLocales = IntlUtilities.GetAvailableLocales();
        var resolvedLocale = ResolveDateTimeFormatLocale(_engine, availableLocales, requestedLocales, optionsObj);

        // Get options
        var calendar = GetUnicodeExtensionOption(optionsObj, "calendar");
        var numberingSystem = GetUnicodeExtensionOption(optionsObj, "numberingSystem");
        var timeZone = GetTimeZoneOption(optionsObj);
        var hourCycle = GetStringOption(optionsObj, "hourCycle", HourCycleValues, null);

        // Handle hour12 option - it overrides hourCycle
        var hour12Value = optionsObj.Get("hour12");
        bool? hour12 = null;
        if (!hour12Value.IsUndefined())
        {
            hour12 = TypeConverter.ToBoolean(hour12Value);
            // hour12 takes precedence over hourCycle
            if (hour12.Value)
            {
                hourCycle = "h12";
            }
            else
            {
                hourCycle = "h23";
            }
        }

        // Date/time style options
        var dateStyle = GetStringOption(optionsObj, "dateStyle", DateStyleValues, null);
        var timeStyle = GetStringOption(optionsObj, "timeStyle", TimeStyleValues, null);

        // Component options (only used if dateStyle/timeStyle not specified)
        string? weekday = null, era = null, year = null, month = null, day = null;
        string? dayPeriod = null, hour = null, minute = null, second = null;
        int? fractionalSecondDigits = null;
        string? timeZoneName = null;

        if (dateStyle != null || timeStyle != null)
        {
            // If dateStyle or timeStyle is specified, component options must NOT be specified
            // https://tc39.es/ecma402/#sec-initializedatetimeformat step 35
            var componentOptions = new[]
            {
                "weekday", "era", "year", "month", "day", "dayPeriod",
                "hour", "minute", "second", "fractionalSecondDigits", "timeZoneName"
            };

            foreach (var option in componentOptions)
            {
                var value = optionsObj.Get(option);
                if (!value.IsUndefined())
                {
                    Throw.TypeError(_realm, $"Can't set option {option} when dateStyle or timeStyle is used");
                }
            }
        }
        else
        {
            weekday = GetStringOption(optionsObj, "weekday", WeekdayValues, null);
            era = GetStringOption(optionsObj, "era", EraValues, null);
            year = GetStringOption(optionsObj, "year", YearValues, null);
            month = GetStringOption(optionsObj, "month", MonthValues, null);
            day = GetStringOption(optionsObj, "day", DayValues, null);
            dayPeriod = GetStringOption(optionsObj, "dayPeriod", DayPeriodValues, null);
            hour = GetStringOption(optionsObj, "hour", HourValues, null);
            minute = GetStringOption(optionsObj, "minute", MinuteValues, null);
            second = GetStringOption(optionsObj, "second", SecondValues, null);
            fractionalSecondDigits = GetNumberOption(optionsObj, "fractionalSecondDigits", 1, 3, null);
            timeZoneName = GetStringOption(optionsObj, "timeZoneName", TimeZoneNameValues, null);

            // If no options specified, use default date format
            if (weekday == null && era == null && year == null && month == null &&
                day == null && hour == null && minute == null && second == null)
            {
                year = "numeric";
                month = "numeric";
                day = "numeric";
            }
        }

        // Get DateTimeFormatInfo for the locale
        var culture = IntlUtilities.GetCultureInfo(resolvedLocale) ?? CultureInfo.InvariantCulture;
        var dateTimeFormatInfo = (DateTimeFormatInfo) culture.DateTimeFormat.Clone();

        // Get prototype from newTarget (for cross-realm construction)
        var proto = GetPrototypeFromConstructor(newTarget, static intrinsics => intrinsics.DateTimeFormat.PrototypeObject);

        return new JsDateTimeFormat(
            _engine,
            proto,
            resolvedLocale,
            calendar,
            numberingSystem,
            timeZone,
            hourCycle,
            dateStyle,
            timeStyle,
            weekday,
            era,
            year,
            month,
            day,
            dayPeriod,
            hour,
            minute,
            second,
            fractionalSecondDigits,
            timeZoneName,
            dateTimeFormatInfo,
            culture);
    }

    private string? GetStringOption(ObjectInstance options, string property, HashSet<string> values, string? fallback)
    {
        var value = options.Get(property);
        if (value.IsUndefined())
        {
            return fallback;
        }

        var stringValue = TypeConverter.ToString(value);

        if (values != null && values.Count > 0)
        {
            if (!values.Contains(stringValue))
            {
                Throw.RangeError(_realm, $"Invalid value '{stringValue}' for option '{property}'");
            }
        }

        return stringValue;
    }

    private string? GetUnicodeExtensionOption(ObjectInstance options, string property)
    {
        var value = options.Get(property);
        if (value.IsUndefined())
        {
            return null;
        }

        var stringValue = TypeConverter.ToString(value);

        // Validate against pattern: (3*8alphanum) *("-" (3*8alphanum))
        if (!IntlUtilities.IsValidUnicodeExtensionValue(stringValue))
        {
            Throw.RangeError(_realm, $"Invalid value '{stringValue}' for option '{property}'");
        }

        return stringValue;
    }

    private int? GetNumberOption(ObjectInstance options, string property, int minimum, int maximum, int? fallback)
    {
        var value = options.Get(property);
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

    private string? GetTimeZoneOption(ObjectInstance options)
    {
        var value = options.Get("timeZone");
        if (value.IsUndefined())
        {
            return null;
        }

        var timeZone = TypeConverter.ToString(value);

        // Validate and canonicalize time zone
        if (string.Equals(timeZone, "UTC", StringComparison.OrdinalIgnoreCase))
        {
            return "UTC";
        }

        // Try to find the time zone
        try
        {
#if NET6_0_OR_GREATER
            if (TimeZoneInfo.TryFindSystemTimeZoneById(timeZone, out _))
            {
                return timeZone;
            }
#else
            TimeZoneInfo.FindSystemTimeZoneById(timeZone);
            return timeZone;
#endif
        }
        catch
        {
            // Invalid time zone
        }

        Throw.RangeError(_realm, $"Invalid time zone: {timeZone}");
        return null;
    }

    private static string ResolveDateTimeFormatLocale(Engine engine, IReadOnlyCollection<string> availableLocales, List<string> requestedLocales, ObjectInstance options)
    {
        var resolved = IntlUtilities.ResolveLocale(engine, availableLocales, requestedLocales, options, []);
        return resolved.Locale;
    }

    /// <summary>
    /// https://tc39.es/ecma402/#sec-intl.datetimeformat.supportedlocalesof
    /// </summary>
    private JsArray SupportedLocalesOf(JsValue thisObject, JsCallArguments arguments)
    {
        var locales = arguments.At(0);
        var options = arguments.At(1);

        var requestedLocales = IntlUtilities.CanonicalizeLocaleList(_engine, locales);
        var availableLocales = IntlUtilities.GetAvailableLocales();

        // Validate localeMatcher option
        var optionsObj = IntlUtilities.CoerceOptionsToObject(_engine, options);
        GetStringOption(optionsObj, "localeMatcher", LocaleMatcherValues, fallback: null);

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
