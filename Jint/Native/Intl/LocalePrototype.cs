using Jint.Native.Intl.Data;
using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Descriptors;

namespace Jint.Native.Intl;

/// <summary>
/// https://tc39.es/ecma402/#sec-properties-of-intl-locale-prototype-object
/// </summary>
[JsObject]
internal sealed partial class LocalePrototype : Prototype
{
    [JsProperty(Name = "constructor", Flags = PropertyFlag.NonEnumerable)]
    private readonly LocaleConstructor _constructor;

    [JsSymbol("ToStringTag", Flags = PropertyFlag.Configurable)] private static readonly JsString LocaleToStringTag = new("Intl.Locale");

    public LocalePrototype(Engine engine,
        Realm realm,
        LocaleConstructor constructor,
        ObjectPrototype objectPrototype) : base(engine, realm)
    {
        _prototype = objectPrototype;
        _constructor = constructor;
    }

    protected override void Initialize()
    {
        CreateProperties_Generated();
        CreateSymbols_Generated();
    }

    private JsLocale ValidateLocale(JsValue thisObject)
    {
        if (thisObject is JsLocale locale)
        {
            return locale;
        }

        Throw.TypeError(_realm, "Value is not an Intl.Locale");
        return null!; // Never reached
    }

    /// <summary>
    /// https://tc39.es/ecma402/#sec-Intl.Locale.prototype.maximize
    /// </summary>
    [JsFunction(Length = 0)]
    private ObjectInstance Maximize(JsValue thisObject)
    {
        var locale = ValidateLocale(thisObject);

        // Use CLDR likely subtags algorithm
        var maximizedName = LikelySubtags.AddLikelySubtags(locale.Locale);

        // Create new locale with maximized name
        return _constructor.Construct([new JsString(maximizedName)], _constructor);
    }

    /// <summary>
    /// https://tc39.es/ecma402/#sec-Intl.Locale.prototype.minimize
    /// </summary>
    [JsFunction(Length = 0)]
    private ObjectInstance Minimize(JsValue thisObject)
    {
        var locale = ValidateLocale(thisObject);

        // Use CLDR likely subtags algorithm
        var minimizedName = LikelySubtags.RemoveLikelySubtags(locale.Locale);

        return _constructor.Construct([new JsString(minimizedName)], _constructor);
    }

    /// <summary>
    /// https://tc39.es/ecma402/#sec-Intl.Locale.prototype.toString
    /// </summary>
    [JsFunction(Length = 0, Name = "toString")]
    private JsValue ToLocaleString(JsValue thisObject)
    {
        var locale = ValidateLocale(thisObject);
        return locale.Locale;
    }

    [JsAccessor("baseName")]
    private JsValue GetBaseName(JsValue thisObject)
    {
        var locale = ValidateLocale(thisObject);
        return locale.BaseName;
    }

    [JsAccessor("calendar")]
    private JsValue GetCalendar(JsValue thisObject)
    {
        var locale = ValidateLocale(thisObject);
        return locale.Calendar ?? Undefined;
    }

    [JsAccessor("caseFirst")]
    private JsValue GetCaseFirst(JsValue thisObject)
    {
        var locale = ValidateLocale(thisObject);
        return locale.CaseFirst ?? Undefined;
    }

    [JsAccessor("collation")]
    private JsValue GetCollation(JsValue thisObject)
    {
        var locale = ValidateLocale(thisObject);
        return locale.Collation ?? Undefined;
    }

    [JsAccessor("hourCycle")]
    private JsValue GetHourCycle(JsValue thisObject)
    {
        var locale = ValidateLocale(thisObject);
        return locale.HourCycle ?? Undefined;
    }

    [JsAccessor("language")]
    private JsValue GetLanguage(JsValue thisObject)
    {
        var locale = ValidateLocale(thisObject);
        return locale.Language;
    }

