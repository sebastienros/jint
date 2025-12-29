using System.Collections.Generic;
using Jint.Native;
using Jint.Native.Generator;
using Jint.Native.Iterator;
using Jint.Native.Object;

namespace Jint.Runtime.Interpreter.Expressions;

internal sealed class JintYieldExpression : JintExpression
{
    public JintYieldExpression(YieldExpression expression) : base(expression)
    {
    }

    protected override object EvaluateInternal(EvaluationContext context)
    {
        if ((context.Engine.Options.ExperimentalFeatures & ExperimentalFeature.Generators) == ExperimentalFeature.None)
        {
            Throw.JavaScriptException(
                context.Engine.Intrinsics.Error,
                "Yield expressions are not supported in the engine, you can enable the experimental feature 'Generators' in engine options to use them.");
        }

        var expression = (YieldExpression) _expression;
        var generator = context.Engine.ExecutionContext.Generator;

        // Check if we're resuming from a yield* delegation
        if (generator is not null
            && generator._delegatingIterator is not null
            && ReferenceEquals(_expression, generator._delegatingYieldNode))
        {
            // Continue the yield* delegation loop
            return ContinueYieldDelegate(context, generator);
        }

        // Check if we're resuming from a suspended yield
        // When resuming, we need to return the value passed to next() for the yield we suspended at
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

                // If we're resuming with a Return completion, throw to trigger finally blocks
                // This allows for-of loops to close their iterators properly
                if (generator._resumeCompletionType == CompletionType.Return)
                {
                    generator._resumeCompletionType = CompletionType.Normal; // Reset for future
                    throw new GeneratorReturnException(returnValue);
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

        // Normal yield: evaluate argument and yield the value
        JsValue value;
        if (expression.Argument is not null)
        {
            value = Build(expression.Argument).GetValue(context);
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
        var generator = engine.ExecutionContext.Generator!;

        // Store the iterator for delegation continuation
        generator._delegatingIterator = iterator;
        generator._delegatingYieldNode = _expression;

        // Start the first iteration with a normal completion
        return RunYieldDelegateLoop(context, generator, iterator, CompletionType.Normal, JsValue.Undefined);
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
                    innerResultValue = Await(innerResultValue);
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
                    var asyncReceived = AsyncGeneratorYield(IteratorValue(innerResult));
                    receivedType = asyncReceived.Type;
                    receivedValue = asyncReceived.Value;
                }
                else
                {
                    // Yield the value from the inner iterator and suspend
                    // Per spec, pass innerResult directly to GeneratorYield to preserve its exact state
                    SuspendForDelegation(context, generator, innerResult, CompletionType.Normal);
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
                        innerResult = Await(innerResult);
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
                        var asyncReceived = AsyncGeneratorYield(IteratorValue(innerObj));
                        receivedType = asyncReceived.Type;
                        receivedValue = asyncReceived.Value;
                    }
                    else
                    {
                        // Yield the result and suspend
                        SuspendForDelegation(context, generator, innerObj, CompletionType.Normal);
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
                        temp = Await(receivedValue);
                    }

                    // Per spec: "Return Completion(received)" - the generator should complete
                    // with the received return value, not just return it from yield*
                    generator._delegatingIterator = null;
                    generator._delegatingYieldNode = null;
                    generator._generatorState = GeneratorState.Completed;
                    generator._shouldEarlyReturn = true;
                    generator._earlyReturnValue = temp;

                    // Throw to interrupt execution - ResumeExecution will see the early return flag
                    throw new YieldSuspendException(temp);
                }

                var innerReturnResult = returnMethod.Call(iteratorInstance, new[] { receivedValue });
                if (generatorKind == GeneratorKind.Async)
                {
                    innerReturnResult = Await(innerReturnResult);
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
                    // Throw GeneratorReturnException to signal Return completion
                    // This will trigger finally blocks and then complete the generator
                    throw new GeneratorReturnException(returnValue);
                }

                if (generatorKind == GeneratorKind.Async)
                {
                    var asyncReceived = AsyncGeneratorYield(IteratorValue(innerReturnObj));
                    receivedType = asyncReceived.Type;
                    receivedValue = asyncReceived.Value;
                }
                else
                {
                    // Yield the result and suspend
                    SuspendForDelegation(context, generator, innerReturnObj, CompletionType.Normal);
                }
            }
        }
    }

    /// <summary>
    /// Suspends the generator during yield* delegation.
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

        // Throw to suspend - don't extract value, just signal suspension
        throw new YieldSuspendException(JsValue.Undefined);
    }

    private static bool IteratorComplete(JsValue iterResult)
    {
        return TypeConverter.ToBoolean(iterResult.Get(CommonProperties.Done));
    }

    private static JsValue IteratorValue(JsValue iterResult)
    {
        return iterResult.Get(CommonProperties.Value);
    }

    private static void AsyncIteratorClose(object iteratorRecord, CompletionType closeCompletion)
    {
        Throw.NotImplementedException("async");
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-asyncgeneratoryield
    /// </summary>
    private static Completion AsyncGeneratorYield(object iteratorValue)
    {
        Throw.NotImplementedException("async");
        return default;
    }

    /// <summary>
    /// https://tc39.es/ecma262/#await
    /// </summary>
    private static ObjectInstance Await(JsValue innerResult)
    {
        Throw.NotImplementedException("await");
        return null!;
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-yield
    /// </summary>
    private static JsValue Yield(EvaluationContext context, JsValue iterNextObj)
    {
        var engine = context.Engine;
        var generatorKind = engine.ExecutionContext.GetGeneratorKind();
        if (generatorKind == GeneratorKind.Async)
        {
            // TODO return ? AsyncGeneratorYield(undefined);
            Throw.NotImplementedException("async not implemented");
        }

        // https://tc39.es/ecma262/#sec-generatoryield
        var genContext = engine.ExecutionContext;
        var generator = genContext.Generator;
        generator!._generatorState = GeneratorState.SuspendedYield;
        // Store the yielded value so it can be retrieved even if the containing statement
        // has a different completion value (e.g., variable declarations return Empty)
        generator._suspendedValue = iterNextObj;

        // Throw an exception to immediately interrupt expression evaluation.
        // This is caught at the statement level to handle generator suspension properly.
        throw new YieldSuspendException(iterNextObj);
    }
}
