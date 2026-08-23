using System.Threading;
using Jint.Native;
using Jint.Native.Promise;
using Jint.Runtime;

namespace Jint;

#pragma warning disable MA0042 // The async methods intentionally call sync variants then wrap the result

public partial class Engine
{
    // INVARIANT for every entry in this file: the public method validates its arguments and takes the
    // reservation, and then hands off to a `private async` body that owns the release in a finally. The
    // split is the failure channel, and both halves of it are load-bearing.
    //
    // A usage error - a null argument, an unprepared script, and ReserveAsyncHostOperation refusing because
    // the engine is already in use - says the operation never started, so it belongs on the caller's stack;
    // there is no evaluation for a task to describe, and Advanced.WaitForScheduledWorkAsync reserves
    // synchronously for the same reason. Everything else says the operation started and failed, and belongs
    // on the returned task: an async body captures its own synchronous phase, which is exactly what makes
    // that true for the parse and for the whole synchronous run of the script.
    //
    // So do NOT move a reservation into a body, and do NOT hoist work out of one. Before the split, the
    // family was divided by nothing more than which methods happened to be declared `async`: ExecuteAsync
    // was, so a tripped constraint reached its task, while EvaluateAsync was not, so the identical failure
    // on the identical script erupted from the call - and erupted only sometimes, because a host callback
    // charged to the operation from another thread could trip the post-script check before the engine thread
    // reached it. See https://github.com/sebastienros/jint/issues/3241.

    /// <summary>
    /// Evaluates JavaScript code asynchronously, properly awaiting any promises.
    /// This is the non-blocking alternative to Evaluate() + UnwrapIfPromise().
    /// During IO-bound operations (e.g., .NET Tasks awaited from JS), the calling
    /// thread is released and zero threads are consumed until work is available.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Where failures arrive.</b> Everything the evaluation itself does — parsing, running the script,
    /// an execution constraint tripping, a rejected promise — is reported through the returned
    /// <see cref="Task{TResult}"/> and never thrown out of this call, so a <c>catch</c> around the
    /// <c>await</c> sees all of it however far the evaluation got before failing. Only a usage error
    /// arrives synchronously: a <see langword="null"/> argument, and the
    /// <see cref="InvalidOperationException"/> refusing the call because the engine is already in use.
    /// Both mean the operation never started, so there is no evaluation for a task to describe.
    /// </para>
    /// <para>
    /// <paramref name="cancellationToken"/> does <b>not</b> preempt the synchronous evaluation loop. The
    /// script is evaluated to completion first and the token is only observed afterwards, while awaiting
    /// promise settlement — that is, at event-loop continuation boundaries. A script that never yields
    /// (<c>while (true) { }</c>) is therefore not cancellable through this parameter. To bound the
    /// interpreter itself, register an execution constraint on the engine's <see cref="Options"/>:
    /// <see cref="ConstraintsOptionsExtensions.ObserveCancellation"/> for token-driven cancellation or
    /// <see cref="ConstraintsOptionsExtensions.LimitExecutionTime"/> for a wall-clock bound. Both are
    /// amortizable, so neither disarms the interpreter's tight-loop lane.
    /// </para>
    /// <para>
    /// There is deliberately no <see cref="ScriptParsingOptions"/> parameter here: parse the source once
    /// with <see cref="PrepareScript"/> and pass the result to
    /// <see cref="EvaluateAsync(in Prepared{Script}, CancellationToken)"/>, which is both the way to reach
    /// custom parsing options and the cheaper thing to do when the source is evaluated more than once.
    /// </para>
    /// </remarks>
    /// <param name="code">The JavaScript code to evaluate.</param>
    /// <param name="source">Optional source identifier for debugging.</param>
    /// <param name="cancellationToken">Cancellation token to observe while awaiting promise settlement; see the remarks.</param>
    /// <returns>The resolved value if the result is a promise, otherwise the direct result.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="code"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">This engine is already in use.</exception>
    public Task<JsValue> EvaluateAsync(string code, string? source = null, CancellationToken cancellationToken = default)
    {
        if (code is null)
        {
            Throw.ArgumentNullException(nameof(code));
        }

        var owner = ReserveAsyncHostOperation();
        return EvaluateOnReservationAsync(code, source, owner, cancellationToken);
    }

    private async Task<JsValue> EvaluateOnReservationAsync(string code, string? source, object owner, CancellationToken cancellationToken)
    {
        try
        {
            Task<JsValue> task;
            using (EnterHostCall(owner))
            {
                task = UnwrapResultAsync(Evaluate(code, source), owner, cancellationToken);
            }

            return await task.ConfigureAwait(false);
        }
        finally
        {
            ReleaseAsyncHostOperation(owner);
        }
    }

