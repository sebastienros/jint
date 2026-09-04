using Jint.Native;
using Jint.Native.Promise;
using Jint.Runtime;

namespace Jint;

public partial class Engine
{
    private TaskOperations? _tasks;

    /// <summary>
    /// Gets the host loop: the turns this engine needs before a timer callback, a worker message or a
    /// host-settled promise can run.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Jint never starts a thread, so an engine nobody pumps runs no <c>setTimeout</c> callback. A host
    /// using timers, workers or host-settled promises has to call
    /// <see cref="TaskOperations.ProcessTasks"/>, and
    /// <see cref="TaskOperations.TimeUntilNextScheduledWork"/> answers when.
    /// </para>
    /// <para>
    /// Created on first access, so an engine that does none of those never allocates one.
    /// </para>
    /// </remarks>
    public TaskOperations Tasks => _tasks ??= new TaskOperations(this);

    /// <summary>
    /// The host loop: the turns an <see cref="Engine"/> needs before queued or scheduled work can run.
    /// </summary>
    public sealed partial class TaskOperations
    {
        private readonly Engine _engine;

        internal TaskOperations(Engine engine)
        {
            _engine = engine;
        }

        /// <summary>
        /// Gives the engine a turn: runs everything the event loop currently has to run — queued jobs,
        /// promise reactions, a due timer, a completion that arrived from a background thread — and returns.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>This is the canonical host loop, together with
        /// <see cref="TimeUntilNextScheduledWork"/>, which answers <i>when</i> to call it.</b> Jint never
        /// starts a thread to run script, so an engine nobody pumps runs no <c>setTimeout</c> callback,
        /// settles no <c>Atomics.waitAsync</c> and delivers no worker message. Any host using timers,
        /// promises or workers has to call this, or one of the blocking waits built on it
        /// (<see cref="WaitForScheduledWork(TimeSpan, System.Threading.CancellationToken)"/>).
        /// </para>
        /// <para>
        /// It runs what is available and does not block: it is not a drain for a budget, and there is
        /// deliberately no such method — see the loop shapes on <see cref="TimeUntilNextScheduledWork"/>.
        /// A job belonging to an evaluation cycle that
        /// <see cref="AdvancedOperations.RestoreGlobalSnapshot"/> has ended is discarded rather than run.
        /// </para>
        /// </remarks>
        public void ProcessTasks()
        {
            using var ownership = _engine.EnterHostCall();
            _engine.RunAvailableContinuations();
        }

        /// <summary>
        /// Enqueues <paramref name="action"/> as an event-loop job, from any thread, waking a parked pump.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>The action runs on whichever thread pumps the engine, never on the caller's.</b> It queues
        /// behind everything already queued and runs in the next <see cref="ProcessTasks"/> — or in a drain a
        /// running evaluation performs — so one posted from inside a job runs after that job, in order with
        /// the jobs already queued.
        /// </para>
        /// <para>
        /// <b>It is the one entry a thread that does not own the engine may call.</b> It claims nothing and is
        /// never refused as concurrent use, and it ends a
        /// <see cref="WaitForScheduledWork(TimeSpan, System.Threading.CancellationToken)"/> the way any other
        /// cross-thread arrival does.
        /// </para>
        /// <para>
        /// <b>A turn is not a run.</b> Execution constraints are not re-armed for it — see
        /// <see cref="Constraint.Reset"/> — so it inherits the counters the engine has, while its allocation
        /// budget begins afresh like every other external event's. An exception it throws erupts out of the
        /// pump that ran it, leaving everything queued behind it to run on the next turn.
        /// </para>
        /// <para>
        /// The job belongs to the evaluation cycle current when it was posted, so an
        /// <see cref="AdvancedOperations.RestoreGlobalSnapshot"/> in between drops it; post again afterwards.
        /// <see cref="Engine.Dispose"/> is not a barrier — an engine has no disposed state, and a job posted
        /// to a disposed engine still runs if it is pumped again.
        /// </para>
        /// </remarks>
        /// <param name="action">The callback to run on the engine's own thread.</param>
        /// <exception cref="ArgumentNullException"><paramref name="action"/> is <c>null</c>.</exception>
        /// <example>
        /// One thread per engine, with the rest of the process handing it work:
        /// <code>
        /// // any thread
        /// engine.Tasks.Post(() => engine.Invoke("onMessage", payload));
        ///
        /// // the engine's own thread
        /// engine.Tasks.WaitForScheduledWork(TimeSpan.FromMilliseconds(50), token);
        /// engine.Tasks.ProcessTasks();
        /// </code>
        /// </example>
        public void Post(Action action)
        {
            if (action is null)
            {
                Throw.ArgumentNullException(nameof(action));
            }

            // Deliberately unguarded — no EnterHostCall — because the caller is the thread that does not own
            // the engine; the queue is concurrent and the enqueue is what wakes a park.
            //
            // The generation is read here rather than at dequeue so that a post belongs to the cycle the host
            // made it in, exactly as a registered promise belongs to the cycle it was registered in. The
            // overload carrying no memory state is the one for work that is not part of any single engine
            // operation: a host thread's post has no originating operation whose budget its turn belongs to,
            // and capturing one would mean writing to the running operation's state from this thread.
            _engine.AddToEventLoop(action, _engine.EventLoopGeneration, EventLoopJobKind.Task);
        }

