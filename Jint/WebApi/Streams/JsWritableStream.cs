#if NET8_0_OR_GREATER
using Jint.Native;
using Jint.Native.Object;
using Jint.Native.Promise;
using Jint.Runtime;

namespace Jint.WebApi.Streams;

/// <summary>
/// The state a <c>WritableStream</c> is in: the specification's <c>[[state]]</c> slot, whose values are the
/// strings "<c>writable</c>", "<c>erroring</c>", "<c>closed</c>" and "<c>errored</c>".
/// <para>
/// https://streams.spec.whatwg.org/#ws-internal-slots
/// </para>
/// </summary>
/// <remarks>
/// "<c>erroring</c>" is the state between a failure being noticed and the in-flight sink operation
/// finishing; it exists so that an abort never interrupts a write or a close that the sink has already been
/// handed.
/// </remarks>
internal enum WritableStreamState
{
    Writable,
    Erroring,
    Closed,
    Errored,
}

/// <summary>
/// https://streams.spec.whatwg.org/#pending-abort-request — a request to abort a stream that cannot be
/// acted on yet because an operation is in flight.
/// </summary>
internal sealed class PendingAbortRequest
{
    internal PendingAbortRequest(PromiseCapability capability, JsValue reason, bool wasAlreadyErroring)
    {
        Capability = capability;
        Reason = reason;
        WasAlreadyErroring = wasAlreadyErroring;
    }

    /// <summary>https://streams.spec.whatwg.org/#pending-abort-request-promise</summary>
    internal PromiseCapability Capability { get; }

    /// <summary>https://streams.spec.whatwg.org/#pending-abort-request-reason</summary>
    internal JsValue Reason { get; }

    /// <summary>https://streams.spec.whatwg.org/#pending-abort-request-was-already-erroring</summary>
    internal bool WasAlreadyErroring { get; }
}

/// <summary>
/// A <c>WritableStream</c> instance.
/// <para>
/// https://streams.spec.whatwg.org/#ws-class
/// </para>
/// </summary>
/// <remarks>
/// The write requests are the promises <c>writer.write()</c> handed out and have not settled; one of them
/// moves into <see cref="InFlightWriteRequest"/> while the sink is processing it, which is what keeps an
/// error from rejecting a write the sink has already accepted.
/// </remarks>
internal sealed class JsWritableStream : ObjectInstance
{
    /// <summary>
    /// https://streams.spec.whatwg.org/#initialize-writable-stream — the whole of it: the state starts
    /// "<c>writable</c>", every request slot starts undefined and backpressure starts false.
    /// </summary>
    internal JsWritableStream(Engine engine, Realm realm) : base(engine, ObjectClass.Object)
    {
        Realm = realm;
    }

    /// <summary>The realm the stream was created in, which owns every promise it hands out.</summary>
    internal Realm Realm { get; }

    /// <summary>https://streams.spec.whatwg.org/#writablestream-state</summary>
    internal WritableStreamState State { get; set; } = WritableStreamState.Writable;

    /// <summary>
    /// https://streams.spec.whatwg.org/#writablestream-storederror — only meaningful while the state is
    /// "<c>erroring</c>" or "<c>errored</c>", and deliberately allowed to be undefined even then.
    /// </summary>
    internal JsValue StoredError { get; set; } = Undefined;

    /// <summary>https://streams.spec.whatwg.org/#writablestream-writer</summary>
    internal JsWritableStreamDefaultWriter? Writer { get; set; }

    /// <summary>
    /// https://streams.spec.whatwg.org/#writablestream-controller. Assigned by
    /// <c>SetUpWritableStreamDefaultController</c> before the stream can be reached by anything.
    /// </summary>
    internal JsWritableStreamDefaultController Controller { get; set; } = null!;

    /// <summary>https://streams.spec.whatwg.org/#writablestream-inflightwriterequest</summary>
    internal PromiseCapability? InFlightWriteRequest { get; set; }

    /// <summary>https://streams.spec.whatwg.org/#writablestream-closerequest</summary>
    internal PromiseCapability? CloseRequest { get; set; }

    /// <summary>https://streams.spec.whatwg.org/#writablestream-inflightcloserequest</summary>
    internal PromiseCapability? InFlightCloseRequest { get; set; }

    /// <summary>https://streams.spec.whatwg.org/#writablestream-pendingabortrequest</summary>
    internal PendingAbortRequest? PendingAbortRequest { get; set; }

    /// <summary>https://streams.spec.whatwg.org/#writablestream-writerequests</summary>
    internal Queue<PromiseCapability> WriteRequests { get; } = new();

    /// <summary>https://streams.spec.whatwg.org/#writablestream-backpressure</summary>
    internal bool Backpressure { get; set; }

    /// <inheritdoc cref="JsReadableStream.Detached" />
    internal bool Detached { get; set; }
}
#endif
