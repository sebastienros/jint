#if NET8_0_OR_GREATER
using Jint.Native;
using Jint.Native.Function;
using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Descriptors;
using Jint.WebApi.Streams;

namespace Jint.WebApi.Compression;

/// <summary>
/// The <c>DecompressionStream</c> interface object.
/// <para>
/// https://compression.spec.whatwg.org/#decompression-stream
/// </para>
/// </summary>
internal sealed class DecompressionStreamConstructor : Constructor
{
    private static readonly JsString _functionName = new("DecompressionStream");

    internal DecompressionStreamConstructor(
        Engine engine,
        Realm realm,
        FunctionPrototype functionPrototype,
        ObjectPrototype objectPrototype)
        : base(engine, realm, _functionName)
    {
        _prototype = functionPrototype;
        PrototypeObject = new DecompressionStreamPrototype(engine, realm, this, objectPrototype);
        _length = new PropertyDescriptor(JsNumber.PositiveOne, PropertyFlag.Configurable);
        _prototypeDescriptor = new PropertyDescriptor(PrototypeObject, PropertyFlag.AllForbidden);
    }

    internal DecompressionStreamPrototype PrototypeObject { get; }

    /// <summary>
    /// https://compression.spec.whatwg.org/#dom-decompressionstream-decompressionstream
    /// </summary>
    public override ObjectInstance Construct(JsCallArguments arguments, JsValue newTarget)
    {
        var format = CompressionFormats.ReadFormat(_realm, arguments, "DecompressionStream");

        var stream = OrdinaryCreateFromConstructor(
            newTarget,
            static intrinsics => intrinsics.DecompressionStream.PrototypeObject,
            static (Engine engine, Realm realm, CompressionFormat state) => new JsDecompressionStream(engine, realm, state),
            format);

        // The cancel algorithm is not one the standard gives, and it is unobservable from script: it exists
        // so that a stream nobody finishes releases its native decompression context without waiting for a
        // finalizer.
        stream.Transform = TransformStreamOperations.SetUp(
            _engine,
            _realm,
            stream.DecompressAndEnqueue,
            stream.DecompressFlushAndEnqueue,
            stream.Dispose);

        return stream;
    }
}
#endif
