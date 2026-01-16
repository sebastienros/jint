using Jint.Native.Date;
using Jint.Native.Object;
using Jint.Native.Symbol;
using Jint.Runtime;
using Jint.Runtime.Descriptors;
using Jint.Runtime.Interop;

namespace Jint.Native.Intl;

/// <summary>
/// https://tc39.es/ecma402/#sec-properties-of-intl-datetimeformat-prototype-object
/// </summary>
internal sealed class DateTimeFormatPrototype : Prototype
{
    private readonly DateTimeFormatConstructor _constructor;

    public DateTimeFormatPrototype(Engine engine,
        Realm realm,
        DateTimeFormatConstructor constructor,
        ObjectPrototype objectPrototype) : base(engine, realm)
    {
        _prototype = objectPrototype;
        _constructor = constructor;
    }

    protected override void Initialize()
    {
        const PropertyFlag LengthFlags = PropertyFlag.Configurable;
        const PropertyFlag PropertyFlags = PropertyFlag.Writable | PropertyFlag.Configurable;

        var properties = new PropertyDictionary(5, checkExistingKeys: false)
        {
            ["constructor"] = new PropertyDescriptor(_constructor, PropertyFlag.NonEnumerable),
            ["resolvedOptions"] = new PropertyDescriptor(new ClrFunction(Engine, "resolvedOptions", ResolvedOptions, 0, LengthFlags), PropertyFlags),
            ["formatToParts"] = new PropertyDescriptor(new ClrFunction(Engine, "formatToParts", FormatToParts, 1, LengthFlags), PropertyFlags),
            ["formatRange"] = new PropertyDescriptor(new ClrFunction(Engine, "formatRange", FormatRange, 2, LengthFlags), PropertyFlags),
            ["formatRangeToParts"] = new PropertyDescriptor(new ClrFunction(Engine, "formatRangeToParts", FormatRangeToParts, 2, LengthFlags), PropertyFlags),
        };
        SetProperties(properties);

        // format is an accessor property - accessor properties don't have writable attribute
        SetAccessor("format", new GetSetPropertyDescriptor(
            new ClrFunction(Engine, "get format", GetFormat, 0, LengthFlags),
            Undefined,
            PropertyFlag.Configurable));

        var symbols = new SymbolDictionary(1)
        {
            [GlobalSymbolRegistry.ToStringTag] = new("Intl.DateTimeFormat", PropertyFlag.Configurable)
        };
        SetSymbols(symbols);
    }

    private void SetAccessor(string name, GetSetPropertyDescriptor descriptor)
    {
        SetProperty(name, descriptor);
    }

    private JsDateTimeFormat ValidateDateTimeFormat(JsValue thisObject)
    {
        if (thisObject is JsDateTimeFormat dateTimeFormat)
        {
            return dateTimeFormat;
        }

        Throw.TypeError(_realm, "Value is not an Intl.DateTimeFormat");
        return null!; // Never reached
    }

    /// <summary>
    /// https://tc39.es/ecma402/#sec-intl.datetimeformat.prototype.format
    /// </summary>
    private ClrFunction GetFormat(JsValue thisObject, JsCallArguments arguments)
    {
        var dateTimeFormat = ValidateDateTimeFormat(thisObject);

        // Return a bound format function
        return new ClrFunction(Engine, "", (_, args) =>
        {
            var dateValue = args.At(0);
            var dateTime = ToDateTime(dateValue);
            return dateTimeFormat.Format(dateTime);
        }, 1, PropertyFlag.Configurable);
    }

    /// <summary>
    /// https://tc39.es/ecma402/#sec-intl.datetimeformat.prototype.formattoparts
    /// </summary>
    private JsArray FormatToParts(JsValue thisObject, JsCallArguments arguments)
    {
        var dateTimeFormat = ValidateDateTimeFormat(thisObject);
        var dateValue = arguments.At(0);
        var dateTime = ToDateTime(dateValue);

        var parts = dateTimeFormat.FormatToParts(dateTime);
        var result = new JsArray(Engine, (uint) parts.Count);

        for (var i = 0; i < parts.Count; i++)
        {
            var partObj = OrdinaryObjectCreate(Engine, Engine.Realm.Intrinsics.Object.PrototypeObject);
            partObj.Set("type", parts[i].Type);
            partObj.Set("value", parts[i].Value);
            result.SetIndexValue((uint) i, partObj, updateLength: true);
        }

        return result;
    }

