using System.Threading;
using Jint.Native;
using Jint.Native.Promise;
using Jint.Runtime;

namespace Jint;

#pragma warning disable MA0042 // The async methods intentionally call sync variants then wrap the result

public partial class Engine
{
    /// <summary>
    /// Evaluates JavaScript code asynchronously, properly awaiting any promises.
    /// This is the non-blocking alternative to Evaluate() + UnwrapIfPromise().
    /// During IO-bound operations (e.g., .NET Tasks awaited from JS), the calling
    /// thread is released and zero threads are consumed until work is available.
    /// </summary>
    /// <param name="code">The JavaScript code to evaluate.</param>
    /// <param name="source">Optional source identifier for debugging.</param>
    /// <param name="cancellationToken">Cancellation token to observe.</param>
    /// <returns>The resolved value if the result is a promise, otherwise the direct result.</returns>
    public Task<JsValue> EvaluateAsync(string code, string? source = null, CancellationToken cancellationToken = default)
    {
        var result = Evaluate(code, source);
        return UnwrapResultAsync(result, cancellationToken);
    }

    /// <summary>
    /// Evaluates a prepared script asynchronously, properly awaiting any promises.
    /// </summary>
    /// <param name="preparedScript">The pre-parsed script to evaluate.</param>
    /// <param name="cancellationToken">Cancellation token to observe.</param>
    /// <returns>The resolved value if the result is a promise, otherwise the direct result.</returns>
    public Task<JsValue> EvaluateAsync(in Prepared<Script> preparedScript, CancellationToken cancellationToken = default)
    {
        var result = Evaluate(preparedScript);
        return UnwrapResultAsync(result, cancellationToken);
    }

    /// <summary>
    /// Executes JavaScript code asynchronously, properly awaiting completion of any promises.
    /// This is the non-blocking alternative to Execute() when the code may contain async operations.
    /// </summary>
    /// <param name="code">The JavaScript code to execute.</param>
    /// <param name="source">Optional source identifier for debugging.</param>
    /// <param name="cancellationToken">Cancellation token to observe.</param>
    /// <returns>The engine instance for chaining, after all async work completes.</returns>
    public async Task<Engine> ExecuteAsync(string code, string? source = null, CancellationToken cancellationToken = default)
    {
        var result = Execute(code, source)._completionValue;
        if (result is JsPromise)
        {
            await UnwrapResultAsync(result, cancellationToken).ConfigureAwait(false);
        }

        return this;
    }

    /// <summary>
    /// Invokes a JavaScript function asynchronously, properly awaiting any returned promise.
    /// </summary>
    /// <param name="propertyName">The name of the function to invoke.</param>
    /// <param name="arguments">Arguments to pass to the function.</param>
    /// <returns>The resolved value if the function returns a promise, otherwise the direct result.</returns>
    public Task<JsValue> InvokeAsync(string propertyName, params object?[] arguments)
    {
        return InvokeAsync(propertyName, CancellationToken.None, arguments);
    }

    /// <summary>
    /// Invokes a JavaScript function asynchronously, properly awaiting any returned promise.
    /// </summary>
    /// <param name="propertyName">The name of the function to invoke.</param>
    /// <param name="cancellationToken">Cancellation token to observe.</param>
    /// <param name="arguments">Arguments to pass to the function.</param>
    /// <returns>The resolved value if the function returns a promise, otherwise the direct result.</returns>
    public Task<JsValue> InvokeAsync(string propertyName, CancellationToken cancellationToken, params object?[] arguments)
    {
        var result = Invoke(propertyName, arguments);
        return UnwrapResultAsync(result, cancellationToken);
    }

    /// <summary>
    /// Core async unwrap: if the result is a JsPromise, awaits its settlement
    /// without blocking any thread. For non-promise values, returns synchronously.
    /// </summary>
    internal Task<JsValue> UnwrapResultAsync(JsValue result, CancellationToken cancellationToken)
    {
        if (result is not JsPromise promise)
        {
            return Task.FromResult(result);
        }

        // Fast path: process any queued microtasks and check if already settled
        RunAvailableContinuations();

        if (promise.State == PromiseState.Fulfilled)
        {
            return Task.FromResult(promise.Value);
        }

        if (promise.State == PromiseState.Rejected)
        {
            return Task.FromException<JsValue>(new PromiseRejectedException(promise.Value));
        }

        // Slow path: promise is pending, use truly async waiting.
        // No thread is consumed during the wait — the event loop wake signal
        // will resume execution when new work arrives (e.g., from Task.ContinueWith).
        return AwaitPromiseSettlementAsync(promise, cancellationToken);
    }

    /// <summary>
    /// Truly async promise settlement loop. Releases the thread between event loop
    /// processing cycles. When a .NET Task completes (e.g., gRPC IO), its ContinueWith
    /// callback enqueues work on the event loop and signals the wake, causing this method
    /// to resume on a thread pool thread, process the JS continuation, and either complete
    /// or go back to sleep if another await is hit.
    /// </summary>
    private async Task<JsValue> AwaitPromiseSettlementAsync(JsPromise promise, CancellationToken cancellationToken)
    {
        var eventLoop = _eventLoop;
        var timeout = Options.Constraints.PromiseTimeout;
        var hasTimeout = timeout > TimeSpan.Zero;

        // Build an effective CancellationToken that respects both user cancellation
        // and the PromiseTimeout constraint. This ensures WaitForEventAsync wakes up
        // when the timeout expires, even if no events have been enqueued.
        CancellationTokenSource? ownedCts = null;
        CancellationToken effectiveCt;

        if (hasTimeout && cancellationToken.CanBeCanceled)
        {
            ownedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            ownedCts.CancelAfter(timeout);
            effectiveCt = ownedCts.Token;
        }
        else if (hasTimeout)
        {
            ownedCts = new CancellationTokenSource(timeout);
            effectiveCt = ownedCts.Token;
        }
        else
        {
            effectiveCt = cancellationToken;
        }

        try
        {
            while (promise.State == PromiseState.Pending)
            {
                effectiveCt.ThrowIfCancellationRequested();

                // Truly async wait — releases the thread back to the pool.
                // Zero threads consumed while waiting for IO to complete.
                await eventLoop.WaitForEventAsync(effectiveCt).ConfigureAwait(false);

                // Woke up — take ownership of the event loop for this processing cycle.
                // Setting _waitingThreadId prevents any other thread from processing
                // JavaScript continuations while we're running.
                var previousWaitingThreadId = eventLoop._waitingThreadId;
                eventLoop._waitingThreadId = Environment.CurrentManagedThreadId;
                try
                {
                    RunAvailableContinuations();
                }
                finally
                {
                    eventLoop._waitingThreadId = previousWaitingThreadId;
                }
            }
        }
        catch (OperationCanceledException) when (hasTimeout && ownedCts!.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
        {
            // The timeout CTS fired, not the user's cancellation token.
            // Translate to PromiseRejectedException to match sync API behavior.
            throw new PromiseRejectedException($"Timeout of {timeout} reached");
        }
        finally
        {
            ownedCts?.Dispose();
        }

        return promise.State switch
        {
            PromiseState.Fulfilled => promise.Value,
            PromiseState.Rejected => throw new PromiseRejectedException(promise.Value),
            _ => throw new InvalidOperationException("Promise is still pending after async loop completed")
        };
    }
}
