#if NET8_0_OR_GREATER
using Jint.Native;
using Jint.Native.Object;
using Jint.Runtime;

namespace Jint.WebApi.Streams;

/// <summary>
/// The state a <c>ReadableStream</c> is in: the specification's <c>[[state]]</c> slot, whose values are the
/// strings "<c>readable</c>", "<c>closed</c>" and "<c>errored</c>".
/// <para>
/// https://streams.spec.whatwg.org/#rs-internal-slots
/// </para>
/// </summary>
internal enum ReadableStreamState
{
    Readable,
    Closed,
    Errored,
}

/// <summary>
/// A <c>ReadableStream</c> instance.
/// <para>
/// https://streams.spec.whatwg.org/#rs-class
/// </para>
/// </summary>
/// <remarks>
/// <para>
/// Every WebIDL attribute of the interface lives in CLR state here rather than as an own property, and
/// <see cref="ReadableStreamPrototype"/> reads it through a brand check — so
/// <c>Object.getOwnPropertyNames(new ReadableStream())</c> is empty, exactly as in a browser.
/// </para>
/// <para>
/// The <c>[[controller]]</c> slot is a <see cref="JsReadableStreamDefaultController"/> or, for a stream
/// constructed with <c>type: "bytes"</c>, a <see cref="JsReadableByteStreamController"/>. <c>[[Detached]]</c>
/// has no counterpart, because a stream can only become detached by being transferred through
/// <c>postMessage()</c>, and there is nothing to transfer it to.
/// </para>
/// </remarks>
internal sealed class JsReadableStream : ObjectInstance
{
    /// <summary>
    /// https://streams.spec.whatwg.org/#initialize-readable-stream — the whole of it: the state starts
    /// "<c>readable</c>", the reader and stored error start undefined, and the stream starts undisturbed.
    /// </summary>
    internal JsReadableStream(Engine engine, Realm realm) : base(engine, ObjectClass.Object)
    {
        Realm = realm;
    }

    /// <summary>
    /// The realm the stream was created in, which is the realm every promise it hands out belongs to.
    /// </summary>
    internal Realm Realm { get; }

    /// <summary>https://streams.spec.whatwg.org/#readablestream-state</summary>
    internal ReadableStreamState State { get; set; } = ReadableStreamState.Readable;

    /// <summary>https://streams.spec.whatwg.org/#readablestream-storederror</summary>
    internal JsValue StoredError { get; set; } = Undefined;

    /// <summary>
    /// https://streams.spec.whatwg.org/#readablestream-disturbed — set once the stream has been read from
    /// or canceled. Nothing inside this specification reads it; it exists for the specifications built on
    /// top, and is maintained so that they can be.
    /// </summary>
    internal bool Disturbed { get; set; }

    /// <summary>
    /// https://streams.spec.whatwg.org/#readablestream-reader — the reader the stream is locked to, or
    /// <see langword="null"/> when it is not locked.
    /// </summary>
    internal JsReadableStreamReader? Reader { get; set; }

    /// <summary>
    /// https://streams.spec.whatwg.org/#readablestream-controller. Assigned by
    /// <see cref="ReadableStreamOperations.SetUpDefaultController"/> or
    /// <see cref="ReadableByteStreamControllerOperations.SetUp"/> before the stream can be reached by
    /// anything, which is what makes the non-nullable declaration honest.
    /// </summary>
    internal JsReadableStreamController Controller { get; set; } = null!;

    /// <summary>
    /// The controller of a stream the engine built itself through
    /// <see cref="ReadableStreamOperations.CreateReadableStream"/> — a tee branch, a transform stream's
    /// readable side, <c>ReadableStream.from</c>. Those are default controllers by construction, which is
    /// what makes the cast safe; a stream reached from script goes through the polymorphic members on
    /// <see cref="JsReadableStreamController"/> instead.
    /// </summary>
    internal JsReadableStreamDefaultController DefaultController => (JsReadableStreamDefaultController) Controller;

    /// <summary>
    /// The controller of a stream the engine built itself through
    /// <see cref="ReadableStreamOperations.CreateReadableByteStream"/> — a byte tee's branch, a body's
    /// stream, a blob's stream. Those are byte controllers by construction, which is what makes the cast
    /// safe; the same rule as <see cref="DefaultController"/>, on the other side of the pair.
    /// </summary>
    internal JsReadableByteStreamController ByteController => (JsReadableByteStreamController) Controller;
}
#endif
