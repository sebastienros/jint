#if NET8_0_OR_GREATER
using Jint.Native;
using Jint.Native.Promise;
using Jint.Runtime;
using Jint.WebApi.Abort;
using Jint.WebApi.DomException;
using Jint.WebApi.Streams;

namespace Jint.WebApi.Locks;

/// <summary>
/// The engine-side half of one <c>navigator.locks.request()</c> call: the callback, the released promise,
/// the <c>AbortSignal</c> and the two promises a granted lock has.
/// <para>
/// https://w3c.github.io/web-locks/#request-a-lock
/// </para>
/// </summary>
/// <remarks>
/// <para>
/// <b>Every method here runs on the owning engine's thread</b>, as an event-loop task the
/// <see cref="LockManager"/> scheduled, or — for the abort algorithm — inside the
/// <c>controller.abort()</c> that ran it. Nothing in the manager ever touches a member of this class other
/// than by handing one of these methods to <see cref="LockAgent.Schedule"/>.
/// </para>
/// <para>
/// The two promises are the ones §2.4 names. The <i>released</i> promise is what <c>request()</c> returned;
/// the <i>waiting</i> promise is what the callback's return value is resolved into, and it is created here
/// rather than in the manager because it only exists once a grant happens.
/// </para>
/// </remarks>
internal sealed class LockOperation
{
    private readonly Engine _engine;
    private readonly Realm _realm;
    private readonly LockManager _manager;
    private readonly ICallable _callback;

    /// <summary>
    /// https://w3c.github.io/web-locks/#lock-released-promise — the promise <c>request()</c> returned. It
    /// carries the specification's <c>[[AlreadyResolved]]</c> guard, which is what makes a steal and a
    /// later release settle it once between them.
    /// </summary>
    private readonly PromiseCapability _released;

    private JsAbortSignal? _signal;
    private Action? _abortAlgorithm;

    /// <summary>
    /// Whether this operation has already given its lock back. Engine-thread-only: every writer but
    /// <see cref="MarkStolen"/> runs there, and that one has a field of its own.
    /// </summary>
    private bool _settled;

    /// <summary>
    /// Whether another request took this lock with the <c>steal</c> option.
    /// </summary>
    /// <remarks>
    /// <b>Written under the manager's gate, possibly from another engine's thread</b>, and read on this
    /// engine's — the same one-way volatile flag discipline <c>BroadcastChannelSubscription.Closed</c>
    /// keeps. It has to be set at the moment of the steal rather than by the job the steal schedules,
    /// because the callback's waiting promise can settle in the microtask checkpoint <i>before</i> that job
    /// runs: without it, a lock stolen from under a callback that then returned normally would resolve the
    /// released promise the steal is about to reject, and the rejection would be the no-op.
    /// </remarks>
    private volatile bool _stolen;

    internal LockOperation(Engine engine, Realm realm, LockManager manager, ICallable callback, PromiseCapability released)
    {
        _engine = engine;
        _realm = realm;
        _manager = manager;
        _callback = callback;
        _released = released;
    }

    /// <summary>The promise <c>request()</c> hands back.</summary>
    internal JsValue Promise => _released.PromiseInstance;

    /// <summary>
    /// Step 2 of <i>request a lock</i>: "add the algorithm <i>signal to abort the request request</i> with
    /// signal to signal", which is https://w3c.github.io/web-locks/#signal-to-abort-the-request — enqueue
    /// the abort to the lock task queue, then reject the released promise with the signal's abort reason.
    /// </summary>
    /// <remarks>
    /// The caller has already refused an <i>already</i> aborted signal (step 9 of the method steps), so this
    /// only ever registers on a live one.
    /// </remarks>
    internal void AttachSignal(JsAbortSignal signal, LockRequestEntry request)
    {
        _signal = signal;
        _abortAlgorithm = () =>
        {
            _manager.AbortRequest(request);
            _released.Reject(signal.Reason);
        };

        signal.AddAbortAlgorithm(_abortAlgorithm);
    }