    /// <summary>
    /// Evaluates a prepared script asynchronously, properly awaiting any promises.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Where failures arrive.</b> Everything the evaluation itself does — running the script, an
    /// execution constraint tripping, a rejected promise — is reported through the returned
    /// <see cref="Task{TResult}"/> and never thrown out of this call, so a <c>catch</c> around the
    /// <c>await</c> sees all of it however far the evaluation got before failing. Only a usage error
    /// arrives synchronously: a <paramref name="preparedScript"/> that did not come from
    /// <c>PrepareScript</c>, and the
    /// <see cref="InvalidOperationException"/> refusing the call because the engine is already in use.
    /// Both mean the operation never started, so there is no evaluation for a task to describe.
    /// </para>
    /// <para>
    /// <paramref name="cancellationToken"/> does <b>not</b> preempt the synchronous evaluation loop. The
    /// script is evaluated to completion first and the token is only observed afterwards, while awaiting
    /// promise settlement — that is, at event-loop continuation boundaries. A script that never yields
    /// (<c>while (true) { }</c>) is therefore not cancellable through this parameter. To bound the
    /// interpreter itself, register an execution constraint on the engine's <see cref="Options"/>:
    /// <see cref="ConstraintsOptionsExtensions.ObserveCancellation"/> for token-driven cancellation or
    /// <see cref="ConstraintsOptionsExtensions.LimitExecutionTime"/> for a wall-clock bound. Both are
    /// amortizable, so neither disarms the interpreter's tight-loop lane.
    /// </para>
    /// </remarks>
    /// <param name="preparedScript">The pre-parsed script to evaluate.</param>
    /// <param name="cancellationToken">Cancellation token to observe while awaiting promise settlement; see the remarks.</param>
    /// <returns>The resolved value if the result is a promise, otherwise the direct result.</returns>
    /// <exception cref="ArgumentException"><paramref name="preparedScript"/> did not come from <c>PrepareScript</c>.</exception>
    /// <exception cref="InvalidOperationException">This engine is already in use.</exception>
    public Task<JsValue> EvaluateAsync(in Prepared<Script> preparedScript, CancellationToken cancellationToken = default)
    {
        if (!preparedScript.IsValid)
        {
            Throw.InvalidPreparedScriptArgumentException(nameof(preparedScript));
        }

        var prepared = preparedScript;
        var owner = ReserveAsyncHostOperation();
        return EvaluateOnReservationAsync(prepared, owner, cancellationToken);
    }

    private async Task<JsValue> EvaluateOnReservationAsync(Prepared<Script> preparedScript, object owner, CancellationToken cancellationToken)
    {
        try
        {
            Task<JsValue> task;
            using (EnterHostCall(owner))
            {
                task = UnwrapResultAsync(Evaluate(in preparedScript), owner, cancellationToken);
            }

            return await task.ConfigureAwait(false);
        }
        finally
        {
            ReleaseAsyncHostOperation(owner);
        }
    }

    /// <summary>
    /// Executes JavaScript code asynchronously, properly awaiting completion of any promises.
    /// This is the non-blocking alternative to Execute() when the code may contain async operations.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Where failures arrive.</b> Everything the execution itself does — parsing, running the code, an
    /// execution constraint tripping, a rejected promise — is reported through the returned
    /// <see cref="Task{TResult}"/> and never thrown out of this call. Only a usage error arrives
    /// synchronously: a <see langword="null"/> argument, and the <see cref="InvalidOperationException"/>
    /// refusing the call because the engine is already in use.
    /// </para>
    /// <para>
    /// <paramref name="cancellationToken"/> does <b>not</b> preempt the synchronous evaluation loop. The
    /// code runs to completion first and the token is only observed afterwards, while awaiting promise
    /// settlement — that is, at event-loop continuation boundaries. To bound the interpreter itself,
    /// register an execution constraint on the engine's <see cref="Options"/>:
    /// <see cref="ConstraintsOptionsExtensions.ObserveCancellation"/> or
    /// <see cref="ConstraintsOptionsExtensions.LimitExecutionTime"/>.
    /// </para>
    /// <para>
    /// There is deliberately no <see cref="ScriptParsingOptions"/> parameter here: parse the source once
    /// with <see cref="PrepareScript"/> and pass the result to
    /// <see cref="ExecuteAsync(in Prepared{Script}, CancellationToken)"/>.
    /// </para>
    /// </remarks>
    /// <param name="code">The JavaScript code to execute.</param>
    /// <param name="source">Optional source identifier for debugging.</param>
    /// <param name="cancellationToken">Cancellation token to observe while awaiting promise settlement; see the remarks.</param>
    /// <returns>The engine instance for chaining, after all async work completes.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="code"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">This engine is already in use.</exception>
    public Task<Engine> ExecuteAsync(string code, string? source = null, CancellationToken cancellationToken = default)
    {
        if (code is null)
        {
            Throw.ArgumentNullException(nameof(code));
        }

        var owner = ReserveAsyncHostOperation();
        return ExecuteOnReservationAsync(code, source, owner, cancellationToken);
    }

