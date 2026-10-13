using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using Jint.Constraints;
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
    // The entry's cancellation token travels with the reservation: ReserveAsyncHostOperation installs it and
    // whichever release actually drops the reservation clears it, so it lives exactly as long as anything may
    // run under the entry - including a host callback admitted under it that is still finishing after the
    // body's finally. Every body is RunOnReservationAsync, whose first act is refusing a token that is already
    // cancelled, so a new script-running entry gets both halves by reserving with its token and calling it.

    /// <summary>
    /// The token of the <c>*Async</c> entry whose reservation is held, or <see langword="default"/> outside
    /// one. The interpreter observes it on the amortized cadence (<see cref="CheckAmortizedConstraints"/>),
    /// which is what lets the entry's own token stop a script that never yields without the host registering
    /// <see cref="ConstraintsOptionsExtensions.ObserveCancellation"/> — a registration fixed when the
    /// <see cref="Options"/> are built, and therefore no use to a host whose token is per request.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Engine state rather than a <see cref="Constraint"/>: the constraint set is built once and fixed for
    /// the engine's life, while this lives for one reservation. It is observation-only for the same reasons
    /// <see cref="CancellationConstraint"/> is amortizable — a token never un-cancels — so it rides the
    /// cadence and never joins the exact partition, and the tight-loop lane stays armed.
    /// </para>
    /// <para>
    /// Installed by <see cref="ReserveAsyncHostOperation"/> and cleared where the reservation is dropped —
    /// <see cref="ReleaseAsyncHostOperation"/>, or <see cref="ReleaseHostCallbackAdmission"/> when the last
    /// callback admitted under it finishes — so a callback still running under the reservation observes it
    /// and nothing after the reservation does. No two entries overlap: the reservation refuses a second one,
    /// so one field is the whole of the state. A nested <em>synchronous</em> re-entry from a host callback is
    /// part of the operation and observes the token too.
    /// </para>
    /// <para>
    /// Only a token that <see cref="CancellationToken.CanBeCanceled"/> is installed, so an entry handed
    /// <see langword="default"/> leaves the interpreter on exactly the path it ran before. Work that must
    /// outlive the entry never reads this field later: it captures it once through
    /// <see cref="CaptureCancellation"/>.
    /// </para>
    /// </remarks>
    internal CancellationToken _asyncEntryToken;

    private void InstallAsyncEntryToken(CancellationToken entryToken)
    {
        if (entryToken.CanBeCanceled)
        {
            _asyncEntryToken = entryToken;
            _evaluationContext.RefreshAmortizedChecks();
        }
    }

    /// <summary>
    /// Uninstalls the token <see cref="InstallAsyncEntryToken"/> installed. Never throws: it runs where a
    /// reservation is released, which may be a finally or the last admitted callback's thread.
    /// </summary>
    /// <remarks>
    /// It does not take the engine, and does not need to: by the time a reservation is dropped nothing runs
    /// under it any more, and a later reservation can only install its own token once this one is gone.
    /// </remarks>
    private void ClearAsyncEntryToken()
    {
        if (_asyncEntryToken.CanBeCanceled)
        {
            _asyncEntryToken = default;
            _evaluationContext.RefreshAmortizedChecks();
        }
    }

    /// <summary>
    /// Fails the run with an <see cref="OperationCanceledException"/> carrying the entry's own token when the
    /// <c>*Async</c> entry in progress has been cancelled. A real <see cref="OperationCanceledException"/>, as
    /// <see cref="OperationDeadlineConstraint"/> throws, and not Jint's <see cref="ExecutionCanceledException"/>:
    /// the host passed this token to an <c>*Async</c> method, and that is the exception .NET promises it back.
    /// </summary>
    /// <remarks>
    /// The field is read once: the release that clears it may run on another thread, and a second read could
    /// throw for a token that is no longer the one that was found cancelled.
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void ThrowIfAsyncEntryCancelled()
    {
        var entryToken = _asyncEntryToken;
        if (entryToken.IsCancellationRequested)
        {
            Throw.OperationCanceledException(entryToken);
        }
    }

    /// <summary>
    /// The tokens that say the work about to start has been abandoned: a registered
    /// <see cref="CancellationConstraint"/>'s, and the token of the <c>*Async</c> entry in progress.
    /// </summary>
    /// <remarks>
    /// Read once, by whatever starts the work, and kept for as long as the work runs — see
    /// <see cref="EngineCancellation"/> for why that is the whole of the lifetime rule.
    /// </remarks>
    internal EngineCancellation CaptureCancellation()
        => new(Constraints.Find<CancellationConstraint>()?.Token ?? default, _asyncEntryToken);

    /// <summary>
    /// Evaluates JavaScript code asynchronously, awaiting the promise it returns without blocking a thread.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Everything the call does — parsing, running script, a constraint tripping, a rejected promise — is
    /// reported through the returned task. Only a usage error is thrown out of the call: an invalid argument,
    /// or an <see cref="InvalidOperationException"/> because the engine is already in use.
    /// </para>
    /// <para>
    /// An already-cancelled <paramref name="cancellationToken"/> cancels the task before any script runs.
    /// Cancelled later, it stops the script, its continuations and any wait inside the call with an
    /// <see cref="OperationCanceledException"/> carrying it. A host callback that never returns is not stopped.
    /// </para>
    /// <para>
    /// A <c>fetch</c>, socket or module load the call starts keeps observing the token after the call returns,
    /// and is cancelled when it fires.
    /// </para>
    /// <para>
    /// A cancelled call can leave queued jobs behind, which the engine's next pump runs. Before reusing the
    /// engine, restore a snapshot with <see cref="AdvancedOperations.RestoreGlobalSnapshot"/> or discard it.
    /// </para>
    /// </remarks>
    /// <param name="code">
    /// The JavaScript code, parsed with default options; pass a <see cref="PrepareScript(string, string, bool, ScriptPreparationOptions)"/>
    /// result to the other overload to choose others or to parse once.
    /// </param>
    /// <param name="source">Optional source identifier for debugging.</param>
    /// <param name="cancellationToken">The token that cancels the call, script included; see the remarks.</param>
    /// <returns>The resolved value if the result is a promise, otherwise the direct result.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="code"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">This engine is already in use or has been retired.</exception>
    public Task<JsValue> EvaluateAsync(string code, string? source = null, CancellationToken cancellationToken = default)
    {
        if (code is null)
        {
            Throw.ArgumentNullException(nameof(code));
        }

        var owner = ReserveAsyncHostOperation(entryToken: cancellationToken);
        return RunOnReservationAsync(
            owner,
            (code, source),
            static (engine, state) => engine.Evaluate(state.code, state.source),
            cancellationToken);
    }

    /// <summary>
    /// Evaluates a prepared script asynchronously, awaiting the promise it returns without blocking a thread.
    /// </summary>
    /// <inheritdoc cref="EvaluateAsync(string, string, CancellationToken)" path="/remarks"/>
    /// <param name="preparedScript">The pre-parsed script to evaluate.</param>
    /// <param name="cancellationToken">The token that cancels the call, script included; see the remarks.</param>
    /// <returns>The resolved value if the result is a promise, otherwise the direct result.</returns>
    /// <exception cref="ArgumentException"><paramref name="preparedScript"/> did not come from <c>PrepareScript</c>.</exception>
    /// <exception cref="InvalidOperationException">This engine is already in use or has been retired.</exception>
    public Task<JsValue> EvaluateAsync(in Prepared<Script> preparedScript, CancellationToken cancellationToken = default)
    {
        if (!preparedScript.IsValid)
        {
            Throw.InvalidPreparedScriptArgumentException(nameof(preparedScript));
        }

        var owner = ReserveAsyncHostOperation(entryToken: cancellationToken);
        return RunOnReservationAsync(
            owner,
            preparedScript,
            static (engine, prepared) => engine.Evaluate(in prepared),
            cancellationToken);
    }

    /// <summary>
    /// Executes JavaScript code asynchronously, completing once any promise it returns has settled.
    /// </summary>
    /// <inheritdoc cref="EvaluateAsync(string, string, CancellationToken)" path="/remarks"/>
    /// <param name="code">
    /// The JavaScript code, parsed with default options; pass a <see cref="PrepareScript(string, string, bool, ScriptPreparationOptions)"/>
    /// result to the other overload to choose others or to parse once.
    /// </param>
    /// <param name="source">Optional source identifier for debugging.</param>
    /// <param name="cancellationToken">The token that cancels the call, script included; see the remarks.</param>
    /// <returns>The engine instance for chaining, after all async work completes.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="code"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">This engine is already in use or has been retired.</exception>
    public Task<Engine> ExecuteAsync(string code, string? source = null, CancellationToken cancellationToken = default)
    {
        if (code is null)
        {
            Throw.ArgumentNullException(nameof(code));
        }

        var owner = ReserveAsyncHostOperation(entryToken: cancellationToken);
        return ThisWhenCompletedAsync(RunOnReservationAsync(
            owner,
            (code, source),
            static (engine, state) => engine.Evaluate(state.code, state.source),
            cancellationToken));
    }

    /// <summary>
    /// Executes a prepared script asynchronously, completing once any promise it returns has settled.
    /// </summary>
    /// <inheritdoc cref="EvaluateAsync(string, string, CancellationToken)" path="/remarks"/>
    /// <param name="preparedScript">The pre-parsed script to execute.</param>
    /// <param name="cancellationToken">The token that cancels the call, script included; see the remarks.</param>
    /// <returns>The engine instance for chaining, after all async work completes.</returns>
    /// <exception cref="ArgumentException"><paramref name="preparedScript"/> did not come from <c>PrepareScript</c>.</exception>
    /// <exception cref="InvalidOperationException">This engine is already in use or has been retired.</exception>
    public Task<Engine> ExecuteAsync(in Prepared<Script> preparedScript, CancellationToken cancellationToken = default)
    {
        if (!preparedScript.IsValid)
        {
            Throw.InvalidPreparedScriptArgumentException(nameof(preparedScript));
        }

        var owner = ReserveAsyncHostOperation(entryToken: cancellationToken);
        return ThisWhenCompletedAsync(RunOnReservationAsync(
            owner,
            preparedScript,
            static (engine, prepared) => engine.Evaluate(in prepared),
            cancellationToken));
    }

    /// <summary>
    /// Invokes a JavaScript function asynchronously, properly awaiting any returned promise.
    /// </summary>
    /// <inheritdoc cref="EvaluateAsync(string, string, CancellationToken)" path="/remarks"/>
    /// <param name="propertyName">
    /// The name of one property of the global object holding the function, used as it is and never parsed as
    /// a path.
    /// </param>
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
    /// <inheritdoc cref="EvaluateAsync(string, string, CancellationToken)" path="/remarks"/>
    /// <param name="propertyName">
    /// The name of one property of the global object holding the function, used as it is and never parsed as
    /// a path.
    /// </param>
    /// <param name="cancellationToken">The token that cancels the call, script included; see the remarks.</param>
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

        var owner = ReserveAsyncHostOperation(entryToken: cancellationToken);
        return RunOnReservationAsync(
            owner,
            (propertyName, arguments),
            static (engine, state) => engine.Invoke(state.propertyName, state.arguments),
            cancellationToken);
    }

    /// <summary>
    /// Core async unwrap: if the result is a JsPromise, awaits its settlement
    /// without blocking any thread. For non-promise values, returns synchronously.
    /// </summary>
    internal Task<JsValue> UnwrapResultAsync(JsValue result, CancellationToken cancellationToken)
    {
        var owner = ReserveAsyncHostOperation(entryToken: cancellationToken);
        return RunOnReservationAsync(owner, result, static (_, value) => value, cancellationToken);
    }

    /// <summary>
    /// The body every script-running <c>*Async</c> entry shares, run on the reservation its public method
    /// took with <c>entryToken: cancellationToken</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// It refuses a token that is already cancelled before <paramref name="run"/> starts, so that case cancels
    /// the task deterministically instead of on whichever amortized check the interpreter reaches first; runs
    /// <paramref name="run"/> under the reservation; awaits whatever it returns; and releases the reservation
    /// in a finally, which is also what uninstalls the token. Being <c>async</c> is what puts every failure of
    /// those steps on the returned task.
    /// </para>
    /// <para>
    /// The state is passed rather than captured so a <see langword="static"/> lambda serves every entry, and
    /// a call allocates nothing for it.
    /// </para>
    /// </remarks>
    internal async Task<JsValue> RunOnReservationAsync<TState>(
        object owner,
        TState state,
        Func<Engine, TState, JsValue> run,
        CancellationToken cancellationToken)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            Task<JsValue> task;
            using (EnterHostCall(owner))
            {
                task = UnwrapResultAsync(run(this, state), owner, cancellationToken);
            }

            return await task.ConfigureAwait(false);
        }
        finally
        {
            ReleaseAsyncHostOperation(owner);
        }
    }

    private async Task<Engine> ThisWhenCompletedAsync(Task<JsValue> task)
    {
        await task.ConfigureAwait(false);
        return this;
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

                ThrowIfSettlementCancelled(cancellationToken, effectiveCt);

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
                catch (OperationCanceledException) when (effectiveCt.IsCancellationRequested)
                {
                    // Only a wake. The unbounded wait reports cancellation by throwing an exception that carries
                    // no token at all, and the bounded one by simply returning; the check below is what turns
                    // either into the exception the token that fired is owed.
                }
                finally
                {
                    Interlocked.Decrement(ref _hostCallbackAdmission);
                }

                // Before any continuation runs: a cancellation that arrived during the wait must not be
                // outrun by one last turn that happens to settle the promise.
                ThrowIfSettlementCancelled(cancellationToken, effectiveCt);

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
        catch (OperationCanceledException exception) when (hasTimeout
                                                           && exception.CancellationToken == effectiveCt
                                                           && !cancellationToken.IsCancellationRequested)
        {
            // The timeout CTS fired, not the user's cancellation token: ThrowIfSettlementCancelled is the
            // only thing that throws for effectiveCt. Any other cancellation - one a host callback raised for
            // a token of its own - keeps its own identity.
            // Translate to PromiseRejectedException to match sync API behavior.
            throw new PromiseRejectedException($"Timeout of {timeout} reached");
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

    /// <summary>
    /// The settlement loop's cancellation check: the caller's own token first, so a caller that cancelled
    /// is handed back the very token it passed, and then the wait's effective token, which past that point
    /// can only have fired for <c>PromiseTimeout</c>.
    /// </summary>
    private static void ThrowIfSettlementCancelled(CancellationToken cancellationToken, CancellationToken effectiveCt)
    {
        cancellationToken.ThrowIfCancellationRequested();
        effectiveCt.ThrowIfCancellationRequested();
    }
}

/// <summary>
/// The two tokens that say an engine's work has been abandoned: a registered
/// <see cref="CancellationConstraint"/>'s, which spans the engine's life, and the token of the <c>*Async</c>
/// entry in progress (<see cref="Engine._asyncEntryToken"/>), which spans that entry.
/// </summary>
/// <remarks>
/// <para>
/// Captured once, by whatever starts a piece of work (<see cref="Engine.CaptureCancellation"/>), and kept for
/// as long as that work runs. That is the whole lifetime rule: work that outlives the entry it started under —
/// a fetch still in flight after the call returned — goes on observing the token it captured, and work
/// started after the entry never sees that token at all.
/// </para>
/// <para>
/// Nothing here owns a source. Every source the link methods create belongs to the caller that asked, which
/// disposes it when its own work ends, so no consumer is ever handed a linked source somebody else may have
/// disposed — and a linked source disposed by its creator stops forwarding cancellation in silence. Only the
/// two raw tokens are ever shared. Linking one of those after its owner disposed it is harmless where it can
/// happen at all: a token disposed before it was cancelled can never fire, and the runtimes the web APIs
/// run on register against a disposed source as a no-op.
/// </para>
/// </remarks>
[StructLayout(LayoutKind.Auto)]
internal readonly record struct EngineCancellation(CancellationToken ConstraintToken, CancellationToken EntryToken)
{
    /// <summary>
    /// Whether either token has fired.
    /// </summary>
    public bool IsCancellationRequested => ConstraintToken.IsCancellationRequested || EntryToken.IsCancellationRequested;

    /// <summary>
    /// The token that fired, the constraint's first — the order <see cref="Engine.CheckAmortizedConstraints"/>
    /// observes them in — or <see langword="default"/> when neither has.
    /// </summary>
    public CancellationToken CancelledToken
    {
        get
        {
            if (ConstraintToken.IsCancellationRequested)
            {
                return ConstraintToken;
            }

            return EntryToken.IsCancellationRequested ? EntryToken : default;
        }
    }

    /// <summary>
    /// One token observing both tokens and <paramref name="other"/>. A linked source is created only when
    /// more than one of the three can fire; <paramref name="linkedSource"/> is then the caller's to dispose.
    /// </summary>
    public CancellationToken Link(CancellationToken other, out CancellationTokenSource? linkedSource)
    {
        linkedSource = null;

        var first = default(CancellationToken);
        var second = default(CancellationToken);
        var third = default(CancellationToken);
        var count = 0;
        Collect(other, ref first, ref second, ref third, ref count);
        Collect(ConstraintToken, ref first, ref second, ref third, ref count);
        Collect(EntryToken, ref first, ref second, ref third, ref count);

        switch (count)
        {
            case 0:
            case 1:
                return first;
            case 2:
                linkedSource = CancellationTokenSource.CreateLinkedTokenSource(first, second);
                return linkedSource.Token;
            default:
                linkedSource = CancellationTokenSource.CreateLinkedTokenSource(first, second, third);
                return linkedSource.Token;
        }
    }

    /// <summary>
    /// A new source linked to both tokens and to <paramref name="other"/>, for work that needs a source of
    /// its own to cancel or to arm a deadline on. The caller owns it.
    /// </summary>
    public CancellationTokenSource CreateLinkedTokenSource(CancellationToken other)
    {
        return HasDistinctEntryToken
            ? CancellationTokenSource.CreateLinkedTokenSource(other, ConstraintToken, EntryToken)
            : CancellationTokenSource.CreateLinkedTokenSource(other, ConstraintToken);
    }

    /// <summary>
    /// A new source linked to both tokens, for work that needs a source of its own to cancel. The caller
    /// owns it.
    /// </summary>
    public CancellationTokenSource CreateLinkedTokenSource()
    {
        return HasDistinctEntryToken
            ? CancellationTokenSource.CreateLinkedTokenSource(ConstraintToken, EntryToken)
            : CancellationTokenSource.CreateLinkedTokenSource(ConstraintToken);
    }

    /// <summary>
    /// Whether the entry token adds anything to link: a host that passes the token it also registered with
    /// <c>ObserveCancellation</c> gets one registration, not two.
    /// </summary>
    private bool HasDistinctEntryToken => EntryToken.CanBeCanceled && EntryToken != ConstraintToken;

    private static void Collect(
        CancellationToken token,
        ref CancellationToken first,
        ref CancellationToken second,
        ref CancellationToken third,
        ref int count)
    {
        if (!token.CanBeCanceled || (count > 0 && token == first) || (count > 1 && token == second))
        {
            return;
        }

        switch (count)
        {
            case 0:
                first = token;
                break;
            case 1:
                second = token;
                break;
            default:
                third = token;
                break;
        }

        count++;
    }
}
