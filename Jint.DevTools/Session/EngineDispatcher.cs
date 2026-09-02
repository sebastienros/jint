using System.Collections.Concurrent;
using Jint.DevTools.Protocol;

namespace Jint.DevTools.Session;

/// <summary>
/// One target's mailbox, and <b>the only path from a transport thread to the engine</b>.
/// </summary>
/// <remarks>
/// <para>
/// The mechanism the thread rule rests on. A transport thread has a protocol request and no right to touch
/// the engine, so it enqueues the work here and waits for the finished JSON;
/// <see cref="Engine.TaskOperations.Post(System.Action)"/> — the one engine entry a thread that does not own
/// the engine may call — wakes whichever thread is pumping, which runs <see cref="Drain"/> as an ordinary
/// event-loop job and answers there. The reply that crosses back is a <see langword="string"/>, which is the
/// invariant: no <c>JsValue</c> ever leaves the engine thread.
/// </para>
/// <para>
/// <b>The drain runs inside an event-loop job.</b> That is what makes a command interleave with microtasks
/// the way V8's does, and it is also what a command may not undo: nothing here may call
/// <see cref="Engine.TaskOperations.WaitForScheduledWork(System.TimeSpan, System.Threading.CancellationToken)"/>
/// or unwrap a promise by draining, because the pump's re-entrancy guard forbids it. A command that has to
/// wait for a promise attaches reactions and completes later, from the job that runs them.
/// </para>
/// <para>
/// <b>Host work and protocol commands share the queue</b>, so the order a host posted them in is the order
/// they run in — with one deliberate exception: while the target is waiting for a debugger, host work is
/// held and protocol commands are not, or the command that ends the wait could never be answered.
/// </para>
/// <para>
/// <b>A command that times out is answered, not cancelled.</b> The item stays queued and still runs when the
/// engine is next pumped; what the timeout decides is only that the client stops waiting. An engine nobody
/// pumps would otherwise hang every client that ever spoke to it.
/// </para>
/// </remarks>
internal sealed class EngineDispatcher : ICommandGateway
{
    private readonly Engine _engine;
    private readonly Action _drain;
    private readonly ConcurrentQueue<CommandItem> _commands = new();
    private readonly ConcurrentQueue<Action<Engine>> _hostWork = new();

    private int _waitingForDebugger;

    internal EngineDispatcher(Engine engine, TimeSpan commandTimeout, bool waitForDebuggerOnStart)
    {
        _engine = engine;
        _drain = Drain;
        CommandTimeout = commandTimeout;
        _waitingForDebugger = waitForDebuggerOnStart ? 1 : 0;
    }

    /// <summary>
    /// Gets or sets how long a client waits for one command before it is told the engine is not pumped.
    /// </summary>
    /// <remarks>
    /// Settable because a target may exist before the server it is added to, and the bound is the server's
    /// configuration rather than the target's.
    /// </remarks>
    internal TimeSpan CommandTimeout { get; set; }

    /// <summary>Gets whether host work is still held for a client that has not said to run it.</summary>
    internal bool IsWaitingForDebugger => Volatile.Read(ref _waitingForDebugger) != 0;

    /// <summary>
    /// Raised on the engine thread the first time the wait for a debugger ends, so a host-owned target can
    /// stop pumping for it.
    /// </summary>
    internal event Action? DebuggerWaitEnded;

    /// <inheritdoc/>
    public async ValueTask<string> DispatchAsync(DevToolsSession session, ProtocolRequest request, CommandContext context)
    {
        // The parameters are cloned out of the caller's JsonDocument, and that is not a micro-optimisation
        // in reverse: a command that times out is answered while it is still queued, the caller then
        // disposes its document -- returning its buffers to the pool -- and the engine thread would later
        // read memory somebody else owns. A clone is backed by a document of its own.
        var item = new CommandItem(session, request with { Parameters = request.Parameters?.Clone() }, context);
        _commands.Enqueue(item);
        ScheduleDrain();

        try
        {
            return await item.Completion.Task.WaitAsync(CommandTimeout, context.CancellationToken).ConfigureAwait(false);
        }
        catch (TimeoutException)
        {
            // Told apart because the two are fixed differently, and a host reading the wrong one debugs the
            // wrong thing: an item nothing ever dequeued means no thread is pumping, while one that started
            // and did not finish means the command itself is slow or the script it ran has not returned.
            return item.HasStarted
                ? Throw.ServerError<string>(
                    "Command timed out",
                    "the engine began answering the command and had not finished within the command timeout")
                : Throw.ServerError<string>(
                    "Engine is not being pumped",
                    "no thread ran the engine's event loop within the command timeout; call engine.Tasks.ProcessTasks() or EngineTarget.Pump(), or give the target ThreadMode.LibraryOwned");
        }
    }

