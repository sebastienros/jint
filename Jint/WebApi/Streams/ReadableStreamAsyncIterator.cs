#if NET8_0_OR_GREATER
using Jint.Native;
using Jint.Native.Object;
using Jint.Runtime;

namespace Jint.WebApi.Streams;

/// <summary>
/// The object <c>ReadableStream.prototype.values()</c> returns: WebIDL's "default asynchronous iterator
/// object" for the <c>async_iterable&lt;any&gt;</c> declaration on <c>ReadableStream</c>.
/// <para>
/// https://streams.spec.whatwg.org/#rs-asynciterator and
/// https://webidl.spec.whatwg.org/#js-default-asynchronous-iterator-object
/// </para>
/// </summary>
/// <remarks>
/// <para>
/// Iterating a stream locks it: the iterator holds a reader for its whole life, and gives it up only when
/// the iteration ends — which <c>for await…of</c> does through <c>return()</c> when the loop is left by
/// <c>break</c>, <c>return</c> or a thrown exception. By default that also <b>cancels</b> the stream;
/// <c>stream.values({ preventCancel: true })</c> releases the lock without cancelling, leaving the remaining
/// chunks for another consumer.
/// </para>
/// <para>
/// <see cref="OngoingPromise"/> is what serializes the calls: WebIDL requires a second <c>next()</c> to wait
/// for the first, so the read requests can never interleave and <c>for await…of</c> sees the chunks in
/// order however impatiently it is driven.
/// </para>
/// </remarks>
internal sealed class ReadableStreamAsyncIterator : ObjectInstance
{
    internal ReadableStreamAsyncIterator(Engine engine, Realm realm) : base(engine, ObjectClass.Object)
    {
        Realm = realm;
    }

    /// <summary>The realm the iterator was created in.</summary>
    internal Realm Realm { get; }

    /// <summary>https://streams.spec.whatwg.org/#readablestream-async-iterator-reader</summary>
    internal JsReadableStreamDefaultReader Reader { get; set; } = null!;

    /// <summary>https://streams.spec.whatwg.org/#readablestream-async-iterator-prevent-cancel</summary>
    internal bool PreventCancel { get; set; }

    /// <summary>
    /// https://webidl.spec.whatwg.org/#default-asynchronous-iterator-object-ongoing-promise — the promise a
    /// further <c>next()</c> or <c>return()</c> queues behind, or <see langword="null"/> when the iterator is
    /// idle.
    /// </summary>
    internal JsPromise? OngoingPromise { get; set; }

    /// <summary>
    /// https://webidl.spec.whatwg.org/#default-asynchronous-iterator-object-is-finished — set once the
    /// iterator has produced its last result, after which <c>next()</c> answers
    /// <c>{ value: undefined, done: true }</c> without touching the stream.
    /// </summary>
    internal bool IsFinished { get; set; }
}
#endif
