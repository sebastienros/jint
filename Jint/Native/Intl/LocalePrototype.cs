using Jint.Native.Intl.Data;
using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Descriptors;

namespace Jint.Native.Intl;

/// <summary>
/// https://tc39.es/ecma402/#sec-properties-of-intl-locale-prototype-object
/// </summary>
[JsObject(UseShape = true)]
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
    [JsFunction]
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
    [JsFunction]
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
    [JsFunction(Name = "toString")]
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

        return string.Join('-', variants);
    }

    /// <summary>
    /// https://tc39.es/ecma402/#sec-Intl.Locale.prototype.firstDayOfWeek returns [[FirstDayOfWeek]]
    /// unchanged. A keyword carrying no value - "en-u-fw", or "en-u-fw-true" once UTS #35 Annex C has
    /// removed the "true" - is the empty string, which is present rather than absent, so only a null
    /// answers undefined here.
    /// </summary>
    [JsAccessor("firstDayOfWeek")]
    private JsValue GetFirstDayOfWeek(JsValue thisObject)
    {
        var locale = ValidateLocale(thisObject);
        return locale.FirstDayOfWeek ?? Undefined;
    }

    /// <summary>
    /// https://tc39.es/ecma402/#sec-Intl.Locale.prototype.getCalendars, which is
    /// https://tc39.es/ecma402/#sec-calendarsoflocale.
    /// </summary>
    /// <remarks>
    /// The orderings are the CLDR <c>calendarPreferenceData</c> Jint embeds, read for the region
    /// https://tc39.es/ecma402/#sec-regionpreference picks. <see cref="DefaultCldrProvider.GetDefaultCalendar"/>
    /// reads the same table, so with the shipped provider the first calendar listed is the one
    /// <c>Intl.DateTimeFormat</c> defaults to. <see cref="ICldrProvider"/> has no member for the ordering, so a
    /// host overriding <see cref="ICldrProvider.GetDefaultCalendar"/> moves the formatter's default and not
    /// this list, and the two can then disagree.
    /// </remarks>
    [JsFunction]
    private JsArray GetCalendars(JsValue thisObject)
    {
        var locale = ValidateLocale(thisObject);

        // 1. If loc.[[Calendar]] is not undefined, return CreateArrayFromList(« loc.[[Calendar]] »).
        if (locale.Calendar is not null)
        {
            return CreateArrayOfOne(locale.Calendar);
        }

        // 2-6. The calendars in common use in the region RegionPreference picks.
        var calendarsInUse = CalendarPreferenceData.GetCalendarsInUse(RegionPreference.Of(locale.Locale));

        // 7-9. Each one canonicalized, kept only if AvailableCalendars() contains it, and listed once. CLDR
        // lists islamic and islamic-rgsa for several regions, and neither is available unless a host
        // calendar provider claims it.
        var list = new List<string>(calendarsInUse.Length);
        foreach (var identifier in calendarsInUse)
        {
            var canonical = IntlUtilities.CanonicalizeUValue("ca", identifier);
            if (AvailableCalendars.Contains(Engine, canonical) && !list.Contains(canonical))
            {
                list.Add(canonical);
            }
        }

        // 10. If list is empty, set list to « "gregory" ».
        if (list.Count == 0)
        {
            return CreateArrayOfOne(CalendarPreferenceData.Default);
        }

        // 11. Return CreateArrayFromList(list).
        var values = new JsValue[list.Count];
        for (var i = 0; i < values.Length; i++)
        {
            values[i] = JsString.Create(list[i]);
        }

        return new JsArray(Engine, values);
    }

    private JsArray CreateArrayOfOne(string value)
    {
        var result = new JsArray(Engine, 1);
        result.SetIndexValue(0, value, updateLength: true);
        return result;
    }

    /// <summary>
    /// https://tc39.es/ecma402/#sec-Intl.Locale.prototype.getCollations
    /// </summary>
    [JsFunction]
    private JsArray GetCollations(JsValue thisObject)
    {
        var locale = ValidateLocale(thisObject);

        // 1. If loc.[[Collation]] is not undefined, return CreateArrayFromList(« loc.[[Collation]] »).
        if (locale.Collation is not null)
        {
            var requested = new JsArray(Engine, 1);
            requested.SetIndexValue(0, locale.Collation, updateLength: true);
            return requested;
        }

        // 2-6. Otherwise report the matched locale's collations, or the hardcoded root list when the
        // tag matches no available Collator locale, sorted in lexicographic code unit order.
        var collations = CollatorConstructor.GetCollationsForLanguage(locale.Language);
        var values = new JsValue[collations.Length];
        for (var i = 0; i < collations.Length; i++)
        {
            values[i] = JsString.Create(collations[i]);
        }

        return new JsArray(Engine, values);
    }

    /// <summary>
    /// https://tc39.es/ecma402/#sec-Intl.Locale.prototype.getHourCycles, which is
    /// https://tc39.es/ecma402/#sec-hourcyclesoflocale.
    /// </summary>
    /// <remarks>
    /// The hour cycles are the CLDR <c>timeData</c> Jint embeds, read for the language and the region
    /// https://tc39.es/ecma402/#sec-regionpreference picks. They used to be read off the .NET culture's short
    /// time pattern, which gave one cycle, depended on the machine's globalization data, and ignored the
    /// <c>-u-rg-</c> and <c>-u-sd-</c> keywords. <see cref="ICldrProvider"/> has no member for them, so a host
    /// provider cannot change this answer.
    /// </remarks>
    [JsFunction]
    private JsArray GetHourCycles(JsValue thisObject)
    {
        var locale = ValidateLocale(thisObject);

        // 1. If loc.[[HourCycle]] is not undefined, return CreateArrayFromList(« loc.[[HourCycle]] »).
        if (locale.HourCycle is not null)
        {
            return CreateArrayOfOne(locale.HourCycle);
        }

        // 2-7. The hour cycles in common use for the language in the region RegionPreference picks, or « "h23" ».
        // GetLocaleLanguage is the language subtag, which JsLocale keeps canonicalized.
        var hourCycles = TimeData.GetHourCycles(locale.Language, RegionPreference.Of(locale.Locale));

        // 8. Return CreateArrayFromList(hourCycles).
        var values = new JsValue[hourCycles.Length];
        for (var i = 0; i < values.Length; i++)
        {
            values[i] = JsString.Create(hourCycles[i]);
        }

        return new JsArray(Engine, values);
    }

    /// <summary>
    /// https://tc39.es/ecma402/#sec-Intl.Locale.prototype.getNumberingSystems
    /// </summary>
    [JsFunction]
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
    [JsFunction]
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
    [JsFunction]
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
    [JsFunction]
    private JsObject GetWeekInfo(JsValue thisObject)
    {
        var locale = ValidateLocale(thisObject);
        var weekInfo = Engine.Options.Intl.CldrProvider.GetWeekInfo(locale.Locale);

        // A provider with no opinion falls back to the embedded CLDR data, read for the region
        // https://tc39.es/ecma402/#sec-weekinfooflocale picks rather than for the region subtag alone.
        var fallbackRegion = weekInfo is null ? WeekData.GetLookupRegion(RegionPreference.Of(locale.Locale)) : null;

        var result = OrdinaryObjectCreate(Engine, Engine.Realm.Intrinsics.Object.PrototypeObject);

        // First day of week (1=Monday, 7=Sunday). The fw extension wins over the provider and the data.
        int firstDayNum;
        if (locale.FirstDayOfWeek is { } firstDayOfWeek && WeekdayUValueToNumber(firstDayOfWeek) is { } overrideDay)
        {
            firstDayNum = overrideDay;
        }
        else if (weekInfo != null)
        {
            firstDayNum = IntlUtilities.DayOfWeekToCldrDayNumber(weekInfo.FirstDay);
        }
        else
        {
            firstDayNum = WeekData.GetFirstDayOfWeek(fallbackRegion);
        }
        result.CreateDataPropertyOrThrow("firstDay", firstDayNum);

        // [[Weekend]] is a list of day numbers in ascending order, so sort whatever the provider gave.
        int[] weekendDays;
        if (weekInfo?.Weekend is { } providerWeekend)
        {
            weekendDays = new int[providerWeekend.Length];
            for (var i = 0; i < providerWeekend.Length; i++)
            {
                weekendDays[i] = IntlUtilities.DayOfWeekToCldrDayNumber(providerWeekend[i]);
            }
            System.Array.Sort(weekendDays);
        }
        else if (weekInfo != null)
        {
            weekendDays = [];
        }
        else
        {
            weekendDays = WeekData.GetWeekend(fallbackRegion);
        }

        var weekend = new JsArray(Engine, (uint) weekendDays.Length);
        for (var i = 0; i < weekendDays.Length; i++)
        {
            weekend.SetIndexValue((uint) i, weekendDays[i], updateLength: true);
        }
        result.CreateDataPropertyOrThrow("weekend", weekend);

        return result;
    }

    /// <summary>
    /// https://tc39.es/ecma402/#sec-weekdayuvaluetonumber - the ISO 8601 day number for a
    /// Unicode First Day Identifier, and undefined for any other string.
    /// </summary>
    /// <remarks>
    /// The undefined answer is what makes https://tc39.es/ecma402/#sec-Intl.Locale.prototype.getWeekInfo
    /// leave [[FirstDay]] at the region's own value: the override applies only "If firstDay is not
    /// undefined". This used to answer Monday for every unrecognized identifier, which a tag could not
    /// reach while "en-u-fw" and "en-u-fw-true" were parked in the unrecognized-keyword list; now that
    /// they resolve to a present, empty [[FirstDayOfWeek]], they reach it.
    /// </remarks>
    private static int? WeekdayUValueToNumber(string dayName)
    {
        return dayName switch
        {
            "mon" => 1,
            "tue" => 2,
            "wed" => 3,
            "thu" => 4,
            "fri" => 5,
            "sat" => 6,
            "sun" => 7,
            _ => null
        };
    }
}
