using System.Globalization;
using System.Text.Json;
using Jint.DevTools.Protocol;
using Jint.DevTools.Protocol.Runtime;
using Jint.DevTools.Session;
using Jint.Native;
using Jint.Runtime;
using Jint.Runtime.Interop;

namespace Jint.DevTools.Domains;

/// <summary>
/// Answers the <c>Runtime</c> commands one session needs before anything else works: the execution context,
/// the release of a target waiting for a debugger, and evaluation.
/// </summary>
/// <remarks>
/// <para>
/// Deliberately the smaller half of the domain. Remote-object handles, <c>getProperties</c>,
/// <c>callFunctionOn</c>, previews and the console events are the rest of it and arrive together, because
/// each of them needs the object table and half a table is worse than none: a client handed an
/// <c>objectId</c> nothing keeps alive is worse off than a client told the value by description.
/// </para>
/// <para>
/// Every member runs on the engine thread, brought there by <see cref="EngineDispatcher"/>. The one thing it
/// must never do is drain the event loop — that is what the <c>awaitPromise</c> path attaches reactions for
/// rather than waiting.
/// </para>
/// <para>
/// See <see href="https://chromedevtools.github.io/devtools-protocol/tot/Runtime/"/>.
/// </para>
/// </remarks>
internal sealed class RuntimeDomain : RuntimeDomainBase
{
    /// <summary>
    /// The one execution context an engine target has. Chrome numbers page contexts from 1 and so does this;
    /// there is no second realm to number, because a <c>ShadowRealm</c> is not something a client can address.
    /// </summary>
    private const int MainExecutionContextId = 1;

    private readonly EngineTarget _target;
    private int _exceptionId;

    internal RuntimeDomain(EngineTarget target)
    {
        _target = target;
    }

    /// <inheritdoc/>
    protected override async ValueTask<EmptyResult> EnableAsync(EmptyParameters parameters, CommandContext context)
    {
        await MarkEnabledAsync(context).ConfigureAwait(false);
        return EmptyResult.Instance;
    }

    /// <inheritdoc/>
    protected override async ValueTask<EmptyResult> DisableAsync(EmptyParameters parameters, CommandContext context)
    {
        await MarkDisabledAsync(context).ConfigureAwait(false);
        return EmptyResult.Instance;
    }

    /// <summary>
    /// Announces the one execution context, which is what a client waits for before it evaluates anything.
    /// </summary>
    /// <remarks>
    /// Replayed on every <c>enable</c> rather than raised once when the context appeared: the context
    /// outlives every session, so a client attaching later would otherwise never hear of it. That is V8's
    /// behaviour too.
    /// </remarks>
    protected override ValueTask OnEnabledAsync(CommandContext context)
    {
        var created = new ExecutionContextCreatedEvent
        {
            Context = new ExecutionContextDescription
            {
                Id = MainExecutionContextId,

                // An engine target has no document, so it has no origin and no name to give. Chrome sends
                // the empty string for both on a Node target and clients read them as opaque.
                Origin = "",
                Name = "",
                UniqueId = _target.TargetId + "." + MainExecutionContextId.ToString(CultureInfo.InvariantCulture),
                AuxData = AuxData,
            },
        };

        return EmitAsync(RuntimeEvents.ExecutionContextCreated(created), context.CancellationToken);
    }

    /// <inheritdoc/>
    protected override ValueTask<EmptyResult> RunIfWaitingForDebuggerAsync(EmptyParameters parameters, CommandContext context)
    {
        _target.Dispatcher.ReleaseDebuggerWait();
        return new ValueTask<EmptyResult>(EmptyResult.Instance);
    }

    /// <summary>
    /// Answers nothing, and truthfully: this package keeps no console buffer to discard.
    /// </summary>
    /// <remarks>
    /// Answered rather than refused because clients send it while tidying up and a <c>-32601</c> there is
    /// noise in a client's log about a state it does not have.
    /// </remarks>
    protected override ValueTask<EmptyResult> DiscardConsoleEntriesAsync(EmptyParameters parameters, CommandContext context)
    {
        return new ValueTask<EmptyResult>(EmptyResult.Instance);
    }

