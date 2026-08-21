#if NET8_0_OR_GREATER
using System.Threading;

namespace Jint.WebApi;

/// <summary>
/// One live parent↔worker pair, as the host sees it: the two engines, whether it is still running, and the
/// host's own <c>terminate()</c>. Requires .NET 8 or higher.
/// </summary>
/// <remarks>
/// <para>
/// <b>Fully thread-safe, deliberately.</b> A connection is created on the parent's thread and then observed
/// from at least two: the parent's script may <c>terminate()</c> it while the worker's own thread is inside
/// <c>ProcessTasks</c>, and a worker's <c>close()</c> ends it from there. Every property may be read from any
/// thread, and <see cref="End"/> may be called from any thread and is idempotent under <i>concurrent</i>
/// callers, not merely repeated ones.
/// </para>
/// <para>
/// What makes that affordable is how little ending does: it closes port endpoints, cancels a
/// <see cref="CancellationTokenSource"/> and writes interlocked bookkeeping. It never enters either engine —
/// no <c>Dispose</c>, no <c>ProcessTasks</c>, no <c>Constraints</c>, no <c>RestoreGlobalSnapshot</c> — because
/// the only part of an engine another thread may touch is its event-loop queue. That is also the obligation it
/// passes on to the host: see <see cref="WorkerProvider.OnWorkerEnded"/>.
/// </para>
/// <para>
/// Ending a connection does <b>not</b> dispose the worker engine. The engine does not own it — the host built
/// it and the host pumps it — so disposal belongs to the thread that was pumping, once its loop has observed
/// <see cref="IsEnded"/>.
/// </para>
/// </remarks>
public sealed class WorkerConnection
{
    private readonly Lock _lock = new();

    /// <summary>
    /// What the engine does when the connection ends: close the endpoints, cancel the termination source, tell
    /// the provider. Supplied by the engine wiring, and invoked at most once, outside <see cref="_lock"/>.
    /// </summary>
    private readonly Action<WorkerEndReason>? _onEnded;

    /// <summary>
    /// Written last inside <see cref="_lock"/>, so a thread that reads <see langword="true"/> — from any
    /// thread, without the lock — also sees <see cref="_endReason"/> and <see cref="_error"/>. The volatile
    /// write is the release, the volatile read the acquire.
    /// </summary>
    private volatile bool _ended;

    private WorkerEndReason _endReason;
    private Exception? _error;

    /// <summary>
    /// Read and written through <see cref="Volatile"/> because a host commonly writes it on the parent's thread
    /// in <see cref="WorkerProvider.OnWorkerStarted"/> and reads it from the thread that pumps.
    /// </summary>
    private object? _hostState;

    internal WorkerConnection(
        Engine parent,
        Engine worker,
        string name,
        Action<WorkerEndReason>? onEnded,
        CancellationToken terminationToken)
    {
        Parent = parent;
        Worker = worker;
        Name = name;
        TerminationToken = terminationToken;
        _onEnded = onEnded;
    }

    /// <summary>
    /// The engine that created the worker.
    /// </summary>
    public Engine Parent { get; }

    /// <summary>
    /// The engine that runs the worker — the one the host pumps, and the one the host disposes after its pump
    /// loop has left.
    /// </summary>
    public Engine Worker { get; }

    /// <summary>
    /// The <c>name</c> option the worker was created with, or the empty string.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Cancelled when anything but the worker itself ends the connection. Safe to read and to wait on from
    /// any thread.
    /// </summary>
    /// <remarks>
    /// This is what a pump loop parks on, so that ending the connection from <i>another</i> thread — a
    /// <c>terminate()</c>, a restore, <see cref="End"/> — wakes it rather than leaving it asleep until its
    /// own ceiling elapses. The one end that does not cancel is the worker's own <c>close()</c>: its teardown
    /// runs on the pumping thread itself, which therefore observes <see cref="IsEnded"/> on its very next
    /// loop iteration with nothing to be woken from.
    /// </remarks>
    public CancellationToken TerminationToken { get; }

    /// <summary>
    /// Whether the connection has ended. Safe to read from any thread, and the flag a pump loop keys on.
    /// </summary>
    /// <remarks>
    /// Once <see langword="true"/> it never returns to <see langword="false"/>, and everything else this class
    /// reports is settled: a thread that observes it also observes <see cref="EndReason"/>,
    /// <see cref="IsFaulted"/> and <see cref="Error"/>.
    /// </remarks>
    public bool IsEnded => _ended;

    /// <summary>
    /// Why the connection ended, or <see langword="null"/> while it is still live.
    /// </summary>
    public WorkerEndReason? EndReason => _ended ? _endReason : null;

    /// <summary>
    /// Whether the connection ended because something failed, as opposed to somebody ending it. Set together
    /// with <see cref="Error"/>, and today only for <see cref="WorkerEndReason.StartupFailed"/>.
    /// </summary>
    public bool IsFaulted => _ended && _error is not null;