    private DateTime ToDateTime(JsValue value)
    {
        if (value.IsUndefined())
        {
            return DateTime.Now;
        }

        if (value is JsDate jsDate)
        {
            // ECMA-402 requires formatting in local time unless a specific timezone is provided
            var dt = jsDate.ToDateTime();
            if (dt.Kind == DateTimeKind.Utc || dt.Kind == DateTimeKind.Unspecified)
            {
                dt = dt.ToLocalTime();
            }
            return dt;
        }

        var timeValue = TypeConverter.ToNumber(value);
        DatePresentation presentation = timeValue;
        presentation = presentation.TimeClip();

        if (presentation.IsNaN)
        {
            Throw.RangeError(_realm, "Invalid time value");
        }

        // Clamp to .NET DateTime range if necessary
        if (presentation.Value < JsDate.Min)
        {
            return DateTime.MinValue;
        }
        if (presentation.Value > JsDate.Max)
        {
            return DateTime.MaxValue;
        }

        return presentation.ToDateTime().ToLocalTime();
    }

    /// <summary>
    /// Gets the default hour cycle for a locale.
    /// Most locales use h12, but some use h23 (24-hour format without leading zero for midnight).
    /// </summary>
    private static string GetDefaultHourCycle(string locale)
    {
        // Most English-speaking locales use 12-hour format
        var lang = locale.Split('-')[0].ToLowerInvariant();

        // 24-hour format locales
        if (lang is "de" or "fr" or "it" or "es" or "pt" or "nl" or "ru" or "pl" or "sv" or "da" or "nb" or "fi")
        {
            return "h23";
        }

        // Japanese uses h11 for 12-hour format (0-11 instead of 1-12)
        if (string.Equals(lang, "ja", StringComparison.Ordinal))
        {
            return "h11";
        }

        // Default to 12-hour format h12 (1-12)
        return "h12";
    }

    /// <summary>
    /// https://tc39.es/ecma402/#sec-intl.datetimeformat.prototype.resolvedoptions
    /// </summary>
    private JsObject ResolvedOptions(JsValue thisObject, JsCallArguments arguments)
    {
        var dateTimeFormat = ValidateDateTimeFormat(thisObject);

        var result = OrdinaryObjectCreate(Engine, Engine.Realm.Intrinsics.Object.PrototypeObject);

        // Use CreateDataPropertyOrThrow to avoid prototype chain setters
        result.CreateDataPropertyOrThrow("locale", dateTimeFormat.Locale);

        if (dateTimeFormat.Calendar != null)
        {
            result.CreateDataPropertyOrThrow("calendar", dateTimeFormat.Calendar);
        }
        else
        {
            result.CreateDataPropertyOrThrow("calendar", "gregory");
        }

        result.CreateDataPropertyOrThrow("numberingSystem", dateTimeFormat.NumberingSystem ?? "latn");

        // timeZone is always present - use local timezone if not specified
        // Convert Windows timezone IDs to IANA format per ECMA-402
        var timeZoneId = dateTimeFormat.TimeZone ?? TimeZoneInfo.Local.Id;
        result.CreateDataPropertyOrThrow("timeZone", ToIanaTimeZoneId(timeZoneId));

        // hourCycle and hour12 should be returned if hour is present
        if (dateTimeFormat.Hour != null)
        {
            // Use provided hourCycle or derive default from locale
            var hourCycle = dateTimeFormat.HourCycle ?? GetDefaultHourCycle(dateTimeFormat.Locale);
            result.CreateDataPropertyOrThrow("hourCycle", hourCycle);
            result.CreateDataPropertyOrThrow("hour12", string.Equals(hourCycle, "h11", StringComparison.Ordinal) ||
                                 string.Equals(hourCycle, "h12", StringComparison.Ordinal));
        }

        if (dateTimeFormat.DateStyle != null)
        {
            result.CreateDataPropertyOrThrow("dateStyle", dateTimeFormat.DateStyle);
        }

        if (dateTimeFormat.TimeStyle != null)
        {
            result.CreateDataPropertyOrThrow("timeStyle", dateTimeFormat.TimeStyle);
        }

        // Component options
        if (dateTimeFormat.Weekday != null)
        {
            result.CreateDataPropertyOrThrow("weekday", dateTimeFormat.Weekday);
        }

        if (dateTimeFormat.Era != null)
        {
            result.CreateDataPropertyOrThrow("era", dateTimeFormat.Era);
        }

        if (dateTimeFormat.Year != null)
        {
            result.CreateDataPropertyOrThrow("year", dateTimeFormat.Year);
        }

        if (dateTimeFormat.Month != null)
        {
            result.CreateDataPropertyOrThrow("month", dateTimeFormat.Month);
        }

        if (dateTimeFormat.Day != null)
        {
            result.CreateDataPropertyOrThrow("day", dateTimeFormat.Day);
        }

        // dayPeriod comes after day and before hour per ECMA-402 spec order
        if (dateTimeFormat.DayPeriod != null)
        {
            result.CreateDataPropertyOrThrow("dayPeriod", dateTimeFormat.DayPeriod);
        }

        if (dateTimeFormat.Hour != null)
        {
            result.CreateDataPropertyOrThrow("hour", dateTimeFormat.Hour);
        }

        if (dateTimeFormat.Minute != null)
        {
            result.CreateDataPropertyOrThrow("minute", dateTimeFormat.Minute);
        }

        if (dateTimeFormat.Second != null)
        {
            result.CreateDataPropertyOrThrow("second", dateTimeFormat.Second);
        }

        if (dateTimeFormat.FractionalSecondDigits.HasValue)
        {
            result.CreateDataPropertyOrThrow("fractionalSecondDigits", dateTimeFormat.FractionalSecondDigits.Value);
        }

        if (dateTimeFormat.TimeZoneName != null)
        {
            result.CreateDataPropertyOrThrow("timeZoneName", dateTimeFormat.TimeZoneName);
        }

        return result;
    }

