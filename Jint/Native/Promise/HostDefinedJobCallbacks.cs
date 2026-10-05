using Jint.Runtime;

namespace Jint.Native.Promise;

/// <summary>
/// An engine-internal reaction continuation — an <c>await</c>, a <c>for await</c> step, an "upon fulfillment"
/// algorithm step — whose HostMakeJobCallback captured host state: the Job Callback Record's
/// <c>[[HostDefined]]</c> (https://tc39.es/ecma262/#sec-hostmakejobcallback), and the
/// HostCallJobCallback (https://tc39.es/ecma262/#sec-hostcalljobcallback) that runs the continuation under it.
/// </summary>
/// <remarks>
/// Allocated only on an engine with <see cref="Options.HostOptions.JobCallbacks"/> installed, and only when its
/// <see cref="JobCallbackHooks.Capture"/> returned something — every other reaction keeps its continuation
/// directly, so the reaction record and the job that runs it are unchanged. The whole continuation runs inside
/// the hooks because the spec's Await closures resume the suspended body from inside the closure call.
/// </remarks>
internal sealed class HostDefinedContinuation : IPromiseContinuation
{
    private readonly JobCallbackHooks _hooks;
    private readonly object _hostDefined;
    private readonly IPromiseContinuation _continuation;

    internal HostDefinedContinuation(JobCallbackHooks hooks, object hostDefined, IPromiseContinuation continuation)
    {
        _hooks = hooks;
        _hostDefined = hostDefined;
        _continuation = continuation;
    }

    public void Invoke(Engine engine, JsValue value, ReactionType type)
    {
        var token = _hooks.Enter(engine, _hostDefined);
        try
        {
            _continuation.Invoke(engine, value, type);
        }
        finally
        {
            _hooks.Exit(engine, token);
        }
    }
}

/// <summary>
/// Both reactions of one <c>then</c> whose HostMakeJobCallback captured host state
/// (https://tc39.es/ecma262/#sec-performpromisethen steps 3-6), run by NewPromiseReactionJob
/// (https://tc39.es/ecma262/#sec-newpromisereactionjob) with step 1.f's HostCallJobCallback around the handler.
/// </summary>
/// <remarks>
/// One instance serves the fulfill and the reject reaction, which both carry it as their continuation and no
/// handler or capability of their own. Running it is <see cref="PromiseOperations.RunReactionJob"/>'s handler
/// path verbatim, except that only the handler call itself sits inside the hooks — resolving the derived
/// promise does not, as in the spec, so a thenable it adopts captures the state current outside the callback.
/// </remarks>
internal sealed class HostDefinedReactionHandlers : IPromiseContinuation
{
    private readonly JobCallbackHooks _hooks;
    private readonly object _hostDefined;
    private readonly ICallable? _onFulfilled;
    private readonly ICallable? _onRejected;
    private readonly PromiseCapability? _capability;

    internal HostDefinedReactionHandlers(
        JobCallbackHooks hooks,
        object hostDefined,
        ICallable? onFulfilled,
        ICallable? onRejected,
        PromiseCapability? capability)
    {
        _hooks = hooks;
        _hostDefined = hostDefined;
        _onFulfilled = onFulfilled;
        _onRejected = onRejected;
        _capability = capability;
    }

    public void Invoke(Engine engine, JsValue value, ReactionType type)
    {
        var handler = type == ReactionType.Fulfill ? _onFulfilled : _onRejected;
        if (handler is null)
        {
            // e. The handler is empty: the settlement passes through to the derived promise, and there is no
            // job callback to call.
            if (type == ReactionType.Fulfill)
            {
                _capability?.Resolve(value);
            }
            else
            {
                _capability?.Reject(value);
            }

            return;
        }

        try
        {
            JsValue result;

            // f. Let handlerResult be HostCallJobCallback(handler, undefined, « argument »).
            var token = _hooks.Enter(engine, _hostDefined);
            try
            {
                result = handler.Call(JsValue.Undefined, value);
            }
            finally
            {
                _hooks.Exit(engine, token);
            }

            _capability?.Resolve(result);
        }
        catch (JavaScriptException e)
        {
            _capability?.Reject(e.Error);
        }
    }
}
