#if NET8_0_OR_GREATER
using Jint.Native;
using Jint.Native.Function;
using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Descriptors;

namespace Jint.WebApi.Streams;

/// <summary>
/// The <c>TransformStream</c> interface object.
/// <para>
/// https://streams.spec.whatwg.org/#ts-class
/// </para>
/// </summary>
internal sealed class TransformStreamConstructor : Constructor
{
    private static readonly JsString _functionName = new("TransformStream");

    internal TransformStreamConstructor(
        Engine engine,
        Realm realm,
        FunctionPrototype functionPrototype,
        ObjectPrototype objectPrototype)
        : base(engine, realm, _functionName)
    {
        _prototype = functionPrototype;
        PrototypeObject = new TransformStreamPrototype(engine, realm, this, objectPrototype);
        _length = new PropertyDescriptor(JsNumber.PositiveZero, PropertyFlag.Configurable);
        _prototypeDescriptor = new PropertyDescriptor(PrototypeObject, PropertyFlag.AllForbidden);
    }

    internal TransformStreamPrototype PrototypeObject { get; }

    /// <summary>
    /// https://streams.spec.whatwg.org/#ts-constructor
    /// </summary>
    /// <remarks>
    /// The default high water marks are asymmetric — 1 for the writable side, 0 for the readable side — so a
    /// transform stream buffers one chunk on the way in and none on the way out. That is what makes the
    /// readable side's consumer, rather than the writable side's producer, decide when a transform runs.
    /// </remarks>
    public override ObjectInstance Construct(JsCallArguments arguments, JsValue newTarget)
    {
        if (newTarget.IsUndefined())
        {
            Throw.TypeError(_realm, $"Constructor {GetOwnFunctionNameForMessage()} requires 'new'");
        }

        var transformer = arguments.At(0);
        if (!transformer.IsUndefined() && transformer is not ObjectInstance)
        {
            Throw.TypeError(_realm, "Failed to construct 'TransformStream': the transformer is not an object");
        }

        var writableStrategy = StreamDictionaries.ReadQueuingStrategy(_realm, arguments.At(1), "The writable queuing strategy");
        var readableStrategy = StreamDictionaries.ReadQueuingStrategy(_realm, arguments.At(2), "The readable queuing strategy");

        var stream = OrdinaryCreateFromConstructor(
            newTarget,
            static intrinsics => intrinsics.TransformStream.PrototypeObject,
            static (Engine engine, Realm realm, object? _) => new JsTransformStream(engine, realm));

        var dictionary = StreamDictionaries.ReadTransformer(_realm, transformer);

        if (dictionary.ReadableTypeExists)
        {
            Throw.RangeError(_realm, "Failed to construct 'TransformStream': the transformer's readableType is invalid");
        }

        if (dictionary.WritableTypeExists)
        {
            Throw.RangeError(_realm, "Failed to construct 'TransformStream': the transformer's writableType is invalid");
        }

        var readableHighWaterMark = StreamDictionaries.ExtractHighWaterMark(_realm, in readableStrategy, 0);
        var readableSizeAlgorithm = StreamDictionaries.ExtractSizeAlgorithm(in readableStrategy);
        var writableHighWaterMark = StreamDictionaries.ExtractHighWaterMark(_realm, in writableStrategy, 1);
        var writableSizeAlgorithm = StreamDictionaries.ExtractSizeAlgorithm(in writableStrategy);

        var startCapability = StreamPromises.NewPromise(_engine, _realm);

        TransformStreamOperations.Initialize(
            stream,
            StreamPromises.PromiseOf(startCapability),
            writableHighWaterMark,
            writableSizeAlgorithm,
            readableHighWaterMark,
            readableSizeAlgorithm);

        TransformStreamOperations.SetUpControllerFromTransformer(stream, transformer, in dictionary);

        // The transformer's start() has return type `any`: an exception it raises propagates out of the
        // constructor, while a promise it returns simply delays both sides.
        if (dictionary.Start is { } start)
        {
            startCapability.Resolve(start.Call(transformer, stream.Controller));
        }
        else
        {
            startCapability.Resolve(Undefined);
        }

        return stream;
    }
}
#endif
