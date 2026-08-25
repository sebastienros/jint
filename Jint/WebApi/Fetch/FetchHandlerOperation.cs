#if NET8_0_OR_GREATER
using System.Net.Http;
using System.Runtime.ExceptionServices;
using Jint.Native;
using Jint.Native.Promise;
using Jint.Runtime;
using Jint.Runtime.Descriptors;
using Jint.Runtime.Interop;
using Jint.WebApi.Abort;

namespace Jint.WebApi.Fetch;

/// <summary>
/// An inbound request being handled by script, handed back by
/// <c>Engine.WebApi.InvokeFetchHandler</c>. The engine makes progress on it only when it is given turns, so
/// a host drives it by calling <c>engine.Tasks.ProcessTasks()</c> and watching <see cref="IsCompleted"/>.
/// Requires .NET 8 or higher.
/// </summary>
/// <remarks>
/// <para>
/// This is the same shape <c>Engine.Modules.StartImport</c> hands back, for the same reason: a host with a
/// thread it must not block — a game loop, a UI thread, a request pipeline that owns its own scheduling —
/// needs every turn of the engine to run where it decided, not on whichever thread a continuation happened to
/// resume on. A host that is content to <c>await</c> wants
/// <c>Engine.WebApi.InvokeFetchHandlerAsync</c> instead.
/// </para>
/// <para>
/// A handler that answers a <c>Response</c> synchronously produces an operation that is already
/// <see cref="IsCompleted"/> when this is returned; one that answers a promise needs at least one turn.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// var operation = engine.WebApi.InvokeFetchHandler(request);
/// while (!operation.IsCompleted)
/// {
///     engine.Tasks.ProcessTasks();
/// }
///
/// // Throws whatever the handler failed with — the host decides what that means on the wire.
/// using var response = operation.GetResult();
/// </code>
/// </example>
public sealed class FetchHandlerOperation
{
    private readonly Engine _engine;

    /// <summary>
    /// The evaluation cycle the invocation was started in. Once the engine has moved past it, no turn of the
    /// event loop can finish this operation; see <see cref="ObserveAbandonment"/>.
    /// </summary>
    private readonly int _generation;

    private bool _completed;
    private HttpResponseMessage? _response;
    private ExceptionDispatchInfo? _failure;

    /// <summary>
    /// The link from the host token this invocation was given to the <c>Request</c>'s signal, when there is
    /// one. Held so the registration can be released the moment the invocation ends — see
    /// <see cref="ReleaseHostAbortBridge"/>.
    /// </summary>
    private HostAbortSignalBridge? _hostAbortBridge;

    internal FetchHandlerOperation(Engine engine)
    {
        _engine = engine;
        _generation = engine.EventLoopGeneration;
    }

    /// <summary>
    /// Whether the handler has finished, successfully or not. It becomes true during a turn of the event loop
    /// unless the handler answered synchronously, so it is only worth re-reading after the engine has been
    /// given one.
    /// </summary>
    /// <remarks>
    /// There is one way for an invocation to end without a turn:
    /// <c>Engine.Advanced.RestoreGlobalSnapshot</c> ends the evaluation cycle it was started in, and a promise
    /// fenced off that way can never settle into the engine again. Such an operation is reported here as
    /// completed and <see cref="IsFaulted"/>, so a host polling this cannot poll forever.
    /// </remarks>
    public bool IsCompleted
    {
        get
        {
            ObserveAbandonment();
            return _completed;
        }
    }

    /// <summary>Whether the handler finished by failing.</summary>
    public bool IsFaulted
    {
        get
        {
            ObserveAbandonment();
            return _failure is not null;
        }
    }

    /// <summary>
    /// The response the handler produced once it has succeeded, otherwise <see langword="null"/>. The host
    /// owns it and is responsible for disposing it.
    /// </summary>
    public HttpResponseMessage? Response
    {
        get
        {
            ObserveAbandonment();
            return _response;
        }
    }

    /// <summary>
    /// What the invocation failed with once it has failed, otherwise <see langword="null"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>A handler failure is never turned into a status code here.</b> What a failing handler should send —
    /// a 500, a rendered error page, a retry — is a policy question only the host can answer, so the failure
    /// arrives as the exception it was and the host maps it.
    /// </para>
    /// <para>
    /// The exception says which kind of failure it was. A handler that threw gives a
    /// <see cref="JavaScriptException"/>, whose <see cref="JavaScriptException.Error"/> is the thrown value; a
    /// handler whose promise rejected gives a <see cref="PromiseRejectedException"/>, whose
    /// <see cref="PromiseRejectedException.RejectedValue"/> is the rejection reason — which is what an
    /// <c>async</c> handler's <c>throw</c> becomes. An execution constraint that fired gives its own exception
    /// (<c>StatementsCountOverflowException</c>, <c>TimeoutException</c>, …), and a handler that answered with
    /// something that is not a <c>Response</c> gives an <see cref="InvalidOperationException"/>. All of Jint's
    /// own exceptions derive from <see cref="JintException"/>, so that is the one type a host has to catch to
    /// tell "the script failed" from "the host called this wrongly".
    /// </para>
    /// </remarks>
    public Exception? Error
    {
        get
        {
            ObserveAbandonment();
            return _failure?.SourceException;
        }
    }

