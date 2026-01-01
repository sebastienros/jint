using Jint.Native;
using Jint.Native.AsyncGenerator;
using Jint.Native.Generator;
using Jint.Native.Iterator;
using Jint.Native.Object;
using Jint.Native.Promise;
using Jint.Runtime.Interop;

namespace Jint.Runtime.Interpreter.Expressions;

internal sealed class JintYieldExpression : JintExpression
{
    public JintYieldExpression(YieldExpression expression) : base(expression)
    {
    }

    protected override object EvaluateInternal(EvaluationContext context)
    {
        var expression = (YieldExpression) _expression;
        var generator = context.Engine.ExecutionContext.Generator;
        var asyncGenerator = context.Engine.ExecutionContext.AsyncGenerator;

        // Check if we're resuming from a yield* delegation (sync generators)
        if (generator is not null
            && generator._delegatingIterator is not null
            && ReferenceEquals(_expression, generator._delegatingYieldNode))
        {
            // Continue the yield* delegation loop
            return ContinueYieldDelegate(context, generator);
        }

        // Check if we're resuming from a suspended yield (sync generators)
        if (generator is not null && generator._isResuming)
        {
            // Check if THIS yield expression is the one we suspended at (by node identity)
            if (ReferenceEquals(_expression, generator._lastYieldNode))
            {
                // This is the yield we suspended at - return _nextValue as the result
                var returnValue = generator._nextValue ?? JsValue.Undefined;

                // Clear resume state
                generator._isResuming = false;
                generator._lastYieldNode = null;

                // If we're resuming with a Throw completion, throw the exception at the yield point
                // This allows try-catch blocks to handle the exception properly
                if (generator._resumeCompletionType == CompletionType.Throw)
                {
                    generator._resumeCompletionType = CompletionType.Normal; // Reset for future
                    Throw.JavaScriptException(context.Engine, returnValue, AstExtensions.DefaultLocation);
                }

                // If we're resuming with a Return completion, signal return request
                // This allows for-of loops to close their iterators properly via finally blocks
                if (generator._resumeCompletionType == CompletionType.Return)
                {
                    generator._resumeCompletionType = CompletionType.Normal; // Reset for future
                    generator._returnRequested = true;
                    generator._suspendedValue = returnValue;
                    return returnValue; // Callers check _returnRequested flag
                }

                // Store this value for future iterations (e.g., same yield in a loop)
                generator._yieldNodeValues ??= new Dictionary<object, JsValue>();
                generator._yieldNodeValues[_expression] = returnValue;

                return returnValue;
            }

            // Check if this yield has a stored value from a previous resume
            // This happens when re-executing a loop - the same yield node was resumed before
            if (generator._yieldNodeValues?.TryGetValue(_expression, out var storedValue) == true)
            {
                return storedValue;
            }

            // This is a new yield that hasn't been processed yet
            // Fall through to normal yield logic
        }

        // Check if we're resuming from a yield* delegation (async generators)
        if (asyncGenerator is not null
            && ReferenceEquals(_expression, asyncGenerator._delegatingYieldNode))
        {
            if (asyncGenerator._delegatingIterator is not null)
            {
                // Continue the yield* delegation loop
                return ContinueAsyncYieldDelegate(context, asyncGenerator);
            }
            else
            {
                // Delegation has completed - return the final value and continue after yield*
                var returnValue = asyncGenerator._nextValue ?? JsValue.Undefined;
                asyncGenerator._delegatingYieldNode = null;
                asyncGenerator._isResuming = false;
                asyncGenerator._nextValue = null;
                return returnValue;
            }
        }

        // Check if we're resuming from a suspended yield (async generators)
        if (asyncGenerator is not null && asyncGenerator._isResuming)
        {
            // Check if THIS yield expression is the one we suspended at (by node identity)
            if (ReferenceEquals(_expression, asyncGenerator._lastYieldNode))
            {
                // This is the yield we suspended at - return _nextValue as the result
                var returnValue = asyncGenerator._nextValue ?? JsValue.Undefined;

                // Clear resume state
                asyncGenerator._isResuming = false;
                asyncGenerator._lastYieldNode = null;

                // If we're resuming with a Throw completion, throw the exception at the yield point
                if (asyncGenerator._resumeCompletionType == CompletionType.Throw)
                {
                    asyncGenerator._resumeCompletionType = CompletionType.Normal;
                    Throw.JavaScriptException(context.Engine, returnValue, AstExtensions.DefaultLocation);
                }

                // If we're resuming with a Return completion, signal return request
                if (asyncGenerator._resumeCompletionType == CompletionType.Return)
                {
                    asyncGenerator._resumeCompletionType = CompletionType.Normal;
                    asyncGenerator._returnRequested = true;
                    asyncGenerator._suspendedValue = returnValue;
                    return returnValue;
                }

                // Store this value for future iterations
                asyncGenerator._yieldNodeValues ??= new Dictionary<object, JsValue>();
                asyncGenerator._yieldNodeValues[_expression] = returnValue;

                return returnValue;
            }

            // Check if this yield has a stored value from a previous resume
            if (asyncGenerator._yieldNodeValues?.TryGetValue(_expression, out var storedValue) == true)
            {
                return storedValue;
            }

            // This is a new yield that hasn't been processed yet
            // Fall through to normal yield logic
        }

        // Normal yield: evaluate argument and yield the value
        JsValue value;
        if (expression.Argument is not null)
        {
            value = Build(expression.Argument).GetValue(context);

            // If the argument evaluation suspended the generator (nested yield), propagate
            // the suspension up without yielding again - the inner yield already suspended
            if (context.IsSuspended())
            {
                return value;
            }
        }
        else
        {
            value = JsValue.Undefined;
        }

        if (expression.Delegate)
        {
            return YieldDelegate(context, value);
        }

        // Store the node we're yielding at for resume tracking
        if (generator is not null)
        {
            generator._lastYieldNode = _expression;
        }
        else if (asyncGenerator is not null)
        {
            asyncGenerator._lastYieldNode = _expression;
        }

        return Yield(context, value);
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-generator-function-definitions-runtime-semantics-evaluation
    /// Starts the yield* delegation loop.
    /// </summary>
    private JsValue YieldDelegate(EvaluationContext context, JsValue value)
    {
        var engine = context.Engine;
        var generatorKind = engine.ExecutionContext.GetGeneratorKind();
        var iterator = value.GetIterator(engine.Realm, generatorKind);

        if (generatorKind == GeneratorKind.Async)
        {
            var asyncGenerator = engine.ExecutionContext.AsyncGenerator!;

            // Store the iterator for delegation continuation
            asyncGenerator._delegatingIterator = iterator;
            asyncGenerator._delegatingYieldNode = _expression;

            // Start the first iteration with a normal completion
            return RunAsyncYieldDelegateLoop(context, asyncGenerator, iterator, CompletionType.Normal, JsValue.Undefined);
        }
        else
        {
            var generator = engine.ExecutionContext.Generator!;

            // Store the iterator for delegation continuation
            generator._delegatingIterator = iterator;
            generator._delegatingYieldNode = _expression;

            // Start the first iteration with a normal completion
            return RunYieldDelegateLoop(context, generator, iterator, CompletionType.Normal, JsValue.Undefined);
        }
    }

    /// <summary>
    /// Continues the yield* delegation loop after resuming from a suspension.
    /// </summary>
    private static JsValue ContinueYieldDelegate(EvaluationContext context, GeneratorInstance generator)
    {
        var iterator = generator._delegatingIterator!;
        var resumeType = generator._delegationResumeType;
        var resumeValue = generator._nextValue ?? JsValue.Undefined;

        // Clear the resuming flag since we're handling it here
        generator._isResuming = false;

        return RunYieldDelegateLoop(context, generator, iterator, resumeType, resumeValue);
    }

    /// <summary>
    /// Continues the yield* delegation loop for async generators after resuming from a suspension.
    /// </summary>
    private static JsValue ContinueAsyncYieldDelegate(EvaluationContext context, AsyncGeneratorInstance asyncGenerator)
    {
        var iterator = asyncGenerator._delegatingIterator!;
        var resumeType = asyncGenerator._delegationResumeType;
        var resumeValue = asyncGenerator._nextValue ?? JsValue.Undefined;

        // Clear the resuming flag since we're handling it here
        asyncGenerator._isResuming = false;

        return RunAsyncYieldDelegateLoop(context, asyncGenerator, iterator, resumeType, resumeValue);
    }

    /// <summary>
    /// Runs the yield* delegation loop. Called both for initial delegation and continuation.
    /// </summary>
    private static JsValue RunYieldDelegateLoop(
        EvaluationContext context,
        GeneratorInstance generator,
        IteratorInstance iterator,
        CompletionType receivedType,
        JsValue receivedValue)
    {
        var engine = context.Engine;
        var generatorKind = engine.ExecutionContext.GetGeneratorKind();

        while (true)
        {
            if (receivedType == CompletionType.Normal)
            {
                // Per spec 14.4.14 step 7.a.i: Call iterator.next with received.[[Value]]
                // This passes the value from generator.next() to the inner iterator
                var iteratorInstance = iterator.Instance;
                var nextMethod = iteratorInstance.GetMethod(CommonProperties.Next);
                if (nextMethod is null)
                {
                    Throw.TypeError(engine.Realm, "Iterator does not have next method");
                    return JsValue.Undefined; // unreachable
                }

                var innerResultValue = nextMethod.Call(iteratorInstance, new[] { receivedValue });
                if (generatorKind == GeneratorKind.Async)
                {
                    innerResultValue = Await(context, innerResultValue);
                }

                if (innerResultValue is not ObjectInstance innerResult)
                {
                    Throw.TypeError(engine.Realm, "Iterator result is not an object");
                    return JsValue.Undefined; // unreachable
                }

                var done = IteratorComplete(innerResult);
                if (done)
                {
                    // Delegation complete - clean up and return the final value
                    generator._delegatingIterator = null;
                    generator._delegatingYieldNode = null;
                    return IteratorValue(innerResult);
                }

                if (generatorKind == GeneratorKind.Async)
                {
                    var asyncReceived = AsyncGeneratorYield(context, IteratorValue(innerResult));
                    receivedType = asyncReceived.Type;
                    receivedValue = asyncReceived.Value;
                }
                else
                {
                    // Yield the value from the inner iterator and suspend
                    // Per spec, pass innerResult directly to GeneratorYield to preserve its exact state
                    SuspendForDelegation(context, generator, innerResult, CompletionType.Normal);

                    // Check if suspended - if so, return to propagate suspension up the call stack
                    if (context.IsSuspended())
                    {
                        return JsValue.Undefined;
                    }
                }
            }
            else if (receivedType == CompletionType.Throw)
            {
                var iteratorInstance = iterator.Instance;
                var throwMethod = iteratorInstance.GetMethod("throw");
                if (throwMethod is not null)
                {
                    var innerResult = throwMethod.Call(iteratorInstance, new[] { receivedValue });
                    if (generatorKind == GeneratorKind.Async)
                    {
                        innerResult = Await(context, innerResult);
                    }

                    if (innerResult is not ObjectInstance innerObj)
                    {
                        Throw.TypeError(engine.Realm, "Iterator result is not an object");
                        return JsValue.Undefined; // unreachable
                    }

                    var done = IteratorComplete(innerObj);
                    if (done)
                    {
                        // Delegation complete - clean up and return the final value
                        generator._delegatingIterator = null;
                        generator._delegatingYieldNode = null;
                        return IteratorValue(innerObj);
                    }

                    if (generatorKind == GeneratorKind.Async)
                    {
                        var asyncReceived = AsyncGeneratorYield(context, IteratorValue(innerObj));
                        receivedType = asyncReceived.Type;
                        receivedValue = asyncReceived.Value;
                    }
                    else
                    {
                        // Yield the result and suspend
                        SuspendForDelegation(context, generator, innerObj, CompletionType.Normal);

                        // Check if suspended - if so, return to propagate suspension up the call stack
                        if (context.IsSuspended())
                        {
                            return JsValue.Undefined;
                        }
                    }
                }
                else
                {
                    // NOTE: If iterator does not have a throw method, this throw is going to terminate the yield* loop.
                    // But first we need to give iterator a chance to clean up.
                    if (generatorKind == GeneratorKind.Async)
                    {
                        AsyncIteratorClose(iterator, CompletionType.Normal);
                    }
                    else
                    {
                        iterator.Close(CompletionType.Normal);
                    }

                    generator._delegatingIterator = null;
                    generator._delegatingYieldNode = null;
                    Throw.TypeError(engine.Realm, "Iterator does not have throw method");
                }
            }
            else // receivedType == CompletionType.Return
            {
                var iteratorInstance = iterator.Instance;
                var returnMethod = iteratorInstance.GetMethod("return");
                if (returnMethod is null)
                {
                    var temp = receivedValue;
                    if (generatorKind == GeneratorKind.Async)
                    {
                        temp = Await(context, receivedValue);
                    }

                    // Per spec: "Return Completion(received)" - the generator should complete
                    // with the received return value, but we must let try-finally blocks execute.
                    // Use _returnRequested to signal return completion through normal execution flow.
                    generator._delegatingIterator = null;
                    generator._delegatingYieldNode = null;
                    generator._returnRequested = true;
                    generator._suspendedValue = temp;

                    // Return to let the normal execution flow handle the return,
                    // which will trigger any finally blocks before completing
                    return temp;
                }

                var innerReturnResult = returnMethod.Call(iteratorInstance, new[] { receivedValue });
                if (generatorKind == GeneratorKind.Async)
                {
                    innerReturnResult = Await(context, innerReturnResult);
                }

                if (innerReturnResult is not ObjectInstance innerReturnObj)
                {
                    Throw.TypeError(engine.Realm, "Iterator result is not an object");
                    return JsValue.Undefined; // unreachable
                }

                var done = IteratorComplete(innerReturnObj);
                if (done)
                {
                    // Per spec 14.4.14 step 5.c.vii: Return Completion{[[Type]]: return, [[Value]]: value}
                    // This means we need to signal a Return completion to the generator
                    var returnValue = IteratorValue(innerReturnObj);
                    generator._delegatingIterator = null;
                    generator._delegatingYieldNode = null;

                    // Signal return request - callers check _returnRequested flag
                    // This will trigger finally blocks and then complete the generator
                    generator._returnRequested = true;
                    generator._suspendedValue = returnValue;
                    return returnValue;
                }

                if (generatorKind == GeneratorKind.Async)
                {
                    var asyncReceived = AsyncGeneratorYield(context, IteratorValue(innerReturnObj));
                    receivedType = asyncReceived.Type;
                    receivedValue = asyncReceived.Value;
                }
                else
                {
                    // Yield the result and suspend
                    SuspendForDelegation(context, generator, innerReturnObj, CompletionType.Normal);

                    // Check if suspended - if so, return to propagate suspension up the call stack
                    if (context.IsSuspended())
                    {
                        return JsValue.Undefined;
                    }
                }
            }
        }
    }

    /// <summary>
    /// Suspends the generator during yield* delegation.
    /// Sets generator state to suspendedYield - callers check context.IsSuspended().
    /// </summary>
    private static void SuspendForDelegation(
        EvaluationContext context,
        GeneratorInstance generator,
        ObjectInstance innerResult,
        CompletionType expectedResumeType)
    {
        generator._generatorState = GeneratorState.SuspendedYield;
        // Don't access 'value' property here - the spec says we should not access it until needed
        // We pass through the entire innerResult and let the caller access 'value' when they want
        generator._suspendedValue = null;
        generator._delegationResumeType = expectedResumeType;
        // Store the inner result to return it directly (preserving its exact 'done' property state)
        generator._delegationInnerResult = innerResult;

        // Return normally - callers check ExecutionContext.Suspended flag
    }

    /// <summary>
    /// Runs the yield* delegation loop for async generators.
    /// </summary>
    private static JsValue RunAsyncYieldDelegateLoop(
        EvaluationContext context,
        AsyncGeneratorInstance asyncGenerator,
        IteratorInstance iterator,
        CompletionType receivedType,
        JsValue receivedValue)
    {
        var engine = context.Engine;

        while (true)
        {
            if (receivedType == CompletionType.Normal)
            {
                // Call iterator.next with received value
                var iteratorInstance = iterator.Instance;
                var nextMethod = iteratorInstance.GetMethod(CommonProperties.Next);
                if (nextMethod is null)
                {
                    Throw.TypeError(engine.Realm, "Iterator does not have next method");
                    return JsValue.Undefined;
                }

                var innerResultValue = nextMethod.Call(iteratorInstance, new[] { receivedValue });

                // For async iterators, the result is a Promise - we need to await it
                // But since the inner iterator is also an async generator, it returns a Promise
                if (innerResultValue is JsPromise innerPromise)
                {
                    // Store delegation state for resume
                    asyncGenerator._delegationResumeType = CompletionType.Normal;

                    // The inner async iterator returned a Promise
                    // We need to await it and then yield or complete based on the result
                    return AwaitAndYieldDelegation(context, asyncGenerator, iterator, innerPromise);
                }

                // If not a Promise (shouldn't happen for async iterators, but handle sync case)
                if (innerResultValue is not ObjectInstance innerResult)
                {
                    Throw.TypeError(engine.Realm, "Iterator result is not an object");
                    return JsValue.Undefined;
                }

                var done = IteratorComplete(innerResult);
                if (done)
                {
                    asyncGenerator._delegatingIterator = null;
                    asyncGenerator._delegatingYieldNode = null;
                    return IteratorValue(innerResult);
                }

                // Yield the value from the inner iterator
                asyncGenerator.AsyncGeneratorYield(IteratorValue(innerResult));
                return JsValue.Undefined;
            }
            else if (receivedType == CompletionType.Throw)
            {
                var iteratorInstance = iterator.Instance;
                var throwMethod = iteratorInstance.GetMethod("throw");
                if (throwMethod is not null)
                {
                    var innerResult = throwMethod.Call(iteratorInstance, new[] { receivedValue });

                    if (innerResult is JsPromise innerPromise)
                    {
                        asyncGenerator._delegationResumeType = CompletionType.Throw;
                        return AwaitAndYieldDelegation(context, asyncGenerator, iterator, innerPromise);
                    }

                    if (innerResult is not ObjectInstance innerObj)
                    {
                        Throw.TypeError(engine.Realm, "Iterator result is not an object");
                        return JsValue.Undefined;
                    }

                    var done = IteratorComplete(innerObj);
                    if (done)
                    {
                        asyncGenerator._delegatingIterator = null;
                        asyncGenerator._delegatingYieldNode = null;
                        return IteratorValue(innerObj);
                    }

                    asyncGenerator.AsyncGeneratorYield(IteratorValue(innerObj));
                    return JsValue.Undefined;
                }
                else
                {
                    AsyncIteratorClose(iterator, CompletionType.Normal);
                    asyncGenerator._delegatingIterator = null;
                    asyncGenerator._delegatingYieldNode = null;
                    Throw.TypeError(engine.Realm, "Iterator does not have throw method");
                }
            }
            else // Return
            {
                var iteratorInstance = iterator.Instance;
                var returnMethod = iteratorInstance.GetMethod("return");
                if (returnMethod is null)
                {
                    asyncGenerator._delegatingIterator = null;
                    asyncGenerator._delegatingYieldNode = null;
                    asyncGenerator._returnRequested = true;
                    asyncGenerator._suspendedValue = receivedValue;
                    return receivedValue;
                }

                var innerReturnResult = returnMethod.Call(iteratorInstance, new[] { receivedValue });

                if (innerReturnResult is JsPromise innerPromise)
                {
                    asyncGenerator._delegationResumeType = CompletionType.Return;
                    return AwaitAndYieldDelegation(context, asyncGenerator, iterator, innerPromise);
                }

                if (innerReturnResult is not ObjectInstance innerReturnObj)
                {
                    Throw.TypeError(engine.Realm, "Iterator result is not an object");
                    return JsValue.Undefined;
                }

                var done = IteratorComplete(innerReturnObj);
                if (done)
                {
                    var returnValue = IteratorValue(innerReturnObj);
                    asyncGenerator._delegatingIterator = null;
                    asyncGenerator._delegatingYieldNode = null;
                    asyncGenerator._returnRequested = true;
                    asyncGenerator._suspendedValue = returnValue;
                    return returnValue;
                }

                asyncGenerator.AsyncGeneratorYield(IteratorValue(innerReturnObj));
                return JsValue.Undefined;
            }
        }
    }

    /// <summary>
    /// Awaits the inner iterator's Promise and continues delegation.
    /// </summary>
    private static JsValue AwaitAndYieldDelegation(
        EvaluationContext context,
        AsyncGeneratorInstance asyncGenerator,
        IteratorInstance iterator,
        JsPromise innerPromise)
    {
        var engine = context.Engine;

        // We need to await the inner Promise and then either yield or complete
        // Store the iterator for resumption
        asyncGenerator._delegatingIterator = iterator;

        // Set state to SuspendedYield while waiting for the inner Promise
        asyncGenerator._asyncGeneratorState = AsyncGeneratorState.SuspendedYield;

        // Capture the current promise capability - we'll resolve it when the inner Promise resolves
        var promiseCapability = asyncGenerator._currentPromiseCapability;

        // Create handlers for the Promise
        var onFulfilled = new ClrFunction(engine, "", (_, args) =>
        {
            var resolvedResult = args.At(0);

            if (resolvedResult is not ObjectInstance resultObj)
            {
                asyncGenerator._delegatingIterator = null;
                asyncGenerator._delegatingYieldNode = null;
                asyncGenerator._currentPromiseCapability = null;
                asyncGenerator._asyncGeneratorState = AsyncGeneratorState.Completed;
                if (promiseCapability is not null)
                {
                    var error = engine.Realm.Intrinsics.TypeError.Construct("Iterator result is not an object");
                    AsyncGeneratorInstance.AsyncGeneratorReject(error, promiseCapability);
                }
                asyncGenerator.AsyncGeneratorResumeNext();
                return JsValue.Undefined;
            }

            var done = IteratorComplete(resultObj);
            if (done)
            {
                // Delegation complete - clean up and resume the outer generator
                asyncGenerator._delegatingIterator = null;
                // Keep _delegatingYieldNode for now - used to detect resumption after delegation

                // Resume execution after delegation
                if (promiseCapability is not null)
                {
                    asyncGenerator.ResumeAfterDelegation(IteratorValue(resultObj), promiseCapability);
                }
            }
            else
            {
                // Yield the value from the inner iterator
                // Resolve the current request with done=false and the inner value
                var yieldedValue = IteratorValue(resultObj);

                // Clear the current promise capability before resolving
                asyncGenerator._currentPromiseCapability = null;

                if (promiseCapability is not null)
                {
                    asyncGenerator.AsyncGeneratorResolve(yieldedValue, false, promiseCapability);
                }

                // Process next request - this will continue the delegation when next() is called
                asyncGenerator.AsyncGeneratorResumeNext();
            }

            return JsValue.Undefined;
        }, 1, Runtime.Descriptors.PropertyFlag.Configurable);

        var onRejected = new ClrFunction(engine, "", (_, args) =>
        {
            var rejectedValue = args.At(0);
            asyncGenerator._delegatingIterator = null;
            asyncGenerator._delegatingYieldNode = null;
            asyncGenerator._currentPromiseCapability = null;
            asyncGenerator._asyncGeneratorState = AsyncGeneratorState.Completed;

            if (promiseCapability is not null)
            {
                AsyncGeneratorInstance.AsyncGeneratorReject(rejectedValue, promiseCapability);
            }
            asyncGenerator.AsyncGeneratorResumeNext();
            return JsValue.Undefined;
        }, 1, Runtime.Descriptors.PropertyFlag.Configurable);

        PromiseOperations.PerformPromiseThen(engine, innerPromise, onFulfilled, onRejected, null!);

        return JsValue.Undefined;
    }

    private static bool IteratorComplete(JsValue iterResult)
    {
        return TypeConverter.ToBoolean(iterResult.Get(CommonProperties.Done));
    }

    private static JsValue IteratorValue(JsValue iterResult)
    {
        return iterResult.Get(CommonProperties.Value);
    }

    private static void AsyncIteratorClose(IteratorInstance iteratorRecord, CompletionType closeCompletion)
    {
        var engine = iteratorRecord.Engine;
        var iterator = iteratorRecord.Instance;
        var returnMethod = iterator.GetMethod(CommonProperties.Return);

        if (returnMethod is null)
        {
            return;
        }

        try
        {
            var innerResult = returnMethod.Call(iterator, Arguments.Empty);
            // TODO: Should await innerResult for async iterators
            // For now, just call the return method
        }
        catch
        {
            // If closeCompletion is throw, rethrow
            if (closeCompletion == CompletionType.Throw)
            {
                throw;
            }
            // Otherwise ignore the exception
        }
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-asyncgeneratoryield
    /// For yield* delegation in async generators.
    /// </summary>
    private static Completion AsyncGeneratorYield(EvaluationContext context, JsValue iteratorValue)
    {
        var engine = context.Engine;
        var asyncGenerator = engine.ExecutionContext.AsyncGenerator!;

        // Call the AsyncGeneratorYield method on the instance
        asyncGenerator.AsyncGeneratorYield(iteratorValue);

        // Return a completion that indicates we yielded
        // The caller should check if suspended and return appropriately
        return new Completion(CompletionType.Normal, iteratorValue, null!);
    }

    /// <summary>
    /// https://tc39.es/ecma262/#await
    /// Simplified await for use in yield* - returns the value directly for now.
    /// Full await implementation would need to suspend and resume.
    /// </summary>
    private static JsValue Await(EvaluationContext context, JsValue value)
    {
        // For now, just return the value
        // A full implementation would need to:
        // 1. Wrap value in a promise
        // 2. Suspend execution
        // 3. Resume when promise settles
        // This is simplified for the initial implementation
        return value;
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-yield
    /// https://tc39.es/ecma262/#sec-generatoryield
    /// </summary>
    private static JsValue Yield(EvaluationContext context, JsValue iterNextObj)
    {
        var engine = context.Engine;
        var generatorKind = engine.ExecutionContext.GetGeneratorKind();
        if (generatorKind == GeneratorKind.Async)
        {
            // AsyncGeneratorYield per spec
            var asyncGenerator = engine.ExecutionContext.AsyncGenerator;
            return asyncGenerator!.AsyncGeneratorYield(iterNextObj);
        }

        // GeneratorYield per spec:
        // 1. Set generator.[[GeneratorState]] to suspendedYield
        var genContext = engine.ExecutionContext;
        var generator = genContext.Generator;
        generator!._generatorState = GeneratorState.SuspendedYield;

        // Store the yielded value so it can be retrieved even if the containing statement
        // has a different completion value (e.g., variable declarations return Empty)
        generator._suspendedValue = iterNextObj;

        // Return normally - callers check ExecutionContext.Suspended flag
        // to detect that the generator has yielded
        return iterNextObj;
    }
}