    private async Task<Engine> ExecuteOnReservationAsync(string code, string? source, object owner, CancellationToken cancellationToken)
    {
        try
        {
            Task<JsValue> task;
            using (EnterHostCall(owner))
            {
                task = UnwrapResultAsync(Evaluate(code, source), owner, cancellationToken);
            }

            await task.ConfigureAwait(false);
            return this;
        }
        finally
        {
            ReleaseAsyncHostOperation(owner);
        }
    }

    /// <summary>
    /// Executes a prepared script asynchronously, properly awaiting completion of any promises.
    /// </summary>
    /// <inheritdoc cref="EvaluateAsync(in Prepared{Script}, CancellationToken)" path="/remarks"/>
    /// <param name="preparedScript">The pre-parsed script to execute.</param>
    /// <param name="cancellationToken">Cancellation token to observe while awaiting promise settlement; see the remarks.</param>
    /// <returns>The engine instance for chaining, after all async work completes.</returns>
    /// <exception cref="ArgumentException"><paramref name="preparedScript"/> did not come from <c>PrepareScript</c>.</exception>
    /// <exception cref="InvalidOperationException">This engine is already in use.</exception>
    public Task<Engine> ExecuteAsync(in Prepared<Script> preparedScript, CancellationToken cancellationToken = default)
    {
        if (!preparedScript.IsValid)
        {
            Throw.InvalidPreparedScriptArgumentException(nameof(preparedScript));
        }

        var prepared = preparedScript;
        var owner = ReserveAsyncHostOperation();
        return ExecuteOnReservationAsync(prepared, owner, cancellationToken);
    }

    private async Task<Engine> ExecuteOnReservationAsync(Prepared<Script> preparedScript, object owner, CancellationToken cancellationToken)
    {
        try
        {
            Task<JsValue> task;
            using (EnterHostCall(owner))
            {
                task = UnwrapResultAsync(Evaluate(in preparedScript), owner, cancellationToken);
            }

            await task.ConfigureAwait(false);
            return this;
        }
        finally
        {
            ReleaseAsyncHostOperation(owner);
        }
    }

    /// <summary>
    /// Invokes a JavaScript function asynchronously, properly awaiting any returned promise.
    /// </summary>
    /// <inheritdoc cref="InvokeAsync(string, CancellationToken, object[])" path="/remarks"/>
    /// <param name="propertyName">The name of a property of the global object holding the function to invoke.</param>
    /// <param name="arguments">Arguments to pass to the function.</param>
    /// <returns>The resolved value if the function returns a promise, otherwise the direct result.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="propertyName"/> or <paramref name="arguments"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">This engine is already in use.</exception>
    public Task<JsValue> InvokeAsync(string propertyName, params object?[] arguments)
    {
        return InvokeAsync(propertyName, CancellationToken.None, arguments);
    }

    /// <summary>
    /// Invokes a JavaScript function asynchronously, properly awaiting any returned promise.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Where failures arrive.</b> Everything the call itself does — resolving the name, running the
    /// function, an execution constraint tripping, a rejected promise — is reported through the returned
    /// <see cref="Task{TResult}"/> and never thrown out of this call. Only a usage error arrives
    /// synchronously: a <see langword="null"/> argument, and the <see cref="InvalidOperationException"/>
    /// refusing the call because the engine is already in use.
    /// </para>
    /// <para>
    /// <paramref name="cancellationToken"/> does <b>not</b> preempt the synchronous call. The function runs
    /// to completion first and the token is only observed afterwards, while awaiting promise settlement —
    /// that is, at event-loop continuation boundaries. To bound the interpreter itself, register an
    /// execution constraint on the engine's <see cref="Options"/>:
    /// <see cref="ConstraintsOptionsExtensions.ObserveCancellation"/> or
    /// <see cref="ConstraintsOptionsExtensions.LimitExecutionTime"/>.
    /// </para>
    /// <para>
    /// <paramref name="propertyName"/> resolves exactly as <see cref="Invoke(string, object, object[])"/>
    /// resolves it: a single property name of the global object, never parsed and never a dotted path.
    /// </para>
    /// </remarks>
    /// <param name="propertyName">The name of a property of the global object holding the function to invoke.</param>
    /// <param name="cancellationToken">Cancellation token to observe while awaiting promise settlement; see the remarks.</param>
    /// <param name="arguments">Arguments to pass to the function.</param>
    /// <returns>The resolved value if the function returns a promise, otherwise the direct result.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="propertyName"/> or <paramref name="arguments"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">This engine is already in use.</exception>
    public Task<JsValue> InvokeAsync(string propertyName, CancellationToken cancellationToken, params object?[] arguments)
    {
        if (propertyName is null)
        {
            Throw.ArgumentNullException(nameof(propertyName));
        }

        if (arguments is null)
        {
            Throw.ArgumentNullException(nameof(arguments));
        }

        var owner = ReserveAsyncHostOperation();
        return InvokeOnReservationAsync(propertyName, arguments, owner, cancellationToken);
    }

