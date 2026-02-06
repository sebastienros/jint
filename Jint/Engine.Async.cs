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
    /// Core async unwrap: if the result is a JsPromise, uses a TaskCompletionSource
    /// to await its settlement without blocking a thread.
    /// </summary>
    private Task<JsValue> UnwrapResultAsync(JsValue result, CancellationToken cancellationToken)
    {
        if (result is not JsPromise promise)
        {
            return Task.FromResult(result);
        }

        // Fast path: already settled
        RunAvailableContinuations();
        if (promise.State == PromiseState.Fulfilled)
        {
            return Task.FromResult(promise.Value);
        }

        if (promise.State == PromiseState.Rejected)
        {
            return Task.FromException<JsValue>(new PromiseRejectedException(promise.Value));
        }

        // Pending: use TaskCompletionSource for non-blocking await.
        // We still need to pump the event loop since Jint is single-threaded.
        var tcs = new TaskCompletionSource<JsValue>(TaskCreationOptions.RunContinuationsAsynchronously);

        if (cancellationToken.CanBeCanceled)
        {
            cancellationToken.Register(() => tcs.TrySetCanceled(cancellationToken));
        }

        // Set up a poll loop that runs on the thread pool.
        // We pump the event loop from the polling thread, which is safe
        // because _waitingThreadId ensures only the designated thread can run JS.
        _ = Task.Run(() => PollPromiseCompletion(promise, tcs, cancellationToken), cancellationToken);

        return tcs.Task;
    }

    /// <summary>
    /// Polls the promise for completion, pumping the event loop until settled.
    /// Runs on a thread pool thread to avoid blocking the caller.
    /// </summary>
    private void PollPromiseCompletion(JsPromise promise, TaskCompletionSource<JsValue> tcs, CancellationToken cancellationToken)
    {
        var eventLoop = _eventLoop;
        var previousWaitingThreadId = eventLoop._waitingThreadId;
        eventLoop._waitingThreadId = Environment.CurrentManagedThreadId;

        try
        {
            var timeout = Options.Constraints.PromiseTimeout;
            var hasTimeout = timeout > TimeSpan.Zero;
            var deadline = hasTimeout ? DateTime.UtcNow + timeout : DateTime.MaxValue;
            var pollInterval = TimeSpan.FromMilliseconds(1);

            while (promise.State == PromiseState.Pending)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    tcs.TrySetCanceled(cancellationToken);
                    return;
                }

                RunAvailableContinuations();

                if (promise.State != PromiseState.Pending)
                {
                    break;
                }

                if (hasTimeout)
                {
                    var remaining = deadline - DateTime.UtcNow;
                    if (remaining <= TimeSpan.Zero)
                    {
                        tcs.TrySetException(new PromiseRejectedException($"Timeout of {timeout} reached"));
                        return;
                    }

                    var waitTime = remaining < pollInterval ? remaining : pollInterval;
                    promise.CompletedEvent.Wait(waitTime, cancellationToken);
                }
                else
                {
                    promise.CompletedEvent.Wait(pollInterval, cancellationToken);
                }
            }

            switch (promise.State)
            {
                case PromiseState.Fulfilled:
                    tcs.TrySetResult(promise.Value);
                    break;
                case PromiseState.Rejected:
                    tcs.TrySetException(new PromiseRejectedException(promise.Value));
                    break;
                default:
                    tcs.TrySetException(new InvalidOperationException("Promise is still pending after polling completed"));
                    break;
            }
        }
        catch (OperationCanceledException)
        {
            tcs.TrySetCanceled(cancellationToken);
        }
        catch (Exception ex)
        {
            tcs.TrySetException(ex);
        }
        finally
        {
            eventLoop._waitingThreadId = previousWaitingThreadId;
        }
    }
}
