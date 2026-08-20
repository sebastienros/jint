#if NET8_0_OR_GREATER
using Jint.Native;
using Jint.Native.Function;
using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Descriptors;

namespace Jint.WebApi.Streams;

/// <summary>
/// The <c>WritableStream</c> interface object.
/// <para>
/// https://streams.spec.whatwg.org/#ws-class
/// </para>
/// </summary>
internal sealed class WritableStreamConstructor : Constructor
{
    private static readonly JsString _functionName = new("WritableStream");

    internal WritableStreamConstructor(
        Engine engine,
        Realm realm,
        FunctionPrototype functionPrototype,
        ObjectPrototype objectPrototype)
        : base(engine, realm, _functionName)
    {
        _prototype = functionPrototype;
        PrototypeObject = new WritableStreamPrototype(engine, realm, this, objectPrototype);
        _length = new PropertyDescriptor(JsNumber.PositiveZero, PropertyFlag.Configurable);
        _prototypeDescriptor = new PropertyDescriptor(PrototypeObject, PropertyFlag.AllForbidden);
    }

    internal WritableStreamPrototype PrototypeObject { get; }

    /// <summary>
    /// https://streams.spec.whatwg.org/#ws-constructor
    /// </summary>
    public override ObjectInstance Construct(JsCallArguments arguments, JsValue newTarget)
    {
        if (newTarget.IsUndefined())
        {
            Throw.TypeError(_realm, $"Constructor {GetOwnFunctionNameForMessage()} requires 'new'");
        }

        var underlyingSink = arguments.At(0);
        if (!underlyingSink.IsUndefined() && underlyingSink is not ObjectInstance)
        {
            Throw.TypeError(_realm, "Failed to construct 'WritableStream': the underlying sink is not an object");
        }

        var strategy = StreamDictionaries.ReadQueuingStrategy(_realm, arguments.At(1), "The queuing strategy");

        var stream = OrdinaryCreateFromConstructor(
            newTarget,
            static intrinsics => intrinsics.WritableStream.PrototypeObject,
            static (Engine engine, Realm realm, object? _) => new JsWritableStream(engine, realm));

        var sink = StreamDictionaries.ReadUnderlyingSink(_realm, underlyingSink);

        if (sink.TypeExists)
        {
            // The specification reserves the name so that byte-oriented writable streams can be added later
            // without a compatibility problem; any value at all is a RangeError today.
            Throw.RangeError(_realm, "Failed to construct 'WritableStream': the underlying sink's type is invalid");
        }

        var sizeAlgorithm = StreamDictionaries.ExtractSizeAlgorithm(in strategy);
        var highWaterMark = StreamDictionaries.ExtractHighWaterMark(_realm, in strategy, 1);

        WritableStreamDefaultControllerOperations.SetUpFromUnderlyingSink(
            stream, underlyingSink, in sink, highWaterMark, sizeAlgorithm);

        return stream;
    }
}
#endif
