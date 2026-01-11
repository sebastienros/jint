using Jint.Native.Object;
using Jint.Native.Symbol;
using Jint.Runtime;
using Jint.Runtime.Descriptors;
using Jint.Runtime.Interop;

namespace Jint.Native.Intl;

/// <summary>
/// https://tc39.es/ecma402/#sec-properties-of-intl-locale-prototype-object
/// </summary>
internal sealed class LocalePrototype : Prototype
{
    private readonly LocaleConstructor _constructor;

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
        const PropertyFlag LengthFlags = PropertyFlag.Configurable;
        const PropertyFlag PropertyFlags = PropertyFlag.Writable | PropertyFlag.Configurable;
        const PropertyFlag AccessorFlags = PropertyFlag.Configurable;

        var properties = new PropertyDictionary(11, checkExistingKeys: false)
        {
            ["constructor"] = new PropertyDescriptor(_constructor, PropertyFlag.NonEnumerable),
            ["maximize"] = new PropertyDescriptor(new ClrFunction(Engine, "maximize", Maximize, 0, LengthFlags), PropertyFlags),
            ["minimize"] = new PropertyDescriptor(new ClrFunction(Engine, "minimize", Minimize, 0, LengthFlags), PropertyFlags),
            ["toString"] = new PropertyDescriptor(new ClrFunction(Engine, "toString", ToLocaleString, 0, LengthFlags), PropertyFlags),
            ["getCalendars"] = new PropertyDescriptor(new ClrFunction(Engine, "getCalendars", GetCalendars, 0, LengthFlags), PropertyFlags),
            ["getCollations"] = new PropertyDescriptor(new ClrFunction(Engine, "getCollations", GetCollations, 0, LengthFlags), PropertyFlags),
            ["getHourCycles"] = new PropertyDescriptor(new ClrFunction(Engine, "getHourCycles", GetHourCycles, 0, LengthFlags), PropertyFlags),
            ["getNumberingSystems"] = new PropertyDescriptor(new ClrFunction(Engine, "getNumberingSystems", GetNumberingSystems, 0, LengthFlags), PropertyFlags),
            ["getTimeZones"] = new PropertyDescriptor(new ClrFunction(Engine, "getTimeZones", GetTimeZones, 0, LengthFlags), PropertyFlags),
            ["getTextInfo"] = new PropertyDescriptor(new ClrFunction(Engine, "getTextInfo", GetTextInfo, 0, LengthFlags), PropertyFlags),
            ["getWeekInfo"] = new PropertyDescriptor(new ClrFunction(Engine, "getWeekInfo", GetWeekInfo, 0, LengthFlags), PropertyFlags),
        };
        SetProperties(properties);

        // Accessor properties - accessor properties don't have writable attribute
        SetAccessor("baseName", new GetSetPropertyDescriptor(
            new ClrFunction(Engine, "get baseName", GetBaseName, 0, LengthFlags),
            Undefined,
            AccessorFlags));

        SetAccessor("calendar", new GetSetPropertyDescriptor(
            new ClrFunction(Engine, "get calendar", GetCalendar, 0, LengthFlags),
            Undefined,
            AccessorFlags));

        SetAccessor("caseFirst", new GetSetPropertyDescriptor(
            new ClrFunction(Engine, "get caseFirst", GetCaseFirst, 0, LengthFlags),
            Undefined,
            AccessorFlags));

        SetAccessor("collation", new GetSetPropertyDescriptor(
            new ClrFunction(Engine, "get collation", GetCollation, 0, LengthFlags),
            Undefined,
            AccessorFlags));

        SetAccessor("hourCycle", new GetSetPropertyDescriptor(
            new ClrFunction(Engine, "get hourCycle", GetHourCycle, 0, LengthFlags),
            Undefined,
            AccessorFlags));

        SetAccessor("language", new GetSetPropertyDescriptor(
            new ClrFunction(Engine, "get language", GetLanguage, 0, LengthFlags),
            Undefined,
            AccessorFlags));

        SetAccessor("numberingSystem", new GetSetPropertyDescriptor(
            new ClrFunction(Engine, "get numberingSystem", GetNumberingSystem, 0, LengthFlags),
            Undefined,
            AccessorFlags));

        SetAccessor("numeric", new GetSetPropertyDescriptor(
            new ClrFunction(Engine, "get numeric", GetNumeric, 0, LengthFlags),
            Undefined,
            AccessorFlags));

        SetAccessor("region", new GetSetPropertyDescriptor(
            new ClrFunction(Engine, "get region", GetRegion, 0, LengthFlags),
            Undefined,
            AccessorFlags));

        SetAccessor("script", new GetSetPropertyDescriptor(
            new ClrFunction(Engine, "get script", GetScript, 0, LengthFlags),
            Undefined,
            AccessorFlags));

