using Jint.Native.Object;
using Jint.Native.Symbol;
using Jint.Runtime;
using Jint.Runtime.Descriptors;
using Jint.Runtime.Interop;

namespace Jint.Native.Temporal;

/// <summary>
/// https://tc39.es/proposal-temporal/#sec-properties-of-the-temporal-plaindate-prototype-object
/// </summary>
internal sealed class PlainDatePrototype : Prototype
{
    private readonly PlainDateConstructor _constructor;

    internal PlainDatePrototype(
        Engine engine,
        Realm realm,
        PlainDateConstructor constructor,
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
        DefineAccessor("era", GetEra);
        DefineAccessor("eraYear", GetEraYear);
        DefineAccessor("year", GetYear);
        DefineAccessor("month", GetMonth);
        DefineAccessor("monthCode", GetMonthCode);
        DefineAccessor("day", GetDay);
        DefineAccessor("dayOfWeek", GetDayOfWeek);
        DefineAccessor("dayOfYear", GetDayOfYear);
        DefineAccessor("weekOfYear", GetWeekOfYear);
        DefineAccessor("yearOfWeek", GetYearOfWeek);
        DefineAccessor("daysInWeek", GetDaysInWeek);
        DefineAccessor("daysInMonth", GetDaysInMonth);
        DefineAccessor("daysInYear", GetDaysInYear);
        DefineAccessor("monthsInYear", GetMonthsInYear);
        DefineAccessor("inLeapYear", GetInLeapYear);

        var symbols = new SymbolDictionary(1)
        {
            [GlobalSymbolRegistry.ToStringTag] = new("Temporal.PlainDate", PropertyFlag.Configurable)
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

    private JsPlainDate ValidatePlainDate(JsValue thisObject)
    {
        if (thisObject is JsPlainDate plainDate)
            return plainDate;
        Throw.TypeError(_realm, "Value is not a Temporal.PlainDate");
        return null!;
    }

    private JsString GetCalendarId(JsValue thisObject, JsCallArguments arguments) => new JsString(ValidatePlainDate(thisObject).Calendar);
    private JsValue GetEra(JsValue thisObject, JsCallArguments arguments) => Undefined;
    private JsValue GetEraYear(JsValue thisObject, JsCallArguments arguments) => Undefined;
    private JsNumber GetYear(JsValue thisObject, JsCallArguments arguments) => JsNumber.Create(ValidatePlainDate(thisObject).IsoDate.Year);
    private JsNumber GetMonth(JsValue thisObject, JsCallArguments arguments) => JsNumber.Create(ValidatePlainDate(thisObject).IsoDate.Month);
    private JsString GetMonthCode(JsValue thisObject, JsCallArguments arguments) => new JsString($"M{ValidatePlainDate(thisObject).IsoDate.Month:D2}");
    private JsNumber GetDay(JsValue thisObject, JsCallArguments arguments) => JsNumber.Create(ValidatePlainDate(thisObject).IsoDate.Day);
    private JsNumber GetDayOfWeek(JsValue thisObject, JsCallArguments arguments) => JsNumber.Create(ValidatePlainDate(thisObject).IsoDate.DayOfWeek());
    private JsNumber GetDayOfYear(JsValue thisObject, JsCallArguments arguments) => JsNumber.Create(ValidatePlainDate(thisObject).IsoDate.DayOfYear());
    private JsNumber GetWeekOfYear(JsValue thisObject, JsCallArguments arguments) => JsNumber.Create(ValidatePlainDate(thisObject).IsoDate.WeekOfYear());
    private JsNumber GetYearOfWeek(JsValue thisObject, JsCallArguments arguments) => JsNumber.Create(ValidatePlainDate(thisObject).IsoDate.YearOfWeek());
    private JsNumber GetDaysInWeek(JsValue thisObject, JsCallArguments arguments) => JsNumber.Create(7);
    private JsNumber GetDaysInMonth(JsValue thisObject, JsCallArguments arguments) => JsNumber.Create(ValidatePlainDate(thisObject).IsoDate.DaysInMonth());
    private JsNumber GetDaysInYear(JsValue thisObject, JsCallArguments arguments) => JsNumber.Create(ValidatePlainDate(thisObject).IsoDate.DaysInYear());
    private JsNumber GetMonthsInYear(JsValue thisObject, JsCallArguments arguments) => JsNumber.Create(12);
    private JsBoolean GetInLeapYear(JsValue thisObject, JsCallArguments arguments) => IsoDate.IsLeapYear(ValidatePlainDate(thisObject).IsoDate.Year) ? JsBoolean.True : JsBoolean.False;
}