    /// <summary>
    /// Step 3.5.1 of <i>request a lock</i>: the <c>ifAvailable</c> miss — "let r be the result of invoking
    /// callback with null as the only argument; resolve promise with r".
    /// </summary>
    /// <remarks>
    /// The callback's WebIDL return type is <c>Promise&lt;any&gt;</c>, so a callback that throws produces a
    /// rejected promise rather than an exception, and <c>request()</c>'s promise rejects with what it threw —
    /// which is what <c>web-locks/ifAvailable.https.any.js</c> asserts in both its synchronous and its
    /// asynchronous form.
    /// </remarks>
    internal void InvokeWithoutLock()
        => _released.Resolve(StreamPromises.PromiseCall(_engine, _realm, _callback, JsValue.Undefined, [JsValue.Null]));

    /// <summary>
    /// The engine half of https://w3c.github.io/web-locks/#process-the-lock-request-queue's last step: the
    /// signal is re-read, the <c>Lock</c> object is built, the callback is invoked, and the waiting promise
    /// decides when the lock is released.
    /// </summary>
    internal void Grant(HeldLockEntry held)
    {
        if (_signal is { } signal)
        {
            // "If signal is aborted, then enqueue the step to release the lock and return." The abort
            // algorithm has already rejected the released promise; what is left is to give the lock back
            // without ever invoking the callback.
            if (signal.Aborted)
            {
                Release(held);
                return;
            }

            // "Remove the algorithm signal to abort the request request from signal." Past this line the
            // signal is ignored, which is what lets a lock granted before an abort still release normally.
            if (_abortAlgorithm is { } algorithm)
            {
                signal.RemoveAbortAlgorithm(algorithm);
                _abortAlgorithm = null;
            }

            _signal = null;
        }

        var lockObject = JsLock.Create(_engine, _realm, held.Name, held.Mode);

        // "Let r be the result of invoking callback with a new Lock object ...; resolve waiting with r." The
        // WebIDL invocation of a promise-returning callback is what turns a non-promise return value into a
        // resolved promise and a throw into a rejected one.
        var waiting = StreamPromises.PromiseCall(_engine, _realm, _callback, JsValue.Undefined, [lockObject]);

        // §2.4: "When lock's waiting promise settles (fulfills or rejects) ... release the lock lock; resolve
        // lock's released promise with lock's waiting promise." Resolving with the promise rather than with
        // its value is what makes request()'s promise settle strictly after the callback's own, which
        // web-locks/acquire.https.any.js asserts by ordering two `then` callbacks.
        StreamPromises.UponPromise(
            _engine,
            waiting,
            _ => SettleReleased(held, waiting),
            _ => SettleReleased(held, waiting));
    }

    /// <summary>
    /// Records that step 3.4 of <i>request a lock</i> has taken this lock out of the held set. Called by the
    /// manager, under its gate; see <see cref="_stolen"/> for why it is not left to the job below.
    /// </summary>
    internal void MarkStolen() => _stolen = true;

    /// <summary>
    /// Step 3.4 of <i>request a lock</i>, from the stolen lock's side: "reject lock's released promise with
    /// an <c>AbortError</c> <c>DOMException</c>". Runs on the engine that held the lock, which may not be
    /// the engine that stole it.
    /// </summary>
    internal void RejectReleasedWithAbortError()
    {
        _settled = true;
        _released.Reject(_realm.Intrinsics.DomException.CreateException(
            DomExceptionNames.Abort,
            "Lock broken by another request with the 'steal' option."));
    }

    private void SettleReleased(HeldLockEntry held, JsPromise waiting)
    {
        if (_settled || _stolen)
        {
            // Stolen: the lock left the held set when the steal ran, and the released promise is that
            // request's to reject. Releasing again would process a queue for nothing, and resolving would
            // win a race the specification does not have.
            return;
        }

        _settled = true;
        Release(held);
        _released.Resolve(waiting);
    }

    private void Release(HeldLockEntry held)
    {
        _settled = true;
        _manager.Release(held);
    }
}
#endif
