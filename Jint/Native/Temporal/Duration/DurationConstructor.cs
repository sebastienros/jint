using Jint.Native.Function;
using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Descriptors;
using Jint.Runtime.Interop;

namespace Jint.Native.Temporal;

/// <summary>
/// https://tc39.es/proposal-temporal/#sec-temporal.duration
/// </summary>
internal sealed class DurationConstructor : Constructor
{
    private static readonly JsString _functionName = new("Duration");

    internal DurationConstructor(
        Engine engine,
        Realm realm,
        FunctionPrototype functionPrototype,
        ObjectPrototype objectPrototype) : base(engine, realm, _functionName)
    {
        _prototype = functionPrototype;
        PrototypeObject = new DurationPrototype(engine, realm, this, objectPrototype);
        _length = new PropertyDescriptor(JsNumber.PositiveZero, PropertyFlag.Configurable);
        _prototypeDescriptor = new PropertyDescriptor(PrototypeObject, PropertyFlag.AllForbidden);
    }

    public DurationPrototype PrototypeObject { get; }

    protected override void Initialize()
    {
        const PropertyFlag PropertyFlags = PropertyFlag.Writable | PropertyFlag.Configurable;
        const PropertyFlag LengthFlags = PropertyFlag.Configurable;

        var properties = new PropertyDictionary(2, checkExistingKeys: false)
        {
            ["from"] = new(new ClrFunction(Engine, "from", From, 1, LengthFlags), PropertyFlags),
            ["compare"] = new(new ClrFunction(Engine, "compare", Compare, 2, LengthFlags), PropertyFlags),
        };
        SetProperties(properties);
    }

    /// <summary>
    /// https://tc39.es/proposal-temporal/#sec-temporal.duration.from
    /// </summary>
    private JsDuration From(JsValue thisObject, JsCallArguments arguments)
    {
        var item = arguments.At(0);
        // If item is already a Duration, create a copy (spec requires a new object)
        if (item is JsDuration duration)
        {
            return Construct(duration.DurationRecord);
        }
        return ToTemporalDuration(item);
    }

    /// <summary>
    /// https://tc39.es/proposal-temporal/#sec-temporal.duration.compare
    /// </summary>
    private JsNumber Compare(JsValue thisObject, JsCallArguments arguments)
    {
        var one = ToTemporalDuration(arguments.At(0));
        var two = ToTemporalDuration(arguments.At(1));
        var options = arguments.At(2);

        var optionsObj = TemporalHelpers.GetOptionsObject(_realm, options);
        var relativeToResult = TemporalHelpers.GetTemporalRelativeToOption(_engine, _realm, optionsObj);

        var d1 = one.DurationRecord;
        var d2 = two.DurationRecord;

        // Step 5: If all components are equal, return +0
        if (d1.Years == d2.Years && d1.Months == d2.Months && d1.Weeks == d2.Weeks &&
            d1.Days == d2.Days && d1.Hours == d2.Hours && d1.Minutes == d2.Minutes &&
            d1.Seconds == d2.Seconds && d1.Milliseconds == d2.Milliseconds &&
            d1.Microseconds == d2.Microseconds && d1.Nanoseconds == d2.Nanoseconds)
        {
            return JsNumber.PositiveZero;
        }

        var zonedRelativeTo = relativeToResult.ZonedRelativeTo;
        var plainRelativeTo = relativeToResult.PlainRelativeTo;

        // Step 9-10: DefaultTemporalLargestUnit
        var largestUnit1 = TemporalHelpers.DefaultTemporalLargestUnit(d1);
        var largestUnit2 = TemporalHelpers.DefaultTemporalLargestUnit(d2);

        // Step 11: ToInternalDurationRecord for each
        var timeDuration1 = TemporalHelpers.TimeDurationFromComponents(d1);
        var timeDuration2 = TemporalHelpers.TimeDurationFromComponents(d2);

        // Step 7: If zonedRelativeTo is not undefined and either largest unit is a date unit
        var isDateUnit1 = IsDateUnit(largestUnit1);
        var isDateUnit2 = IsDateUnit(largestUnit2);

        if (zonedRelativeTo is not null && (isDateUnit1 || isDateUnit2))
        {
            var timeZone = zonedRelativeTo.TimeZone;
            var calendar = zonedRelativeTo.Calendar;
            var provider = _engine.Options.Temporal.TimeZoneProvider;
            var after1 = TemporalHelpers.AddZonedDateTime(_realm, provider, zonedRelativeTo.EpochNanoseconds, timeZone, calendar, d1, "constrain");
            var after2 = TemporalHelpers.AddZonedDateTime(_realm, provider, zonedRelativeTo.EpochNanoseconds, timeZone, calendar, d2, "constrain");
            return JsNumber.Create(after1.CompareTo(after2));
        }

        // Step 8: If IsCalendarUnit for either largest unit
        double days1, days2;
        if (TemporalHelpers.IsCalendarUnit(largestUnit1) || TemporalHelpers.IsCalendarUnit(largestUnit2))
        {
            if (plainRelativeTo is null)
            {
                Throw.RangeError(_realm, "Duration comparison with calendar units requires relativeTo");
            }
            days1 = DateDurationDays(d1, plainRelativeTo);
            days2 = DateDurationDays(d2, plainRelativeTo);
        }
        else
        {
            days1 = d1.Days;
            days2 = d2.Days;
        }

        // Step 12-13: Add24HourDaysToTimeDuration (with maxTimeDuration check)
        var td1 = TemporalHelpers.Add24HourDaysToTimeDurationChecked(_realm, timeDuration1, days1);
        var td2 = TemporalHelpers.Add24HourDaysToTimeDurationChecked(_realm, timeDuration2, days2);

        // Step 14: CompareTimeDuration
        return JsNumber.Create(td1.CompareTo(td2));
    }

