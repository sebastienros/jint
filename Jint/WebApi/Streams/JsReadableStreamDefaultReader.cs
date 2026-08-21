#if NET8_0_OR_GREATER
using Jint.Native;
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
/// The <c>ReadableStreamGenericReader</c> mixin's <c>[[stream]]</c> and <c>[[closedPromise]]</c> slots live
/// on <see cref="JsReadableStreamReader"/>; this class adds only its own <c>[[readRequests]]</c>.
/// </remarks>
internal sealed class JsReadableStreamDefaultReader : JsReadableStreamReader
{
    internal JsReadableStreamDefaultReader(Engine engine, Realm realm) : base(engine, realm)
    {
    }

    /// <summary>https://streams.spec.whatwg.org/#readablestreamdefaultreader-readrequests</summary>
    internal Queue<ReadRequest> ReadRequests { get; } = new();
}
#endif
