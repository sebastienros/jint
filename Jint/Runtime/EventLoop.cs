using System.Collections.Concurrent;
using System.Threading;
using Jint.Native;
using Jint.Native.Promise;

namespace Jint.Runtime;

/// <summary>
/// A single queued event-loop entry: either an opaque <see cref="Action"/> continuation or a
/// promise reaction job carried as its (reaction, argument) pair so that enqueueing a reaction
/// does not allocate a closure per job.
/// </summary>
internal readonly struct EventLoopJob
{
    private readonly object _state;
    private readonly JsValue? _argument;

    /// <summary>
    /// The <see cref="EventLoop.Generation"/> this job belongs to. A job whose generation is no longer the
    /// loop's current one belongs to an evaluation cycle that has since been ended by
    /// <see cref="Engine.AdvancedOperations.RestoreGlobalSnapshot"/>, and is dropped rather than run — see
    /// <see cref="EventLoop.RunAvailableContinuations"/>.
    /// </summary>
    private readonly int _generation;

    public EventLoopJob(Action continuation, int generation)
    {
        _state = continuation;
        _argument = null;
        _generation = generation;
    }

    public EventLoopJob(PromiseReaction reaction, JsValue argument, int generation)
    {
        _state = reaction;
        _argument = argument;
        _generation = generation;
    }

    public int Generation => _generation;

    public void Run(Engine engine)
    {
        if (_state is PromiseReaction reaction)
        {
            PromiseOperations.RunReactionJob(engine, reaction, _argument!);
        }
        else
        {
            ((Action) _state)();
        }
    }
}

internal sealed record EventLoop
{
    private readonly ConcurrentQueue<EventLoopJob> _events = new();

    /// <summary>
    /// Tracks whether we are currently processing the event loop.
    /// Uses Interlocked.CompareExchange for atomic check-then-set to prevent
    /// TOCTOU race conditions between checking and setting.
    /// 0 = not processing, 1 = processing.
    /// </summary>
    private int _isProcessing;

    /// <summary>
    /// Whether a dequeued job is currently executing. While one is, an exception escaping it has no
    /// caller left to catch it — it erupts out of whatever host call happened to be draining the loop —
    /// so code that can fail from inside a job consults this to decide between throwing to its caller
    /// and settling the failure into the operation it belongs to.
    /// </summary>
    internal bool IsRunningJob => Volatile.Read(ref _isProcessing) == 1;

    /// <summary>
    /// Tracks the thread ID of the thread that is currently waiting on a promise.
    /// Only this thread (or any thread if -1) is allowed to process continuations.
    /// This prevents background threads (e.g., Task.ContinueWith callbacks) from
    /// executing JavaScript code on the Engine.
    /// </summary>
    internal volatile int _waitingThreadId = -1;

    /// <summary>
    /// Async wake signals registered by callers of <see cref="WaitForEventAsync(CancellationToken)"/>.
    /// Each call appends its own TCS so that <see cref="Enqueue(in EventLoopJob)"/> can wake every
    /// outstanding waiter — supporting concurrent awaiters on a single engine
    /// (e.g. a caller that <c>await</c>s two engine-internal promises in parallel
    /// via <c>Task.WhenAll</c>). Replaces an earlier single-field design that
    /// silently dropped the second waiter and could spin until the first one
    /// happened to resume.
    /// </summary>
    private readonly Lock _waitersLock = new();
    private List<TaskCompletionSource<bool>>? _waiters;

    /// <summary>
    /// The synchronous counterpart of the async waiters above: set on every <see cref="Enqueue(in EventLoopJob)"/>
    /// so a thread blocked in <see cref="WaitForWork"/> — the drain behind a synchronous
    /// <c>Engine.Modules.Import</c> waiting out an asynchronous load — wakes when the settle arrives instead
    /// of running out its poll interval. Lazily allocated exactly like <see cref="JsPromise.CompletedEvent"/>,
    /// and for the same reason: most engines never block-drain, so they never pay for the primitive. Reset
    /// only by the single draining thread, inside <see cref="WaitForWork"/>.
    /// </summary>
    private ManualResetEventSlim? _workArrived;

    private ManualResetEventSlim WorkArrivedEvent
    {
        get
        {
            if (_workArrived is not null)
            {
                return _workArrived;
            }

            var newEvent = new ManualResetEventSlim(false);
            var existing = Interlocked.CompareExchange(ref _workArrived, newEvent, null);
            if (existing is not null)
            {
                newEvent.Dispose();
                return existing;
            }

            return newEvent;
        }
    }

    /// <summary>
    /// The current evaluation cycle. Every job carries the generation that was current when the work was
    /// <em>registered</em>, and <see cref="RunAvailableContinuations"/> drops any job whose generation has
    /// since moved on. <see cref="Engine.AdvancedOperations.RestoreGlobalSnapshot"/> advances it, which is
    /// what makes the discard a fence rather than a point-in-time flush: <see cref="Clear"/> can only throw
    /// away what is already queued, and a host <c>Task</c> started by the previous cycle enqueues its settle
    /// job whenever it happens to complete — possibly long after the restore.
    /// </summary>
    private int _generation;