    /// <summary>
    /// The failure behind <see cref="IsFaulted"/>, or <see langword="null"/>.
    /// </summary>
    /// <remarks>
    /// <b>Always a CLR exception, never a worker-realm <c>JsValue</c></b> — a module resolution failure, or a
    /// summary exception carrying the message and location the worker reported. A <c>JsValue</c> belongs to
    /// the engine that made it and may not be touched from another thread, which is also why the <c>error</c>
    /// event the parent's script sees carries <c>error: null</c>. This property is the host's channel and is
    /// filled in without a diagnostics sink being wired at all.
    /// </remarks>
    public Exception? Error => _ended ? _error : null;

    /// <summary>
    /// Per-connection bookkeeping of the host's own — a pump thread, a wait handle, the
    /// <c>OperationDeadlineConstraint</c> that slices a cooperative loop. <b>The engine never reads it.</b>
    /// </summary>
    /// <remarks>
    /// Safe to read and write from any thread, and published so that a value written on the parent's thread
    /// before the pump starts is visible to the thread that pumps. It is not a synchronization primitive: two
    /// threads racing to write it still race.
    /// </remarks>
    public object? HostState
    {
        get => Volatile.Read(ref _hostState);
        set => Volatile.Write(ref _hostState, value);
    }

    /// <summary>
    /// The host's own <c>terminate()</c>: ends the connection, from any thread, exactly once.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Identical in effect to the script's <c>worker.terminate()</c> — the ports close in both directions, the
    /// termination token is cancelled, and <see cref="WorkerProvider.OnWorkerEnded"/> is invoked with
    /// <see cref="WorkerEndReason.Terminated"/>. Calling it on a connection that has already ended does
    /// nothing at all, including when two threads call it at the same instant: one of them ends the
    /// connection and the other returns.
    /// </para>
    /// <para>
    /// It does not dispose <see cref="Worker"/>, and must not be called from
    /// <see cref="WorkerProvider.OnWorkerEnded"/>'s own stack for a reason that is merely economy — by then it
    /// has already happened.
    /// </para>
    /// </remarks>
    public void End() => TryEnd(WorkerEndReason.Terminated, error: null);

    /// <summary>
    /// The engine-side entry: ends the connection with a reason of its own and, for a failure, the CLR
    /// exception behind it.
    /// </summary>
    /// <returns>
    /// <see langword="true"/> when this call is the one that ended it — the caller is then the only thread
    /// running the end sequence — and <see langword="false"/> when it had already ended.
    /// </returns>
    internal bool TryEnd(WorkerEndReason reason, Exception? error)
    {
        lock (_lock)
        {
            if (_ended)
            {
                return false;
            }

            _endReason = reason;
            _error = error;

            // Last, and volatile: it is the release that publishes the two fields above to every thread that
            // reads IsEnded without taking the lock.
            _ended = true;
        }

        // Outside the lock, always: this closes endpoints and calls into the host's OnWorkerEnded, and nothing
        // that can run host code may run while a lock of this feature's is held.
        _onEnded?.Invoke(reason);
        return true;
    }
}

/// <summary>
/// Why a <see cref="WorkerConnection"/> ended. Requires .NET 8 or higher.
/// </summary>
public enum WorkerEndReason
{
    /// <summary>
    /// <c>worker.terminate()</c> from the parent's script, or <see cref="WorkerConnection.End"/> from the
    /// host. Both directions stop immediately: the ports close, whatever the parent had already posted is
    /// discarded, and the worker's own script is cancelled within the engine's amortized check interval.
    /// </summary>
    Terminated,

    /// <summary>
    /// <c>close()</c> from inside the worker. Unlike <see cref="Terminated"/> this lets the turn that called
    /// it run to completion and lets a message it already posted reach the parent — the standard's <i>close a
    /// worker</i> discards the worker's own queued tasks and sets the closing flag, and pointedly does not
    /// abort the running script or empty the parent-side queue.
    /// </summary>
    ClosedByWorker,

    /// <summary>
    /// The worker's module never ran: the specifier did not resolve, the fetch failed, or the graph did not
    /// instantiate. <see cref="WorkerConnection.IsFaulted"/> and <see cref="WorkerConnection.Error"/> carry
    /// the failure, so a host sees a startup failure without wiring anything. Reported as its own reason
    /// because "the specifier was wrong" logged as "somebody called terminate()" is the most misleading line
    /// this feature could ship.
    /// </summary>
    StartupFailed,

    /// <summary>
    /// <c>RestoreGlobalSnapshot</c> on the parent engine. The <c>Worker</c> object stays alive but inert.
    /// </summary>
    ParentRestored,

    /// <summary>
    /// <c>RestoreGlobalSnapshot</c> on the worker engine.
    /// </summary>
    WorkerRestored,

    /// <summary>
    /// <c>Dispose()</c> on the parent engine.
    /// </summary>
    ParentDisposed,

    /// <summary>
    /// <c>Dispose()</c> on the worker engine — the host disposing what it built. The parent's side ends too
    /// rather than staying a <c>Worker</c> object that looks alive while every <c>postMessage</c> pays a full
    /// serialization into a queue nothing will ever drain.
    /// </summary>
    WorkerDisposed,
}
#endif
