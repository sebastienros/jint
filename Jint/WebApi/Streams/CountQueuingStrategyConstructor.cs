#if NET8_0_OR_GREATER
using Jint.Native;
using Jint.Native.Function;
using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Descriptors;
using Jint.Runtime.Interop;

namespace Jint.WebApi.Streams;

/// <summary>
/// A <c>CountQueuingStrategy</c> instance: nothing but the high water mark it was constructed with.
/// <para>
/// https://streams.spec.whatwg.org/#cqs-internal-slots
/// </para>
/// </summary>
internal sealed class JsCountQueuingStrategy : ObjectInstance
{
    internal JsCountQueuingStrategy(Engine engine, double highWaterMark) : base(engine, ObjectClass.Object)
    {
        HighWaterMark = highWaterMark;
    }

    /// <summary>https://streams.spec.whatwg.org/#countqueuingstrategy-highwatermark</summary>
    internal double HighWaterMark { get; }
}

/// <summary>
/// The <c>CountQueuingStrategy</c> interface object.
/// <para>
/// https://streams.spec.whatwg.org/#cqs-class
/// </para>
/// </summary>
internal sealed class CountQueuingStrategyConstructor : Constructor
{
    private static readonly JsString _functionName = new("CountQueuingStrategy");

    private ClrFunction? _sizeFunction;

    internal CountQueuingStrategyConstructor(
        Engine engine,
        Realm realm,
        FunctionPrototype functionPrototype,
        ObjectPrototype objectPrototype)
        : base(engine, realm, _functionName)
    {
        _prototype = functionPrototype;
        PrototypeObject = new CountQueuingStrategyPrototype(engine, realm, this, objectPrototype);
        _length = new PropertyDescriptor(JsNumber.PositiveOne, PropertyFlag.Configurable);
        _prototypeDescriptor = new PropertyDescriptor(PrototypeObject, PropertyFlag.AllForbidden);
    }

    internal CountQueuingStrategyPrototype PrototypeObject { get; }

    /// <summary>
    /// https://streams.spec.whatwg.org/#count-queuing-strategy-size-function — one function per realm,
    /// shared by every instance, which is why <c>a.size === b.size</c> holds for two different strategies.
    /// It is a plain function rather than a method: it never looks at its <c>this</c>, so
    /// <c>const { size } = strategy</c> keeps working.
    /// </summary>
    internal ClrFunction SizeFunction =>
        _sizeFunction ??= new ClrFunction(_engine, _realm, "size", static (_, _) => JsNumber.PositiveOne, 0, PropertyFlag.Configurable);

    /// <summary>
    /// https://streams.spec.whatwg.org/#cqs-constructor. The high water mark is deliberately not validated
    /// here — a negative or NaN one is only rejected by the stream constructor it is handed to.
    /// </summary>
    public override ObjectInstance Construct(JsCallArguments arguments, JsValue newTarget)
    {
        if (newTarget.IsUndefined())
        {
            Throw.TypeError(_realm, $"Constructor {GetOwnFunctionNameForMessage()} requires 'new'");
        }

        var highWaterMark = StreamDictionaries.ReadQueuingStrategyInit(_realm, arguments.At(0), "CountQueuingStrategy");

        return OrdinaryCreateFromConstructor(
            newTarget,
            static intrinsics => intrinsics.CountQueuingStrategy.PrototypeObject,
            static (Engine engine, Realm _, double state) => new JsCountQueuingStrategy(engine, state),
            highWaterMark);
    }
}
#endif