    /// <summary>
    /// Returns whether a unit is a date category unit (year, month, week, day).
    /// </summary>
    private static bool IsDateUnit(string unit)
    {
        return unit is "year" or "month" or "week" or "day";
    }

    /// <summary>
    /// DateDurationDays ( dateDuration, plainRelativeTo )
    /// https://tc39.es/proposal-temporal/#sec-temporal-datedurationdays
    /// Converts calendar units of a duration into a number of days.
    /// </summary>
    private double DateDurationDays(DurationRecord duration, JsPlainDate plainRelativeTo)
    {
        // Create a duration with only years/months/weeks (days=0)
        var yearsMonthsWeeksDuration = new DurationRecord(
            duration.Years, duration.Months, duration.Weeks, 0,
            0, 0, 0, 0, 0, 0);

        // If no calendar components, just return days
        if (yearsMonthsWeeksDuration.Years == 0 && yearsMonthsWeeksDuration.Months == 0 && yearsMonthsWeeksDuration.Weeks == 0)
        {
            return duration.Days;
        }

        // Add years/months/weeks to get the later date
        var later = TemporalHelpers.CalendarDateAdd(_realm, plainRelativeTo.Calendar, plainRelativeTo.IsoDate, yearsMonthsWeeksDuration, "constrain");

        // Compute epoch days difference
        var epochDays1 = TemporalHelpers.IsoDateToDays(plainRelativeTo.IsoDate.Year, plainRelativeTo.IsoDate.Month, plainRelativeTo.IsoDate.Day);
        var epochDays2 = TemporalHelpers.IsoDateToDays(later.Year, later.Month, later.Day);
        var yearsMonthsWeeksInDays = epochDays2 - epochDays1;

        return duration.Days + yearsMonthsWeeksInDays;
    }

    protected internal override JsValue Call(JsValue thisObject, JsCallArguments arguments)
    {
        Throw.TypeError(_realm, "Temporal.Duration cannot be called as a function");
        return Undefined;
    }

    /// <summary>
    /// https://tc39.es/proposal-temporal/#sec-temporal.duration
    /// </summary>
    public override ObjectInstance Construct(JsCallArguments arguments, JsValue newTarget)
    {
        var years = ToIntegerIfIntegral(arguments.At(0), "years");
        var months = ToIntegerIfIntegral(arguments.At(1), "months");
        var weeks = ToIntegerIfIntegral(arguments.At(2), "weeks");
        var days = ToIntegerIfIntegral(arguments.At(3), "days");
        var hours = ToIntegerIfIntegral(arguments.At(4), "hours");
        var minutes = ToIntegerIfIntegral(arguments.At(5), "minutes");
        var seconds = ToIntegerIfIntegral(arguments.At(6), "seconds");
        var milliseconds = ToIntegerIfIntegral(arguments.At(7), "milliseconds");
        var microseconds = ToIntegerIfIntegral(arguments.At(8), "microseconds");
        var nanoseconds = ToIntegerIfIntegral(arguments.At(9), "nanoseconds");

        var record = new DurationRecord(years, months, weeks, days, hours, minutes, seconds, milliseconds, microseconds, nanoseconds);

        if (!TemporalHelpers.IsValidDuration(record))
        {
            Throw.RangeError(_realm, "Invalid duration");
        }

        return Construct(record, newTarget);
    }

