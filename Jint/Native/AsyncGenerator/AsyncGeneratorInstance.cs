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

    object? ISuspendable.LastSuspensionNode => _lastYieldNode;

    bool ISuspendable.ReturnRequested => _returnRequested;

    CompletionType ISuspendable.PendingCompletionType { get; set; }

    JsValue? ISuspendable.PendingCompletionValue { get; set; }

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
    /// https://tc39.es/ecma262/#sec-asyncgeneratorresumenext
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
                    // Resume execution with Return completion so that:
                    // 1. yield* delegation forwards return to inner iterator
                    // 2. try/finally blocks execute their finally clauses
                    _asyncGeneratorState = AsyncGeneratorState.Executing;
                    _nextValue = completion.Value;
                    _isResuming = true;
                    _resumeCompletionType = CompletionType.Return;
                    ResumeExecution(promiseCapability);
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
    /// Resumes execution of the async generator with the current request.
    /// </summary>
    private void ResumeExecution(PromiseCapability promiseCapability)
    {
        _currentPromiseCapability = promiseCapability;

        var genContext = _asyncGeneratorContext;
        _engine.EnterExecutionContext(genContext);

        var context = _engine._activeEvaluationContext ?? new EvaluationContext(_engine);
        Completion result;

        try
        {
            result = _generatorBody.Execute(context);
        }
        catch (JavaScriptException ex)
        {
            _engine.LeaveExecutionContext();
            _currentPromiseCapability = null;
            _asyncGeneratorState = AsyncGeneratorState.Completed;
            AsyncGeneratorReject(ex.Error, promiseCapability);
            AsyncGeneratorResumeNext();
            return;
        }

        _engine.LeaveExecutionContext();

        // Check if suspended at an await expression
        if (_awaitSuspended)
        {
            // The await handler will call AsyncGeneratorContinueForAwait to resume.
            return;
        }

        // Check if we suspended at a yield
        if (_asyncGeneratorState == AsyncGeneratorState.SuspendedYield)
        {
            // Already handled by AsyncGeneratorYield
            // Don't clear _currentPromiseCapability - the yield callback will handle cleanup
            return;
        }

        if (_asyncGeneratorState == AsyncGeneratorState.AwaitingReturn)
        {
            // Already handled by AsyncGeneratorAwaitReturn
            return;
        }

        // Generator completed
        _currentPromiseCapability = null;

        if (result.Type == CompletionType.Return)
        {
            // Per spec step 4.e.i: Set result to Completion(Await(result.[[Value]])).
            // The return value must be awaited to unwrap promises.
            _asyncGeneratorState = AsyncGeneratorState.AwaitingReturn;
            AsyncGeneratorAwaitReturn(result.Value, promiseCapability);
        }
        else if (result.Type == CompletionType.Normal)
        {
            // Per spec step 4.c: If result.[[Type]] is normal, set resultValue to undefined
            _asyncGeneratorState = AsyncGeneratorState.Completed;
            AsyncGeneratorResolve(Undefined, true, promiseCapability);
        }
        else // Throw
        {
            _asyncGeneratorState = AsyncGeneratorState.Completed;
            AsyncGeneratorReject(result.Value, promiseCapability);
        }

        AsyncGeneratorResumeNext();
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-asyncgeneratorresolve
    /// Resolves the current request's promise with an iterator result.
    /// </summary>
    internal void AsyncGeneratorResolve(JsValue value, bool done, PromiseCapability promiseCapability)
    {
        var iterResult = IteratorResult.CreateValueIteratorPosition(_engine, value, done ? JsBoolean.True : JsBoolean.False);
        promiseCapability.Resolve.Call(Undefined, new[] { iterResult });
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-asyncgeneratorreject
    /// Rejects the current request's promise.
    /// </summary>
    internal static void AsyncGeneratorReject(JsValue exception, PromiseCapability promiseCapability)
    {
        promiseCapability.Reject.Call(JsValue.Undefined, new[] { exception });
    }

    /// <summary>
    /// Creates a promise resolved with the given value.
    /// </summary>
    private JsPromise CreateResolvedPromise(JsValue value)
    {
        var capability = PromiseConstructor.NewPromiseCapability(_engine, _engine.Realm.Intrinsics.Promise);
        capability.Resolve.Call(JsValue.Undefined, new[] { value });
        return (JsPromise) capability.PromiseInstance;
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-asyncgeneratorawaitreturn
    /// Awaits the return value before completing the generator.
    /// </summary>
    private void AsyncGeneratorAwaitReturn(JsValue value, PromiseCapability promiseCapability)
    {
        // Per spec: Let promise be Completion(PromiseResolve(%Promise%, value)).
        // If promiseCompletion is an abrupt completion, complete with the error.
        JsPromise promise;
        try
        {
            promise = CreateResolvedPromise(value);
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
    /// Resumes execution after a yield* delegation has completed.
    /// Called when the delegated iterator returns done=true.
    /// </summary>
    internal void ResumeAfterDelegation(JsValue returnValue, PromiseCapability promiseCapability)
    {
        // Set up state for resumption
        _nextValue = returnValue;
        _isResuming = true;
        _currentPromiseCapability = promiseCapability;
        _asyncGeneratorState = AsyncGeneratorState.Executing;

        // Resume execution - enter the generator context and continue
        _engine.EnterExecutionContext(_asyncGeneratorContext);

        try
        {
            var evalContext = _engine._activeEvaluationContext ?? new EvaluationContext(_engine);
            var bodyResult = _generatorBody.Execute(evalContext);

            _engine.LeaveExecutionContext();

            // Check if suspended at an await expression
            if (_awaitSuspended)
            {
                return;
            }

            // Check if we suspended (another yield)
            if (_asyncGeneratorState == AsyncGeneratorState.SuspendedYield)
            {
                // Already handled by yield or delegation
                return;
            }

            // Generator completed - resolve with done=true
            _currentPromiseCapability = null;
            _asyncGeneratorState = AsyncGeneratorState.Completed;
            if (promiseCapability is not null)
            {
                // Per spec: normal completion uses undefined, return completion uses the value
                var completionValue = bodyResult.Type == CompletionType.Return ? bodyResult.Value : Undefined;
                AsyncGeneratorResolve(completionValue, true, promiseCapability);
            }
            AsyncGeneratorResumeNext();
        }
        catch (JavaScriptException ex)
        {
            _engine.LeaveExecutionContext();
            _currentPromiseCapability = null;
            _asyncGeneratorState = AsyncGeneratorState.Completed;
            if (promiseCapability is not null)
            {
                AsyncGeneratorReject(ex.Error, promiseCapability);
            }
            AsyncGeneratorResumeNext();
        }
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-asyncgeneratoryield
    /// Yields a value from the async generator.
    /// Awaits the value, then resolves the current request's promise with the awaited value.
    /// </summary>
    internal JsValue AsyncGeneratorYield(JsValue value)
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

        // Await the value
        var promise = CreateResolvedPromise(value);

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
