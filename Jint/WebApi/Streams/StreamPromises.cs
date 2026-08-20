#if NET8_0_OR_GREATER
using Jint.Native;
using Jint.Native.Promise;
using Jint.Runtime;

namespace Jint.WebApi.Streams;

/// <summary>
/// The promise vocabulary the Streams Standard's algorithms are written in — "a new promise", "a promise
/// resolved with", "upon fulfillment of", "transform promise with" and the rest.
/// <para>
/// https://streams.spec.whatwg.org/
/// </para>
/// </summary>
/// <remarks>
/// <para>
/// <b>Everything here is an ordinary engine promise settled on the engine's thread.</b> A stream never
/// touches a CLR <c>Task</c>, never starts a thread and never settles anything off the event loop: a
/// reaction is a job on the very queue that runs promise reactions and timer callbacks, so the microtask
/// ordering the specification prescribes falls out of the single queue rather than being arranged for.
/// </para>
/// <para>
/// The reactions are engine-internal <see cref="IPromiseContinuation"/>s rather than JavaScript functions.
/// That is what <c>await</c> itself uses, and it matters for more than allocation: the specification's
/// handlers are not observable from script — no <c>Function.prototype.call</c> is looked up, no
/// <c>name</c>/<c>length</c> descriptors are materialized — and a continuation is exactly that. The job
/// granularity is identical to a JavaScript handler's, one job per reaction, which is what keeps the
/// microtask counts the specification depends on (tee's deliberate one-microtask delay, for instance)
/// honest.
/// </para>
/// </remarks>
internal static class StreamPromises
{
    /// <summary>
    /// https://streams.spec.whatwg.org/ — "a new promise". The capability is what the algorithms settle
    /// later; it carries the specification's <c>[[AlreadyResolved]]</c> guard, so a second settle of the
    /// same promise is the no-op the algorithms rely on (tee resolves its <c>cancelPromise</c> from more
    /// than one place).
    /// </summary>
    internal static PromiseCapability NewPromise(Engine engine, Realm realm)
        => PromiseConstructor.NewPromiseCapability(engine, realm.Intrinsics.Promise);

    /// <summary>The promise a capability was created for, which for the <c>%Promise%</c> intrinsic is always an ordinary promise.</summary>
    internal static JsPromise PromiseOf(PromiseCapability capability) => (JsPromise) capability.PromiseInstance;

    /// <summary>
    /// "a promise resolved with <paramref name="value"/>" — <c>PromiseResolve(%Promise%, value)</c>. A
    /// thenable is adopted, which is how a user callback's return value becomes the promise the algorithms
    /// wait on.
    /// </summary>
    internal static JsPromise ResolvedWith(Realm realm, JsValue value)
        => (JsPromise) realm.Intrinsics.Promise.PromiseResolve(value);

    /// <summary>"a promise resolved with undefined".</summary>
    internal static JsPromise ResolvedWithUndefined(Engine engine, Realm realm)
    {
        var capability = NewPromise(engine, realm);
        capability.Resolve(JsValue.Undefined);
        return PromiseOf(capability);
    }

    /// <summary>
    /// "a promise rejected with <paramref name="reason"/>".
    /// </summary>
    /// <remarks>
    /// <paramref name="handled"/> spells the specification's frequent companion step, "set
    /// <c>promise.[[PromiseIsHandled]]</c> to true", and is set <i>before</i> the rejection rather than
    /// after so the engine's rejection tracker never sees a rejection the algorithm already accounted for.
    /// The flag is otherwise exactly the specification's slot.
    /// </remarks>
    internal static JsPromise RejectedWith(Engine engine, Realm realm, JsValue reason, bool handled = false)
    {
        var capability = NewPromise(engine, realm);
        var promise = PromiseOf(capability);
        if (handled)
        {
            promise.PromiseIsHandled = true;
        }

        capability.Reject(reason);
        return promise;
    }

    /// <summary>
    /// Marks a promise as handled: "set <c>promise.[[PromiseIsHandled]]</c> to true". Used for the promises
    /// the algorithms create and settle for their own bookkeeping, which no script ever attaches to.
    /// </summary>
    internal static void MarkHandled(JsPromise promise) => promise.PromiseIsHandled = true;

