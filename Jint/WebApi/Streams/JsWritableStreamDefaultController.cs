#if NET8_0_OR_GREATER
using Jint.Native;
using Jint.Native.Object;
using Jint.Runtime;
using Jint.WebApi.Abort;

namespace Jint.WebApi.Streams;

/// <summary>
/// A <c>WritableStreamDefaultController</c> instance.
/// <para>
/// https://streams.spec.whatwg.org/#ws-default-controller-class
/// </para>
/// </summary>
/// <remarks>
/// <para>
/// The <c>[[abortController]]</c> slot is what backs the <c>signal</c> attribute: a sink's <c>write()</c>
/// can watch it to abandon a long-running write the moment the stream is aborted, without waiting for its
/// own promise to be listened to. It is an ordinary <see cref="JsAbortSignal"/>, so a sink sees exactly the
/// interface the DOM defines even on an engine that did not install the <c>AbortSignal</c> global — the
/// signal is handed to it, never looked up by name.
/// </para>
/// <para>
/// The signal is created lazily, on the first read of <c>controller.signal</c> or the first abort. Most
/// sinks never look at it, and a writable stream that nobody aborts then costs nothing for having the slot.
/// </para>
/// </remarks>
internal sealed class JsWritableStreamDefaultController : ObjectInstance
{
    private JsAbortSignal? _abortSignal;

    internal JsWritableStreamDefaultController(Engine engine, Realm realm) : base(engine, ObjectClass.Object)
    {
        Realm = realm;
    }

    /// <summary>The realm the controller was created in.</summary>
    internal Realm Realm { get; }

    /// <summary>https://streams.spec.whatwg.org/#writablestreamdefaultcontroller-stream</summary>
    internal JsWritableStream Stream { get; set; } = null!;

    /// <summary>
    /// https://streams.spec.whatwg.org/#writablestreamdefaultcontroller-queue and
    /// <c>[[queueTotalSize]]</c>. The queue holds chunks and, at its tail once close has been requested,
    /// the close sentinel.
    /// </summary>
    internal StreamQueue Queue { get; } = new();

    /// <summary>https://streams.spec.whatwg.org/#writablestreamdefaultcontroller-started</summary>
    internal bool Started { get; set; }

    /// <summary>https://streams.spec.whatwg.org/#writablestreamdefaultcontroller-strategyhwm</summary>
    internal double StrategyHighWaterMark { get; set; }

    /// <summary>https://streams.spec.whatwg.org/#writablestreamdefaultcontroller-strategysizealgorithm</summary>
    internal Func<JsValue, double>? StrategySizeAlgorithm { get; set; }

    /// <summary>https://streams.spec.whatwg.org/#writablestreamdefaultcontroller-writealgorithm</summary>
    internal Func<JsValue, JsPromise>? WriteAlgorithm { get; set; }

    /// <summary>https://streams.spec.whatwg.org/#writablestreamdefaultcontroller-closealgorithm</summary>
    internal Func<JsPromise>? CloseAlgorithm { get; set; }

    /// <summary>https://streams.spec.whatwg.org/#writablestreamdefaultcontroller-abortalgorithm</summary>
    internal Func<JsValue, JsPromise>? AbortAlgorithm { get; set; }

    /// <summary>
    /// The signal of the controller's <c>[[abortController]]</c>, created on first use.
    /// </summary>
    internal JsAbortSignal Signal => _abortSignal ??= Realm.Intrinsics.AbortSignal.CreateSignal();

    /// <summary>
    /// The <c>[[abortController]]</c>'s <c>abort()</c>, run by <c>WritableStreamAbort</c> before anything
    /// else so that a sink watching the signal stops as early as possible.
    /// </summary>
    /// <remarks>
    /// <c>abort(reason)</c> takes an optional <c>any</c>, so an omitted reason — which is what
    /// <c>writer.abort()</c> passes down — becomes a fresh <c>AbortError</c> <c>DOMException</c> rather than
    /// <see langword="undefined"/>, per https://dom.spec.whatwg.org/#abortsignal-signal-abort. Note that the
    /// signal's reason and the stream's stored error therefore differ in that case: the stream stores the
    /// undefined reason it was given.
    /// </remarks>
    internal void SignalAbort(JsValue reason)
        => Signal.SignalAbort(Realm.Intrinsics.AbortSignal.DefaultedReason(reason));
}
#endif