    /// <summary>
    /// https://tc39.es/ecma402/#sec-intl.datetimeformat.prototype.formatrange
    /// </summary>
    private JsValue FormatRange(JsValue thisObject, JsCallArguments arguments)
    {
        var dateTimeFormat = ValidateDateTimeFormat(thisObject);

        var startDate = arguments.At(0);
        var endDate = arguments.At(1);

        // Validate arguments
        if (startDate.IsUndefined() || endDate.IsUndefined())
        {
            Throw.TypeError(_realm, "startDate and endDate are required");
        }

        var start = ToDateTimeForRange(startDate);
        var end = ToDateTimeForRange(endDate);

        // Format both dates
        var startFormatted = dateTimeFormat.Format(start);
        var endFormatted = dateTimeFormat.Format(end);

        // If the dates are the same when formatted, return just one
        if (string.Equals(startFormatted, endFormatted, StringComparison.Ordinal))
        {
            return startFormatted;
        }

        // Return a range string
        return $"{startFormatted} – {endFormatted}";
    }

    /// <summary>
    /// https://tc39.es/ecma402/#sec-intl.datetimeformat.prototype.formatrangetoparts
    /// </summary>
    private JsArray FormatRangeToParts(JsValue thisObject, JsCallArguments arguments)
    {
        var dateTimeFormat = ValidateDateTimeFormat(thisObject);

        var startDate = arguments.At(0);
        var endDate = arguments.At(1);

        // Validate arguments
        if (startDate.IsUndefined() || endDate.IsUndefined())
        {
            Throw.TypeError(_realm, "startDate and endDate are required");
        }

        var start = ToDateTimeForRange(startDate);
        var end = ToDateTimeForRange(endDate);

        // Get parts for both dates
        var startParts = dateTimeFormat.FormatToParts(start);
        var endParts = dateTimeFormat.FormatToParts(end);

        // Check if dates are practically equal (same formatted output)
        var startFormatted = dateTimeFormat.Format(start);
        var endFormatted = dateTimeFormat.Format(end);

        var result = new JsArray(Engine);
        uint index = 0;

        if (string.Equals(startFormatted, endFormatted, StringComparison.Ordinal))
        {
            // Dates are practically equal - return parts with source "shared"
            foreach (var part in startParts)
            {
                var partObj = OrdinaryObjectCreate(Engine, Engine.Realm.Intrinsics.Object.PrototypeObject);
                partObj.Set("type", part.Type);
                partObj.Set("value", part.Value);
                partObj.Set("source", "shared");
                result.SetIndexValue(index++, partObj, updateLength: true);
            }
        }
        else
        {
            // Add start date parts with source "startRange"
            foreach (var part in startParts)
            {
                var partObj = OrdinaryObjectCreate(Engine, Engine.Realm.Intrinsics.Object.PrototypeObject);
                partObj.Set("type", part.Type);
                partObj.Set("value", part.Value);
                partObj.Set("source", "startRange");
                result.SetIndexValue(index++, partObj, updateLength: true);
            }

            // Add separator
            var separator = OrdinaryObjectCreate(Engine, Engine.Realm.Intrinsics.Object.PrototypeObject);
            separator.Set("type", "literal");
            separator.Set("value", " – ");
            separator.Set("source", "shared");
            result.SetIndexValue(index++, separator, updateLength: true);

            // Add end date parts with source "endRange"
            foreach (var part in endParts)
            {
                var partObj = OrdinaryObjectCreate(Engine, Engine.Realm.Intrinsics.Object.PrototypeObject);
                partObj.Set("type", part.Type);
                partObj.Set("value", part.Value);
                partObj.Set("source", "endRange");
                result.SetIndexValue(index++, partObj, updateLength: true);
            }
        }

        return result;
    }