    /// <summary>
    /// "Upon fulfillment of <paramref name="promise"/>" / "upon rejection of <paramref name="promise"/>" —
    /// the specification's <c>uponPromise</c>. The handlers produce no value and the reaction has no result
    /// promise, exactly as <c>PerformPromiseThen</c> with no capability.
    /// </summary>
    internal static void UponPromise(
        Engine engine,
        JsPromise promise,
        Action<JsValue>? onFulfilled,
        Action<JsValue>? onRejected)
    {
        PromiseOperations.UponPromise(engine, promise, onFulfilled, onRejected);
    }

    /// <summary>"Upon fulfillment of <paramref name="promise"/>".</summary>
    internal static void UponFulfillment(Engine engine, JsPromise promise, Action<JsValue> onFulfilled)
        => UponPromise(engine, promise, onFulfilled, onRejected: null);

    /// <summary>"Upon rejection of <paramref name="promise"/>".</summary>
    internal static void UponRejection(Engine engine, JsPromise promise, Action<JsValue> onRejected)
        => UponPromise(engine, promise, onFulfilled: null, onRejected);

    /// <summary>
    /// The specification's <c>transformPromiseWith</c>: <c>promise.then(onFulfilled, onRejected)</c> without
    /// going through the observable <c>then</c>. A handler that returns a value fulfils the derived promise
    /// with it (adopting a thenable), and one that raises a JavaScript exception rejects it — which is how
    /// <c>TransformStreamDefaultControllerPerformTransform</c>'s "error the stream and rethrow" reaches its
    /// caller.
    /// </summary>
    internal static JsPromise TransformPromiseWith(
        Engine engine,
        Realm realm,
        JsPromise promise,
        Func<JsValue, JsValue>? onFulfilled,
        Func<JsValue, JsValue>? onRejected)
    {
        var capability = NewPromise(engine, realm);
        PromiseOperations.PerformPromiseThen(engine, promise, new TransformReaction(capability, onFulfilled, onRejected));
        return PromiseOf(capability);
    }

    /// <summary>
    /// The WebIDL rule for invoking a callback function whose return type is a promise type: an abrupt
    /// completion becomes a rejected promise, and the returned value is converted to
    /// <c>Promise&lt;undefined&gt;</c>, i.e. handed to <c>PromiseResolve</c>.
    /// <para>
    /// https://webidl.spec.whatwg.org/#call-a-user-objects-operation, step "If completion is an abrupt
    /// completion and the operation has a return type that is not a promise type, throw"; otherwise "return
    /// a promise rejected with".
    /// </para>
    /// </summary>
    internal static JsPromise PromiseCall(
        Engine engine,
        Realm realm,
        ICallable callback,
        JsValue thisArgument,
        JsCallArguments arguments)
    {
        try
        {
            var result = callback.Call(thisArgument, arguments);
            return ResolvedWith(realm, result);
        }
        catch (JavaScriptException e)
        {
            return RejectedWith(engine, realm, e.Error);
        }
    }

    /// <summary>
    /// The reaction behind <see cref="TransformPromiseWith"/>: the handler's value settles the derived
    /// promise, and its abrupt completion rejects it — the whole of <c>PerformPromiseThen</c>'s job step for
    /// a reaction that carries a capability.
    /// </summary>
    private sealed class TransformReaction : IPromiseContinuation
    {
        private readonly PromiseCapability _capability;
        private readonly Func<JsValue, JsValue>? _onFulfilled;
        private readonly Func<JsValue, JsValue>? _onRejected;

        internal TransformReaction(
            PromiseCapability capability,
            Func<JsValue, JsValue>? onFulfilled,
            Func<JsValue, JsValue>? onRejected)
        {
            _capability = capability;
            _onFulfilled = onFulfilled;
            _onRejected = onRejected;
        }

        public void Invoke(Engine engine, JsValue value, ReactionType type)
        {
            var handler = type == ReactionType.Fulfill ? _onFulfilled : _onRejected;

            // "If handler is empty": a missing handler passes the settlement straight through, which is what
            // makes transformPromiseWith(p, undefined, r) forward p's fulfillment value unchanged.
            if (handler is null)
            {
                if (type == ReactionType.Fulfill)
                {
                    _capability.Resolve(value);
                }
                else
                {
                    _capability.Reject(value);
                }

                return;
            }

            try
            {
                _capability.Resolve(handler(value));
            }
            catch (JavaScriptException e)
            {
                _capability.Reject(e.Error);
            }
        }
    }
}
#endif
