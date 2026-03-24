using Jint.Native;
using Jint.Native.AsyncFunction;
using Jint.Native.AsyncGenerator;
using Jint.Native.Promise;
using Jint.Runtime.Descriptors;
using Jint.Runtime.Interop;

namespace Jint.Runtime.Interpreter.Expressions;

internal sealed class JintAwaitExpression : JintExpression
{
    private readonly JintExpression _awaitExpression;

    public JintAwaitExpression(AwaitExpression expression) : base(expression)
    {
        _awaitExpression = Build(expression.Argument);
    }

    protected override object EvaluateInternal(EvaluationContext context)
    {
        var engine = context.Engine;
        var asyncInstance = engine.ExecutionContext.AsyncFunction;
        var asyncGenerator = engine.ExecutionContext.AsyncGenerator;

        // === Async generator: return cached completed await value ===
        if (asyncGenerator?._completedAwaits?.TryGetValue(_expression, out var agCached) == true)
        {
            return agCached;
        }

        // === Async generator: check if resuming from THIS await point ===
        if (asyncGenerator is not null && asyncGenerator._isResuming
            && asyncGenerator._lastYieldNode is JintAwaitExpression lastAg
            && lastAg._expression == _expression)
        {
            var returnValue = asyncGenerator._nextValue ?? JsValue.Undefined;
            asyncGenerator._isResuming = false;
            asyncGenerator._lastYieldNode = null;

            if (asyncGenerator._resumeWithThrow)
            {
                asyncGenerator._resumeWithThrow = false;
                Throw.JavaScriptException(engine, returnValue, _expression.Location);
            }

            asyncGenerator._completedAwaits ??= new Dictionary<object, JsValue>();
            asyncGenerator._completedAwaits[_expression] = returnValue;
            return returnValue;
        }

        // === Async function: return cached completed await value ===
        if (asyncInstance?._completedAwaits?.TryGetValue(_expression, out var completedValue) == true)
        {
            return completedValue;
        }

        // === Async function: check if resuming from THIS await point ===
        if (asyncInstance is not null && asyncInstance._isResuming
            && asyncInstance._lastAwaitNode is JintExpression lastAwait
            && lastAwait._expression == _expression)
        {
            var returnValue = asyncInstance._resumeValue ?? JsValue.Undefined;

            asyncInstance._isResuming = false;
            asyncInstance._lastAwaitNode = null;

            if (asyncInstance._resumeWithThrow)
            {
                asyncInstance._resumeWithThrow = false;
                Throw.JavaScriptException(engine, returnValue, _expression.Location);
            }

            asyncInstance._completedAwaits ??= new Dictionary<object, JsValue>();
            asyncInstance._completedAwaits[_expression] = returnValue;

            return returnValue;
        }

        // === Evaluate the awaited expression ===
        var value = _awaitExpression.GetValue(context);

        // If the argument evaluation itself caused suspension (e.g., yield inside await argument)
        if (context.IsSuspended())
        {
            return value;
        }

        // Wrap in promise if not already a promise
        JsPromise promise;
        if (value is JsPromise p)
        {
            promise = p;
        }
        else
        {
            promise = new JsPromise(engine)
            {
                _prototype = engine.Realm.Intrinsics.Promise.PrototypeObject
            };
            promise.Resolve(value);
        }

        // === Async generator: suspend for await ===
        if (asyncGenerator is not null)
        {
            return SuspendForAwaitInAsyncGenerator(context, asyncGenerator, promise);
        }

        // === Async function: suspend for await ===
        if (asyncInstance is not null)
        {
            return SuspendForAwait(context, asyncInstance, promise);
        }

        // Fallback for non-async contexts - use blocking behavior
        try
        {
            return value.UnwrapIfPromise(engine.Options.Constraints.PromiseTimeout);
        }
        catch (PromiseRejectedException e)
        {
            Throw.JavaScriptException(engine, e.RejectedValue, _expression.Location);
            return null;
        }
    }

