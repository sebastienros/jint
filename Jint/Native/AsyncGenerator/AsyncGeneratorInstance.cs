using Jint.Native.Disposable;
using Jint.Native.Iterator;
using Jint.Native.Object;
using Jint.Native.Promise;
using Jint.Runtime;
using Jint.Runtime.Descriptors;
using Jint.Runtime.Environments;
using Jint.Runtime.Interop;
using Jint.Runtime.Interpreter;

namespace Jint.Native.AsyncGenerator;

/// <summary>
/// https://tc39.es/ecma262/#sec-asyncgenerator-objects
/// </summary>
internal sealed class AsyncGeneratorInstance : ObjectInstance, ISuspendable
{
    internal AsyncGeneratorState _asyncGeneratorState;
    private ExecutionContext _asyncGeneratorContext;
    private JintStatementList _generatorBody = null!;

    /// <summary>
    /// Queue of pending next/return/throw requests.
    /// Each request has a PromiseCapability that resolves when the request completes.
    /// </summary>
    private readonly List<AsyncGeneratorRequest> _asyncGeneratorQueue = [];

    /// <summary>
    /// The promise capability of the currently executing request.
    /// Used by AsyncGeneratorYield to resolve the current request's promise.
    /// </summary>
    internal PromiseCapability? _currentPromiseCapability;

    // Generator yield tracking (from GeneratorInstance)
    public JsValue? _nextValue;
    public JsValue? _error;
    internal bool _isResuming;
    internal JsValue? _suspendedValue;
    internal object? _lastYieldNode;
    internal Dictionary<object, JsValue>? _yieldNodeValues;
    internal IteratorInstance? _delegatingIterator;
    internal object? _delegatingYieldNode;
    internal ICallable? _delegatingNextMethod;
    internal CompletionType _delegationResumeType;
    internal bool _returnRequested;
    internal CompletionType _resumeCompletionType;

    // Await suspension tracking (for bare `await` inside async generator body)
    internal bool _awaitSuspended;
    internal bool _resumeWithThrow;
    internal Dictionary<object, JsValue>? _completedAwaits;

    // Finally block tracking (shared by both)
    public object? _currentFinallyStatement;

    public SuspendDataDictionary Data { get; } = new();

    // ISuspendable implementation
    bool ISuspendable.IsSuspended => _asyncGeneratorState == AsyncGeneratorState.SuspendedYield
        || _asyncGeneratorState == AsyncGeneratorState.AwaitingReturn
        || _awaitSuspended;

    bool ISuspendable.IsResuming
    {
        get => _isResuming;
        set => _isResuming = value;
    }

    JsValue? ISuspendable.SuspendedValue => _suspendedValue;

    // The same reason as GeneratorInstance's identically shaped property, plus one an async
    // generator has on its own: a delegation that COMPLETES also resumes through a full replay of
    // the body (ResumeAfterDelegation), because the inner iterator's step settles on a later
    // microtask and the stack that started the delegation is long gone. ClearDelegationIterator
    // deliberately keeps _delegatingYieldNode across that hop so the replay re-enters at the yield*,
    // which is exactly the node the enclosing statements need to be told about as well.
    object? ISuspendable.LastSuspensionNode => _lastYieldNode ?? _delegatingYieldNode;

    bool ISuspendable.ReturnRequested => _returnRequested;

    object? ISuspendable.CurrentFinallyStatement
    {
        get => _currentFinallyStatement;
        set => _currentFinallyStatement = value;
    }