    internal int Generation => Volatile.Read(ref _generation);

    /// <summary>
    /// Ends the current cycle: work registered before this call no longer belongs to the engine's timeline
    /// and is dropped at dequeue. Stamping happens at registration and the check happens at dequeue, and
    /// both run on the engine thread, so there is no window in which a settle enqueued by a background
    /// thread can be mistaken for current work.
    /// </summary>
    internal void NextGeneration() => Interlocked.Increment(ref _generation);

    public bool IsEmpty => _events.IsEmpty;

    public void Enqueue(Action continuation) => Enqueue(new EventLoopJob(continuation, Generation));

    public void Enqueue(in EventLoopJob job)
    {
        _events.Enqueue(job);

        // Null means no thread has ever block-drained this engine, so there is nobody to wake. The enqueue
        // above and WaitForWork's event-creation are both full fences, so whichever of the two raced ahead,
        // either this read sees the event or the waiter's queue check sees the job.
        Volatile.Read(ref _workArrived)?.Set();

        // Wake every registered async waiter. Each one re-checks its own promise
        // state on resume, so spurious wakes loop harmlessly back to WaitForEventAsync.
        List<TaskCompletionSource<bool>>? toSignal = null;
        lock (_waitersLock)
        {
            if (_waiters is { Count: > 0 })
            {
                toSignal = _waiters;
                _waiters = null;
            }
        }

        if (toSignal is not null)
        {
            for (var i = 0; i < toSignal.Count; i++)
            {
                toSignal[i].TrySetResult(true);
            }
        }
    }

    /// <summary>
    /// Blocks until new work is enqueued, <paramref name="completedEvent"/> is signaled, cancellation is
    /// requested, or <paramref name="timeout"/> elapses — whichever comes first. The synchronous sibling of
    /// <see cref="WaitForEventAsync(CancellationToken)"/>, used by the drain loops that hold the calling
    /// thread. Only the single draining thread may call this, because it resets <see cref="_workArrived"/>.
    /// </summary>
    /// <remarks>
    /// The reset-then-check order closes the race with a producer: an enqueue before the reset is seen by the
    /// queue check, an enqueue after it sets the event the wait is about to block on. A caller must treat a
    /// return as a hint and re-check its own condition — like the async waiters, spurious wakes are expected.
    /// </remarks>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> was canceled.</exception>
    internal void WaitForWork(ManualResetEventSlim? completedEvent, TimeSpan timeout, CancellationToken cancellationToken)
    {
        var workArrived = WorkArrivedEvent;

        workArrived.Reset();

        // A non-empty queue only ends the wait when this thread can actually drain it. Nested inside a job
        // (IsRunningJob), the re-entrancy guard makes queued jobs unrunnable from here, and returning early
        // for them would turn this bounded wait into a hot spin for the caller's whole timeout.
        if ((!_events.IsEmpty && !IsRunningJob) || completedEvent?.IsSet == true)
        {
            return;
        }

        // Waiting on the work signal alone is deliberate: with this thread registered as the drainer,
        // nothing but a job it runs can advance the condition, and every settle path — a Task continuation,
        // a module load completion, a setTimeout — announces itself through Enqueue. The bounded timeout
        // keeps the old poll cadence as the backstop for anything that does not.
        workArrived.Wait(timeout, cancellationToken);
    }

    /// <summary>
    /// Waits asynchronously for events to be enqueued, releasing the current thread.
    /// Used by the async API path (EvaluateAsync/ExecuteAsync/InvokeAsync) to avoid
    /// blocking a thread during IO-bound operations. During the wait, zero threads
    /// are consumed. Multiple concurrent waiters are supported — each registers its
    /// own TCS and is signaled on every <see cref="Enqueue(in EventLoopJob)"/>.
    /// </summary>
    /// <param name="cancellationToken">Token to observe for cancellation.</param>
    /// <returns>A task that completes when events are available or cancellation is requested.</returns>
    public Task WaitForEventAsync(CancellationToken cancellationToken)
    {
        // Fast path: already have events queued
        if (!_events.IsEmpty)
        {
            return Task.CompletedTask;
        }

        var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        lock (_waitersLock)
        {
            (_waiters ??= new List<TaskCompletionSource<bool>>()).Add(tcs);
        }

        // Double-check after registration to close the race window where an event
        // was enqueued between our IsEmpty check and the list insertion. Self-signal
        // rather than removing from the list — Enqueue's broadcast TrySetResult on
        // an already-completed TCS is a no-op, so leaving the entry costs nothing
        // beyond a single GC root until the next Enqueue clears the list.
        if (!_events.IsEmpty)
        {
            tcs.TrySetResult(true);
            return tcs.Task;
        }

        if (cancellationToken.CanBeCanceled)
        {
            var ctr = cancellationToken.Register(static state => ((TaskCompletionSource<bool>) state!).TrySetCanceled(), tcs);
            _ = tcs.Task.ContinueWith(static (_, state) => ((CancellationTokenRegistration) state!).Dispose(), ctr, TaskScheduler.Default);
        }

        return tcs.Task;
    }

