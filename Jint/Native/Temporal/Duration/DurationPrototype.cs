using Jint.Native.Object;
using Jint.Native.Symbol;
using Jint.Runtime;
using Jint.Runtime.Descriptors;
using Jint.Runtime.Interop;

namespace Jint.Native.Temporal;

/// <summary>
/// https://tc39.es/proposal-temporal/#sec-properties-of-the-temporal-duration-prototype-object
/// </summary>
internal sealed class DurationPrototype : Prototype
{
    private readonly DurationConstructor _constructor;

    internal DurationPrototype(
        Engine engine,
        Realm realm,
        DurationConstructor constructor,
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

        // Getter properties
        DefineAccessor("years", GetYears);
        DefineAccessor("months", GetMonths);
        DefineAccessor("weeks", GetWeeks);
        DefineAccessor("days", GetDays);
        DefineAccessor("hours", GetHours);
        DefineAccessor("minutes", GetMinutes);
        DefineAccessor("seconds", GetSeconds);
        DefineAccessor("milliseconds", GetMilliseconds);
        DefineAccessor("microseconds", GetMicroseconds);
        DefineAccessor("nanoseconds", GetNanoseconds);
        DefineAccessor("sign", GetSign);
        DefineAccessor("blank", GetBlank);

        var symbols = new SymbolDictionary(1)
        {
            [GlobalSymbolRegistry.ToStringTag] = new("Temporal.Duration", PropertyFlag.Configurable)
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

    private JsDuration ValidateDuration(JsValue thisObject)
    {
        if (thisObject is JsDuration duration)
            return duration;
        Throw.TypeError(_realm, "Value is not a Temporal.Duration");
        return null!;
    }

    private JsNumber GetYears(JsValue thisObject, JsCallArguments arguments) => JsNumber.Create(ValidateDuration(thisObject).Years);
    private JsNumber GetMonths(JsValue thisObject, JsCallArguments arguments) => JsNumber.Create(ValidateDuration(thisObject).Months);
    private JsNumber GetWeeks(JsValue thisObject, JsCallArguments arguments) => JsNumber.Create(ValidateDuration(thisObject).Weeks);
    private JsNumber GetDays(JsValue thisObject, JsCallArguments arguments) => JsNumber.Create(ValidateDuration(thisObject).Days);
    private JsNumber GetHours(JsValue thisObject, JsCallArguments arguments) => JsNumber.Create(ValidateDuration(thisObject).Hours);
    private JsNumber GetMinutes(JsValue thisObject, JsCallArguments arguments) => JsNumber.Create(ValidateDuration(thisObject).Minutes);
    private JsNumber GetSeconds(JsValue thisObject, JsCallArguments arguments) => JsNumber.Create(ValidateDuration(thisObject).Seconds);
    private JsNumber GetMilliseconds(JsValue thisObject, JsCallArguments arguments) => JsNumber.Create(ValidateDuration(thisObject).Milliseconds);
    private JsNumber GetMicroseconds(JsValue thisObject, JsCallArguments arguments) => JsNumber.Create(ValidateDuration(thisObject).Microseconds);
    private JsNumber GetNanoseconds(JsValue thisObject, JsCallArguments arguments) => JsNumber.Create(ValidateDuration(thisObject).Nanoseconds);
    private JsNumber GetSign(JsValue thisObject, JsCallArguments arguments) => JsNumber.Create(ValidateDuration(thisObject).DurationRecord.Sign());
    private JsBoolean GetBlank(JsValue thisObject, JsCallArguments arguments) => ValidateDuration(thisObject).DurationRecord.IsZero() ? JsBoolean.True : JsBoolean.False;
}
