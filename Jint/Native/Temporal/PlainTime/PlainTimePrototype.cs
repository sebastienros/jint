using Jint.Native.Object;
using Jint.Native.Symbol;
using Jint.Runtime;
using Jint.Runtime.Descriptors;
using Jint.Runtime.Interop;

namespace Jint.Native.Temporal;

/// <summary>
/// https://tc39.es/proposal-temporal/#sec-properties-of-the-temporal-plaintime-prototype-object
/// </summary>
internal sealed class PlainTimePrototype : Prototype
{
    private readonly PlainTimeConstructor _constructor;

    internal PlainTimePrototype(
        Engine engine,
        Realm realm,
        PlainTimeConstructor constructor,
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

        DefineAccessor("hour", GetHour);
        DefineAccessor("minute", GetMinute);
        DefineAccessor("second", GetSecond);
        DefineAccessor("millisecond", GetMillisecond);
        DefineAccessor("microsecond", GetMicrosecond);
        DefineAccessor("nanosecond", GetNanosecond);

        var symbols = new SymbolDictionary(1)
        {
            [GlobalSymbolRegistry.ToStringTag] = new("Temporal.PlainTime", PropertyFlag.Configurable)
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

    private JsPlainTime ValidatePlainTime(JsValue thisObject)
    {
        if (thisObject is JsPlainTime plainTime)
            return plainTime;
        Throw.TypeError(_realm, "Value is not a Temporal.PlainTime");
        return null!;
    }

    private JsNumber GetHour(JsValue thisObject, JsCallArguments arguments) => JsNumber.Create(ValidatePlainTime(thisObject).IsoTime.Hour);
    private JsNumber GetMinute(JsValue thisObject, JsCallArguments arguments) => JsNumber.Create(ValidatePlainTime(thisObject).IsoTime.Minute);
    private JsNumber GetSecond(JsValue thisObject, JsCallArguments arguments) => JsNumber.Create(ValidatePlainTime(thisObject).IsoTime.Second);
    private JsNumber GetMillisecond(JsValue thisObject, JsCallArguments arguments) => JsNumber.Create(ValidatePlainTime(thisObject).IsoTime.Millisecond);
    private JsNumber GetMicrosecond(JsValue thisObject, JsCallArguments arguments) => JsNumber.Create(ValidatePlainTime(thisObject).IsoTime.Microsecond);
    private JsNumber GetNanosecond(JsValue thisObject, JsCallArguments arguments) => JsNumber.Create(ValidatePlainTime(thisObject).IsoTime.Nanosecond);
}