    /// <summary>
    /// The bounded form of <see cref="WaitForEventAsync(CancellationToken)"/>: also completes once
    /// <paramref name="timeout"/> has elapsed. Needed because a timer coming due enqueues nothing and so
    /// wakes nobody — the only thing that knows it is time to pump again is the clock.
    /// </summary>
    /// <remarks>
    /// One <see cref="Task.Delay(TimeSpan, CancellationToken)"/> per idle wait, and only while a timer is
    /// actually pending; an engine with no timers keeps taking the unbounded overload and allocates nothing
    /// extra. If the delay wins the race, the wake registration the wait made stays in the waiter list until
    /// the next <see cref="Enqueue(in EventLoopJob)"/> clears it. That is deliberately harmless: the
    /// broadcast completes an already-completed or abandoned <see cref="TaskCompletionSource{TResult}"/> as
    /// a no-op, so a stale entry costs one GC root and nothing else — the same trade the double-check in the
    /// unbounded overload already makes.
    /// </remarks>
    /// <param name="timeout">How long to wait at most.</param>
    /// <param name="cancellationToken">Token to observe for cancellation.</param>
    public async Task WaitForEventAsync(TimeSpan timeout, CancellationToken cancellationToken)
    {
        var waitTask = WaitForEventAsync(cancellationToken);
        if (waitTask.IsCompleted)
        {
            return;
        }

        // Neither task is awaited directly, so neither can throw here: cancellation of either one leaves a
        // cancelled task that WhenAny simply reports as the winner, and the caller re-checks its own
        // condition and its own token on resume, exactly as it does after an unbounded wait.
        await Task.WhenAny(waitTask, Task.Delay(timeout, cancellationToken)).ConfigureAwait(false);
    }

    /// <summary>
    /// Discards every currently queued job without running it. Used by
    /// <see cref="Engine.AdvancedOperations.RestoreGlobalSnapshot"/>: a reaction left behind by an
    /// evaluation's unsettled promise would otherwise run during the next evaluation's drain, against
    /// globals it was never meant to see. This only reaches what is already queued — the fence for work
    /// that arrives later is <see cref="NextGeneration"/>.
    /// </summary>
    internal void Clear()
    {
        while (_events.TryDequeue(out _))
        {
        }
    }

    public void RunAvailableContinuations(Engine engine)
    {
        // If there's a waiting thread (e.g., in UnwrapIfPromise), only that thread
        // should execute continuations. This prevents background threads (from Task
        // completions) from executing JavaScript on the Engine.
        var waitingThreadId = _waitingThreadId;
        if (waitingThreadId != -1 && Environment.CurrentManagedThreadId != waitingThreadId)
        {
            return;
        }

        // Atomically check and set _isProcessing to prevent re-entrant calls
        // which can cause stack overflow. If we're already processing, the outer
        // loop will handle any new events.
        if (Interlocked.CompareExchange(ref _isProcessing, 1, 0) != 0)
        {
            return;
        }

        try
        {
            while (true)
            {
                if (!_events.TryDequeue(out var job))
                {
#if NET8_0_OR_GREATER
                    // The queue is empty, so this is the moment — and the only moment — a timer may join it.
                    // Promoting exactly one due timer per exhaustion is what makes this single queue behave as
                    // the microtask queue HTML specifies: everything a job queues, transitively, runs before
                    // the next timer is even looked at, so Promise.resolve().then(f) beats setTimeout(g, 0)
                    // and a chain of reactions can never be starved by a due interval. The check costs one
                    // predictable null test per drain on an engine without timers, never one per job.
                    if (engine.TryPromoteDueTimerJob())
                    {
                        continue;
                    }
#endif

                    break;
                }

                // Work registered before a RestoreGlobalSnapshot belongs to a cycle the engine has ended.
                // Running it would resume a previous evaluation's continuation against the restored global
                // surface — the cross-cycle channel a fresh-engine-per-evaluation host never had. Dropping
                // it here rather than refusing the enqueue is what makes the check race-free: a background
                // Task completion can enqueue at any moment, but only the engine thread dequeues.
                // Re-read per job rather than once per drain: a job is free to call back into host code
                // that restores a snapshot, and everything queued behind it is then stale too.
                if (job.Generation != Generation)
                {
                    continue;
                }

                // note that a job can enqueue new events
                job.Run(engine);
            }
        }
        finally
        {
            Interlocked.Exchange(ref _isProcessing, 0);
        }
    }
}
