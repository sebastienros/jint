using System.Collections.Concurrent;
using System.Threading;

namespace Jint.Runtime;

internal sealed record EventLoop
{
    private readonly ConcurrentQueue<Action> _events = new();

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
    /// Each call appends its own TCS so that <see cref="Enqueue"/> can wake every
    /// outstanding waiter — supporting concurrent awaiters on a single engine
    /// (e.g. a caller that <c>await</c>s two engine-internal promises in parallel
    /// via <c>Task.WhenAll</c>). Replaces an earlier single-field design that
    /// silently dropped the second waiter and could spin until the first one
    /// happened to resume.
    /// </summary>
    private readonly Lock _waitersLock = new();
    private List<TaskCompletionSource<bool>>? _waiters;

    public bool IsEmpty => _events.IsEmpty;

    public void Enqueue(Action continuation)
    {
        _events.Enqueue(continuation);

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
    /// own TCS and is signaled on every <see cref="Enqueue"/>.
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

    public void RunAvailableContinuations()
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
            while (_events.TryDequeue(out var nextContinuation))
            {
                // note that continuation can enqueue new events
                nextContinuation();
            }
        }
        finally
        {
            Interlocked.Exchange(ref _isProcessing, 0);
        }
    }
}