    /// <summary>
    /// The response the handler produced, or the failure it produced, rethrown with its original stack trace.
    /// </summary>
    /// <exception cref="InvalidOperationException">The handler has not finished yet.</exception>
    public HttpResponseMessage GetResult()
    {
        if (!IsCompleted)
        {
            Throw.InvalidOperationException("The fetch handler has not completed. Give the engine turns with engine.Tasks.ProcessTasks() until IsCompleted is true, or await Engine.WebApi.InvokeFetchHandlerAsync instead.");
        }

        _failure?.Throw();
        return _response!;
    }

    /// <summary>
    /// Wires the operation to whatever the handler returned: a <c>Response</c> completes it here and now, a
    /// promise completes it on the turn it settles on.
    /// </summary>
    /// <remarks>
    /// Only a native promise is awaited. A thenable that is not one is refused as "not a Response" rather than
    /// adopted, because adopting it would make the synchronous answer — the common case, and the one that
    /// needs no pump at all — asynchronous for everybody.
    /// </remarks>
    internal void Settle(JsValue result)
    {
        if (result is not JsPromise promise)
        {
            CompleteFrom(result);
            return;
        }

        // Reactions rather than polling the promise's state, so that the operation is also what marks the
        // rejection handled and a failing handler does not read as an unhandled promise rejection to a host
        // watching for those.
        var onFulfilled = new ClrFunction(_engine, "", (_, args) =>
        {
            CompleteFrom(args.At(0));
            return JsValue.Undefined;
        }, 1, PropertyFlag.Configurable);

        var onRejected = new ClrFunction(_engine, "", (_, args) =>
        {
            Fail(new PromiseRejectedException(args.At(0)));
            return JsValue.Undefined;
        }, 1, PropertyFlag.Configurable);

        PromiseOperations.PerformPromiseThen(_engine, promise, onFulfilled, onRejected, resultCapability: null);
    }

    /// <summary>
    /// Converts on the engine's thread, inside whichever turn produced the value, and keeps a conversion
    /// failure inside the operation: escaping here would erupt out of whatever is pumping the engine, leaving
    /// the host polling an operation that can never complete.
    /// </summary>
    private void CompleteFrom(JsValue value)
    {
        try
        {
            Complete(FetchHandlerHosting.CreateResponse(value));
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            Fail(ex);
        }
    }

    /// <summary>
    /// Records the host token registration behind this invocation's <c>request.signal</c>, so that it is
    /// released when the invocation ends however it ends.
    /// </summary>
    /// <remarks>
    /// A <see langword="null"/> bridge is the ordinary case — a token that can never be cancelled registers
    /// nothing, and so does one that was already cancelled. An operation that has somehow completed before
    /// this is called releases straight away rather than keeping a registration nothing would ever come back
    /// for.
    /// </remarks>
    internal void AttachHostAbortBridge(HostAbortSignalBridge? bridge)
    {
        if (bridge is null)
        {
            return;
        }

        if (_completed)
        {
            bridge.Release();
            return;
        }

        _hostAbortBridge = bridge;
    }

    internal void Complete(HttpResponseMessage response)
    {
        if (_completed)
        {
            response.Dispose();
            return;
        }

        _completed = true;
        _response = response;
        ReleaseHostAbortBridge();
    }

    internal void Fail(Exception error)
    {
        if (_completed)
        {
            return;
        }

        _completed = true;
        _failure = ExceptionDispatchInfo.Capture(error);
        ReleaseHostAbortBridge();
    }

    /// <summary>
    /// Gives the host's token registration back the moment the invocation is over — whether it was answered,
    /// failed, or fenced off by a restore.
    /// </summary>
    /// <remarks>
    /// This is the whole of the lifetime contract: a request's token outlives no invocation, so a pooled
    /// engine serving one request after another from a long-lived host token — an application lifetime's, or
    /// a per-request token from a source the host reuses — accumulates no registrations and is retained by
    /// none of them. It deliberately does not chase an abort that is already on the event loop; see
    /// <see cref="HostAbortSignalBridge.Release"/>.
    /// </remarks>
    private void ReleaseHostAbortBridge()
    {
        var bridge = _hostAbortBridge;
        _hostAbortBridge = null;
        bridge?.Release();
    }

    /// <summary>
    /// Fails an invocation the engine has fenced off. <c>Engine.Advanced.RestoreGlobalSnapshot</c> ends the
    /// evaluation cycle, and every job registered in it is discarded at dequeue rather than run — so the
    /// reaction that would complete this operation is exactly the one that can no longer fire. Nothing pushes
    /// that news: the documented contract is that the host polls, and deriving the abandonment from the
    /// engine's generation on read is what keeps that poll from being a poll forever.
    /// </summary>
    private void ObserveAbandonment()
    {
        if (_completed || _engine.EventLoopGeneration == _generation)
        {
            return;
        }

        Fail(new InvalidOperationException("The fetch handler invocation was abandoned: Engine.Advanced.RestoreGlobalSnapshot ended the evaluation cycle it was started in, so nothing it is waiting for can settle into this engine any more. Invoke the handler again on the restored engine."));
    }
}
#endif