    public AsyncGeneratorInstance(Engine engine) : base(engine)
    {
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-asyncgeneratorstart
    /// </summary>
    public JsValue AsyncGeneratorStart(JintStatementList generatorBody)
    {
        var genContext = _engine.UpdateAsyncGenerator(this);
        _generatorBody = generatorBody;

        _asyncGeneratorContext = genContext;
        _asyncGeneratorState = AsyncGeneratorState.SuspendedStart;

        return Undefined;
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-asyncgeneratorenqueue
    /// Creates a new AsyncGeneratorRequest, enqueues it, and returns a Promise.
    /// If the generator is idle (suspended), starts processing the queue.
    /// </summary>
    internal JsValue AsyncGeneratorEnqueue(Completion completion)
    {
        var promiseCapability = PromiseConstructor.NewPromiseCapability(_engine, _engine.Realm.Intrinsics.Promise);
        var request = new AsyncGeneratorRequest(completion, promiseCapability);
        _asyncGeneratorQueue.Add(request);

        // If generator is idle and no pending yield/await operation, start processing
        // _currentPromiseCapability being set indicates a yield/await is in progress
        if (_asyncGeneratorState != AsyncGeneratorState.Executing && _currentPromiseCapability is null)
        {
            AsyncGeneratorResumeNext();
        }

        return promiseCapability.PromiseInstance;
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-asyncgeneratorresume and
    /// https://tc39.es/ecma262/#sec-asyncgeneratordrainqueue
    /// Processes the next request in the queue if any.
    /// </summary>
    internal void AsyncGeneratorResumeNext()
    {
        if (_asyncGeneratorState == AsyncGeneratorState.Executing || _asyncGeneratorState == AsyncGeneratorState.AwaitingReturn)
        {
            return;
        }

        if (_asyncGeneratorQueue.Count == 0)
        {
            return;
        }

        var next = _asyncGeneratorQueue[0];
        _asyncGeneratorQueue.RemoveAt(0);

        var completion = next.Completion;
        var promiseCapability = next.Capability;

        try
        {
            if (completion.Type == CompletionType.Normal)
            {
                // Corresponds to next() call
                if (_asyncGeneratorState == AsyncGeneratorState.SuspendedStart)
                {
                    _asyncGeneratorState = AsyncGeneratorState.Executing;
                    _nextValue = completion.Value;
                    _isResuming = false;
                    ResumeExecution(promiseCapability);
                }
                else if (_asyncGeneratorState == AsyncGeneratorState.SuspendedYield)
                {
                    _asyncGeneratorState = AsyncGeneratorState.Executing;
                    _nextValue = completion.Value;
                    _isResuming = true;
                    // a next() is a normal completion; see GeneratorInstance.GeneratorResume
                    _resumeCompletionType = CompletionType.Normal;
                    ResumeExecution(promiseCapability);
                }
                else if (_asyncGeneratorState == AsyncGeneratorState.Completed)
                {
                    AsyncGeneratorResolve(Undefined, true, promiseCapability);
                }
            }
            else if (completion.Type == CompletionType.Throw)
            {
                if (_asyncGeneratorState == AsyncGeneratorState.SuspendedStart)
                {
                    _asyncGeneratorState = AsyncGeneratorState.Completed;
                    AsyncGeneratorReject(completion.Value, promiseCapability);
                    return;
                }
                else if (_asyncGeneratorState == AsyncGeneratorState.SuspendedYield)
                {
                    _asyncGeneratorState = AsyncGeneratorState.Executing;
                    _nextValue = completion.Value;
                    _isResuming = true;
                    _resumeCompletionType = CompletionType.Throw;
                    ResumeExecution(promiseCapability);
                }
                else if (_asyncGeneratorState == AsyncGeneratorState.Completed)
                {
                    AsyncGeneratorReject(completion.Value, promiseCapability);
                    return;
                }
            }
            else // CompletionType.Return
            {
                if (_asyncGeneratorState == AsyncGeneratorState.SuspendedStart)
                {
                    // Per spec step 7: wrap value in PromiseResolve and await it
                    _asyncGeneratorState = AsyncGeneratorState.AwaitingReturn;
                    AsyncGeneratorAwaitReturn(completion.Value, promiseCapability);
                    return;
                }
                else if (_asyncGeneratorState == AsyncGeneratorState.SuspendedYield)
                {
                    // AsyncGeneratorResume: the generator is executing from here on, and the first thing it does at
                    // the yield is await the value.
                    _asyncGeneratorState = AsyncGeneratorState.Executing;
                    UnwrapReturnResumption(completion.Value, promiseCapability);
                    return;
                }
                else if (_asyncGeneratorState == AsyncGeneratorState.Completed)
                {
                    // Per spec: wrap value in PromiseResolve and await it
                    _asyncGeneratorState = AsyncGeneratorState.AwaitingReturn;
                    AsyncGeneratorAwaitReturn(completion.Value, promiseCapability);
                    return;
                }
            }
        }
        catch (JavaScriptException ex)
        {
            _asyncGeneratorState = AsyncGeneratorState.Completed;
            AsyncGeneratorReject(ex.Error, promiseCapability);
        }
    }

    /// <summary>
    /// Resumes execution of the async generator after a for-await-of suspension.
    /// Unlike <see cref="AsyncGeneratorResumeNext"/>, this continues the CURRENT request's
    /// execution rather than processing the next queued request (which would fail because
    /// the current request was already dequeued when processing started).
    /// Called from the for-await-of iterator promise fulfillment/rejection handlers.
    /// </summary>
    internal void AsyncGeneratorContinueForAwait(PromiseCapability promiseCapability)
    {
        _asyncGeneratorState = AsyncGeneratorState.Executing;
        _isResuming = true;
        ResumeExecution(promiseCapability);
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-asyncgeneratorunwrapyieldresumption for a <c>return(value)</c> request that
    /// finds the generator suspended at a <c>yield</c> (or in a <c>yield*</c>, whose yields are the same
    /// https://tc39.es/ecma262/#sec-asyncgeneratoryield): awaits <paramref name="value"/>, then resumes the body with a
    /// return completion of the awaited value, or with a throw completion of the reason the await rejected with.
    /// </summary>
    /// <remarks>
    /// The await is the first thing the resumed generator does, before any of its code runs, so it is performed here,
    /// before the body is replayed to the yield, rather than at the yield, which would suspend and replay the body a
    /// second time. The body therefore runs on a later microtask than the <c>return()</c> call, its <c>finally</c>
    /// blocks see the awaited value, and a rejection is thrown at the yield, where a <c>catch</c> around it sees it.
    /// The generator is executing all the while, so later requests queue behind this one. A <c>PromiseResolve</c> that
    /// throws (a promise whose <c>constructor</c> getter throws) is an abrupt await, thrown at the yield at once.
    /// </remarks>
    private void UnwrapReturnResumption(JsValue value, PromiseCapability promiseCapability)
    {
        _currentPromiseCapability = promiseCapability;

        // Await's PromiseResolve(%Promise%, value) runs in the generator's own execution context, as the spec's does:
        // it is the generator's realm whose %Promise% it uses.
        JsPromise? promise = null;
        JsValue? error = null;
        _engine.EnterExecutionContext(in _asyncGeneratorContext);
        try
        {
            promise = PromiseConstructor.ResolveForAwaitToPromise(_engine, value);
        }
        catch (JavaScriptException e)
        {
            error = e.Error;
        }
        finally
        {
            _engine.LeaveExecutionContext();
        }

        if (promise is null)
        {
            ResumeWithAbruptCompletion(CompletionType.Throw, error!, promiseCapability);
            return;
        }

        PromiseOperations.PerformPromiseThen(_engine, promise, new ReturnResumptionContinuation(this, promiseCapability));
    }

    /// <summary>
    /// Resumes the body where it is suspended with a return or throw completion of <paramref name="value"/>.
    /// </summary>
    private void ResumeWithAbruptCompletion(CompletionType type, JsValue value, PromiseCapability promiseCapability)
    {
        _nextValue = value;
        _isResuming = true;
        _resumeCompletionType = type;
        ResumeExecution(promiseCapability);
    }

    /// <summary>
    /// The reaction to the await in <see cref="UnwrapReturnResumption"/>: an engine-internal continuation rather than
    /// a pair of functions, as an <c>await</c> in the body has (<c>JintAwaitExpression</c>).
    /// </summary>
    private sealed class ReturnResumptionContinuation : IPromiseContinuation
    {
        private readonly AsyncGeneratorInstance _generator;
        private readonly PromiseCapability _capability;

        public ReturnResumptionContinuation(AsyncGeneratorInstance generator, PromiseCapability capability)
        {
            _generator = generator;
            _capability = capability;
        }

        public void Invoke(Engine engine, JsValue value, ReactionType type)
        {
            var completionType = type == ReactionType.Reject ? CompletionType.Throw : CompletionType.Return;
            _generator.ResumeWithAbruptCompletion(completionType, value, _capability);
        }
    }

    /// <summary>
    /// Resumes execution of the async generator with the current request.
    /// </summary>
    private void ResumeExecution(PromiseCapability promiseCapability)
    {
        _currentPromiseCapability = promiseCapability;

        var genContext = _asyncGeneratorContext;
        _engine.EnterExecutionContext(in genContext);

        var context = _engine._evaluationContext;
        Completion result;

        try
        {
            result = _generatorBody.Execute(context);
        }
        catch (JavaScriptException ex)
        {
            // Leave context up front so any async dispose work below runs (and its
            // Promise.then callbacks fire) without the generator's execution context
            // still on the stack — otherwise outer `await`s in the caller would
            // mis-route through SuspendForAwaitInAsyncGenerator.
            var env = _engine.ExecutionContext.LexicalEnvironment;
            _engine.LeaveExecutionContext();
            if (!env.HasDisposeResources)
            {
                SettleResumeRejection(ex.Error, promiseCapability);
                return;
            }
            DisposeResourcesHelper.DisposeAndThen(
                _engine,
                env,
                new Completion(CompletionType.Throw, ex.Error, null!),
                final => SettleResumeRejection(final.Value, promiseCapability));
            return;
        }
        catch
        {
            // a raw constraint exception (TimeoutException, ExecutionCanceledException) must not leak
            // the entered frame: the execution-context depth gates the host-boundary constraint
            // checks, and a leaked frame would satisfy the gate forever
            _engine.LeaveExecutionContext();
            throw;
        }

        // Check if suspended at an await expression
        if (_awaitSuspended)
        {
            _engine.LeaveExecutionContext();
            return;
        }

        // Check if we suspended at a yield
        if (_asyncGeneratorState == AsyncGeneratorState.SuspendedYield)
        {
            _engine.LeaveExecutionContext();
            return;
        }

        if (_asyncGeneratorState == AsyncGeneratorState.AwaitingReturn)
        {
            _engine.LeaveExecutionContext();
            return;
        }

        // Generator body completed — leave the generator's execution context BEFORE
        // dispatching dispose so its async Promise.then chain doesn't run with the
        // generator's context still on the stack. Settlement helpers below assume
        // the context has already been popped.
        var lexEnv = _engine.ExecutionContext.LexicalEnvironment;
        _engine.LeaveExecutionContext();
        if (!lexEnv.HasDisposeResources)
        {
            SettleResumeCompletion(result, promiseCapability);
            return;
        }
        DisposeResourcesHelper.DisposeAndThen(_engine, lexEnv, result,
            final => SettleResumeCompletion(final, promiseCapability));
    }

    private void SettleResumeRejection(JsValue error, PromiseCapability promiseCapability)
    {
        // Note: caller (ResumeExecution / its callback) has already left the
        // generator's execution context.
        _currentPromiseCapability = null;
        _asyncGeneratorState = AsyncGeneratorState.Completed;
        _completedAwaits = null;
        AsyncGeneratorReject(error, promiseCapability);
        AsyncGeneratorResumeNext();
    }

    private void SettleResumeCompletion(Completion final, PromiseCapability promiseCapability)
    {
        _currentPromiseCapability = null;

        if (final.Type == CompletionType.Throw)
        {
            _asyncGeneratorState = AsyncGeneratorState.Completed;
            _completedAwaits = null;
            AsyncGeneratorReject(final.Value, promiseCapability);
        }
        else if (final.Type == CompletionType.Return)
        {
            // https://tc39.es/ecma262/#sec-asyncgeneratorstart reports the body's return completion as it is: nothing
            // is left to await. A `return expr;` statement awaited its operand before it completed
            // (https://tc39.es/ecma262/#sec-return-statement-runtime-semantics-evaluation,
            // JintAsyncGeneratorReturnStatement), `return;` has no operand, a return() resumption was awaited before
            // the body resumed (UnwrapReturnResumption), and yield* either awaited its own return value already or
            // owes it no await.
            _asyncGeneratorState = AsyncGeneratorState.Completed;
            _completedAwaits = null;
            AsyncGeneratorResolve(final.Value, true, promiseCapability);
        }
        else if (final.Type == CompletionType.Normal)
        {
            _asyncGeneratorState = AsyncGeneratorState.Completed;
            _completedAwaits = null;
            AsyncGeneratorResolve(Undefined, true, promiseCapability);
        }

        AsyncGeneratorResumeNext();
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-asyncgeneratorcompletestep
    /// Resolves the current request's promise with an iterator result.
    /// </summary>
    internal void AsyncGeneratorResolve(JsValue value, bool done, PromiseCapability promiseCapability)
    {
        var iterResult = IteratorResult.CreateValueIteratorPosition(_engine, value, done ? JsBoolean.True : JsBoolean.False);
        promiseCapability.Resolve(iterResult);
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-asyncgeneratorcompletestep
    /// Rejects the current request's promise.
    /// </summary>
    internal static void AsyncGeneratorReject(JsValue exception, PromiseCapability promiseCapability)
    {
        promiseCapability.Reject(exception);
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-asyncgeneratorawaitreturn
    /// Awaits the return value before completing the generator: a return() request to a generator that has not
    /// started or has completed.
    /// </summary>
    private void AsyncGeneratorAwaitReturn(JsValue value, PromiseCapability promiseCapability)
    {
        // Let promiseCompletion be Completion(PromiseResolve(%Promise%, value)); an abrupt one completes the request.
        // PromiseResolve hands a %Promise% back as it is, where resolving a fresh promise with it cost two more
        // microtasks.
        JsPromise promise;
        try
        {
            promise = PromiseConstructor.ResolveForAwaitToPromise(_engine, value);
        }
        catch (JavaScriptException e)
        {
            _asyncGeneratorState = AsyncGeneratorState.Completed;
            AsyncGeneratorReject(e.Error, promiseCapability);
            AsyncGeneratorResumeNext();
            return;
        }

        // Create fulfillment handler
        var onFulfilled = new ClrFunction(_engine, "", (thisObj, args) =>
        {
            _asyncGeneratorState = AsyncGeneratorState.Completed;
            var resolvedValue = args.At(0);
            AsyncGeneratorResolve(resolvedValue, true, promiseCapability);
            AsyncGeneratorResumeNext();
            return Undefined;
        }, 1, PropertyFlag.Configurable);

        // Create rejection handler
        var onRejected = new ClrFunction(_engine, "", (thisObj, args) =>
        {
            _asyncGeneratorState = AsyncGeneratorState.Completed;
            var rejectedValue = args.At(0);
            AsyncGeneratorReject(rejectedValue, promiseCapability);
            AsyncGeneratorResumeNext();
            return Undefined;
        }, 1, PropertyFlag.Configurable);

        // Attach handlers
        PromiseOperations.PerformPromiseThen(_engine, promise, onFulfilled, onRejected, null!);
    }

    /// <summary>
    /// Resumes the body at the yield* expression with the delegation's outcome: its final value, or the
    /// completion parked in <see cref="_resumeCompletionType"/>. From there on the body runs, and settles the
    /// request, exactly as a resumption at a yield does.
    /// </summary>
    internal void ResumeAfterDelegation(JsValue returnValue, PromiseCapability promiseCapability)
    {
        _nextValue = returnValue;
        _isResuming = true;
        _asyncGeneratorState = AsyncGeneratorState.Executing;
        ResumeExecution(promiseCapability);
    }

    /// <summary>
    /// Clears iterator/method delegation state but preserves _delegatingYieldNode
    /// so ResumeAfterDelegation can re-enter at the yield* expression.
    /// </summary>
    internal void ClearDelegationIterator()
    {
        _delegatingIterator = null;
        _delegatingNextMethod = null;
    }

    /// <summary>
    /// Resumes the body with <paramref name="error"/> thrown at the yield* expression, which ends the
    /// delegation: the twin of <see cref="ResumeAfterDelegation"/> for a step the spec marks <c>?</c>.
    /// </summary>
    internal void ThrowAfterDelegation(JsValue error, PromiseCapability? promiseCapability)
    {
        // Only the iterator and its cached next go. _delegatingYieldNode stays, so the replay re-enters
        // at the yield*, finds no iterator, and reads the Throw completion parked here.
        ClearDelegationIterator();
        _resumeCompletionType = CompletionType.Throw;
        if (promiseCapability is not null)
        {
            ResumeAfterDelegation(error, promiseCapability);
        }
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-asyncgeneratoryield
    /// Yields a value from the async generator.
    /// Awaits the value, then resolves the current request's promise with the awaited value.
    /// </summary>
    internal JsValue AsyncGeneratorYield(JsValue value)
        => AsyncGeneratorYield(value, PromiseConstructor.ResolveForAwaitToPromise(_engine, value));

    /// <summary>
    /// <see cref="AsyncGeneratorYield(JsValue)"/> once the Await of <paramref name="value"/> has performed its
    /// PromiseResolve, which is <paramref name="promise"/>: that step may throw, and its throw is the yield's own, so it
    /// is taken before the generator is marked suspended.
    /// </summary>
    internal JsValue AsyncGeneratorYield(JsValue value, JsPromise promise)
    {
        // Clear stale await cache - after yield, the next resume starts from this point,
        // so any previously cached await values are irrelevant
        _completedAwaits?.Clear();

        if (_currentPromiseCapability is null)
        {
            Throw.InvalidOperationException("AsyncGeneratorYield called without current promise capability");
        }

        // Capture the current promise capability for use in closures
        // (it will be cleared after ResumeExecution returns, before callbacks run)
        var promiseCapability = _currentPromiseCapability;

        // Set state to suspendedYield
        _asyncGeneratorState = AsyncGeneratorState.SuspendedYield;
        _suspendedValue = value;

        // Per spec AsyncGeneratorYield step 4: Set value to ? Await(value). The caller has performed Await's
        // PromiseResolve(%Promise%, value), which returns the value as-is if it's already a %Promise% (avoids extra
        // thenable job tick for Promise values); what is left is to await it.

        // Create fulfillment handler
        var onFulfilled = new ClrFunction(_engine, "", (thisObj, args) =>
        {
            var awaitedValue = args.At(0);
            // Clear the current promise capability before resolving
            _currentPromiseCapability = null;
            // Resolve the current request's promise with the awaited value
            AsyncGeneratorResolve(awaitedValue, false, promiseCapability);
            // Process next request in queue
            AsyncGeneratorResumeNext();
            return Undefined;
        }, 1, PropertyFlag.Configurable);

        // Create rejection handler
        var onRejected = new ClrFunction(_engine, "", (thisObj, args) =>
        {
            var rejectedValue = args.At(0);
            // Clear the current promise capability before rejecting
            _currentPromiseCapability = null;
            _asyncGeneratorState = AsyncGeneratorState.Completed;
            AsyncGeneratorReject(rejectedValue, promiseCapability);
            AsyncGeneratorResumeNext();
            return Undefined;
        }, 1, PropertyFlag.Configurable);

        // Attach handlers
        PromiseOperations.PerformPromiseThen(_engine, promise, onFulfilled, onRejected, null!);

        return value;
    }
}
