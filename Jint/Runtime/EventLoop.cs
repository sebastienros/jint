using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

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
    /// Async wake signal used by <see cref="WaitForEventAsync"/> to release the thread
    /// while waiting for events. Set by <see cref="Enqueue"/> when new work arrives.
    /// Uses TaskCompletionSource&lt;bool&gt; for compatibility with netstandard2.0/net462.
    /// </summary>
    private volatile TaskCompletionSource<bool>? _eventAvailable;

    public bool IsEmpty => _events.IsEmpty;

    public void Enqueue(Action continuation)
    {
        _events.Enqueue(continuation);

        // Wake any async waiter. Atomically steal the TCS and signal it.
        // This ensures the async loop in WaitForEventAsync wakes up promptly
        // when new work is enqueued (e.g., from a Task.ContinueWith callback).
        Interlocked.Exchange(ref _eventAvailable, null)?.TrySetResult(true);
    }

    /// <summary>
    /// Waits asynchronously for events to be enqueued, releasing the current thread.
    /// Used by the async API path (EvaluateAsync/ExecuteAsync/InvokeAsync) to avoid
    /// blocking a thread during IO-bound operations. During the wait, zero threads
    /// are consumed.
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

        // Atomically register our TCS. If another waiter is already registered,
        // check whether it's stale (completed/cancelled from a prior timeout).
        var previous = Interlocked.CompareExchange(ref _eventAvailable, tcs, null);
        if (previous is not null)
        {
            if (!previous.Task.IsCompleted)
            {
                // Active waiter exists — events may already be pending.
                return Task.CompletedTask;
            }

            // Previous TCS is stale (cancelled/completed). Try to replace it with ours.
            if (Interlocked.CompareExchange(ref _eventAvailable, tcs, previous) != previous)
            {
                // Enqueue cleared it concurrently — events are likely available.
                return Task.CompletedTask;
            }

            // Successfully replaced stale TCS, fall through to double-check.
        }

        // Double-check after registration to close the race window where an event
        // was enqueued between our IsEmpty check and the CompareExchange.
        if (!_events.IsEmpty)
        {
            Interlocked.Exchange(ref _eventAvailable, null);
            return Task.CompletedTask;
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
