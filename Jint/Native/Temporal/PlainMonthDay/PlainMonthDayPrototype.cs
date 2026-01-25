using Jint.Native.Object;
using Jint.Native.Symbol;
using Jint.Runtime;
using Jint.Runtime.Descriptors;
using Jint.Runtime.Interop;

namespace Jint.Native.Temporal;

/// <summary>
/// https://tc39.es/proposal-temporal/#sec-properties-of-the-temporal-plainmonthday-prototype-object
/// </summary>
internal sealed class PlainMonthDayPrototype : Prototype
{
    private readonly PlainMonthDayConstructor _constructor;

    internal PlainMonthDayPrototype(
        Engine engine,
        Realm realm,
        PlainMonthDayConstructor constructor,
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
        DefineAccessor("monthCode", GetMonthCode);
        DefineAccessor("day", GetDay);

        var symbols = new SymbolDictionary(1)
        {
            [GlobalSymbolRegistry.ToStringTag] = new("Temporal.PlainMonthDay", PropertyFlag.Configurable)
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

    private JsPlainMonthDay ValidatePlainMonthDay(JsValue thisObject)
    {
        if (thisObject is JsPlainMonthDay plainMonthDay)
            return plainMonthDay;
        Throw.TypeError(_realm, "Value is not a Temporal.PlainMonthDay");
        return null!;
    }

    private JsString GetCalendarId(JsValue thisObject, JsCallArguments arguments) => new JsString(ValidatePlainMonthDay(thisObject).Calendar);
    private JsString GetMonthCode(JsValue thisObject, JsCallArguments arguments) => new JsString($"M{ValidatePlainMonthDay(thisObject).IsoDate.Month:D2}");
    private JsNumber GetDay(JsValue thisObject, JsCallArguments arguments) => JsNumber.Create(ValidatePlainMonthDay(thisObject).IsoDate.Day);
}