    [JsAccessor("numberingSystem")]
    private JsValue GetNumberingSystem(JsValue thisObject)
    {
        var locale = ValidateLocale(thisObject);
        return locale.NumberingSystem ?? Undefined;
    }

    [JsAccessor("numeric")]
    private JsBoolean GetNumeric(JsValue thisObject)
    {
        var locale = ValidateLocale(thisObject);
        return locale.Numeric.HasValue ? (locale.Numeric.Value ? JsBoolean.True : JsBoolean.False) : JsBoolean.False;
    }

    [JsAccessor("region")]
    private JsValue GetRegion(JsValue thisObject)
    {
        var locale = ValidateLocale(thisObject);
        return locale.Region ?? Undefined;
    }

    [JsAccessor("script")]
    private JsValue GetScript(JsValue thisObject)
    {
        var locale = ValidateLocale(thisObject);
        return locale.Script ?? Undefined;
    }

    /// <summary>
    /// https://tc39.es/ecma402/#sec-Intl.Locale.prototype.variants
    /// Returns hyphen-separated variants string or undefined if no variants.
    /// </summary>
    [JsAccessor("variants")]
    private JsValue GetVariants(JsValue thisObject)
    {
        var locale = ValidateLocale(thisObject);
        var variants = locale.Variants;

        if (variants.Length == 0)
        {
            return Undefined;
        }

        return string.Join("-", variants);
    }

    [JsAccessor("firstDayOfWeek")]
    private JsValue GetFirstDayOfWeek(JsValue thisObject)
    {
        var locale = ValidateLocale(thisObject);

        // Return the firstDayOfWeek value from the locale if set
        if (!string.IsNullOrEmpty(locale.FirstDayOfWeek))
        {
            return locale.FirstDayOfWeek;
        }

        // If not explicitly set, return undefined per spec
        return Undefined;
    }

    /// <summary>
    /// https://tc39.es/ecma402/#sec-Intl.Locale.prototype.getCalendars
    /// </summary>
    [JsFunction(Length = 0)]
    private JsArray GetCalendars(JsValue thisObject)
    {
        var locale = ValidateLocale(thisObject);

        // Return array of supported calendars
        // For .NET, we primarily support Gregorian calendar
        var result = new JsArray(Engine, 1);
        result.SetIndexValue(0, locale.Calendar ?? "gregory", updateLength: true);
        return result;
    }

    /// <summary>
    /// https://tc39.es/ecma402/#sec-Intl.Locale.prototype.getCollations
    /// </summary>
    [JsFunction(Length = 0)]
    private JsArray GetCollations(JsValue thisObject)
    {
        var locale = ValidateLocale(thisObject);

        // Return array of supported collations
        var result = new JsArray(Engine, 1);
        result.SetIndexValue(0, locale.Collation ?? "default", updateLength: true);
        return result;
    }

    /// <summary>
    /// https://tc39.es/ecma402/#sec-Intl.Locale.prototype.getHourCycles
    /// </summary>
    [JsFunction(Length = 0)]
    private JsArray GetHourCycles(JsValue thisObject)
    {
        var locale = ValidateLocale(thisObject);
        var culture = locale.CultureInfo;

        // Determine hour cycle based on culture's time format
        var timePattern = culture.DateTimeFormat.ShortTimePattern;

        // 24-hour format uses 'H', 12-hour format uses 'h'
        var hourCycle = timePattern.Contains('H') ? "h23" : "h12";

        var result = new JsArray(Engine, 1);
        result.SetIndexValue(0, locale.HourCycle ?? hourCycle, updateLength: true);
        return result;
    }

    /// <summary>
    /// https://tc39.es/ecma402/#sec-Intl.Locale.prototype.getNumberingSystems
    /// </summary>
    [JsFunction(Length = 0)]
    private JsArray GetNumberingSystems(JsValue thisObject)
    {
        var locale = ValidateLocale(thisObject);

        // Return array of numbering systems
        // Most locales use "latn" (Latin digits 0-9)
        var result = new JsArray(Engine, 1);
        result.SetIndexValue(0, locale.NumberingSystem ?? "latn", updateLength: true);
        return result;
    }

