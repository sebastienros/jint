#if NET8_0_OR_GREATER
using Jint.Native;
using Jint.Native.Object;
using Jint.Runtime;

namespace Jint.WebApi.Streams;

/// <summary>
/// The controller of a <c>ReadableStream</c>: either a <see cref="JsReadableStreamDefaultController"/> or a
/// <see cref="JsReadableByteStreamController"/>.
/// <para>
/// https://streams.spec.whatwg.org/#rs-abstract-ops-used-by-controllers
/// </para>
/// </summary>
/// <remarks>
/// The three abstract members are the standard's own polymorphic internal methods: "the readable stream
/// implementation will polymorphically call to either these, or to their counterparts for default
/// controllers". Keeping them as virtual methods rather than as a type test at every call site is what lets
/// the stream algorithms in <see cref="ReadableStreamOperations"/> stay written the way the specification
/// writes them.
/// </remarks>
internal abstract class JsReadableStreamController : ObjectInstance
{
    private protected JsReadableStreamController(Engine engine, Realm realm) : base(engine, ObjectClass.Object)
    {
        Realm = realm;
    }

    /// <summary>The realm the controller was created in.</summary>
    internal Realm Realm { get; }

    /// <summary>The stream this controller controls.</summary>
    internal JsReadableStream Stream { get; set; } = null!;

    /// <summary>
    /// https://streams.spec.whatwg.org/#rs-controller-private-pull — the steps a default reader's
    /// <c>read()</c> runs once the stream is known to be readable.
    /// </summary>
    internal abstract void PullSteps(ReadRequest readRequest);

    /// <summary>
    /// https://streams.spec.whatwg.org/#rs-controller-private-cancel
    /// </summary>
    internal abstract JsPromise CancelSteps(JsValue reason);

    /// <summary>
    /// https://streams.spec.whatwg.org/#abstract-opdef-readablestreamcontroller-releasesteps — what a
    /// controller has to do when its stream's reader releases the lock.
    /// </summary>
    internal abstract void ReleaseSteps();
}
#endif
