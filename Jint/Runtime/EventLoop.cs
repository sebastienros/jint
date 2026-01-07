using System.Collections.Concurrent;
using System.Threading;

namespace Jint.Runtime;

internal sealed record EventLoop
{
    private readonly ConcurrentQueue<Action> _events = new();

    /// <summary>
    /// Tracks whether we are currently processing the event loop (0 = not processing, 1 = processing).
    /// Used to prevent re-entrant calls from causing stack overflow.
    /// Uses int for Interlocked operations to ensure atomic check-and-set.
    /// </summary>
    private int _isProcessing;

    /// <summary>
    /// Tracks the thread ID of the thread that is currently waiting on a promise.
    /// Only this thread (or any thread if -1) is allowed to process continuations.
    /// This prevents background threads (e.g., Task.ContinueWith callbacks) from
    /// executing JavaScript code on the Engine.
    /// </summary>
    internal volatile int _waitingThreadId = -1;

    public bool IsEmpty => _events.IsEmpty;

    public void Enqueue(Action continuation)
    {
        _events.Enqueue(continuation);
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

        // Prevent re-entrant calls which can cause stack overflow.
        // If we're already processing, the outer loop will handle any new events.
        // Use atomic compare-exchange to avoid race condition where multiple threads
        // could pass the check simultaneously.
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
            Volatile.Write(ref _isProcessing, 0);
        }
    }
}
