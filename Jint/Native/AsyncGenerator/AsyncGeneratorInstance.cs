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
#pragma warning disable CS0649 // Field is never assigned to, and will always have its default value
    private readonly JsValue? _generatorBrand;
#pragma warning restore CS0649 // Field is never assigned to, and will always have its default value
    private JintStatementList _generatorBody = null!;

    /// <summary>
    /// Queue of pending next/return/throw requests.
    /// Each request has a PromiseCapability that resolves when the request completes.
    /// </summary>
    internal List<AsyncGeneratorRequest> _asyncGeneratorQueue = new();

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
    internal Iterator.IteratorInstance? _delegatingIterator;
    internal object? _delegatingYieldNode;
    internal CompletionType _delegationResumeType;
    internal ObjectInstance? _delegationInnerResult;
    internal bool _returnRequested;
    internal CompletionType _resumeCompletionType;

    // Await tracking (from AsyncFunctionInstance)
    internal object? _lastAwaitNode;
    internal JsValue? _resumeValue;
    internal bool _resumeWithThrow;
    internal Dictionary<object, JsValue>? _completedAwaits;

    // Finally block tracking (shared by both)
    internal CompletionType _pendingCompletionType;
    internal JsValue? _pendingCompletionValue;
    internal object? _currentFinallyStatement;

    /// <summary>
    /// Unified dictionary for all suspend data (for-of loops, destructuring patterns, etc.).
    /// </summary>
    internal Dictionary<object, SuspendData>? _suspendData;

    // ISuspendable implementation
    bool ISuspendable.IsSuspended => _asyncGeneratorState == AsyncGeneratorState.SuspendedYield || _asyncGeneratorState == AsyncGeneratorState.AwaitingReturn;

    bool ISuspendable.IsResuming
    {
        get => _isResuming;
        set => _isResuming = value;
    }

    CompletionType ISuspendable.PendingCompletionType
    {
        get => _pendingCompletionType;
        set => _pendingCompletionType = value;
    }

    JsValue? ISuspendable.PendingCompletionValue
    {
        get => _pendingCompletionValue;
        set => _pendingCompletionValue = value;
    }

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
                    _asyncGeneratorState = AsyncGeneratorState.Completed;
                    AsyncGeneratorResolve(completion.Value, true, promiseCapability);
                    return;
                }
                else if (_asyncGeneratorState == AsyncGeneratorState.SuspendedYield)
                {
                    _asyncGeneratorState = AsyncGeneratorState.AwaitingReturn;
                    // Await the return value before completing
                    AsyncGeneratorAwaitReturn(completion.Value, promiseCapability);
                    return;
                }
                else if (_asyncGeneratorState == AsyncGeneratorState.Completed)
                {
                    AsyncGeneratorResolve(completion.Value, true, promiseCapability);
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

        // Check if we suspended (yield or await)
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

        // Generator completed - clear the promise capability
        _currentPromiseCapability = null;
        _asyncGeneratorState = AsyncGeneratorState.Completed;

        if (result.Type == CompletionType.Normal || result.Type == CompletionType.Return)
        {
            AsyncGeneratorResolve(result.Value, true, promiseCapability);
        }
        else // Throw
        {
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
        // Wrap value in a promise
        var promise = CreateResolvedPromise(value);

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
    /// Gets or creates suspend data of the specified type.
    /// </summary>
    public T GetOrCreateSuspendData<T>(object key, Iterator.IteratorInstance iterator) where T : SuspendData, new()
    {
        _suspendData ??= new Dictionary<object, SuspendData>();
        if (!_suspendData.TryGetValue(key, out var data))
        {
            data = new T { Iterator = iterator };
            _suspendData[key] = data;
        }
        return (T) data;
    }

    /// <summary>
    /// Gets or creates suspend data of the specified type (for constructs without iterators).
    /// </summary>
    public T GetOrCreateSuspendData<T>(object key) where T : SuspendData, new()
    {
        _suspendData ??= [];
        if (!_suspendData.TryGetValue(key, out var data))
        {
            data = new T();
            _suspendData[key] = data;
        }
        return (T) data;
    }

    /// <summary>
    /// Tries to get existing suspend data of the specified type.
    /// </summary>
    public bool TryGetSuspendData<T>(object key, out T? data) where T : SuspendData
    {
        if (_suspendData?.TryGetValue(key, out var baseData) == true)
        {
            data = (T) baseData;
            return true;
        }
        data = default;
        return false;
    }

    /// <summary>
    /// Clears suspend data for the given key when the construct completes.
    /// </summary>
    public void ClearSuspendData(object key)
    {
        _suspendData?.Remove(key);
    }

    /// <summary>
    /// Closes all pending destructuring iterators.
    /// </summary>
    internal void CloseAllDestructuringIterators(CompletionType completionType)
    {
        if (_suspendData is null)
        {
            return;
        }

        foreach (var kvp in _suspendData)
        {
            if (kvp.Value is DestructuringSuspendData data && !data.Done)
            {
                data.Iterator?.Close(completionType);
                data.Done = true;
            }
        }
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
                AsyncGeneratorResolve(bodyResult.Value, true, promiseCapability);
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
