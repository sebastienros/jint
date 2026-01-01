using Jint.Native;
using Jint.Native.AsyncFunction;
using Jint.Native.Promise;
using Jint.Runtime.Descriptors;
using Jint.Runtime.Interop;

namespace Jint.Runtime.Interpreter.Expressions;

internal sealed class JintAwaitExpression : JintExpression
{
    private JintExpression _awaitExpression = null!;
    private bool _initialized;

    public JintAwaitExpression(AwaitExpression expression) : base(expression)
    {
        _initialized = false;
    }

    protected override object EvaluateInternal(EvaluationContext context)
    {
        if (!_initialized)
        {
            _awaitExpression = Build(((AwaitExpression) _expression).Argument);
            _initialized = true;
        }

        var engine = context.Engine;
        var asyncInstance = engine.ExecutionContext.AsyncFunction;

        // Check if this await was already completed (for expression bodies that re-evaluate)
        // Use the AST expression node as key since JintAwaitExpression may be recreated on each evaluation
        if (asyncInstance?._completedAwaits?.TryGetValue(_expression, out var completedValue) == true)
        {
            return completedValue;
        }

        // If resuming from THIS await point, return the resume value (or throw if rejected)
        if (asyncInstance is not null && asyncInstance._isResuming
            && asyncInstance._lastAwaitNode is JintExpression lastAwait
            && lastAwait._expression == _expression)
        {
            var returnValue = asyncInstance._resumeValue ?? JsValue.Undefined;

            // Clear resume state
            asyncInstance._isResuming = false;
            asyncInstance._lastAwaitNode = null;

            // If resuming with throw (rejected promise), throw at this point
            if (asyncInstance._resumeWithThrow)
            {
                asyncInstance._resumeWithThrow = false;
                Throw.JavaScriptException(engine, returnValue, _expression.Location);
            }

            // Cache the completed value for expression bodies that re-evaluate
            // (e.g., "x = await a + await b" - when we resume from await b, we need await a's cached value)
            asyncInstance._completedAwaits ??= new Dictionary<object, JsValue>();
            asyncInstance._completedAwaits[_expression] = returnValue;

            return returnValue;
        }

        // Evaluate the awaited expression
        var value = _awaitExpression.GetValue(context);

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

        // If we have an async function context, suspend execution
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

    private JsValue SuspendForAwait(EvaluationContext context, AsyncFunctionInstance asyncInstance, JsPromise promise)
    {
        var engine = context.Engine;

        // Mark suspension point - use 'this' since JintAwaitExpression is per-run, AST nodes can be shared
        asyncInstance._lastAwaitNode = this;
        asyncInstance._state = AsyncFunctionState.SuspendedAwait;
        asyncInstance._savedContext = engine.ExecutionContext;

        // Create resume handlers that will be called when the promise settles
        var onFulfilled = new ClrFunction(engine, "", (_, args) =>
        {
            var resolvedValue = args.At(0);

            // Queue job to resume async function with fulfilled value
            engine.AddToEventLoop(() =>
            {
                asyncInstance._resumeValue = resolvedValue;
                asyncInstance._resumeWithThrow = false;
                AsyncFunctionResume(engine, asyncInstance);
            });

            return JsValue.Undefined;
        }, 1, PropertyFlag.Configurable);

        var onRejected = new ClrFunction(engine, "", (_, args) =>
        {
            var rejectedValue = args.At(0);

            // Queue job to resume async function with rejection (will throw at await point)
            engine.AddToEventLoop(() =>
            {
                asyncInstance._resumeValue = rejectedValue;
                asyncInstance._resumeWithThrow = true;
                AsyncFunctionResume(engine, asyncInstance);
            });

            return JsValue.Undefined;
        }, 1, PropertyFlag.Configurable);

        // Attach the reaction handlers to the promise
        // We use a dummy capability since we don't need the result promise
        var resultCapability = PromiseConstructor.NewPromiseCapability(engine, engine.Realm.Intrinsics.Promise);
        PromiseOperations.PerformPromiseThen(engine, promise, onFulfilled, onRejected, resultCapability);

        // Return undefined - the actual value comes when we resume
        return JsValue.Undefined;
    }

    /// <summary>
    /// Resumes execution of a suspended async function.
    /// Called from promise reaction jobs when the awaited promise settles.
    /// </summary>
    internal static void AsyncFunctionResume(Engine engine, AsyncFunctionInstance asyncInstance)
    {
        // Ignore stale reactions - if the async function already completed, don't re-execute.
        // This can happen with nested awaits that queue multiple promise reactions.
        if (asyncInstance._state == AsyncFunctionState.Completed)
        {
            return;
        }

        asyncInstance._state = AsyncFunctionState.Executing;
        asyncInstance._isResuming = true;

        // Restore the execution context and continue executing the body
        engine.EnterExecutionContext(asyncInstance._savedContext);

        // Ensure we have an evaluation context (may be called from event loop outside script evaluation)
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
            engine.LeaveExecutionContext();
            asyncInstance._state = AsyncFunctionState.Completed;
            asyncInstance._capability.Reject.Call(JsValue.Undefined, e.Error);
            return;
        }

        engine.LeaveExecutionContext();

        // Check if suspended again at another await
        if (asyncInstance._state == AsyncFunctionState.SuspendedAwait)
        {
            // Still suspended - promise reaction will resume again
            return;
        }

        // Completed - resolve or reject the async function's return promise
        asyncInstance._state = AsyncFunctionState.Completed;

        if (result.Type == CompletionType.Return)
        {
            asyncInstance._capability.Resolve.Call(JsValue.Undefined, result.Value);
        }
        else if (result.Type == CompletionType.Throw)
        {
            asyncInstance._capability.Reject.Call(JsValue.Undefined, result.Value);
        }
        else
        {
            // Normal completion (no return statement)
            asyncInstance._capability.Resolve.Call(JsValue.Undefined, JsValue.Undefined);
        }
    }
}