    private double ToIntegerIfIntegral(JsValue value, string name)
    {
        if (value.IsUndefined())
            return 0;

        var number = TypeConverter.ToNumber(value);

        if (double.IsNaN(number))
        {
            Throw.RangeError(_realm, $"Duration {name} must be a finite number");
        }

        if (double.IsInfinity(number))
        {
            Throw.RangeError(_realm, $"Duration {name} must be a finite number");
        }

        if (number != System.Math.Truncate(number))
        {
            Throw.RangeError(_realm, $"Duration {name} must be an integer");
        }

        // Mathematical values don't have -0
        return number == 0 ? 0 : number;
    }

    /// <summary>
    /// https://tc39.es/proposal-temporal/#sec-temporal-totemporalduration
    /// </summary>
    internal JsDuration ToTemporalDuration(JsValue item)
    {
        if (item is JsDuration duration)
        {
            return duration;
        }

        if (item.IsString())
        {
            var parsed = TemporalHelpers.ParseDuration(item.ToString());
            if (parsed is null)
            {
                Throw.RangeError(_realm, "Invalid duration string");
            }
            // Validate parsed duration is within valid range (years/months/weeks < 2^32, etc.)
            if (!TemporalHelpers.IsValidDuration(parsed.Value))
            {
                Throw.RangeError(_realm, "Invalid duration");
            }
            return Construct(parsed.Value);
        }

        if (item.IsObject())
        {
            var obj = item.AsObject();
            var record = ToDurationRecord(obj);
            if (!TemporalHelpers.IsValidDuration(record))
            {
                Throw.RangeError(_realm, "Invalid duration");
            }
            return Construct(record);
        }

        Throw.TypeError(_realm, "Invalid duration");
        return null!;
    }

    private DurationRecord ToDurationRecord(ObjectInstance obj)
    {
        // Properties must be read in alphabetical order per spec
        // https://tc39.es/proposal-temporal/#sec-temporal-totemporaldurationrecord
        var hasAny = false;
        var days = GetDurationProperty(obj, "days", ref hasAny);
        var hours = GetDurationProperty(obj, "hours", ref hasAny);
        var microseconds = GetDurationProperty(obj, "microseconds", ref hasAny);
        var milliseconds = GetDurationProperty(obj, "milliseconds", ref hasAny);
        var minutes = GetDurationProperty(obj, "minutes", ref hasAny);
        var months = GetDurationProperty(obj, "months", ref hasAny);
        var nanoseconds = GetDurationProperty(obj, "nanoseconds", ref hasAny);
        var seconds = GetDurationProperty(obj, "seconds", ref hasAny);
        var weeks = GetDurationProperty(obj, "weeks", ref hasAny);
        var years = GetDurationProperty(obj, "years", ref hasAny);

        // At least one property must be defined
        if (!hasAny)
        {
            Throw.TypeError(_realm, "Duration object must have at least one temporal property");
        }

        return new DurationRecord(years, months, weeks, days, hours, minutes, seconds, milliseconds, microseconds, nanoseconds);
    }

    private double GetDurationProperty(ObjectInstance obj, string name, ref bool hasAny)
    {
        var value = obj.Get(name);
        if (value.IsUndefined())
            return 0;

        hasAny = true;

        // Use ToIntegerIfIntegral to ensure proper valueOf access and validation
        return TemporalHelpers.ToIntegerIfIntegral(_realm, value);
    }

    internal JsDuration Construct(DurationRecord duration, JsValue? newTarget = null)
    {
        if (!TemporalHelpers.IsValidDuration(duration))
        {
            Throw.RangeError(_realm, "Invalid duration");
        }

        // OrdinaryCreateFromConstructor for subclassing support
        var proto = newTarget is null
            ? PrototypeObject
            : _realm.Intrinsics.Function.GetPrototypeFromConstructor(newTarget, static intrinsics => intrinsics.TemporalDuration.PrototypeObject);

        return new JsDuration(_engine, proto, duration);
    }
}
