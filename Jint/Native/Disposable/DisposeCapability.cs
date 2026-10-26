using System.Runtime.InteropServices;
using Jint.Runtime;

namespace Jint.Native.Disposable;

/// <summary>
/// Result of a single step of <see cref="DisposeCapability"/>'s state machine.
/// Either the dispose finished (<see cref="IsDone"/> with <see cref="CompletedResult"/>)
/// or it needs to await a value (<see cref="PendingPromise"/>) before continuing: an async dispose method's
/// result, or <see cref="JsValue.Undefined"/> for the implicit <c>Await(undefined)</c> of
/// https://tc39.es/ecma262/#sec-disposeresources steps 4.d and 5, which no getter can observe.
/// </summary>
[StructLayout(LayoutKind.Auto)]
internal readonly record struct DisposeStepResult(
    bool IsDone,
    Completion CompletedResult,
    JsValue? PendingPromise)
{
    public static DisposeStepResult Done(Completion c) => new(true, c, null);
    public static DisposeStepResult Suspend(JsValue promise) => new(false, default, promise);
}

/// <summary>
/// Tracks where in the spec algorithm we suspended, so the next
/// <see cref="DisposeCapability.ContinueDispose"/> call applies the right rules.
/// </summary>
internal enum DisposeResumePoint
{
    None,
    AfterMethodAwait,   // resumed after step 4.e.ii.1
    AfterImplicitAwait, // resumed after step 4.d.i or 5.a
}

internal sealed class DisposeCapability
{
    private readonly Engine _engine;
    private readonly List<DisposableResource> _disposableResourceStack = [];

    // State preserved across suspensions (4.e.ii.1, 4.d.i, 5.a). Not reentrant —
    // a single DisposeCapability instance can only drive one in-flight dispose at a
    // time. This matches the engine's single-thread-per-Engine assumption: a JS
    // dispose method cannot synchronously re-enter dispose on its own capability.
    private int _disposeIndex;
    private bool _disposeHasAwaited;
    private bool _disposeNeedsAwait;
    private Completion _disposeCompletion;
    private DisposeResumePoint _disposeResume;

    public DisposeCapability(Engine engine)
    {
        _engine = engine;
    }

    /// <summary>
    /// Fast check used by callers to avoid allocating dispose-completion closures
    /// when nothing is registered.
    /// </summary>
    public bool HasResources => _disposableResourceStack.Count > 0;

    public void AddDisposableResource(JsValue v, DisposeHint hint, ICallable? method = null)
    {
        DisposableResource resource;
        if (method is null)
        {
            if (v.IsNullOrUndefined() && hint == DisposeHint.Sync)
            {
                return;
            }

            resource = CreateDisposableResource(v, hint);
        }
        else
        {
            resource = CreateDisposableResource(JsValue.Undefined, hint, method);
        }

        _disposableResourceStack.Add(resource);
    }

    private DisposableResource CreateDisposableResource(JsValue v, DisposeHint hint, ICallable? method = null)
    {
        if (method is null)
        {
            if (v.IsNullOrUndefined())
            {
                v = JsValue.Undefined;
                method = null;
            }
            else
            {
                if (!v.IsObject())
                {
                    Throw.TypeError(_engine.Realm, "Expected an object for disposable resource.");
                    return default;
                }
                method = v.AsObject().GetDisposeMethod(hint);
                if (method is null)
                {
                    Throw.TypeError(_engine.Realm, "No dispose method found for the resource.");
                    return default;
                }
            }
        }

        return new DisposableResource(v, hint, method);
    }

    /// <summary>
    /// Legacy synchronous entry point. Drives the state machine to completion,
    /// sync-waiting via <see cref="JsValue.UnwrapIfPromise(TimeSpan)"/>
    /// on every suspension. Used by callers that can't suspend their own context
    /// (sync function bodies, module loading). After all async-context call sites
    /// migrated to <see cref="BeginDispose"/> / <see cref="ContinueDispose"/>, this
    /// only runs for contexts where await-using is syntactically illegal, so no
    /// async-dispose resource can actually be in the stack and the loop exits in
    /// a single Done step without ever calling UnwrapIfPromise.
    /// </summary>
    public Completion DisposeResources(Completion c)
    {
        var step = BeginDispose(c);
        while (!step.IsDone)
        {
            var pending = step.PendingPromise!;
            JsValue resolved;
            bool threw = false;
            try
            {
                resolved = pending.UnwrapIfPromise(_engine.Options.Constraints.PromiseTimeout);
            }
            catch (PromiseRejectedException e)
            {
                resolved = e.RejectedValue;
                threw = true;
            }
            catch (JavaScriptException e)
            {
                resolved = e.Error;
                threw = true;
            }
            step = ContinueDispose(resolved, threw);
        }
        return step.CompletedResult;
    }

