using Jint.Native.Object;
using Jint.Native.Symbol;
using Jint.Runtime;
using Jint.Runtime.Descriptors;
using Jint.Runtime.Interop;

namespace Jint.Native.Temporal;

/// <summary>
/// https://tc39.es/proposal-temporal/#sec-properties-of-the-temporal-zoneddatetime-prototype-object
/// </summary>
internal sealed class ZonedDateTimePrototype : Prototype
{
    private readonly ZonedDateTimeConstructor _constructor;

    internal ZonedDateTimePrototype(
        Engine engine,
        Realm realm,
        ZonedDateTimeConstructor constructor,
        ObjectPrototype objectPrototype) : base(engine, realm)
    {
        _constructor = constructor;
        _prototype = objectPrototype;
    }

    protected override void Initialize()
    {
        var properties = new PropertyDictionary(1, checkExistingKeys: false)
        {
            ["constructor"] = new PropertyDescriptor(_constructor, PropertyFlag.NonEnumerable),
        };
        SetProperties(properties);

        DefineAccessor("calendarId", GetCalendarId);
        DefineAccessor("timeZoneId", GetTimeZoneId);
        DefineAccessor("year", GetYear);
        DefineAccessor("month", GetMonth);
        DefineAccessor("monthCode", GetMonthCode);
        DefineAccessor("day", GetDay);
        DefineAccessor("hour", GetHour);
        DefineAccessor("minute", GetMinute);
        DefineAccessor("second", GetSecond);
        DefineAccessor("millisecond", GetMillisecond);
        DefineAccessor("microsecond", GetMicrosecond);
        DefineAccessor("nanosecond", GetNanosecond);
        DefineAccessor("epochMilliseconds", GetEpochMilliseconds);
        DefineAccessor("epochNanoseconds", GetEpochNanoseconds);
        DefineAccessor("dayOfWeek", GetDayOfWeek);
        DefineAccessor("dayOfYear", GetDayOfYear);
        DefineAccessor("offset", GetOffset);
        DefineAccessor("offsetNanoseconds", GetOffsetNanoseconds);

        var symbols = new SymbolDictionary(1)
        {
            [GlobalSymbolRegistry.ToStringTag] = new("Temporal.ZonedDateTime", PropertyFlag.Configurable)
        };
        SetSymbols(symbols);
    }

    private void DefineAccessor(string name, Func<JsValue, JsCallArguments, JsValue> getter)
    {
        SetProperty(name, new GetSetPropertyDescriptor(
            new ClrFunction(Engine, $"get {name}", getter, 0, PropertyFlag.Configurable),
            Undefined,
            PropertyFlag.Configurable));
    }

    private JsZonedDateTime ValidateZonedDateTime(JsValue thisObject)
    {
        if (thisObject is JsZonedDateTime zonedDateTime)
            return zonedDateTime;
        Throw.TypeError(_realm, "Value is not a Temporal.ZonedDateTime");
        return null!;
    }

    private JsString GetCalendarId(JsValue thisObject, JsCallArguments arguments) => new JsString(ValidateZonedDateTime(thisObject).Calendar);
    private JsString GetTimeZoneId(JsValue thisObject, JsCallArguments arguments) => new JsString(ValidateZonedDateTime(thisObject).TimeZone);
    private JsNumber GetYear(JsValue thisObject, JsCallArguments arguments) => JsNumber.Create(ValidateZonedDateTime(thisObject).GetIsoDateTime().Year);
    private JsNumber GetMonth(JsValue thisObject, JsCallArguments arguments) => JsNumber.Create(ValidateZonedDateTime(thisObject).GetIsoDateTime().Month);
    private JsString GetMonthCode(JsValue thisObject, JsCallArguments arguments) => new JsString($"M{ValidateZonedDateTime(thisObject).GetIsoDateTime().Month:D2}");
    private JsNumber GetDay(JsValue thisObject, JsCallArguments arguments) => JsNumber.Create(ValidateZonedDateTime(thisObject).GetIsoDateTime().Day);
    private JsNumber GetHour(JsValue thisObject, JsCallArguments arguments) => JsNumber.Create(ValidateZonedDateTime(thisObject).GetIsoDateTime().Hour);
    private JsNumber GetMinute(JsValue thisObject, JsCallArguments arguments) => JsNumber.Create(ValidateZonedDateTime(thisObject).GetIsoDateTime().Minute);
    private JsNumber GetSecond(JsValue thisObject, JsCallArguments arguments) => JsNumber.Create(ValidateZonedDateTime(thisObject).GetIsoDateTime().Second);
    private JsNumber GetMillisecond(JsValue thisObject, JsCallArguments arguments) => JsNumber.Create(ValidateZonedDateTime(thisObject).GetIsoDateTime().Millisecond);
    private JsNumber GetMicrosecond(JsValue thisObject, JsCallArguments arguments) => JsNumber.Create(ValidateZonedDateTime(thisObject).GetIsoDateTime().Microsecond);
    private JsNumber GetNanosecond(JsValue thisObject, JsCallArguments arguments) => JsNumber.Create(ValidateZonedDateTime(thisObject).GetIsoDateTime().Nanosecond);
    private JsNumber GetEpochMilliseconds(JsValue thisObject, JsCallArguments arguments) => JsNumber.Create((double)(ValidateZonedDateTime(thisObject).EpochNanoseconds / 1_000_000));
    private JsBigInt GetEpochNanoseconds(JsValue thisObject, JsCallArguments arguments) => JsBigInt.Create(ValidateZonedDateTime(thisObject).EpochNanoseconds);
    private JsNumber GetDayOfWeek(JsValue thisObject, JsCallArguments arguments) => JsNumber.Create(ValidateZonedDateTime(thisObject).GetIsoDateTime().Date.DayOfWeek());
    private JsNumber GetDayOfYear(JsValue thisObject, JsCallArguments arguments) => JsNumber.Create(ValidateZonedDateTime(thisObject).GetIsoDateTime().Date.DayOfYear());
    private JsString GetOffset(JsValue thisObject, JsCallArguments arguments)
    {
        var zdt = ValidateZonedDateTime(thisObject);
        var offsetNs = zdt.OffsetNanoseconds;
        return new JsString(TemporalHelpers.FormatOffsetString(offsetNs));
    }
    private JsNumber GetOffsetNanoseconds(JsValue thisObject, JsCallArguments arguments) => JsNumber.Create(ValidateZonedDateTime(thisObject).OffsetNanoseconds);
}
