using Jint.Native.Object;
using Jint.Native.Symbol;
using Jint.Runtime;
using Jint.Runtime.Descriptors;
using Jint.Runtime.Interop;

namespace Jint.Native.Temporal;

/// <summary>
/// https://tc39.es/proposal-temporal/#sec-properties-of-the-temporal-plainyearmonth-prototype-object
/// </summary>
internal sealed class PlainYearMonthPrototype : Prototype
{
    private readonly PlainYearMonthConstructor _constructor;

    internal PlainYearMonthPrototype(
        Engine engine,
        Realm realm,
        PlainYearMonthConstructor constructor,
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
        DefineAccessor("daysInMonth", GetDaysInMonth);
        DefineAccessor("daysInYear", GetDaysInYear);
        DefineAccessor("monthsInYear", GetMonthsInYear);
        DefineAccessor("inLeapYear", GetInLeapYear);

        var symbols = new SymbolDictionary(1)
        {
            [GlobalSymbolRegistry.ToStringTag] = new("Temporal.PlainYearMonth", PropertyFlag.Configurable)
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

    private JsPlainYearMonth ValidatePlainYearMonth(JsValue thisObject)
    {
        if (thisObject is JsPlainYearMonth plainYearMonth)
            return plainYearMonth;
        Throw.TypeError(_realm, "Value is not a Temporal.PlainYearMonth");
        return null!;
    }

    private JsString GetCalendarId(JsValue thisObject, JsCallArguments arguments) => new JsString(ValidatePlainYearMonth(thisObject).Calendar);
    private JsNumber GetYear(JsValue thisObject, JsCallArguments arguments) => JsNumber.Create(ValidatePlainYearMonth(thisObject).IsoDate.Year);
    private JsNumber GetMonth(JsValue thisObject, JsCallArguments arguments) => JsNumber.Create(ValidatePlainYearMonth(thisObject).IsoDate.Month);
    private JsString GetMonthCode(JsValue thisObject, JsCallArguments arguments) => new JsString($"M{ValidatePlainYearMonth(thisObject).IsoDate.Month:D2}");
    private JsNumber GetDaysInMonth(JsValue thisObject, JsCallArguments arguments) => JsNumber.Create(ValidatePlainYearMonth(thisObject).IsoDate.DaysInMonth());
    private JsNumber GetDaysInYear(JsValue thisObject, JsCallArguments arguments) => JsNumber.Create(ValidatePlainYearMonth(thisObject).IsoDate.DaysInYear());
    private JsNumber GetMonthsInYear(JsValue thisObject, JsCallArguments arguments) => JsNumber.Create(12);
    private JsBoolean GetInLeapYear(JsValue thisObject, JsCallArguments arguments) => IsoDate.IsLeapYear(ValidatePlainYearMonth(thisObject).IsoDate.Year) ? JsBoolean.True : JsBoolean.False;
}
