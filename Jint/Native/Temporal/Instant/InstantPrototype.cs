using Jint.Native.Object;
using Jint.Native.Symbol;
using Jint.Runtime;
using Jint.Runtime.Descriptors;
using Jint.Runtime.Interop;

namespace Jint.Native.Temporal;

/// <summary>
/// https://tc39.es/proposal-temporal/#sec-properties-of-the-temporal-instant-prototype-object
/// </summary>
internal sealed class InstantPrototype : Prototype
{
    private readonly InstantConstructor _constructor;

    internal InstantPrototype(
        Engine engine,
        Realm realm,
        InstantConstructor constructor,
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

        DefineAccessor("epochMilliseconds", GetEpochMilliseconds);
        DefineAccessor("epochNanoseconds", GetEpochNanoseconds);

        var symbols = new SymbolDictionary(1)
        {
            [GlobalSymbolRegistry.ToStringTag] = new("Temporal.Instant", PropertyFlag.Configurable)
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

    private JsInstant ValidateInstant(JsValue thisObject)
    {
        if (thisObject is JsInstant instant)
            return instant;
        Throw.TypeError(_realm, "Value is not a Temporal.Instant");
        return null!;
    }

    private JsNumber GetEpochMilliseconds(JsValue thisObject, JsCallArguments arguments)
    {
        var instant = ValidateInstant(thisObject);
        return JsNumber.Create((double) (instant.EpochNanoseconds / 1_000_000));
    }

    private JsBigInt GetEpochNanoseconds(JsValue thisObject, JsCallArguments arguments)
    {
        var instant = ValidateInstant(thisObject);
        return JsBigInt.Create(instant.EpochNanoseconds);
    }
}
