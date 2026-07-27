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
    /// Tracks the thread ID of the thread that is currently waiting on a promise.
    /// Only this thread (or any thread if -1) is allowed to process continuations.
    /// This prevents background threads (e.g., Task.ContinueWith callbacks) from
    /// executing JavaScript code on the Engine.
    /// </summary>
    internal volatile int _waitingThreadId = -1;

    /// <summary>
    /// Async wake signals registered by callers of <see cref="WaitForEventAsync"/>.
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
            while (_events.TryDequeue(out var job))
            {
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
