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

        // For now, return a simple implementation that returns the whole formatted string as one part
        var formatted = dateTimeFormat.Format(dateTime);

        var result = new JsArray(Engine, 1);
        var part = OrdinaryObjectCreate(Engine, Engine.Realm.Intrinsics.Object.PrototypeObject);
        part.Set("type", "literal");
        part.Set("value", formatted);
        result.SetIndexValue(0, part, updateLength: true);

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
            return jsDate.ToDateTime();
        }

        var timeValue = TypeConverter.ToNumber(value);
        if (double.IsNaN(timeValue) || double.IsInfinity(timeValue))
        {
            Throw.RangeError(_realm, "Invalid time value");
        }

        // Convert milliseconds since epoch to DateTime
        var epoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        return epoch.AddMilliseconds(timeValue).ToLocalTime();
    }

    /// <summary>
    /// https://tc39.es/ecma402/#sec-intl.datetimeformat.prototype.resolvedoptions
    /// </summary>
    private JsObject ResolvedOptions(JsValue thisObject, JsCallArguments arguments)
    {
        var dateTimeFormat = ValidateDateTimeFormat(thisObject);

        var result = OrdinaryObjectCreate(Engine, Engine.Realm.Intrinsics.Object.PrototypeObject);

        result.Set("locale", dateTimeFormat.Locale);

        if (dateTimeFormat.Calendar != null)
        {
            result.Set("calendar", dateTimeFormat.Calendar);
        }
        else
        {
            result.Set("calendar", "gregory");
        }

        result.Set("numberingSystem", dateTimeFormat.NumberingSystem ?? "latn");

        if (dateTimeFormat.TimeZone != null)
        {
            result.Set("timeZone", dateTimeFormat.TimeZone);
        }

        // hourCycle and hour12 should only be returned if hour is present
        if (dateTimeFormat.HourCycle != null && dateTimeFormat.Hour != null)
        {
            result.Set("hourCycle", dateTimeFormat.HourCycle);
            result.Set("hour12", string.Equals(dateTimeFormat.HourCycle, "h11", StringComparison.Ordinal) ||
                                 string.Equals(dateTimeFormat.HourCycle, "h12", StringComparison.Ordinal));
        }

        if (dateTimeFormat.DateStyle != null)
        {
            result.Set("dateStyle", dateTimeFormat.DateStyle);
        }

        if (dateTimeFormat.TimeStyle != null)
        {
            result.Set("timeStyle", dateTimeFormat.TimeStyle);
        }

        // Component options
        if (dateTimeFormat.Weekday != null)
        {
            result.Set("weekday", dateTimeFormat.Weekday);
        }

        if (dateTimeFormat.Era != null)
        {
            result.Set("era", dateTimeFormat.Era);
        }

        if (dateTimeFormat.Year != null)
        {
            result.Set("year", dateTimeFormat.Year);
        }

        if (dateTimeFormat.Month != null)
        {
            result.Set("month", dateTimeFormat.Month);
        }

        if (dateTimeFormat.Day != null)
        {
            result.Set("day", dateTimeFormat.Day);
        }

        if (dateTimeFormat.DayPeriod != null)
        {
            result.Set("dayPeriod", dateTimeFormat.DayPeriod);
        }

        if (dateTimeFormat.Hour != null)
        {
            result.Set("hour", dateTimeFormat.Hour);
        }

        if (dateTimeFormat.Minute != null)
        {
            result.Set("minute", dateTimeFormat.Minute);
        }

        if (dateTimeFormat.Second != null)
        {
            result.Set("second", dateTimeFormat.Second);
        }

        if (dateTimeFormat.FractionalSecondDigits.HasValue)
        {
            result.Set("fractionalSecondDigits", dateTimeFormat.FractionalSecondDigits.Value);
        }

        if (dateTimeFormat.TimeZoneName != null)
        {
            result.Set("timeZoneName", dateTimeFormat.TimeZoneName);
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

        // Format both dates
        var startFormatted = dateTimeFormat.Format(start);
        var endFormatted = dateTimeFormat.Format(end);

        var result = new JsArray(Engine);
        uint index = 0;

        // Add start date parts with source "startRange"
        var startPart = ObjectInstance.OrdinaryObjectCreate(Engine, Engine.Realm.Intrinsics.Object.PrototypeObject);
        startPart.Set("type", "literal");
        startPart.Set("value", startFormatted);
        startPart.Set("source", "startRange");
        result.SetIndexValue(index++, startPart, updateLength: true);

        // If dates are different, add separator and end date
        if (!string.Equals(startFormatted, endFormatted, StringComparison.Ordinal))
        {
            // Add separator
            var separator = ObjectInstance.OrdinaryObjectCreate(Engine, Engine.Realm.Intrinsics.Object.PrototypeObject);
            separator.Set("type", "literal");
            separator.Set("value", " – ");
            separator.Set("source", "shared");
            result.SetIndexValue(index++, separator, updateLength: true);

            // Add end date parts with source "endRange"
            var endPart = ObjectInstance.OrdinaryObjectCreate(Engine, Engine.Realm.Intrinsics.Object.PrototypeObject);
            endPart.Set("type", "literal");
            endPart.Set("value", endFormatted);
            endPart.Set("source", "endRange");
            result.SetIndexValue(index++, endPart, updateLength: true);
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
            return dt;
        }

        var timeValue = TypeConverter.ToNumber(value);
        if (double.IsNaN(timeValue))
        {
            Throw.RangeError(_realm, "Invalid time value");
        }
        if (double.IsInfinity(timeValue))
        {
            Throw.RangeError(_realm, "Invalid time value");
        }

        // Convert milliseconds since epoch to DateTime
        var epoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        return epoch.AddMilliseconds(timeValue).ToLocalTime();
    }
}
