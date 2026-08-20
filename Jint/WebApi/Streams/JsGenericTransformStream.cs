#if NET8_0_OR_GREATER
using Jint.Native;
using Jint.Native.Object;
using Jint.Runtime;

namespace Jint.WebApi.Streams;

/// <summary>
/// The base of every platform object that includes the <c>GenericTransformStream</c> mixin — a transform
/// stream defined by another standard rather than constructed by a script.
/// <para>
/// https://streams.spec.whatwg.org/#generictransformstream
/// </para>
/// </summary>
/// <remarks>
/// <para>
/// Such an object is <b>not</b> a <c>TransformStream</c>: it owns one (its
/// <see cref="Transform"/>) and republishes that stream's two sides as its own <c>readable</c> and
/// <c>writable</c> attributes. The transform itself is never handed to script, which is why the mixin's
/// implementers do not share a prototype chain — each is its own interface, and each brand-checks for its
/// own type.
/// </para>
/// <para>
/// The transform is built by <see cref="TransformStreamOperations.SetUp"/>, so the algorithms behind it are
/// engine code rather than script callbacks. Everything else about it is an ordinary transform stream:
/// backpressure reaches the producer, the readable side buffers nothing by default, and every promise it
/// hands out is an ordinary engine promise settled from the job queue.
/// </para>
/// </remarks>
internal abstract class JsGenericTransformStream : ObjectInstance
{
    private protected JsGenericTransformStream(Engine engine, Realm realm) : base(engine, ObjectClass.Object)
    {
        Realm = realm;
    }

    /// <summary>The realm the object was created in, which its errors and result objects belong to.</summary>
    internal Realm Realm { get; }

    /// <summary>
    /// https://streams.spec.whatwg.org/#generictransformstream-transform — assigned by the constructor
    /// immediately after the instance exists, because the algorithms it is set up with need the instance.
    /// </summary>
    internal JsTransformStream Transform { get; set; } = null!;

    /// <summary>
    /// "Enqueue <c>chunk</c> into this's transform" —
    /// https://streams.spec.whatwg.org/#transformstream-enqueue.
    /// </summary>
    private protected void Enqueue(JsValue chunk) => TransformStreamOperations.Enqueue(Transform, chunk);
}
#endif
