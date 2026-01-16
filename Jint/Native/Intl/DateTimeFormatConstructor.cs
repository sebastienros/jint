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

        // Step 5: Get localeMatcher option first
        var localeMatcher = GetStringOption(optionsObj, "localeMatcher", LocaleMatcherValues, "best fit") ?? "best fit";

        // Resolve locale (uses pre-read localeMatcher)
        var requestedLocales = IntlUtilities.CanonicalizeLocaleList(_engine, locales);
        var availableLocales = IntlUtilities.GetAvailableLocales();
        var resolved = IntlUtilities.ResolveLocale(_engine, availableLocales, requestedLocales, localeMatcher, []);
        var resolvedLocale = resolved.Locale;

        // Step 7: Get calendar option (between localeMatcher and hour12)
        var calendar = GetUnicodeExtensionOption(optionsObj, "calendar");

        // If calendar not in options, check locale's unicode extension (-u-ca-)
        if (calendar == null)
        {
            calendar = ExtractUnicodeExtensionFromLocale(requestedLocales.Count > 0 ? requestedLocales[0] : "", "ca");
        }

        // Canonicalize calendar aliases per ECMA-402
        calendar = CanonicalizeCalendar(calendar);

        // Step 10: Get numberingSystem option
        var numberingSystem = GetUnicodeExtensionOption(optionsObj, "numberingSystem");

        // Step 13: Get hour12 option
        var hour12Value = optionsObj.Get("hour12");
        bool? hour12 = null;
        if (!hour12Value.IsUndefined())
        {
            hour12 = TypeConverter.ToBoolean(hour12Value);
        }

        // Step 14: Get hourCycle option
        var hourCycle = GetStringOption(optionsObj, "hourCycle", HourCycleValues, null);

        // If hourCycle not in options, check locale's unicode extension (-u-hc-)
        if (hourCycle == null)
        {
            hourCycle = ExtractUnicodeExtensionValue(requestedLocales, "hc", HourCycleValues);
        }

        // hour12 takes precedence over hourCycle
        if (hour12.HasValue)
        {
            if (hour12.Value)
            {
                // Japanese locale uses h11, all others use h12 for 12-hour format
                var lang = resolvedLocale.Split('-')[0].ToLowerInvariant();
                hourCycle = string.Equals(lang, "ja", StringComparison.Ordinal) ? "h11" : "h12";
            }
            else
            {
                hourCycle = "h23";
            }
        }

        // Step 29: Get timeZone option
        var timeZone = GetTimeZoneOption(optionsObj);

        // Step 36: Get component options in order (per Table 7 of ECMA-402)
        // Order: weekday, era, year, month, day, dayPeriod, hour, minute, second, fractionalSecondDigits, timeZoneName
        var weekday = GetStringOption(optionsObj, "weekday", WeekdayValues, null);
        var era = GetStringOption(optionsObj, "era", EraValues, null);
        var year = GetStringOption(optionsObj, "year", YearValues, null);
        var month = GetStringOption(optionsObj, "month", MonthValues, null);
        var day = GetStringOption(optionsObj, "day", DayValues, null);
        var dayPeriod = GetStringOption(optionsObj, "dayPeriod", DayPeriodValues, null);
        var hour = GetStringOption(optionsObj, "hour", HourValues, null);
        var minute = GetStringOption(optionsObj, "minute", MinuteValues, null);
        var second = GetStringOption(optionsObj, "second", SecondValues, null);
        var fractionalSecondDigits = GetNumberOption(optionsObj, "fractionalSecondDigits", 1, 3, null);
        var timeZoneName = GetStringOption(optionsObj, "timeZoneName", TimeZoneNameValues, null);

        // Step 37: Get formatMatcher option
        GetStringOption(optionsObj, "formatMatcher", FormatMatcherValues, null);

        // Date/time style options
        var dateStyle = GetStringOption(optionsObj, "dateStyle", DateStyleValues, null);
        var timeStyle = GetStringOption(optionsObj, "timeStyle", TimeStyleValues, null);

        // If dateStyle or timeStyle is specified, component options must NOT be specified
        if (dateStyle != null || timeStyle != null)
        {
            if (weekday != null || era != null || year != null || month != null ||
                day != null || dayPeriod != null || hour != null || minute != null ||
                second != null || fractionalSecondDigits != null || timeZoneName != null)
            {
                Throw.TypeError(_realm, "Can't set date/time component options when dateStyle or timeStyle is used");
            }
        }
        else
        {
            // If no date/time component options specified, use default date format
            // Note: dayPeriod, fractionalSecondDigits, and timeZoneName don't prevent defaults
            if (weekday == null && era == null && year == null && month == null &&
                day == null && dayPeriod == null && hour == null && minute == null && second == null)
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

        // Per spec: check NaN and bounds BEFORE flooring
        if (double.IsNaN(number) || number < minimum || number > maximum)
        {
            Throw.RangeError(_realm, $"Invalid value for option '{property}'");
        }

        // Return floor(value)
        return (int) System.Math.Floor(number);
    }

    private string? GetTimeZoneOption(ObjectInstance options)
    {
        var value = options.Get("timeZone");
        if (value.IsUndefined())
        {
            return null;
        }

        var timeZone = TypeConverter.ToString(value);

        // Validate and canonicalize time zone using IANA database
        // Per ECMA-402, only IANA timezone identifiers are allowed
        if (string.Equals(timeZone, "UTC", StringComparison.OrdinalIgnoreCase))
        {
            return "UTC";
        }

        // Check for offset time zones like "+03", "-07:30", "+05:45"
        var canonicalOffset = TryParseOffsetTimeZone(timeZone);
        if (canonicalOffset != null)
        {
            return canonicalOffset;
        }

        // Look up in our IANA timezone database (case-insensitive)
        // This is the only source of valid timezone names per ECMA-402
        var canonicalId = Data.TimeZoneData.FindCanonical(timeZone);
        if (canonicalId != null)
        {
            return canonicalId;
        }

        // Not a valid IANA timezone identifier
        Throw.RangeError(_realm, $"Invalid time zone: {timeZone}");
        return null;
    }

    /// <summary>
    /// Parses and canonicalizes an offset time zone string.
    /// Valid formats: "+HH", "-HH", "+HH:MM", "-HH:MM", "+HHMM", "-HHMM"
    /// Returns canonical form like "+03:00" or "-07:30", or null if not a valid offset.
    /// </summary>
    private static string? TryParseOffsetTimeZone(string timeZone)
    {
        if (string.IsNullOrEmpty(timeZone) || timeZone.Length < 3)
        {
            return null;
        }

        var sign = timeZone[0];
        if (sign != '+' && sign != '-')
        {
            return null;
        }

        var len = timeZone.Length;
        int hours;
        int minutes = 0;

        // Parse based on format with strict position validation
        if (len == 3)
        {
            // "+HH" or "-HH" format - positions 1,2 must be digits
            if (!char.IsDigit(timeZone[1]) || !char.IsDigit(timeZone[2]))
            {
                return null;
            }
            hours = (timeZone[1] - '0') * 10 + (timeZone[2] - '0');
        }
        else if (len == 5)
        {
            // "+HHMM" format - positions 1,2,3,4 must all be digits
            if (!char.IsDigit(timeZone[1]) || !char.IsDigit(timeZone[2]) ||
                !char.IsDigit(timeZone[3]) || !char.IsDigit(timeZone[4]))
            {
                return null;
            }
            hours = (timeZone[1] - '0') * 10 + (timeZone[2] - '0');
            minutes = (timeZone[3] - '0') * 10 + (timeZone[4] - '0');
        }
        else if (len == 6 && timeZone[3] == ':')
        {
            // "+HH:MM" format - positions 1,2 and 4,5 must be digits, position 3 is ':'
            if (!char.IsDigit(timeZone[1]) || !char.IsDigit(timeZone[2]) ||
                !char.IsDigit(timeZone[4]) || !char.IsDigit(timeZone[5]))
            {
                return null;
            }
            hours = (timeZone[1] - '0') * 10 + (timeZone[2] - '0');
            minutes = (timeZone[4] - '0') * 10 + (timeZone[5] - '0');
        }
        else
        {
            return null;
        }

        // Validate ranges: hours 0-23, minutes 0-59
        // Note: ECMA-402 allows offsets up to ±23:59
        if (hours > 23 || minutes > 59)
        {
            return null;
        }

        // Return canonical form: ±HH:MM
        return $"{sign}{hours:D2}:{minutes:D2}";
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

    /// <summary>
    /// Canonicalizes calendar names per ECMA-402.
    /// Converts deprecated/alias names to their canonical forms.
    /// Per ECMA-402, "islamic" and "islamic-rgsa" should fallback to a valid calendar
    /// from AvailableCalendars (like "islamic-civil").
    /// </summary>
    private static string? CanonicalizeCalendar(string? calendar)
    {
        if (calendar == null)
        {
            return null;
        }

        // Calendar alias and fallback mappings per Unicode CLDR and ECMA-402
        return calendar.ToLowerInvariant() switch
        {
            "ethiopic-amete-alem" => "ethioaa",
            "islamicc" => "islamic-civil",
            // Per ECMA-402 §11.1.2, "islamic" and "islamic-rgsa" are deprecated
            // and should fallback to a valid calendar from AvailableCalendars
            "islamic" => "islamic-civil",
            "islamic-rgsa" => "islamic-civil",
            _ => calendar.ToLowerInvariant()
        };
    }

    /// <summary>
    /// Extracts a unicode extension value from the locale list.
    /// For example, extracts "h11" from "de-u-hc-h11" when key is "hc".
    /// </summary>
    private static string? ExtractUnicodeExtensionValue(List<string> locales, string key, HashSet<string>? validValues)
    {
        foreach (var locale in locales)
        {
            var value = ExtractUnicodeExtensionFromLocale(locale, key);
            if (value != null)
            {
                // Validate against allowed values if provided
                if (validValues == null || validValues.Contains(value))
                {
                    return value;
                }
            }
        }
        return null;
    }

    /// <summary>
    /// Extracts a specific unicode extension value from a single locale string.
    /// </summary>
    private static string? ExtractUnicodeExtensionFromLocale(string locale, string key)
    {
        // Look for -u- marker
        var uIndex = locale.IndexOf("-u-", StringComparison.OrdinalIgnoreCase);
        if (uIndex == -1)
        {
            return null;
        }

        // Parse the unicode extension looking for the key
        var extensionStart = uIndex + 3;
        var i = extensionStart;

        while (i < locale.Length)
        {
            // Find the key (2 characters)
            var keyStart = i;
            while (i < locale.Length && locale[i] != '-')
            {
                i++;
            }

            var currentKey = locale.Substring(keyStart, i - keyStart);

            // Move past the '-' if present
            if (i < locale.Length && locale[i] == '-')
            {
                i++;
            }

            // Check if this is a 2-character key (not a singleton for next extension)
            if (currentKey.Length == 2)
            {
                // Find the value(s) - collect until next key or end
                var valueStart = i;
                while (i < locale.Length)
                {
                    var partStart = i;
                    while (i < locale.Length && locale[i] != '-')
                    {
                        i++;
                    }

                    var part = locale.Substring(partStart, i - partStart);

                    // If this part is a 2-char key or 1-char singleton, stop
                    if (part.Length == 2 || part.Length == 1)
                    {
                        break;
                    }

                    // Move past '-' if present
                    if (i < locale.Length && locale[i] == '-')
                    {
                        i++;
                    }
                }

                var value = locale.Substring(valueStart, i - valueStart).TrimEnd('-');

                if (string.Equals(currentKey, key, StringComparison.OrdinalIgnoreCase) && value.Length > 0)
                {
                    return value.ToLowerInvariant();
                }
            }
            else if (currentKey.Length == 1)
            {
                // This is a singleton starting a new extension type, stop parsing unicode extension
                break;
            }
        }

        return null;
    }
}
