using System.Runtime.CompilerServices;
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
    // there is no evaluation for a task to describe, and Tasks.WaitForScheduledWorkAsync reserves
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
    //
    // The same bodies own the entry's cancellation token: BeginObservingAsyncEntryToken first, inside the try,
    // so a token cancelled before the call cancels the task without running anything, and
    // EndObservingAsyncEntryToken in the finally, so the next entry - synchronous or not - never observes a
    // token that belonged to this one.

    /// <summary>
    /// The token of the <c>*Async</c> entry in progress, or <see langword="default"/> outside one. The
    /// interpreter observes it on the amortized cadence (<see cref="CheckAmortizedConstraints"/>), which is what
    /// lets the entry's own token stop a script that never yields without the host registering
    /// <see cref="ConstraintsOptionsExtensions.ObserveCancellation"/> — a registration fixed when the
    /// <see cref="Options"/> are built, and therefore no use to a host whose token is per request.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Engine state rather than a <see cref="Constraint"/>: the constraint set is built once and fixed for
    /// the engine's life, while this lives for one entry. It is observation-only for the same reasons
    /// <see cref="Constraints.CancellationConstraint"/> is amortizable — a token never un-cancels — so it rides
    /// the cadence and never joins the exact partition, and the tight-loop lane stays armed.
    /// </para>
    /// <para>
    /// Only a token that <see cref="CancellationToken.CanBeCanceled"/> is installed, so an entry handed
    /// <see langword="default"/> leaves the interpreter on exactly the path it ran before; an engine with no
    /// amortized constraint then does not even run the countdown. No two entries overlap: the async
    /// reservation refuses a second one, including one made from a host callback inside the first, so one
    /// field is the whole of the state. A nested <em>synchronous</em> re-entry from a host callback is part of
    /// the operation and observes the token too.
    /// </para>
    /// </remarks>
    internal CancellationToken _asyncEntryToken;

    /// <summary>
    /// Refuses an entry whose token is already cancelled, and otherwise installs that token for the interpreter
    /// to observe until <see cref="EndObservingAsyncEntryToken"/>.
    /// </summary>
    internal void BeginObservingAsyncEntryToken(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (cancellationToken.CanBeCanceled)
        {
            _asyncEntryToken = cancellationToken;
            _evaluationContext.RefreshAmortizedChecks();
        }
    }

    /// <summary>
    /// Uninstalls the token <see cref="BeginObservingAsyncEntryToken"/> installed. Never throws: it runs in the
    /// finally that releases the reservation.
    /// </summary>
    /// <remarks>
    /// It does not take the engine. A host callback admitted under the reservation may still be finishing on
    /// another thread, and what it can see change under it is one reference-sized field going to
    /// <see langword="default"/> and one flag that only decides whether the cadence is counted — either
    /// value of either is safe.
    /// </remarks>
    internal void EndObservingAsyncEntryToken(CancellationToken cancellationToken)
    {
        if (cancellationToken.CanBeCanceled)
        {
            _asyncEntryToken = default;
            _evaluationContext.RefreshAmortizedChecks();
        }
    }

    /// <summary>
    /// Fails the run with an <see cref="OperationCanceledException"/> carrying the entry's own token when the
    /// <c>*Async</c> entry in progress has been cancelled. A real <see cref="OperationCanceledException"/>, as
    /// <see cref="Constraints.OperationDeadlineConstraint"/> throws, and not Jint's
    /// <see cref="ExecutionCanceledException"/>: the host passed this token to an <c>*Async</c> method, and
    /// that is the exception .NET promises it back.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void ThrowIfAsyncEntryCancelled()
    {
        if (_asyncEntryToken.IsCancellationRequested)
        {
            Throw.OperationCanceledException(_asyncEntryToken);
        }
    }

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
    /// <paramref name="cancellationToken"/> covers the whole call. Already cancelled, it cancels the returned
    /// task — awaiting it throws <see cref="OperationCanceledException"/> — before any script runs. Otherwise
    /// the interpreter observes it on the cadence an amortizable constraint uses, through the synchronous run,
    /// every continuation and any synchronous re-entry from a host callback, so <c>while (true) { }</c> is
    /// cancellable through it and the tight-loop lane stays armed. Every cancellation carries this token. Only
    /// a host callback that never returns cannot be stopped.
    /// </para>
    /// <para>
    /// There is deliberately no <see cref="ScriptParsingOptions"/> parameter here: parse the source once
    /// with <see cref="PrepareScript(string, string, bool, ScriptPreparationOptions)"/> and pass the result to
    /// <see cref="EvaluateAsync(in Prepared{Script}, CancellationToken)"/>, which is both the way to reach
    /// custom parsing options and the cheaper thing to do when the source is evaluated more than once.
    /// </para>
    /// </remarks>
    /// <param name="code">The JavaScript code to evaluate.</param>
    /// <param name="source">Optional source identifier for debugging.</param>
    /// <param name="cancellationToken">Cancellation token observed for the whole call, script included; see the remarks.</param>
    /// <returns>The resolved value if the result is a promise, otherwise the direct result.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="code"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">This engine is already in use or has been retired.</exception>
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
            BeginObservingAsyncEntryToken(cancellationToken);
            Task<JsValue> task;
            using (EnterHostCall(owner))
            {
                task = UnwrapResultAsync(Evaluate(code, source), owner, cancellationToken);
            }

            return await task.ConfigureAwait(false);
        }
        finally
        {
            EndObservingAsyncEntryToken(cancellationToken);
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
    /// <paramref name="cancellationToken"/> covers the whole call. Already cancelled, it cancels the returned
    /// task — awaiting it throws <see cref="OperationCanceledException"/> — before any script runs. Otherwise
    /// the interpreter observes it on the cadence an amortizable constraint uses, through the synchronous run,
    /// every continuation and any synchronous re-entry from a host callback, so <c>while (true) { }</c> is
    /// cancellable through it and the tight-loop lane stays armed. Every cancellation carries this token. Only
    /// a host callback that never returns cannot be stopped.
    /// </para>
    /// </remarks>
    /// <param name="preparedScript">The pre-parsed script to evaluate.</param>
    /// <param name="cancellationToken">Cancellation token observed for the whole call, script included; see the remarks.</param>
    /// <returns>The resolved value if the result is a promise, otherwise the direct result.</returns>
    /// <exception cref="ArgumentException"><paramref name="preparedScript"/> did not come from <c>PrepareScript</c>.</exception>
    /// <exception cref="InvalidOperationException">This engine is already in use or has been retired.</exception>
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
            BeginObservingAsyncEntryToken(cancellationToken);
            Task<JsValue> task;
            using (EnterHostCall(owner))
            {
                task = UnwrapResultAsync(Evaluate(in preparedScript), owner, cancellationToken);
            }

            return await task.ConfigureAwait(false);
        }
        finally
        {
            EndObservingAsyncEntryToken(cancellationToken);
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
    /// <paramref name="cancellationToken"/> covers the whole call. Already cancelled, it cancels the returned
    /// task — awaiting it throws <see cref="OperationCanceledException"/> — before any script runs. Otherwise
    /// the interpreter observes it on the cadence an amortizable constraint uses, through the synchronous run,
    /// every continuation and any synchronous re-entry from a host callback, so <c>while (true) { }</c> is
    /// cancellable through it and the tight-loop lane stays armed. Every cancellation carries this token. Only
    /// a host callback that never returns cannot be stopped.
    /// </para>
    /// <para>
    /// There is deliberately no <see cref="ScriptParsingOptions"/> parameter here: parse the source once
    /// with <see cref="PrepareScript(string, string, bool, ScriptPreparationOptions)"/> and pass the result to
    /// <see cref="ExecuteAsync(in Prepared{Script}, CancellationToken)"/>.
    /// </para>
    /// </remarks>
    /// <param name="code">The JavaScript code to execute.</param>
    /// <param name="source">Optional source identifier for debugging.</param>
    /// <param name="cancellationToken">Cancellation token observed for the whole call, script included; see the remarks.</param>
    /// <returns>The engine instance for chaining, after all async work completes.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="code"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">This engine is already in use or has been retired.</exception>
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
            BeginObservingAsyncEntryToken(cancellationToken);
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
            EndObservingAsyncEntryToken(cancellationToken);
            ReleaseAsyncHostOperation(owner);
        }
    }

    /// <summary>
    /// Executes a prepared script asynchronously, properly awaiting completion of any promises.
    /// </summary>
    /// <inheritdoc cref="EvaluateAsync(in Prepared{Script}, CancellationToken)" path="/remarks"/>
    /// <param name="preparedScript">The pre-parsed script to execute.</param>
    /// <param name="cancellationToken">Cancellation token observed for the whole call, script included; see the remarks.</param>
    /// <returns>The engine instance for chaining, after all async work completes.</returns>
    /// <exception cref="ArgumentException"><paramref name="preparedScript"/> did not come from <c>PrepareScript</c>.</exception>
    /// <exception cref="InvalidOperationException">This engine is already in use or has been retired.</exception>
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
            BeginObservingAsyncEntryToken(cancellationToken);
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
            EndObservingAsyncEntryToken(cancellationToken);
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
    /// <exception cref="InvalidOperationException">This engine is already in use or has been retired.</exception>
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
    /// <paramref name="cancellationToken"/> covers the whole call. Already cancelled, it cancels the returned
    /// task — awaiting it throws <see cref="OperationCanceledException"/> — before any script runs. Otherwise
    /// the interpreter observes it on the cadence an amortizable constraint uses, through the synchronous run,
    /// every continuation and any synchronous re-entry from a host callback, so <c>while (true) { }</c> is
    /// cancellable through it and the tight-loop lane stays armed. Every cancellation carries this token. Only
    /// a host callback that never returns cannot be stopped.
    /// </para>
    /// <para>
    /// <paramref name="propertyName"/> resolves exactly as <see cref="Invoke(string, object, object[])"/>
    /// resolves it: a single property name of the global object, never parsed and never a dotted path.
    /// </para>
    /// </remarks>
    /// <param name="propertyName">The name of a property of the global object holding the function to invoke.</param>
    /// <param name="cancellationToken">Cancellation token observed for the whole call, script included; see the remarks.</param>
    /// <param name="arguments">Arguments to pass to the function.</param>
    /// <returns>The resolved value if the function returns a promise, otherwise the direct result.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="propertyName"/> or <paramref name="arguments"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">This engine is already in use or has been retired.</exception>
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
            BeginObservingAsyncEntryToken(cancellationToken);
            Task<JsValue> task;
            using (EnterHostCall(owner))
            {
                task = UnwrapResultAsync(Invoke(propertyName, arguments), owner, cancellationToken);
            }

            return await task.ConfigureAwait(false);
        }
        finally
        {
            EndObservingAsyncEntryToken(cancellationToken);
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
                if (IsRetired)
                {
                    using (EnterTransferredHostCall(owner))
                    {
                        FinishRetirement();
                    }
                    ThrowIfRetired();
                }
                cancellationToken.ThrowIfCancellationRequested();
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
                    if (IsRetired) FinishRetirement();
                    ThrowIfRetired();
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
        catch (OperationCanceledException exception) when (cancellationToken.IsCancellationRequested
                                                           && exception.CancellationToken != cancellationToken
                                                           && (exception.CancellationToken == effectiveCt || !exception.CancellationToken.CanBeCanceled))
        {
            // The caller's token fired, but the wait reported it through the linked source that also carries
            // PromiseTimeout, or through a waiter cancelled with no token at all. Hand back the token the caller
            // passed, which is the one every other cancellation this entry raises carries and the one a
            // `when (e.CancellationToken == token)` filter matches.
            throw new OperationCanceledException(exception.Message, exception, cancellationToken);
        }
        finally
        {
            Interlocked.Decrement(ref _pendingAsyncOperations);
            ownedCts?.Dispose();
        }

        ThrowIfRetired();
        return promise.State switch
        {
            PromiseState.Fulfilled => promise.Value,
            PromiseState.Rejected => throw new PromiseRejectedException(promise.Value),
            _ => throw new InvalidOperationException("Promise is still pending after async loop completed")
        };
    }
}