    private DateTime ToDateTimeForRange(JsValue value)
    {
        if (value is JsDate jsDate)
        {
            var dt = jsDate.ToDateTime();
            if (dt == DateTime.MinValue)
            {
                // Invalid date
                Throw.RangeError(_realm, "Invalid time value");
            }
            // ECMA-402 requires formatting in local time unless a specific timezone is provided
            if (dt.Kind == DateTimeKind.Utc || dt.Kind == DateTimeKind.Unspecified)
            {
                dt = dt.ToLocalTime();
            }
            return dt;
        }

        var timeValue = TypeConverter.ToNumber(value);
        DatePresentation presentation = timeValue;
        presentation = presentation.TimeClip();

        if (presentation.IsNaN)
        {
            Throw.RangeError(_realm, "Invalid time value");
        }

        // Clamp to .NET DateTime range if necessary
        if (presentation.Value < JsDate.Min)
        {
            return DateTime.MinValue;
        }
        if (presentation.Value > JsDate.Max)
        {
            return DateTime.MaxValue;
        }

        return presentation.ToDateTime().ToLocalTime();
    }

    /// <summary>
    /// Converts a timezone ID to IANA format.
    /// Windows uses names like "FLE Standard Time" but ECMA-402 requires IANA names like "Europe/Helsinki".
    /// </summary>
    private static string ToIanaTimeZoneId(string timeZoneId)
    {
        // UTC is already valid
        if (string.Equals(timeZoneId, "UTC", StringComparison.OrdinalIgnoreCase))
        {
            return "UTC";
        }

        // Offset timezones are already valid (e.g., "+03:00")
        if (timeZoneId.Length > 0 && (timeZoneId[0] == '+' || timeZoneId[0] == '-'))
        {
            return timeZoneId;
        }

#if NET6_0_OR_GREATER
        // Try to convert Windows ID to IANA
        if (TimeZoneInfo.TryConvertWindowsIdToIanaId(timeZoneId, out var ianaId))
        {
            return ianaId;
        }
#endif

        // Check if it's already an IANA ID (contains '/')
        if (timeZoneId.Contains('/'))
        {
            return timeZoneId;
        }

        // Fallback: try to get the IANA ID from the TimeZoneInfo
        try
        {
            var tz = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
#if NET6_0_OR_GREATER
            // On .NET 6+, check if HasIanaId is available
            if (tz.HasIanaId)
            {
                return tz.Id;
            }
            // Try conversion again with the found timezone
            if (TimeZoneInfo.TryConvertWindowsIdToIanaId(tz.Id, out ianaId))
            {
                return ianaId;
            }
#endif
        }
        catch
        {
            // Ignore errors
        }

        // Last resort: return as-is (won't pass validation but better than crashing)
        return timeZoneId;
    }
}