    /// <summary>
    /// Answers a stable identifier for the engine, which is what a client groups targets sharing a heap by.
    /// </summary>
    /// <remarks>
    /// One engine is one isolate here: engines share nothing, and a <c>JsValue</c> crossing between two of
    /// them is unsupported, so the target's own identifier is exactly as unique as the isolate is.
    /// </remarks>
    protected override ValueTask<GetIsolateIdResponse> GetIsolateIdAsync(EmptyParameters parameters, CommandContext context)
    {
        return new ValueTask<GetIsolateIdResponse>(new GetIsolateIdResponse { Id = _target.TargetId });
    }

    /// <inheritdoc/>
    protected override ValueTask<EvaluateResponse> EvaluateAsync(EvaluateRequest parameters, CommandContext context)
    {
        if (parameters.ContextId is { } contextId && contextId != MainExecutionContextId)
        {
            // Chrome's wording, which several clients match on to decide a context went away rather than
            // that the call was wrong.
            Throw.ServerError("Cannot find context with specified id");
        }

        var byValue = parameters.ReturnByValue == true;

        JsValue value;
        try
        {
            value = _target.Engine.Evaluate(parameters.Expression);
        }
        catch (JavaScriptException exception)
        {
            // A compile failure arrives here too: the engine raises a SyntaxError for one, with the
            // location filled in, so there is one path rather than two and neither is a protocol error --
            // the command was well formed and it is the client's expression that was not.
            return new ValueTask<EvaluateResponse>(Failure(RemoteValues.Exception(exception, NextExceptionId(), MainExecutionContextId), exception.Error, byValue));
        }

        if (parameters.AwaitPromise == true && value.IsPromise())
        {
            return Settled(value, byValue);
        }

        return new ValueTask<EvaluateResponse>(new EvaluateResponse { Result = RemoteValues.Describe(value, byValue) });
    }

    /// <summary>
    /// Answers when the promise settles, by attaching reactions to it.
    /// </summary>
    /// <remarks>
    /// <b>Never by draining.</b> This runs inside an event-loop job and the pump's re-entrancy guard forbids
    /// a nested drain, so the command completes from the job that runs the reaction — on this same thread,
    /// with the value still in the engine's hands. What crosses back afterwards is a finished record.
    /// </remarks>
    private ValueTask<EvaluateResponse> Settled(JsValue promise, bool byValue)
    {
        var engine = _target.Engine;
        var then = promise.Get("then");
        if (!then.IsCallable())
        {
            return new ValueTask<EvaluateResponse>(new EvaluateResponse { Result = RemoteValues.Describe(promise, byValue) });
        }

        var completion = new TaskCompletionSource<EvaluateResponse>(TaskCreationOptions.RunContinuationsAsynchronously);

        // Both reactions catch everything, and that is not defensive noise: they run as event-loop jobs, so
        // anything they let escape erupts out of the host's own pump. Describing the settled value can fail
        // -- a by-value result the protocol has no form for is a refusal -- and the client is what should
        // hear about it.
        var onFulfilled = new ClrFunction(engine, "", (_, arguments) =>
        {
            try
            {
                var result = arguments.Length > 0 ? arguments[0] : JsValue.Undefined;
                completion.TrySetResult(new EvaluateResponse { Result = RemoteValues.Describe(result, byValue) });
            }
            catch (Exception exception)
            {
                completion.TrySetException(exception);
            }

            return JsValue.Undefined;
        });

        var onRejected = new ClrFunction(engine, "", (_, arguments) =>
        {
            try
            {
                var reason = arguments.Length > 0 ? arguments[0] : JsValue.Undefined;
                completion.TrySetResult(Failure(RemoteValues.Rejection(reason, NextExceptionId(), MainExecutionContextId), reason, byValue));
            }
            catch (Exception exception)
            {
                completion.TrySetException(exception);
            }

            return JsValue.Undefined;
        });

        engine.Call(then, promise, [onFulfilled, onRejected]);
        return new ValueTask<EvaluateResponse>(completion.Task);
    }

    private static EvaluateResponse Failure(ExceptionDetails details, JsValue thrown, bool byValue)
    {
        // The protocol wants both: the details a client renders in its console, and the thrown value itself
        // as the command's result, which is what a client inspects.
        return new EvaluateResponse
        {
            Result = RemoteValues.Describe(thrown, byValue && !thrown.IsObject()),
            ExceptionDetails = details,
        };
    }

    private int NextExceptionId() => ++_exceptionId;

    private static JsonElement AuxData
    {
        get
        {
            using var document = JsonDocument.Parse("""{"isDefault":true,"type":"default"}""");
            return document.RootElement.Clone();
        }
    }
}
