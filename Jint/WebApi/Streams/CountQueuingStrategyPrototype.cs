#if NET8_0_OR_GREATER
using Jint.Native;
using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Descriptors;
using Jint.Runtime.Interop;

namespace Jint.WebApi.Streams;

/// <summary>
/// <c>CountQueuingStrategy.prototype</c> — the interface prototype object.
/// <para>
/// https://streams.spec.whatwg.org/#cqs-prototype
/// </para>
/// </summary>
[JsObject(UseShape = true)]
internal sealed partial class CountQueuingStrategyPrototype : Prototype
{
    [JsProperty(Name = "constructor", Flags = PropertyFlag.NonEnumerable)]
    private readonly CountQueuingStrategyConstructor _constructor;

    [JsSymbol("ToStringTag", Flags = PropertyFlag.Configurable)]
    private static readonly JsString StrategyToStringTag = new("CountQueuingStrategy");

    internal CountQueuingStrategyPrototype(
        Engine engine,
        Realm realm,
        CountQueuingStrategyConstructor constructor,
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
    /// https://streams.spec.whatwg.org/#cqs-high-water-mark
    /// </summary>
    [JsAccessor("highWaterMark", Flags = PropertyFlag.Configurable | PropertyFlag.Enumerable)]
    private JsNumber HighWaterMarkGet(JsValue thisObject) => JsNumber.Create(Brand(thisObject).HighWaterMark);

    /// <summary>
    /// https://streams.spec.whatwg.org/#cqs-size — the realm's one count size function, which always
    /// answers 1 so that the queue's total size is a count of its chunks.
    /// </summary>
    [JsAccessor("size", Flags = PropertyFlag.Configurable | PropertyFlag.Enumerable)]
    private ClrFunction SizeGet(JsValue thisObject)
    {
        // The brand check is the attribute's, not the function's: the function it hands back is shared by
        // the whole realm and belongs to no instance.
        Brand(thisObject);
        return _constructor.SizeFunction;
    }

    private JsCountQueuingStrategy Brand(JsValue thisObject)
    {
        if (thisObject is JsCountQueuingStrategy strategy)
        {
            return strategy;
        }

        Throw.TypeError(_realm, "Illegal invocation: receiver is not a CountQueuingStrategy");
        return null!;
    }
}
#endif
