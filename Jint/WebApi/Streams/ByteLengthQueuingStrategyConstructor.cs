#if NET8_0_OR_GREATER
using Jint.Native;
using Jint.Native.Function;
using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Descriptors;
using Jint.Runtime.Interop;

namespace Jint.WebApi.Streams;

/// <summary>
/// A <c>ByteLengthQueuingStrategy</c> instance: nothing but the high water mark it was constructed with.
/// <para>
/// https://streams.spec.whatwg.org/#blqs-internal-slots
/// </para>
/// </summary>
internal sealed class JsByteLengthQueuingStrategy : ObjectInstance
{
    internal JsByteLengthQueuingStrategy(Engine engine, double highWaterMark) : base(engine, ObjectClass.Object)
    {
        HighWaterMark = highWaterMark;
    }

    /// <summary>https://streams.spec.whatwg.org/#bytelengthqueuingstrategy-highwatermark</summary>
    internal double HighWaterMark { get; }
}

/// <summary>
/// The <c>ByteLengthQueuingStrategy</c> interface object.
/// <para>
/// https://streams.spec.whatwg.org/#blqs-class
/// </para>
/// </summary>
internal sealed class ByteLengthQueuingStrategyConstructor : Constructor
{
    private static readonly JsString _functionName = new("ByteLengthQueuingStrategy");
    private static readonly JsString _byteLength = new("byteLength");

    private ClrFunction? _sizeFunction;

    internal ByteLengthQueuingStrategyConstructor(
        Engine engine,
        Realm realm,
        FunctionPrototype functionPrototype,
        ObjectPrototype objectPrototype)
        : base(engine, realm, _functionName)
    {
        _prototype = functionPrototype;
        PrototypeObject = new ByteLengthQueuingStrategyPrototype(engine, realm, this, objectPrototype);
        _length = new PropertyDescriptor(JsNumber.PositiveOne, PropertyFlag.Configurable);
        _prototypeDescriptor = new PropertyDescriptor(PrototypeObject, PropertyFlag.AllForbidden);
    }

    internal ByteLengthQueuingStrategyPrototype PrototypeObject { get; }

    /// <summary>
    /// https://streams.spec.whatwg.org/#byte-length-queuing-strategy-size-function — one function per realm,
    /// shared by every instance. Its steps are exactly <c>GetV(chunk, "byteLength")</c>, so it raises a
    /// <c>TypeError</c> for <see langword="null"/> or <see langword="undefined"/>, answers
    /// <see langword="undefined"/> for anything without the property, and lets a throwing getter through.
    /// </summary>
    internal ClrFunction SizeFunction
    {
        get
        {
            if (_sizeFunction is not null)
            {
                return _sizeFunction;
            }

            var realm = _realm;
            _sizeFunction = new ClrFunction(
                _engine,
                realm,
                "size",
                (_, arguments) =>
                {
                    var chunk = arguments.At(0);
                    return TypeConverter.ToObject(realm, chunk).Get(_byteLength, chunk);
                },
                1,
                PropertyFlag.Configurable);

            return _sizeFunction;
        }
    }

    /// <summary>
    /// https://streams.spec.whatwg.org/#blqs-constructor
    /// </summary>
    public override ObjectInstance Construct(JsCallArguments arguments, JsValue newTarget)
    {
        if (newTarget.IsUndefined())
        {
            Throw.TypeError(_realm, $"Constructor {GetOwnFunctionNameForMessage()} requires 'new'");
        }

        var highWaterMark = StreamDictionaries.ReadQueuingStrategyInit(_realm, arguments.At(0), "ByteLengthQueuingStrategy");

        return OrdinaryCreateFromConstructor(
            newTarget,
            static intrinsics => intrinsics.ByteLengthQueuingStrategy.PrototypeObject,
            static (Engine engine, Realm _, double state) => new JsByteLengthQueuingStrategy(engine, state),
            highWaterMark);
    }
}
#endif
