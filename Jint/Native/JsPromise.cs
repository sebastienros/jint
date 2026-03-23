using System.Threading;
using Jint.Native.Object;
using Jint.Native.Promise;
using Jint.Runtime;
using Jint.Runtime.Descriptors;
using Jint.Runtime.Interop;

namespace Jint.Native;

internal sealed class JsPromise : ObjectInstance
{
    internal PromiseState State { get; private set; }

    // valid only in settled state (Fulfilled or Rejected)
    internal JsValue Value { get; private set; } = null!;

    /// <summary>
    /// Lazily allocated ManualResetEventSlim, only created when someone actually
    /// calls UnwrapIfPromise() and needs to wait. This avoids allocating an OS
    /// synchronization primitive for every promise (most are never polled).
    /// </summary>
    private ManualResetEventSlim? _completedEvent;

    /// <summary>
    /// Gets or creates the completed event for synchronous waiting.
    /// Thread-safe via Interlocked.CompareExchange.
    /// </summary>
    internal ManualResetEventSlim CompletedEvent
    {
        get
        {
            if (_completedEvent is not null)
            {
                return _completedEvent;
            }

            var newEvent = new ManualResetEventSlim(State != PromiseState.Pending);
            var existing = Interlocked.CompareExchange(ref _completedEvent, newEvent, null);
            if (existing is not null)
            {
                // Another thread created it first, dispose ours
                newEvent.Dispose();
                return existing;
            }

            return newEvent;
        }
    }

    /// <summary>
    /// Whether this promise has been observed by a reject handler.
    /// Used by HostPromiseRejectionTracker per TC39 spec.
    /// </summary>
    internal bool PromiseIsHandled { get; set; }

    internal List<PromiseReaction> PromiseRejectReactions = new();
    internal List<PromiseReaction> PromiseFulfillReactions = new();

    internal JsPromise(Engine engine) : base(engine)
    {
    }

    // https://tc39.es/ecma262/#sec-createresolvingfunctions
    // Note that functions capture over alreadyResolved
    // that does imply that the same promise can be resolved twice but with different resolving functions
    internal ResolvingFunctions CreateResolvingFunctions()
    {
        var alreadyResolved = false;
        var resolve = new ClrFunction(_engine, "", (thisObj, args) =>
        {
            if (alreadyResolved)
            {
                return Undefined;
            }

            alreadyResolved = true;
            return Resolve(thisObj, args);
        }, 1, PropertyFlag.Configurable);

        var reject = new ClrFunction(_engine, "", (thisObj, args) =>
        {
            if (alreadyResolved)
            {
                return Undefined;
            }

            alreadyResolved = true;
            return Reject(thisObj, args);
        }, 1, PropertyFlag.Configurable);

        return new ResolvingFunctions(resolve, reject);
    }

    // https://tc39.es/ecma262/#sec-promise-resolve-functions
    private JsValue Resolve(JsValue thisObject, JsCallArguments arguments)
    {
        var result = arguments.At(0);
        return Resolve(result);
    }

    internal JsValue Resolve(JsValue result)
    {
        // Note that alreadyResolved logic lives in CreateResolvingFunctions method

        if (ReferenceEquals(result, this))
        {
            return RejectPromise(_engine.Realm.Intrinsics.TypeError.Construct("Cannot resolve Promise with itself"));
        }

        if (result is not ObjectInstance resultObj)
        {
            return FulfillPromise(result);
        }

        JsValue thenProp;
        try
        {
            thenProp = resultObj.Get("then");
        }
        catch (JavaScriptException e)
        {
            return RejectPromise(e.Error);
        }

        if (thenProp is not ICallable thenMethod)
        {
            return FulfillPromise(result);
        }

        var realm = _engine.Realm;
        var job = PromiseOperations.NewPromiseResolveThenableJob(this, resultObj, thenMethod);
        _engine._host.HostEnqueuePromiseJob(job, realm);

        return Undefined;
    }

    // https://tc39.es/ecma262/#sec-promise-reject-functions
    private JsValue Reject(JsValue thisObject, JsCallArguments arguments)
    {
        // Note that alreadyResolved logic lives in CreateResolvingFunctions method

        var reason = arguments.At(0);

        return RejectPromise(reason);
    }


    // https://tc39.es/ecma262/#sec-rejectpromise
    // 1. Assert: The value of promise.[[PromiseState]] is pending.
    // 2. Let reactions be promise.[[PromiseRejectReactions]].
    // 3. Set promise.[[PromiseResult]] to reason.
    // 4. Set promise.[[PromiseFulfillReactions]] to undefined.
    // 5. Set promise.[[PromiseRejectReactions]] to undefined.
    // 6. Set promise.[[PromiseState]] to rejected.
    // 7. If promise.[[PromiseIsHandled]] is false, perform HostPromiseRejectionTracker(promise, "reject").
    // 8. Return TriggerPromiseReactions(reactions, reason).
    private JsValue RejectPromise(JsValue reason)
    {
        if (State != PromiseState.Pending)
        {
            Throw.InvalidOperationException("Promise should be in Pending state");
        }

        Settle(PromiseState.Rejected, reason);

        var reactions = PromiseRejectReactions;
        PromiseRejectReactions = new List<PromiseReaction>();
        PromiseFulfillReactions.Clear();
        _completedEvent?.Set();

        // 7. If promise.[[PromiseIsHandled]] is false, perform HostPromiseRejectionTracker(promise, "reject").
        if (!PromiseIsHandled)
        {
            _engine._host.HostPromiseRejectionTracker(this, PromiseRejectionOperation.Reject);
        }

        return PromiseOperations.TriggerPromiseReactions(_engine, reactions, reason);
    }

    // https://tc39.es/ecma262/#sec-fulfillpromise
    private JsValue FulfillPromise(JsValue result)
    {
        if (State != PromiseState.Pending)
        {
            Throw.InvalidOperationException("Promise should be in Pending state");
        }

        Settle(PromiseState.Fulfilled, result);
        var reactions = PromiseFulfillReactions;
        PromiseFulfillReactions = new List<PromiseReaction>();
        PromiseRejectReactions.Clear();
        _completedEvent?.Set();

        return PromiseOperations.TriggerPromiseReactions(_engine, reactions, result);
    }

    private void Settle(PromiseState state, JsValue result)
    {
        State = state;
        Value = result;
    }
}