    /// <summary>
    /// Suspends the async generator at an await expression, creating promise handlers
    /// that will resume execution when the awaited promise settles.
    /// Follows the same pattern as SuspendForAsyncIteration in JintForInForOfStatement.
    /// </summary>
    private JsValue SuspendForAwaitInAsyncGenerator(
        EvaluationContext context,
        AsyncGeneratorInstance asyncGenerator,
        JsPromise promise)
    {
        var engine = context.Engine;

        // Mark suspension point - store 'this' (JintAwaitExpression instance) as the node identity,
        // same convention as AsyncFunctionInstance._lastAwaitNode.
        // _lastYieldNode is object? and yield stores AST YieldExpression nodes, so no collision.
        asyncGenerator._lastYieldNode = this;
        asyncGenerator._awaitSuspended = true;

        // Capture the current promise capability. The request was already dequeued by
        // AsyncGeneratorResumeNext(), so we must continue THIS request's execution on resume.
        var currentCapability = asyncGenerator._currentPromiseCapability!;

        // Resume directly in the reaction handler (like async functions), not via AddToEventLoop.
        // This ensures await consumes exactly 1 microtask tick for correct interleaving.
        var onFulfilled = new ClrFunction(engine, "", (_, args) =>
        {
            var resolvedValue = args.At(0);
            asyncGenerator._nextValue = resolvedValue;
            asyncGenerator._resumeWithThrow = false;
            asyncGenerator._awaitSuspended = false;
            asyncGenerator.AsyncGeneratorContinueForAwait(currentCapability);
            return JsValue.Undefined;
        }, 1, PropertyFlag.Configurable);

        var onRejected = new ClrFunction(engine, "", (_, args) =>
        {
            var rejectedValue = args.At(0);
            asyncGenerator._nextValue = rejectedValue;
            asyncGenerator._resumeWithThrow = true;
            asyncGenerator._awaitSuspended = false;
            asyncGenerator.AsyncGeneratorContinueForAwait(currentCapability);
            return JsValue.Undefined;
        }, 1, PropertyFlag.Configurable);

        PromiseOperations.PerformPromiseThen(engine, promise, onFulfilled, onRejected, null!);

        // Return undefined - the body will unwind because IsSuspended is now true
        return JsValue.Undefined;
    }

    private JsValue SuspendForAwait(EvaluationContext context, AsyncFunctionInstance asyncInstance, JsPromise promise)
    {
        var engine = context.Engine;

        asyncInstance._lastAwaitNode = this;
        asyncInstance._state = AsyncFunctionState.SuspendedAwait;
        asyncInstance._savedContext = engine.ExecutionContext;

        var onFulfilled = new ClrFunction(engine, "", (_, args) =>
        {
            var resolvedValue = args.At(0);
            asyncInstance._resumeValue = resolvedValue;
            asyncInstance._resumeWithThrow = false;
            AsyncFunctionResume(engine, asyncInstance);
            return JsValue.Undefined;
        }, 1, PropertyFlag.Configurable);

        var onRejected = new ClrFunction(engine, "", (_, args) =>
        {
            var rejectedValue = args.At(0);
            asyncInstance._resumeValue = rejectedValue;
            asyncInstance._resumeWithThrow = true;
            AsyncFunctionResume(engine, asyncInstance);
            return JsValue.Undefined;
        }, 1, PropertyFlag.Configurable);

        PromiseOperations.PerformPromiseThen(engine, promise, onFulfilled, onRejected, null!);

        return JsValue.Undefined;
    }

    /// <summary>
    /// Resumes execution of a suspended async function.
    /// Called from promise reaction jobs when the awaited promise settles.
    /// </summary>
    internal static void AsyncFunctionResume(Engine engine, AsyncFunctionInstance asyncInstance)
    {
        if (asyncInstance._state == AsyncFunctionState.Completed)
        {
            return;
        }

        asyncInstance._state = AsyncFunctionState.Executing;
        asyncInstance._isResuming = true;

        engine.EnterExecutionContext(asyncInstance._savedContext);

        var context = engine._activeEvaluationContext ?? new EvaluationContext(engine);

        Completion result;
        try
        {
            if (asyncInstance._body is not null)
            {
                result = asyncInstance._body.Execute(context);
            }
            else if (asyncInstance._bodyFunction is not null)
            {
                result = asyncInstance._bodyFunction(context);
            }
            else
            {
                result = new Completion(CompletionType.Normal, JsValue.Undefined, null!);
            }
        }
        catch (JavaScriptException e)
        {
            var env = engine.ExecutionContext.LexicalEnvironment;
            var disposeResult = env.DisposeResources(new Completion(CompletionType.Throw, e.Error, null!));
            engine.LeaveExecutionContext();
            asyncInstance._state = AsyncFunctionState.Completed;
            asyncInstance._capability.Reject.Call(JsValue.Undefined, disposeResult.Value);
            return;
        }

        if (asyncInstance._state == AsyncFunctionState.SuspendedAwait)
        {
            engine.LeaveExecutionContext();
            return;
        }

        var lexEnv = engine.ExecutionContext.LexicalEnvironment;
        result = lexEnv.DisposeResources(result);

        engine.LeaveExecutionContext();

        asyncInstance._state = AsyncFunctionState.Completed;

        if (result.Type == CompletionType.Throw)
        {
            asyncInstance._capability.Reject.Call(JsValue.Undefined, result.Value);
        }
        else if (result.Type == CompletionType.Return)
        {
            asyncInstance._capability.Resolve.Call(JsValue.Undefined, result.Value);
        }
        else
        {
            asyncInstance._capability.Resolve.Call(JsValue.Undefined, JsValue.Undefined);
        }
    }
}