        SetAccessor("firstDayOfWeek", new GetSetPropertyDescriptor(
            new ClrFunction(Engine, "get firstDayOfWeek", GetFirstDayOfWeek, 0, LengthFlags),
            Undefined,
            AccessorFlags));

        SetAccessor("variants", new GetSetPropertyDescriptor(
            new ClrFunction(Engine, "get variants", GetVariants, 0, LengthFlags),
            Undefined,
            AccessorFlags));

        var symbols = new SymbolDictionary(1)
        {
            [GlobalSymbolRegistry.ToStringTag] = new("Intl.Locale", PropertyFlag.Configurable)
        };
        SetSymbols(symbols);
    }

    private void SetAccessor(string name, GetSetPropertyDescriptor descriptor)
    {
        SetProperty(name, descriptor);
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
    private ObjectInstance Maximize(JsValue thisObject, JsCallArguments arguments)
    {
        var locale = ValidateLocale(thisObject);

        // Try to maximize using CultureInfo
        var culture = locale.CultureInfo;
        var maximizedName = locale.Locale;

        // If the locale doesn't have a region, try to add one
        if (string.IsNullOrEmpty(locale.Region))
        {
            // Use the default region for the language if available
            try
            {
                var specificCulture = System.Globalization.CultureInfo.CreateSpecificCulture(locale.Language);
                if (!string.IsNullOrEmpty(specificCulture.Name) && specificCulture.Name.Contains('-'))
                {
                    var parts = specificCulture.Name.Split('-');
                    if (parts.Length >= 2)
                    {
                        maximizedName = locale.Language;
                        if (!string.IsNullOrEmpty(locale.Script))
                        {
                            maximizedName += "-" + locale.Script;
                        }
                        maximizedName += "-" + parts[parts.Length - 1].ToUpperInvariant();

                        // Add back any extensions
                        if (locale.Locale.Contains("-u-", StringComparison.Ordinal))
                        {
                            var extIndex = locale.Locale.IndexOf("-u-", StringComparison.Ordinal);
                            maximizedName += locale.Locale.Substring(extIndex);
                        }
                    }
                }
            }
            catch
            {
                // Keep the original
            }
        }

        // Create new locale with maximized name
        return _constructor.Construct([new JsString(maximizedName)], _constructor);
    }

    /// <summary>
    /// https://tc39.es/ecma402/#sec-Intl.Locale.prototype.minimize
    /// </summary>
    private ObjectInstance Minimize(JsValue thisObject, JsCallArguments arguments)
    {
        var locale = ValidateLocale(thisObject);

        // Minimize by removing script and region if they are the defaults
        var minimizedName = locale.Language;

        // Keep script only if it's non-default for the language
        // Keep region only if necessary
        // For simplicity, just return the language
        // A full implementation would use CLDR likely subtags data

        // Add back any extensions
        if (locale.Locale.Contains("-u-", StringComparison.Ordinal))
        {
            var extIndex = locale.Locale.IndexOf("-u-", StringComparison.Ordinal);
            minimizedName += locale.Locale.Substring(extIndex);
        }

        return _constructor.Construct([new JsString(minimizedName)], _constructor);
    }

    /// <summary>
    /// https://tc39.es/ecma402/#sec-Intl.Locale.prototype.toString
    /// </summary>
    private JsValue ToLocaleString(JsValue thisObject, JsCallArguments arguments)
    {
        var locale = ValidateLocale(thisObject);
        return locale.Locale;
    }

    private JsValue GetBaseName(JsValue thisObject, JsCallArguments arguments)
    {
        var locale = ValidateLocale(thisObject);
        return locale.BaseName;
    }

    private JsValue GetCalendar(JsValue thisObject, JsCallArguments arguments)
    {
        var locale = ValidateLocale(thisObject);
        return locale.Calendar ?? Undefined;
    }

    private JsValue GetCaseFirst(JsValue thisObject, JsCallArguments arguments)
    {
        var locale = ValidateLocale(thisObject);
        return locale.CaseFirst ?? Undefined;
    }

    private JsValue GetCollation(JsValue thisObject, JsCallArguments arguments)
    {
        var locale = ValidateLocale(thisObject);
        return locale.Collation ?? Undefined;
    }

    private JsValue GetHourCycle(JsValue thisObject, JsCallArguments arguments)
    {
        var locale = ValidateLocale(thisObject);
        return locale.HourCycle ?? Undefined;
    }

    private JsValue GetLanguage(JsValue thisObject, JsCallArguments arguments)
    {
        var locale = ValidateLocale(thisObject);
        return locale.Language;
    }

    private JsValue GetNumberingSystem(JsValue thisObject, JsCallArguments arguments)
    {
        var locale = ValidateLocale(thisObject);
        return locale.NumberingSystem ?? Undefined;
    }

    private JsBoolean GetNumeric(JsValue thisObject, JsCallArguments arguments)
    {
        var locale = ValidateLocale(thisObject);
        return locale.Numeric.HasValue ? (locale.Numeric.Value ? JsBoolean.True : JsBoolean.False) : JsBoolean.False;
    }

    private JsValue GetRegion(JsValue thisObject, JsCallArguments arguments)
    {
        var locale = ValidateLocale(thisObject);
        return locale.Region ?? Undefined;
    }

    private JsValue GetScript(JsValue thisObject, JsCallArguments arguments)
    {
        var locale = ValidateLocale(thisObject);
        return locale.Script ?? Undefined;
    }

    /// <summary>
    /// https://tc39.es/ecma402/#sec-Intl.Locale.prototype.variants
    /// </summary>
    private JsArray GetVariants(JsValue thisObject, JsCallArguments arguments)
    {
        var locale = ValidateLocale(thisObject);
        var variants = locale.Variants;

        var result = new JsArray(Engine, (uint) variants.Length);
        for (var i = 0; i < variants.Length; i++)
        {
            result.SetIndexValue((uint) i, variants[i], updateLength: true);
        }

        return result;
    }

    private JsValue GetFirstDayOfWeek(JsValue thisObject, JsCallArguments arguments)
    {
        var locale = ValidateLocale(thisObject);
        var culture = locale.CultureInfo;

        // Get first day of week from culture
        var firstDay = culture.DateTimeFormat.FirstDayOfWeek;

        // Return day number (1=Monday, 7=Sunday per ECMA-402)
        return firstDay switch
        {
            DayOfWeek.Monday => 1,
            DayOfWeek.Tuesday => 2,
            DayOfWeek.Wednesday => 3,
            DayOfWeek.Thursday => 4,
            DayOfWeek.Friday => 5,
            DayOfWeek.Saturday => 6,
            DayOfWeek.Sunday => 7,
            _ => 1 // Default to Monday
        };
    }

    /// <summary>
    /// https://tc39.es/ecma402/#sec-Intl.Locale.prototype.getCalendars
    /// </summary>
    private JsArray GetCalendars(JsValue thisObject, JsCallArguments arguments)
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
    private JsArray GetCollations(JsValue thisObject, JsCallArguments arguments)
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
    private JsArray GetHourCycles(JsValue thisObject, JsCallArguments arguments)
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
    private JsArray GetNumberingSystems(JsValue thisObject, JsCallArguments arguments)
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
    private JsValue GetTimeZones(JsValue thisObject, JsCallArguments arguments)
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
    private JsObject GetTextInfo(JsValue thisObject, JsCallArguments arguments)
    {
        var locale = ValidateLocale(thisObject);
        var culture = locale.CultureInfo;

        var result = OrdinaryObjectCreate(Engine, Engine.Realm.Intrinsics.Object.PrototypeObject);

        // Determine text direction
        // RTL languages include Arabic, Hebrew, Persian, Urdu, etc.
        var isRtl = culture.TextInfo.IsRightToLeft;
        result.Set("direction", isRtl ? "rtl" : "ltr");

        return result;
    }

    /// <summary>
    /// https://tc39.es/ecma402/#sec-Intl.Locale.prototype.getWeekInfo
    /// </summary>
    private JsObject GetWeekInfo(JsValue thisObject, JsCallArguments arguments)
    {
        var locale = ValidateLocale(thisObject);
        var culture = locale.CultureInfo;

        var result = OrdinaryObjectCreate(Engine, Engine.Realm.Intrinsics.Object.PrototypeObject);

        // First day of week (1=Monday, 7=Sunday)
        var firstDay = culture.DateTimeFormat.FirstDayOfWeek;
        var firstDayNum = firstDay switch
        {
            DayOfWeek.Monday => 1,
            DayOfWeek.Tuesday => 2,
            DayOfWeek.Wednesday => 3,
            DayOfWeek.Thursday => 4,
            DayOfWeek.Friday => 5,
            DayOfWeek.Saturday => 6,
            DayOfWeek.Sunday => 7,
            _ => 1
        };
        result.Set("firstDay", firstDayNum);

        // Weekend days - typically Saturday and Sunday for most locales
        var weekend = new JsArray(Engine, 2);
        weekend.SetIndexValue(0, 6, updateLength: true); // Saturday
        weekend.SetIndexValue(1, 7, updateLength: true); // Sunday
        result.Set("weekend", weekend);

        // Minimal days in first week (typically 1 or 4)
        result.Set("minimalDays", 1);

        return result;
    }
}
