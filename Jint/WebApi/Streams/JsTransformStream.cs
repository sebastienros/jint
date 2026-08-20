#if NET8_0_OR_GREATER
using Jint.Native;
using Jint.Native.Object;
using Jint.Native.Promise;
using Jint.Runtime;

namespace Jint.WebApi.Streams;

/// <summary>
/// A <c>TransformStream</c> instance.
/// <para>
/// https://streams.spec.whatwg.org/#ts-class
/// </para>
/// </summary>
/// <remarks>
/// A transform stream owns nothing but a pair of streams and a backpressure latch: the writable side's sink
/// runs the transformer, and the readable side's source does nothing but release that latch. Backpressure
/// from the readable side therefore reaches the producer writing into the writable side, which is the whole
/// point of the type.
/// </remarks>
internal sealed class JsTransformStream : ObjectInstance
{
    internal JsTransformStream(Engine engine, Realm realm) : base(engine, ObjectClass.Object)
    {
        Realm = realm;
    }

    /// <summary>The realm the stream was created in.</summary>
    internal Realm Realm { get; }

    /// <summary>https://streams.spec.whatwg.org/#transformstream-readable</summary>
    internal JsReadableStream Readable { get; set; } = null!;

    /// <summary>https://streams.spec.whatwg.org/#transformstream-writable</summary>
    internal JsWritableStream Writable { get; set; } = null!;

    /// <summary>https://streams.spec.whatwg.org/#transformstream-controller</summary>
    internal JsTransformStreamDefaultController Controller { get; set; } = null!;

    /// <summary>
    /// https://streams.spec.whatwg.org/#transformstream-backpressure. The specification starts it as
    /// undefined purely so that <c>TransformStreamSetBackpressure</c>'s "assert it is changing" holds on the
    /// first call; a nullable boolean says the same thing without needing the assertion.
    /// </summary>
    internal bool? Backpressure { get; set; }

    /// <summary>
    /// https://streams.spec.whatwg.org/#transformstream-backpressurechangepromise — resolved whenever
    /// <see cref="Backpressure"/> changes, and replaced by a fresh pending promise each time. A write that
    /// arrives while there is backpressure waits on it.
    /// </summary>
    internal PromiseCapability? BackpressureChangeCapability { get; set; }
}
#endif
