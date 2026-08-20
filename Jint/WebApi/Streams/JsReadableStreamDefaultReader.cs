#if NET8_0_OR_GREATER
using Jint.Native;
using Jint.Native.Object;
using Jint.Native.Promise;
using Jint.Runtime;

namespace Jint.WebApi.Streams;

/// <summary>
/// A read request: the three algorithms a consumer supplies to react to the stream's internal queue filling
/// or its state changing.
/// <para>
/// https://streams.spec.whatwg.org/#read-request
/// </para>
/// </summary>
/// <remarks>
/// A class rather than three delegates because every consumer of a read request supplies all three, and the
/// specification's own consumers (a reader's <c>read()</c>, tee, piping, the async iterator) each keep state
/// across the three. Exactly one of the three runs per request.
/// </remarks>
internal abstract class ReadRequest
{
    /// <summary>Called when a chunk is available for reading.</summary>
    internal abstract void ChunkSteps(JsValue chunk);

    /// <summary>Called when no chunks are available because the stream is closed.</summary>
    internal abstract void CloseSteps();

    /// <summary>Called when no chunks are available because the stream is errored.</summary>
    internal abstract void ErrorSteps(JsValue error);
}

/// <summary>
/// A <c>ReadableStreamDefaultReader</c> instance.
/// <para>
/// https://streams.spec.whatwg.org/#default-reader-class
/// </para>
/// </summary>
/// <remarks>
/// Carries the <c>ReadableStreamGenericReader</c> mixin's <c>[[stream]]</c> and <c>[[closedPromise]]</c>
/// slots as well as its own <c>[[readRequests]]</c>. The closed promise is held as a capability rather than
/// as a bare promise because releasing a lock on a non-readable stream <i>replaces</i> it with a freshly
/// rejected one rather than rejecting the existing one — the specification distinguishes the two, and so
/// does this.
/// </remarks>
internal sealed class JsReadableStreamDefaultReader : ObjectInstance
{
    internal JsReadableStreamDefaultReader(Engine engine, Realm realm) : base(engine, ObjectClass.Object)
    {
        Realm = realm;
    }

    /// <summary>The realm the reader was created in, which owns its <c>closed</c> and <c>read()</c> promises.</summary>
    internal Realm Realm { get; }

    /// <summary>
    /// https://streams.spec.whatwg.org/#readablestreamgenericreader-stream — the stream the reader is
    /// active for, or <see langword="null"/> once its lock has been released.
    /// </summary>
    internal JsReadableStream? Stream { get; set; }

    /// <summary>https://streams.spec.whatwg.org/#readablestreamgenericreader-closedpromise</summary>
    internal PromiseCapability ClosedCapability { get; set; } = null!;

    /// <summary>https://streams.spec.whatwg.org/#readablestreamdefaultreader-readrequests</summary>
    internal Queue<ReadRequest> ReadRequests { get; } = new();

    /// <summary>The promise the <c>closed</c> getter answers with.</summary>
    internal JsPromise ClosedPromise => StreamPromises.PromiseOf(ClosedCapability);
}
#endif
