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
/// <para>
/// <b>There is a second drain, and it exists because the first one cannot run while the engine is paused.</b>
/// A debugger pause blocks the engine thread inside <c>DebugHandler</c>'s handler, so nothing posted through
/// <see cref="Engine.TaskOperations.Post(System.Action)"/> will ever run: the pump is that very thread.
/// <see cref="DrainPaused"/> is what the pause loop calls instead — the same queue, answered inline on the
/// thread that is already holding it — and <see cref="Wake"/> is what tells that loop something arrived.
/// </para>
/// </remarks>
internal sealed class EngineDispatcher : ICommandGateway, IDisposable
{
    private readonly Engine _engine;
    private readonly Action _drain;
    private readonly ConcurrentQueue<CommandItem> _commands = new();
    private readonly ConcurrentQueue<Action<Engine>> _hostWork = new();
    private readonly ManualResetEventSlim _arrivals = new(initialState: false);

    private int _waitingForDebugger;
    private int _draining;

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
    /// <para>
    /// Called as the event-loop job <see cref="Engine.TaskOperations.Post(System.Action)"/> queued, and
    /// directly by <see cref="EngineTarget.Pump"/> and the library-owned loop. It drains everything rather
    /// than one item, so the extra passes an unconditional post produces cost a dequeue that finds nothing.
    /// </para>
    /// <para>
    /// <b>It never re-enters itself.</b> A command runs script, script pauses, the pause loop answers a
    /// command that runs more script — and a second running-mode drain anywhere down that stack would run
    /// host work in the middle of a paused engine and dequeue items the loop above it is still answering.
    /// The pause loop's own <see cref="DrainPaused"/> is deliberately exempt: nesting <i>it</i> inside this
    /// one is the whole mechanism.
    /// </para>
    /// </remarks>
    internal void Drain()
    {
        if (Interlocked.Exchange(ref _draining, 1) != 0)
        {
            return;
        }

        try
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
        finally
        {
            Volatile.Write(ref _draining, 0);
        }
    }

    /// <summary>
    /// Answers the commands waiting for an engine that is paused in the debugger, inline on the engine's own
    /// thread.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Host work is not drained.</b> A host's <c>Post</c> is work for a running engine; running it from
    /// inside a paused one would execute script at a point the client is looking at. It waits for the resume,
    /// which is the whole difference between this and <see cref="Drain"/>.
    /// </para>
    /// <para>
    /// The two commands that are refused are the two that would re-enter a public engine entry while the
    /// engine is suspended part-way through a statement. Everything else — every <c>Runtime</c> read, every
    /// <c>Debugger</c> command, the browser-level domains — is answered, because a client that serializes on
    /// one of them would otherwise deadlock against its own pause.
    /// </para>
    /// </remarks>
    internal void DrainPaused()
    {
        while (_commands.TryDequeue(out var item))
        {
            if (IsPauseSafe(item.Method))
            {
                item.Run();
            }
            else
            {
                item.Refuse();
            }
        }
    }

    /// <summary>Tells a waiting pause loop that something arrived for it.</summary>
    internal void Wake() => Set();

    /// <summary>Clears the arrival flag, which a pause loop does before it drains.</summary>
    /// <remarks>
    /// Reset first, then drain: an arrival <i>during</i> the drain sets the flag again, so the wait that
    /// follows returns at once rather than parking on work already queued.
    /// </remarks>
    internal void ResetWake()
    {
        try
        {
            _arrivals.Reset();
        }
        catch (ObjectDisposedException)
        {
            // The target is going away underneath the pause loop, which resumes on the next pass.
        }
    }

    /// <summary>Parks the pause loop until something arrives, the bound elapses, or the target stops.</summary>
    internal void WaitForWake(TimeSpan timeout, CancellationToken cancellationToken)
    {
        try
        {
            _arrivals.Wait(timeout, cancellationToken);
        }
        catch (OperationCanceledException)
        {
        }
        catch (ObjectDisposedException)
        {
        }
    }

    /// <inheritdoc/>
    public void Dispose() => _arrivals.Dispose();

    /// <summary>
    /// Whether a command may be answered while the engine is paused.
    /// </summary>
    /// <remarks>
    /// <c>Runtime.runScript</c> calls <c>Engine.Evaluate</c>, a public entry, on
    /// an engine that is suspended inside a statement of another one; <c>Profiler</c> starts and stops a
    /// recording around the same. Both are answered with a reason rather than run.
    /// </remarks>
    private static bool IsPauseSafe(string method)
    {
        return !string.Equals(method, "Runtime.runScript", StringComparison.Ordinal)
            && !method.StartsWith("Profiler.", StringComparison.Ordinal);
    }

    private void Set()
    {
        try
        {
            _arrivals.Set();
        }
        catch (ObjectDisposedException)
        {
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

        // The post above wakes whichever thread pumps the engine, and a paused engine has no such thread:
        // it is inside the debugger's handler, running a loop of its own. This is what that loop waits on.
        Set();
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

        /// <summary>Gets the qualified method the client asked for, which is what a paused drain filters on.</summary>
        internal string Method => _request.Method;

        /// <summary>Answers a command the engine may not run in the state it is in.</summary>
        internal void Refuse()
        {
            Volatile.Write(ref _started, 1);
            Completion.TrySetException(new ProtocolException(
                ProtocolErrorCodes.ServerError,
                "Not allowed while paused",
                "the command re-enters a public engine entry, and the engine is suspended inside the debugger; resume first"));
        }

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
