#if NET8_0_OR_GREATER
using Jint.Native;
using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Descriptors;
using Jint.Runtime.Interop;

namespace Jint.WebApi.Streams;

/// <summary>
/// <c>ByteLengthQueuingStrategy.prototype</c> — the interface prototype object.
/// <para>
/// https://streams.spec.whatwg.org/#blqs-prototype
/// </para>
/// </summary>
[JsObject(UseShape = true)]
internal sealed partial class ByteLengthQueuingStrategyPrototype : Prototype
{
    [JsProperty(Name = "constructor", Flags = PropertyFlag.NonEnumerable)]
    private readonly ByteLengthQueuingStrategyConstructor _constructor;

    [JsSymbol("ToStringTag", Flags = PropertyFlag.Configurable)]
    private static readonly JsString StrategyToStringTag = new("ByteLengthQueuingStrategy");

    internal ByteLengthQueuingStrategyPrototype(
        Engine engine,
        Realm realm,
        ByteLengthQueuingStrategyConstructor constructor,
        ObjectInstance objectPrototype) : base(engine, realm)
    {
        _prototype = objectPrototype;
        _constructor = constructor;
    }

    protected override void Initialize()
    {
        CreateProperties_Generated();
        CreateSymbols_Generated();
    }

    /// <summary>
    /// https://streams.spec.whatwg.org/#blqs-high-water-mark
    /// </summary>
    [JsAccessor("highWaterMark", Flags = PropertyFlag.Configurable | PropertyFlag.Enumerable)]
    private JsNumber HighWaterMarkGet(JsValue thisObject) => JsNumber.Create(Brand(thisObject).HighWaterMark);

    /// <summary>
    /// https://streams.spec.whatwg.org/#blqs-size — the realm's one byte-length size function, which
    /// measures a chunk by its <c>byteLength</c> property.
    /// </summary>
    [JsAccessor("size", Flags = PropertyFlag.Configurable | PropertyFlag.Enumerable)]
    private ClrFunction SizeGet(JsValue thisObject)
    {
        Brand(thisObject);
        return _constructor.SizeFunction;
    }

    private JsByteLengthQueuingStrategy Brand(JsValue thisObject)
    {
        if (thisObject is JsByteLengthQueuingStrategy strategy)
        {
            return strategy;
        }

        Throw.TypeError(_realm, "Illegal invocation: receiver is not a ByteLengthQueuingStrategy");
        return null!;
    }
}
#endif