    /// <summary>
    /// Initialize the dispose state machine and run until the first suspension or completion.
    /// </summary>
    public DisposeStepResult BeginDispose(Completion c)
    {
        _disposeIndex = _disposableResourceStack.Count - 1;
        _disposeHasAwaited = false;
        _disposeNeedsAwait = false;
        _disposeCompletion = c;
        _disposeResume = DisposeResumePoint.None;
        return Advance();
    }

    /// <summary>
    /// Continue the dispose state machine after a suspended Await settles.
    /// </summary>
    public DisposeStepResult ContinueDispose(JsValue awaitResult, bool awaitThrew)
    {
        switch (_disposeResume)
        {
            case DisposeResumePoint.AfterMethodAwait:
                // Spec step 4.e.ii.1 just settled. Promote rejection per 4.e.iii.
                if (awaitThrew)
                {
                    HandleDisposeException(awaitResult);
                }
                _disposeIndex--;
                break;

            case DisposeResumePoint.AfterImplicitAwait:
                // Spec step 4.d.i or 5.a — Await(undefined) has no error path.
                if (_disposeIndex < 0)
                {
                    // We were on step 5.a (final implicit tick).
                    return Finish();
                }
                // Otherwise we were on step 4.d.i — fall through to process
                // the current resource (no decrement).
                break;
        }

        _disposeResume = DisposeResumePoint.None;
        return Advance();
    }

    private DisposeStepResult Advance()
    {
        while (_disposeIndex >= 0)
        {
            var resource = _disposableResourceStack[_disposeIndex];
            var value = resource.ResourceValue;
            var hint = resource.Hint;
            var method = resource.DisposeMethod;

            // Step 4.d: implicit Await tick when a sync-dispose follows pending async resources. It sets needsAwait
            // back and leaves hasAwaited as it is, so a null async-dispose resource further down owes an await of its
            // own (https://tc39.es/ecma262/#sec-disposeresources).
            if (hint == DisposeHint.Sync && _disposeNeedsAwait && !_disposeHasAwaited)
            {
                _disposeNeedsAwait = false;
                _disposeResume = DisposeResumePoint.AfterImplicitAwait;
                // Don't decrement — current resource will be processed after resume.
                return DisposeStepResult.Suspend(JsValue.Undefined);
            }

            // Step 4.e: call the dispose method.
            if (method is not null)
            {
                JsValue methodResult;
                try
                {
                    methodResult = method.Call(value);
                }
                catch (JavaScriptException e)
                {
                    HandleDisposeException(e.Error);
                    _disposeIndex--;
                    continue;
                }

                if (hint == DisposeHint.Async)
                {
                    // Step 4.e.ii.1: Await the result before continuing.
                    _disposeHasAwaited = true;
                    _disposeResume = DisposeResumePoint.AfterMethodAwait;
                    return DisposeStepResult.Suspend(methodResult);
                }
            }
            else
            {
                // Step 4.f: null/undefined async-dispose resource — accumulates needsAwait.
                _disposeNeedsAwait = true;
            }

            _disposeIndex--;
        }

        // Step 5: final implicit Await(undefined) if needsAwait was set but never satisfied.
        if (_disposeNeedsAwait && !_disposeHasAwaited)
        {
            _disposeHasAwaited = true;
            _disposeNeedsAwait = false;
            _disposeResume = DisposeResumePoint.AfterImplicitAwait;
            // _disposeIndex is already -1; ContinueDispose will Finish on resume.
            return DisposeStepResult.Suspend(JsValue.Undefined);
        }

        return Finish();
    }

    private DisposeStepResult Finish()
    {
        var result = _disposeCompletion;
        _disposableResourceStack.Clear();
        _disposeResume = DisposeResumePoint.None;
        return DisposeStepResult.Done(result);
    }

    private void HandleDisposeException(JsValue error)
    {
        if (_disposeCompletion.Type == CompletionType.Throw)
        {
            var suppressed = _engine.Intrinsics.SuppressedError.Construct(
                _engine.Intrinsics.SuppressedError, "", error, _disposeCompletion.Value);
            _disposeCompletion = new Completion(CompletionType.Throw, suppressed, _disposeCompletion._source);
        }
        else
        {
            _disposeCompletion = new Completion(CompletionType.Throw, error, _disposeCompletion._source);
        }
    }

    private readonly record struct DisposableResource(JsValue ResourceValue, DisposeHint Hint, ICallable? DisposeMethod);
}
