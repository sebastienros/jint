#if NET8_0_OR_GREATER
using Jint.Native;
using Jint.Native.Function;
using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Descriptors;
using Jint.WebApi.Streams;

namespace Jint.WebApi.Compression;

/// <summary>
/// The <c>CompressionStream</c> interface object.
/// <para>
/// https://compression.spec.whatwg.org/#compression-stream
/// </para>
/// </summary>
internal sealed class CompressionStreamConstructor : Constructor
{
    private static readonly JsString _functionName = new("CompressionStream");

    internal CompressionStreamConstructor(
        Engine engine,
        Realm realm,
        FunctionPrototype functionPrototype,
        ObjectPrototype objectPrototype)
        : base(engine, realm, _functionName)
    {
        _prototype = functionPrototype;
        PrototypeObject = new CompressionStreamPrototype(engine, realm, this, objectPrototype);
        _length = new PropertyDescriptor(JsNumber.PositiveOne, PropertyFlag.Configurable);
        _prototypeDescriptor = new PropertyDescriptor(PrototypeObject, PropertyFlag.AllForbidden);
    }

    internal CompressionStreamPrototype PrototypeObject { get; }

    /// <summary>
    /// https://compression.spec.whatwg.org/#dom-compressionstream-compressionstream
    /// </summary>
    public override ObjectInstance Construct(JsCallArguments arguments, JsValue newTarget)
    {
        var format = CompressionFormats.ReadFormat(_realm, arguments, "CompressionStream");

        var stream = OrdinaryCreateFromConstructor(
            newTarget,
            static intrinsics => intrinsics.CompressionStream.PrototypeObject,
            static (Engine engine, Realm realm, CompressionFormat state) => new JsCompressionStream(engine, realm, state),
            format);

        // The algorithms close over the instance, so the transform can only be set up once it exists. The
        // cancel algorithm is not one the standard gives, and it is unobservable from script: it exists so
        // that a stream nobody finishes releases its native compression context without waiting for a
        // finalizer.
        stream.Transform = TransformStreamOperations.SetUp(
            _engine,
            _realm,
            stream.CompressAndEnqueue,
            stream.CompressFlushAndEnqueue,
            stream.Dispose);

        return stream;
    }
}
#endif