        /// <summary>
        /// Hands script a promise this host settles itself, returning the promise together with its resolve
        /// and reject functions.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>Resolve and reject may be called from another thread, and take a CLR value.</b> Settlement is
        /// enqueued safely and drains inline only when the calling thread can claim exclusive engine
        /// ownership; otherwise the owning host turn or a later <see cref="ProcessTasks"/> call drains it. The
        /// conversion of the value happens in that enqueued job — on the engine's thread — which is the
        /// reason the parameter is <see cref="object"/> and not <see cref="JsValue"/>: a host settling from a
        /// <see cref="System.Threading.Tasks.Task"/> continuation holds a CLR value and has nowhere safe to
        /// convert it, because a <see cref="JsValue"/> belongs to the engine that made it and that engine may
        /// be busy. Passing a <see cref="JsValue"/> is still correct and costs nothing extra, and passing
        /// <see langword="null"/> settles with <see cref="JsValue.Null"/>.
        /// </para>
        /// <para>
        /// A promise registered before an <see cref="AdvancedOperations.RestoreGlobalSnapshot"/> is dropped
        /// when it settles rather than resuming into the restored globals; register one that must outlive a
        /// restore after it.
        /// </para>
        /// <para>
        /// This is a low-level primitive — the supported way for host code to hand script a promise it
        /// settles itself — and it is an ordinary part of the public surface: unlike the diagnostics marked
        /// <c>JINT0001</c> (see <see cref="JintDiagnosticIds"/>), what it returns is a real capability rather
        /// than a report about an internal representation, so a change to it is a migration-guide row like
        /// any other. It carried an "EXPERIMENTAL! Subject to change" banner from before that distinction
        /// existed; the banner was removed rather than promoted, so that the word means one thing here.
        /// </para>
        /// </remarks>
        /// <returns>a Promise instance and functions to either resolve or reject it</returns>
        public ManualPromise RegisterPromise()
        {
            using var ownership = _engine.EnterHostCall();
            return _engine.RegisterPromise();
        }

        /// <summary>
        /// Event raised when a promise is rejected without a handler (operation = Reject),
        /// or when a handler is added to a previously unhandled rejected promise (operation = Handle).
        /// This implements the HostPromiseRejectionTracker abstract operation from the TC39 spec.
        /// </summary>
        /// <remarks>
        /// https://tc39.es/ecma262/#sec-host-promise-rejection-tracker
        /// </remarks>
        public event EventHandler<PromiseRejectionTrackerEventArgs>? PromiseRejectionTracker;

        internal void RaisePromiseRejectionTracker(JsPromise promise, PromiseRejectionOperation operation)
        {
            PromiseRejectionTracker?.Invoke(_engine, new PromiseRejectionTrackerEventArgs(promise, operation));
        }
    }
}

/// <summary>
/// Event arguments for the PromiseRejectionTracker event.
/// </summary>
public sealed class PromiseRejectionTrackerEventArgs : EventArgs
{
    /// <summary>
    /// The promise that triggered the rejection tracking.
    /// </summary>
    public JsValue Promise { get; }

    /// <summary>
    /// The rejection reason (only meaningful when Operation is Reject).
    /// </summary>
    public JsValue? Value { get; }

    /// <summary>
    /// Whether this is a new unhandled rejection ("Reject") or a previously
    /// unhandled rejection that now has a handler ("Handle").
    /// </summary>
    public PromiseRejectionOperation Operation { get; }

    internal PromiseRejectionTrackerEventArgs(JsPromise promise, PromiseRejectionOperation operation)
    {
        Promise = promise;
        Value = promise.State == PromiseState.Rejected ? promise.Value : null;
        Operation = operation;
    }
}