    private async Task<JsValue> InvokeOnReservationAsync(string propertyName, object?[] arguments, object owner, CancellationToken cancellationToken)
    {
        try
        {
            Task<JsValue> task;
            using (EnterHostCall(owner))
            {
                task = UnwrapResultAsync(Invoke(propertyName, arguments), owner, cancellationToken);
            }

            return await task.ConfigureAwait(false);
        }
        finally
        {
            ReleaseAsyncHostOperation(owner);
        }
    }

    /// <summary>
    /// Core async unwrap: if the result is a JsPromise, awaits its settlement
    /// without blocking any thread. For non-promise values, returns synchronously.
    /// </summary>
    internal Task<JsValue> UnwrapResultAsync(JsValue result, CancellationToken cancellationToken)
    {
        var owner = ReserveAsyncHostOperation();
        return UnwrapOnReservationAsync(result, owner, cancellationToken);
    }

    private async Task<JsValue> UnwrapOnReservationAsync(JsValue result, object owner, CancellationToken cancellationToken)
    {
        try
        {
            Task<JsValue> task;
            using (EnterHostCall(owner))
            {
                task = UnwrapResultAsync(result, owner, cancellationToken);
            }

            return await task.ConfigureAwait(false);
        }
        finally
        {
            ReleaseAsyncHostOperation(owner);
        }
    }

    internal Task<JsValue> UnwrapResultAsync(JsValue result, object owner, CancellationToken cancellationToken)
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
        return AwaitPromiseSettlementAsync(promise, owner, cancellationToken);
    }

    /// <summary>
    /// Truly async promise settlement loop. Releases the thread between event loop
    /// processing cycles. When a .NET Task completes (e.g., gRPC IO), its ContinueWith
    /// callback enqueues work on the event loop and signals the wake, causing this method
    /// to resume on a thread pool thread, process the JS continuation, and either complete
    /// or go back to sleep if another await is hit.
    /// </summary>
    private async Task<JsValue> AwaitPromiseSettlementAsync(JsPromise promise, object owner, CancellationToken cancellationToken)
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

        // Taken here rather than at the top of the method so that a throw while building the token source
        // cannot leak the count. Everything from this point on is inside the try whose finally releases it,
        // and all of it runs synchronously up to the first await, so the count is already raised by the time
        // the caller holds the Task.
        Interlocked.Increment(ref _pendingAsyncOperations);
        try
        {
            while (promise.State == PromiseState.Pending)
            {
                effectiveCt.ThrowIfCancellationRequested();

                // Truly async wait — releases the thread back to the pool.
                // Zero threads consumed while waiting for IO to complete.
                Interlocked.Increment(ref _hostCallbackAdmission);
                try
                {
                    // Work the engine scheduled for itself — a pending Atomics.waitAsync timeout, a pending
                    // web-API timer — is the one thing that can make this loop's condition advance without
                    // anything being enqueued, so the wait has to be bounded by its due time.
                    var untilNextWork = TimeUntilNextPumpScheduledWork();
                    if (untilNextWork is not { } untilDue)
                    {
                        await eventLoop.WaitForEventAsync(effectiveCt).ConfigureAwait(false);
                    }
                    else if (untilDue > TimeSpan.Zero)
                    {
                        await eventLoop.WaitForEventAsync(untilDue, effectiveCt).ConfigureAwait(false);
                    }
                    else if (eventLoop.IsRunningJob)
                    {
                        await eventLoop.WaitForEventAsync(effectiveCt).ConfigureAwait(false);
                    }
                }
                finally
                {
                    Interlocked.Decrement(ref _hostCallbackAdmission);
                }

                using (EnterTransferredHostCall(owner))
                {
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
        }
        catch (OperationCanceledException) when (hasTimeout && ownedCts!.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
        {
            // The timeout CTS fired, not the user's cancellation token.
            // Translate to PromiseRejectedException to match sync API behavior.
            throw new PromiseRejectedException($"Timeout of {timeout} reached");
        }
        finally
        {
            Interlocked.Decrement(ref _pendingAsyncOperations);
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