    /// <summary>
    /// https://tc39.es/ecma402/#sec-Intl.Locale.prototype.getTimeZones
    /// </summary>
    [JsFunction(Length = 0)]
    private JsValue GetTimeZones(JsValue thisObject)
    {
        var locale = ValidateLocale(thisObject);

        // TimeZones are only available for locales with a region
        if (string.IsNullOrEmpty(locale.Region))
        {
            return Undefined;
        }

        // Return time zones for the region
        // This is a simplified implementation
        var result = new JsArray(Engine, 1);

        // Map common regions to their primary time zones
        var timeZone = locale.Region?.ToUpperInvariant() switch
        {
            "US" => "America/New_York",
            "GB" => "Europe/London",
            "DE" => "Europe/Berlin",
            "FR" => "Europe/Paris",
            "JP" => "Asia/Tokyo",
            "CN" => "Asia/Shanghai",
            "AU" => "Australia/Sydney",
            "IN" => "Asia/Kolkata",
            "BR" => "America/Sao_Paulo",
            "RU" => "Europe/Moscow",
            _ => null
        };

        if (timeZone != null)
        {
            result.SetIndexValue(0, timeZone, updateLength: true);
        }

        return result;
    }

    /// <summary>
    /// https://tc39.es/ecma402/#sec-Intl.Locale.prototype.getTextInfo
    /// </summary>
    [JsFunction(Length = 0)]
    private JsObject GetTextInfo(JsValue thisObject)
    {
        var locale = ValidateLocale(thisObject);
        var culture = locale.CultureInfo;

        var result = OrdinaryObjectCreate(Engine, Engine.Realm.Intrinsics.Object.PrototypeObject);

        // Determine text direction
        // RTL languages include Arabic, Hebrew, Persian, Urdu, etc.
        var isRtl = culture.TextInfo.IsRightToLeft;
        result.CreateDataPropertyOrThrow("direction", isRtl ? "rtl" : "ltr");

        return result;
    }

    /// <summary>
    /// https://tc39.es/ecma402/#sec-Intl.Locale.prototype.getWeekInfo
    /// </summary>
    [JsFunction(Length = 0)]
    private JsObject GetWeekInfo(JsValue thisObject)
    {
        var locale = ValidateLocale(thisObject);
        var region = locale.Region;

        var result = OrdinaryObjectCreate(Engine, Engine.Realm.Intrinsics.Object.PrototypeObject);

        // First day of week (1=Monday, 7=Sunday)
        // Use fw extension if present, otherwise from CLDR data
        int firstDayNum;
        if (locale.FirstDayOfWeek != null)
        {
            firstDayNum = ConvertDayNameToNumber(locale.FirstDayOfWeek);
        }
        else
        {
            firstDayNum = WeekData.GetFirstDayOfWeek(region);
        }
        result.CreateDataPropertyOrThrow("firstDay", firstDayNum);

        // Weekend days from CLDR data
        var weekendDays = WeekData.GetWeekend(region);
        var weekend = new JsArray(Engine, (uint) weekendDays.Length);
        for (var i = 0; i < weekendDays.Length; i++)
        {
            weekend.SetIndexValue((uint) i, weekendDays[i], updateLength: true);
        }
        result.CreateDataPropertyOrThrow("weekend", weekend);

        return result;
    }

    /// <summary>
    /// Converts a day name abbreviation (mon, tue, wed, etc.) to a number (1-7).
    /// </summary>
    private static int ConvertDayNameToNumber(string dayName)
    {
        return dayName.ToLowerInvariant() switch
        {
            "mon" => 1,
            "tue" => 2,
            "wed" => 3,
            "thu" => 4,
            "fri" => 5,
            "sat" => 6,
            "sun" => 7,
            _ => 1 // Default to Monday
        };
    }
}
