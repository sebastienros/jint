#if NET8_0_OR_GREATER
using Jint.Native;
using Jint.Runtime;

namespace Jint.WebApi.Streams;

/// <summary>
/// A read-into request: the three algorithms a BYOB consumer supplies to react to its buffer being filled or
/// the stream's state changing.
/// <para>
/// https://streams.spec.whatwg.org/#read-into-request
/// </para>
/// </summary>
/// <remarks>
/// The close steps take a chunk where <see cref="ReadRequest.CloseSteps"/> takes nothing, so that the
/// backing memory can be returned to the caller: a BYOB <c>read(view)</c> against a closed stream fulfils
/// with a fresh, empty view onto the same memory. A <i>cancelled</i> stream discards the memory instead and
/// the close steps receive <see cref="JsValue.Undefined"/>, which is the whole reason for the parameter.
/// </remarks>
internal abstract class ReadIntoRequest
{
    /// <summary>Called when the buffer has been filled to at least its minimum.</summary>
    internal abstract void ChunkSteps(JsValue chunk);

    /// <summary>Called when no chunk is available because the stream is closed.</summary>
    internal abstract void CloseSteps(JsValue chunk);

    /// <summary>Called when no chunk is available because the stream is errored.</summary>
    internal abstract void ErrorSteps(JsValue error);
}

/// <summary>
/// A <c>ReadableStreamBYOBReader</c> instance.
/// <para>
/// https://streams.spec.whatwg.org/#byob-reader-class
/// </para>
/// </summary>
/// <remarks>
/// The <c>ReadableStreamGenericReader</c> mixin's <c>[[stream]]</c> and <c>[[closedPromise]]</c> slots live
/// on <see cref="JsReadableStreamReader"/>; this class adds only its own <c>[[readIntoRequests]]</c>. A BYOB
/// reader can only be acquired from a stream whose controller is a
/// <see cref="JsReadableByteStreamController"/>.
/// </remarks>
internal sealed class JsReadableStreamBYOBReader : JsReadableStreamReader
{
    internal JsReadableStreamBYOBReader(Engine engine, Realm realm) : base(engine, realm)
    {
    }

    /// <summary>https://streams.spec.whatwg.org/#readablestreambyobreader-readintorequests</summary>
    internal Queue<ReadIntoRequest> ReadIntoRequests { get; } = new();
}
#endif
