using Jint.Native.Object;
using Jint.Native.Symbol;
using Jint.Runtime;
using Jint.Runtime.Descriptors;
using Jint.Runtime.Interop;

namespace Jint.Native.Temporal;

/// <summary>
/// https://tc39.es/proposal-temporal/#sec-properties-of-the-temporal-plaindatetime-prototype-object
/// </summary>
internal sealed class PlainDateTimePrototype : Prototype
{
    private readonly PlainDateTimeConstructor _constructor;

    internal PlainDateTimePrototype(
        Engine engine,
        Realm realm,
        PlainDateTimeConstructor constructor,
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
        DefineAccessor("dayOfWeek", GetDayOfWeek);
        DefineAccessor("dayOfYear", GetDayOfYear);

        var symbols = new SymbolDictionary(1)
        {
            [GlobalSymbolRegistry.ToStringTag] = new("Temporal.PlainDateTime", PropertyFlag.Configurable)
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

    private JsPlainDateTime ValidatePlainDateTime(JsValue thisObject)
    {
        if (thisObject is JsPlainDateTime plainDateTime)
            return plainDateTime;
        Throw.TypeError(_realm, "Value is not a Temporal.PlainDateTime");
        return null!;
    }

    private JsString GetCalendarId(JsValue thisObject, JsCallArguments arguments) => new JsString(ValidatePlainDateTime(thisObject).Calendar);
    private JsNumber GetYear(JsValue thisObject, JsCallArguments arguments) => JsNumber.Create(ValidatePlainDateTime(thisObject).IsoDateTime.Year);
    private JsNumber GetMonth(JsValue thisObject, JsCallArguments arguments) => JsNumber.Create(ValidatePlainDateTime(thisObject).IsoDateTime.Month);
    private JsString GetMonthCode(JsValue thisObject, JsCallArguments arguments) => new JsString($"M{ValidatePlainDateTime(thisObject).IsoDateTime.Month:D2}");
    private JsNumber GetDay(JsValue thisObject, JsCallArguments arguments) => JsNumber.Create(ValidatePlainDateTime(thisObject).IsoDateTime.Day);
    private JsNumber GetHour(JsValue thisObject, JsCallArguments arguments) => JsNumber.Create(ValidatePlainDateTime(thisObject).IsoDateTime.Hour);
    private JsNumber GetMinute(JsValue thisObject, JsCallArguments arguments) => JsNumber.Create(ValidatePlainDateTime(thisObject).IsoDateTime.Minute);
    private JsNumber GetSecond(JsValue thisObject, JsCallArguments arguments) => JsNumber.Create(ValidatePlainDateTime(thisObject).IsoDateTime.Second);
    private JsNumber GetMillisecond(JsValue thisObject, JsCallArguments arguments) => JsNumber.Create(ValidatePlainDateTime(thisObject).IsoDateTime.Millisecond);
    private JsNumber GetMicrosecond(JsValue thisObject, JsCallArguments arguments) => JsNumber.Create(ValidatePlainDateTime(thisObject).IsoDateTime.Microsecond);
    private JsNumber GetNanosecond(JsValue thisObject, JsCallArguments arguments) => JsNumber.Create(ValidatePlainDateTime(thisObject).IsoDateTime.Nanosecond);
    private JsNumber GetDayOfWeek(JsValue thisObject, JsCallArguments arguments) => JsNumber.Create(ValidatePlainDateTime(thisObject).IsoDateTime.Date.DayOfWeek());
    private JsNumber GetDayOfYear(JsValue thisObject, JsCallArguments arguments) => JsNumber.Create(ValidatePlainDateTime(thisObject).IsoDateTime.Date.DayOfYear());
}