    /// <summary>Queues host work to run on the engine thread, from any thread.</summary>
    internal void Post(Action<Engine> work)
    {
        _hostWork.Enqueue(work);
        ScheduleDrain();
    }

    /// <summary>
    /// Ends the wait for a debugger, releasing whatever host work was held. Idempotent, and a no-op on a
    /// target that was never waiting.
    /// </summary>
    internal void ReleaseDebuggerWait()
    {
        if (Interlocked.Exchange(ref _waitingForDebugger, 0) == 0)
        {
            return;
        }

        DebuggerWaitEnded?.Invoke();
        ScheduleDrain();
    }

    /// <summary>
    /// Runs everything queued, on the engine thread.
    /// </summary>
    /// <remarks>
    /// Called as the event-loop job <see cref="Engine.TaskOperations.Post(System.Action)"/> queued, and
    /// directly by <see cref="EngineTarget.Pump"/> and the library-owned loop. It drains everything rather
    /// than one item, so the extra passes an unconditional post produces cost a dequeue that finds nothing.
    /// </remarks>
    internal void Drain()
    {
        while (_commands.TryDequeue(out var item))
        {
            item.Run();
        }

        if (IsWaitingForDebugger)
        {
            return;
        }

        while (_hostWork.TryDequeue(out var work))
        {
            work(_engine);
        }
    }

    /// <summary>
    /// Wakes whichever thread pumps the engine, by queueing the drain as an ordinary event-loop job.
    /// </summary>
    /// <remarks>
    /// One post per arrival, deliberately, rather than one per batch behind a "already scheduled" flag. Such
    /// a flag is set before the post and cleared by the drain, so a job the engine <i>drops</i> — which
    /// <see cref="Engine.AdvancedOperations.RestoreGlobalSnapshot"/> does to every job of the cycle it ends —
    /// would leave it set for ever and every later arrival would decline to post, silently killing the
    /// mailbox for a host that pumps through <see cref="Engine.TaskOperations.ProcessTasks"/> alone. Protocol
    /// traffic is human-scale; a concurrent enqueue per message is not worth that.
    /// </remarks>
    private void ScheduleDrain()
    {
        _engine.Tasks.Post(_drain);
    }

    /// <summary>
    /// One queued command: what to answer, and the promise the transport thread is waiting on.
    /// </summary>
    /// <remarks>
    /// The completion source runs its continuations asynchronously, deliberately: completing it inline would
    /// resume the transport thread's <c>await</c> <i>on the engine thread</i>, which is the one thing the
    /// whole design exists to prevent.
    /// </remarks>
    private sealed class CommandItem
    {
        private readonly DevToolsSession _session;
        private readonly ProtocolRequest _request;
        private readonly CommandContext _context;
        private int _started;

        internal CommandItem(DevToolsSession session, ProtocolRequest request, CommandContext context)
        {
            _session = session;
            _request = request;
            _context = context;
        }

        internal TaskCompletionSource<string> Completion { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        /// <summary>Whether the engine thread has begun answering this command.</summary>
        internal bool HasStarted => Volatile.Read(ref _started) != 0;

        internal void Run()
        {
            Volatile.Write(ref _started, 1);

            try
            {
                var pending = _session.DispatchAsync(in _request, _context);
                if (pending.IsCompletedSuccessfully)
                {
                    Completion.TrySetResult(pending.Result);
                    return;
                }

                // A command that answers later — Runtime.evaluate awaiting a promise — completes from the
                // job that runs its reactions, which is this same thread. What crosses back is still only a
                // string, so finishing the continuation inline is safe and saves a hop.
                pending.AsTask().ContinueWith(
                    static (task, state) => Complete(task, (TaskCompletionSource<string>) state!),
                    Completion,
                    CancellationToken.None,
                    TaskContinuationOptions.ExecuteSynchronously,
                    TaskScheduler.Default);
            }
            catch (Exception exception)
            {
                // Everything the drain runs has to come back as a completion rather than as a throw: this
                // runs inside an event-loop job, and an exception here erupts out of the host's pump.
                Completion.TrySetException(exception);
            }
        }

        private static void Complete(Task<string> task, TaskCompletionSource<string> completion)
        {
            if (task.IsFaulted)
            {
                completion.TrySetException(task.Exception!.InnerExceptions);
            }
            else if (task.IsCanceled)
            {
                completion.TrySetCanceled();
            }
            else
            {
                completion.TrySetResult(task.Result);
            }
        }
    }
}
