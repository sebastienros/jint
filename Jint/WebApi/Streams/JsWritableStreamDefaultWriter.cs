#if NET8_0_OR_GREATER
using Jint.Native;
using Jint.Native.Object;
using Jint.Native.Promise;
using Jint.Runtime;

namespace Jint.WebApi.Streams;

/// <summary>
/// A <c>WritableStreamDefaultWriter</c> instance.
/// <para>
/// https://streams.spec.whatwg.org/#default-writer-class
/// </para>
/// </summary>
/// <remarks>
/// Both promises are held as capabilities rather than as bare promises: the specification replaces either
/// with a freshly settled one when the one it holds has already settled — "ensure the ready promise is
/// rejected" is a different operation from "reject the ready promise" — and <c>ready</c> is additionally
/// replaced with a brand new pending promise every time backpressure re-appears, which is the whole of how
/// <c>await writer.ready</c> works.
/// </remarks>
internal sealed class JsWritableStreamDefaultWriter : ObjectInstance
{
    internal JsWritableStreamDefaultWriter(Engine engine, Realm realm) : base(engine, ObjectClass.Object)
    {
        Realm = realm;
    }

    /// <summary>The realm the writer was created in, which owns its promises.</summary>
    internal Realm Realm { get; }

    /// <summary>
    /// https://streams.spec.whatwg.org/#writablestreamdefaultwriter-stream — the stream the writer is
    /// active for, or <see langword="null"/> once its lock has been released.
    /// </summary>
    internal JsWritableStream? Stream { get; set; }

    /// <summary>https://streams.spec.whatwg.org/#writablestreamdefaultwriter-closedpromise</summary>
    internal PromiseCapability ClosedCapability { get; set; } = null!;

    /// <summary>https://streams.spec.whatwg.org/#writablestreamdefaultwriter-readypromise</summary>
    internal PromiseCapability ReadyCapability { get; set; } = null!;

    /// <summary>The promise the <c>closed</c> getter answers with.</summary>
    internal JsPromise ClosedPromise => StreamPromises.PromiseOf(ClosedCapability);

    /// <summary>The promise the <c>ready</c> getter answers with.</summary>
    internal JsPromise ReadyPromise => StreamPromises.PromiseOf(ReadyCapability);
}
#endif
