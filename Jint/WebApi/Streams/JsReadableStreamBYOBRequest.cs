#if NET8_0_OR_GREATER
using Jint.Native;
using Jint.Native.Object;
using Jint.Runtime;

namespace Jint.WebApi.Streams;

/// <summary>
/// A <c>ReadableStreamBYOBRequest</c> instance: the view a byte stream's underlying source is being asked to
/// write into, and the two ways of reporting that it has.
/// <para>
/// https://streams.spec.whatwg.org/#rs-byob-request-class
/// </para>
/// </summary>
/// <remarks>
/// Both slots are cleared together when the request is invalidated — which happens the moment the buffer
/// behind it moves, whether because it was responded to, because a chunk was enqueued past it, or because
/// the stream errored. A request script kept hold of therefore keeps working as an object while answering
/// <c>null</c> for <c>view</c> and refusing <c>respond()</c> with a <c>TypeError</c>, exactly as the
/// standard prescribes.
/// </remarks>
internal sealed class JsReadableStreamBYOBRequest : ObjectInstance
{
    internal JsReadableStreamBYOBRequest(Engine engine, Realm realm) : base(engine, ObjectClass.Object)
    {
        Realm = realm;
    }

    /// <summary>The realm the request was created in.</summary>
    internal Realm Realm { get; }

    /// <summary>
    /// https://streams.spec.whatwg.org/#readablestreambyobrequest-controller — <see langword="null"/> once
    /// the request has been invalidated, which is the specification's "undefined".
    /// </summary>
    internal JsReadableByteStreamController? Controller { get; set; }

    /// <summary>
    /// https://streams.spec.whatwg.org/#readablestreambyobrequest-view — the destination region, or
    /// <see langword="null"/> once the request has been invalidated.
    /// </summary>
    internal JsTypedArray? View { get; set; }
}
#endif
