using Jint.Native;
using Jint.Native.AsyncGenerator;
using Jint.Native.Generator;
using Jint.Native.Iterator;
using Jint.Native.Object;
using Jint.Native.Promise;
using Jint.Runtime.Interop;

namespace Jint.Runtime.Interpreter.Expressions;

/// <summary>
/// Where a <c>yield</c> stands when it is not nested in another expression, which decides whether a return() resumption
/// may come back from it as a value (<see cref="JintYieldExpression"/>'s <c>DeliverReturn</c>).
/// </summary>
internal enum YieldStatementPosition : byte
{
    /// <summary>Inside a larger expression, or a statement nothing tests a requested return after: it unwinds.</summary>
    Nested,

    /// <summary>
    /// The whole of an expression statement in a <see cref="JintStatementList"/>, which tests
    /// <see cref="ISuspendable.ReturnRequested"/> after every statement, for either kind of generator.
    /// </summary>
    StatementList,

    /// <summary>
    /// The whole of the only statement of a block, which <see cref="Statements.JintBlockStatement"/> runs itself and
    /// follows with a test of a synchronous generator's request only; an async generator's unwinds.
    /// </summary>
    SingleStatementBlock,
}

internal sealed class JintYieldExpression : JintExpression
{
    private readonly JintExpression? _argument;

    // Who reads a return() resumption this yield receives; see DeliverReturn.
    private YieldStatementPosition _statementPosition;

    public JintYieldExpression(YieldExpression expression) : base(expression)
    {
        _argument = expression.Argument is not null ? Build(expression.Argument) : null;
    }

    /// <summary>
    /// Records that this yield is the whole expression of a statement that <paramref name="position"/> runs, so that a
    /// return() resumption may be handed back to it as a value. Called once, while the handler tree is being built.
    /// </summary>
    internal void MarkStatementPosition(YieldStatementPosition position) => _statementPosition = position;

    /// <summary>
    /// The value of a <c>yield</c> resumed with a return completion. The return completion has to reach the statement
    /// without anything between the <c>yield</c> and the statement running: per
    /// https://tc39.es/ecma262/#sec-generatorresumeabrupt and https://tc39.es/ecma262/#sec-yield the yield evaluates to
    /// that abrupt completion, and each enclosing <c>?</c> propagates it, so there is no PutValue, call, operator or
    /// branch after it (sebastienros/jint#4304). An expression handler sees an abrupt completion only as an exception,
    /// so that is what this raises (<see cref="GeneratorReturnException"/>), and the nearest statement list converts it
    /// back. The one position where the value is safe to return is a statement whose runner tests
    /// <see cref="ISuspendable.ReturnRequested"/>, which the caller has already set, before it does anything else:
    /// that keeps a <c>for</c>-<c>of</c> leaving early through a generator parked at <c>yield x;</c> free of an
    /// exception.
    /// </summary>
    private JsValue DeliverReturn(JsValue value, bool syncGenerator)
    {
        if (_statementPosition == YieldStatementPosition.StatementList
            || (_statementPosition == YieldStatementPosition.SingleStatementBlock && syncGenerator))
        {
            return value;
        }

        GeneratorReturnException.Raise(value, _expression);
        return value;
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
                    Throw.JavaScriptException(context.Engine, returnValue, in AstExtensions.DefaultLocation);
                }

                // If we're resuming with a Return completion, signal return request
                // This allows for-of loops to close their iterators properly via finally blocks
                if (generator._resumeCompletionType == CompletionType.Return)
                {
                    generator._resumeCompletionType = CompletionType.Normal; // Reset for future
                    generator._returnRequested = true;
                    generator._suspendedValue = returnValue;
                    return DeliverReturn(returnValue, syncGenerator: true);
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
                // Delegation has completed - return the final value
                var returnValue = asyncGenerator._nextValue ?? JsValue.Undefined;
                asyncGenerator._delegatingYieldNode = null;
                asyncGenerator._isResuming = false;
                asyncGenerator._nextValue = null;

                // If the delegation ended with a Throw (e.g., IteratorValue threw),
                // throw at the yield* point so try/catch in the body can handle it
                if (asyncGenerator._resumeCompletionType == CompletionType.Throw)
                {
                    asyncGenerator._resumeCompletionType = CompletionType.Normal;
                    Throw.JavaScriptException(context.Engine, returnValue, in AstExtensions.DefaultLocation);
                }

                // If this was a Return completion from the inner iterator,
                // signal the return so try/finally blocks can execute
                if (asyncGenerator._resumeCompletionType == CompletionType.Return)
                {
                    asyncGenerator._resumeCompletionType = CompletionType.Normal;
                    asyncGenerator._returnRequested = true;
                    asyncGenerator._suspendedValue = returnValue;
                    return DeliverReturn(returnValue, syncGenerator: false);
                }

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
                    Throw.JavaScriptException(context.Engine, returnValue, in AstExtensions.DefaultLocation);
                }

                // A return() resumption. AsyncGeneratorInstance has already awaited its value, as
                // https://tc39.es/ecma262/#sec-asyncgeneratorunwrapyieldresumption does at the yield before anything
                // else, and a rejected await came back as the throw above; so the yield evaluates to a return
                // completion of the awaited value, and nothing awaits it again once the body completes.
                if (asyncGenerator._resumeCompletionType == CompletionType.Return)
                {
                    asyncGenerator._resumeCompletionType = CompletionType.Normal;
                    asyncGenerator._returnRequested = true;
                    asyncGenerator._suspendedValue = returnValue;
                    return DeliverReturn(returnValue, syncGenerator: false);
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

        // Normal yield: evaluate argument and yield the value.
        //
        // Reaching here is a *fresh* evaluation of this yield node, so whatever the node's previous
        // evaluation produced stops being an answer to it. The memo above exists only to replay one
        // evaluation - a statement that is re-executed from the top after a later suspension must
        // not re-run the yields it already got past - and a loop that comes back round to the same
        // node starts a new evaluation the memo has nothing to say about. Leaving the stale entry in
        // place made `yield (yield* g())` inside a loop answer its second iteration with the first
        // iteration's value without ever evaluating the operand, so the delegation was abandoned
        // mid-flight (and, when the operand carried the loop's own decrement, the loop never ended).
        generator?._yieldNodeValues?.Remove(_expression);
        asyncGenerator?._yieldNodeValues?.Remove(_expression);

        JsValue value;
        if (_argument is not null)
        {
            value = _argument.GetValue(context);

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

        // Per spec step 7.a.i: use the cached [[NextMethod]] from the iterator record
        // (already obtained when the iterator was created via GetIterator)
        var nextMethod = iterator.NextMethod ?? iterator.Instance.GetMethod(CommonProperties.Next);

        if (generatorKind == GeneratorKind.Async)
        {
            var asyncGenerator = engine.ExecutionContext.AsyncGenerator!;

            // Store the iterator for delegation continuation
            asyncGenerator._delegatingIterator = iterator;
            asyncGenerator._delegatingYieldNode = _expression;
            asyncGenerator._delegatingNextMethod = nextMethod;

            // Start the first iteration with a normal completion
            return RunAsyncYieldDelegateLoop(context, asyncGenerator, iterator, CompletionType.Normal, JsValue.Undefined);
        }
        else
        {
            var generator = engine.ExecutionContext.Generator!;

            // Store the iterator for delegation continuation
            generator._delegatingIterator = iterator;
            generator._delegatingYieldNode = _expression;
            generator._delegatingNextMethod = nextMethod;

            // Start the first iteration with a normal completion
            return RunYieldDelegateLoop(context, generator, iterator, CompletionType.Normal, JsValue.Undefined);
        }
    }

    /// <summary>
    /// Continues the yield* delegation loop after resuming from a suspension.
    /// </summary>
    private JsValue ContinueYieldDelegate(EvaluationContext context, GeneratorInstance generator)
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

        // What the delegation receives is the consumer's resumption, whatever the delegate was last sent: a next()
        // after a throw() or a return() the delegate answered with a value is a next() again. The return() value
        // is the awaited one (AsyncGeneratorInstance.UnwrapReturnResumption), and a rejected await is a throw.
        var resumeType = asyncGenerator._resumeCompletionType;
        asyncGenerator._resumeCompletionType = CompletionType.Normal; // reset

        var resumeValue = asyncGenerator._nextValue ?? JsValue.Undefined;

        // Clear the resuming flag since we're handling it here
        asyncGenerator._isResuming = false;

        return RunAsyncYieldDelegateLoop(context, asyncGenerator, iterator, resumeType, resumeValue);
    }

    /// <summary>
    /// Runs the yield* delegation loop. Called both for initial delegation and continuation.
    /// </summary>
    private JsValue RunYieldDelegateLoop(
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
                // Per spec 14.4.14 step 7.a.v: use cached nextMethod (obtained once in step 7.a.i)
                var nextMethod = generator._delegatingNextMethod;
                if (nextMethod is null)
                {
                    Throw.TypeError(engine.Realm, "Iterator does not have next method");
                    return JsValue.Undefined; // unreachable
                }

                var innerResultValue = nextMethod.Call(iterator.Instance, new[] { receivedValue });
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
                var returnMethod = iteratorInstance.GetMethod(CommonProperties.Return);
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

                    // Hand the return completion on to the statement, which runs the finally blocks
                    // before the generator completes; see DeliverReturn.
                    return DeliverReturn(temp, syncGenerator: true);
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
                    return DeliverReturn(returnValue, syncGenerator: true);
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
                // Per spec step 7.a.v: use cached nextMethod (obtained once in step 7.a.i)
                var nextMethod = asyncGenerator._delegatingNextMethod;
                if (nextMethod is null)
                {
                    Throw.TypeError(engine.Realm, "Iterator does not have next method");
                    return JsValue.Undefined;
                }

                var innerResultValue = nextMethod.Call(iterator.Instance, new[] { receivedValue });

                // Per spec step 7.a.vi: if generatorKind is async, set innerResult to Await(innerResult).
                // Await wraps via PromiseResolve, which handles both Promises and thenables.
                asyncGenerator._delegationResumeType = CompletionType.Normal;
                var innerPromise = EnsurePromise(engine, innerResultValue);
                return AwaitAndYieldDelegation(context, asyncGenerator, iterator, innerPromise);
            }
            else if (receivedType == CompletionType.Throw)
            {
                var iteratorInstance = iterator.Instance;
                var throwMethod = iteratorInstance.GetMethod("throw");
                if (throwMethod is not null)
                {
                    var innerResult = throwMethod.Call(iteratorInstance, new[] { receivedValue });

                    // Per spec: if generatorKind is async, set innerResult to Await(innerResult)
                    asyncGenerator._delegationResumeType = CompletionType.Throw;
                    var innerPromise = EnsurePromise(engine, innerResult);
                    return AwaitAndYieldDelegation(context, asyncGenerator, iterator, innerPromise);
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
                return DelegateReturn(context, asyncGenerator, iterator, receivedValue);
            }
        }
    }

    /// <summary>
    /// Wraps a value in a promise via PromiseResolve if it isn't already a JsPromise.
    /// This implements the spec's Await semantics for async generator yield* delegation.
    /// </summary>
    private static JsPromise EnsurePromise(Engine engine, JsValue value)
    {
        if (value is JsPromise p)
        {
            return p;
        }

        // PromiseResolve(%Promise%, value) — handles thenables via NewPromiseResolveThenableJob
        return (JsPromise) engine.Intrinsics.Promise.PromiseResolve(value);
    }

    /// <summary>
    /// Awaits the inner iterator's result and continues the delegation: yields it, completes the
    /// delegation on <c>done</c>, or throws it into the body.
    /// </summary>
    /// <remarks>
    /// https://tc39.es/ecma262/#sec-generator-function-definitions-runtime-semantics-evaluation marks every
    /// step after <c>? Await(innerResult)</c> with <c>?</c>: a rejected result, a non-object one and a
    /// throwing <c>done</c> or <c>value</c> getter are all throw completions of the yield* expression, which
    /// a try/catch in the body may handle. The body is what decides the pending request's fate afterwards.
    /// </remarks>
    private static JsValue AwaitAndYieldDelegation(
        EvaluationContext context,
        AsyncGeneratorInstance asyncGenerator,
        IteratorInstance iterator,
        JsPromise innerPromise)
    {
        var engine = context.Engine;

        // Store the iterator for resumption and park the generator while the inner result settles
        asyncGenerator._delegatingIterator = iterator;
        asyncGenerator._asyncGeneratorState = AsyncGeneratorState.SuspendedYield;

        // Capture the current promise capability - the request the inner result answers
        var promiseCapability = asyncGenerator._currentPromiseCapability;

        // Capture the delegation resume type to know if this was a return/throw delegation
        var delegationResumeType = asyncGenerator._delegationResumeType;

        var onFulfilled = new ClrFunction(engine, "", (_, args) =>
        {
            var resolvedResult = args.At(0);

            if (resolvedResult is not ObjectInstance resultObj)
            {
                var error = engine.Realm.Intrinsics.TypeError.Construct("Iterator result is not an object");
                asyncGenerator.ThrowAfterDelegation(error, promiseCapability);
                return JsValue.Undefined;
            }

            // IteratorComplete and IteratorValue may throw (poisoned done/value getters)
            bool done;
            JsValue value;
            try
            {
                done = IteratorComplete(resultObj);
                value = IteratorValue(resultObj);
            }
            catch (JavaScriptException e)
            {
                asyncGenerator.ThrowAfterDelegation(e.Error, promiseCapability);
                return JsValue.Undefined;
            }

            if (done)
            {
                // Delegation complete - clean up and resume the outer generator with the final value
                asyncGenerator.ClearDelegationIterator();

                if (delegationResumeType == CompletionType.Return)
                {
                    asyncGenerator._resumeCompletionType = CompletionType.Return;
                }

                if (promiseCapability is not null)
                {
                    asyncGenerator.ResumeAfterDelegation(value, promiseCapability);
                }
            }
            else
            {
                // AsyncGeneratorYield(value): the inner value answers the pending request as it is
                asyncGenerator._currentPromiseCapability = null;

                if (promiseCapability is not null)
                {
                    asyncGenerator.AsyncGeneratorResolve(value, false, promiseCapability);
                }

                asyncGenerator.AsyncGeneratorResumeNext();
            }

            return JsValue.Undefined;
        }, 1, Runtime.Descriptors.PropertyFlag.Configurable);

        var onRejected = new ClrFunction(engine, "", (_, args) =>
        {
            // The delegate failed: `? Await(innerResult)` is abrupt, and the throw is the yield*'s own
            asyncGenerator.ThrowAfterDelegation(args.At(0), promiseCapability);
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

    /// <summary>
    /// The return branch of https://tc39.es/ecma262/#sec-generator-function-definitions-runtime-semantics-evaluation
    /// (step 7.c) in an async generator: closes the delegate through its own <c>return</c>, with the value the
    /// consumer's <c>return()</c> carried. That value is the awaited one: what the delegation receives comes out of
    /// https://tc39.es/ecma262/#sec-asyncgeneratoryield, whose
    /// https://tc39.es/ecma262/#sec-asyncgeneratorunwrapyieldresumption <c>AsyncGeneratorInstance</c> ran before the
    /// body resumed, and a rejected await arrives as a throw instead. Every step here is <c>?</c>, and the body is
    /// running, so a failure is thrown at the yield*, where a try/catch around it sees it.
    /// </summary>
    private static JsValue DelegateReturn(
        EvaluationContext context,
        AsyncGeneratorInstance asyncGenerator,
        IteratorInstance iterator,
        JsValue receivedValue)
    {
        var engine = context.Engine;

        JsValue innerReturnResult;
        try
        {
            var returnMethod = iterator.Instance.GetMethod(CommonProperties.Return);
            if (returnMethod is null)
            {
                return AwaitDelegationReturnValue(engine, asyncGenerator, receivedValue);
            }

            innerReturnResult = returnMethod.Call(iterator.Instance, new[] { receivedValue });
        }
        catch (JavaScriptException)
        {
            // The throw ends the delegation here and now, so nothing may resume into it later.
            asyncGenerator.ClearDelegationIterator();
            asyncGenerator._delegatingYieldNode = null;
            throw;
        }

        // Await and step through the delegate's answer exactly as a next() result is, with Return as the
        // completion a done result resumes the body with.
        asyncGenerator._delegationResumeType = CompletionType.Return;
        return AwaitAndYieldDelegation(context, asyncGenerator, iterator, EnsurePromise(engine, innerReturnResult));
    }

    /// <summary>
    /// The delegate has no <c>return</c> method: per step 7.c.iii the yield* awaits the received value, a second
    /// await after the one https://tc39.es/ecma262/#sec-asyncgeneratorunwrapyieldresumption did, and then evaluates
    /// to a return completion of it.
    /// </summary>
    private static JsValue AwaitDelegationReturnValue(
        Engine engine,
        AsyncGeneratorInstance asyncGenerator,
        JsValue receivedValue)
    {
        // Clear the iterator but keep _delegatingYieldNode, so the resume re-enters at the yield* and finds the
        // delegation over.
        asyncGenerator.ClearDelegationIterator();

        var promise = EnsurePromise(engine, receivedValue);
        var promiseCapability = asyncGenerator._currentPromiseCapability;
        asyncGenerator._asyncGeneratorState = AsyncGeneratorState.SuspendedYield;

        var onFulfilled = new ClrFunction(engine, "", (_, args) =>
        {
            asyncGenerator._resumeCompletionType = CompletionType.Return;
            if (promiseCapability is not null)
            {
                asyncGenerator.ResumeAfterDelegation(args.At(0), promiseCapability);
            }

            return JsValue.Undefined;
        }, 1, Runtime.Descriptors.PropertyFlag.Configurable);

        var onRejected = new ClrFunction(engine, "", (_, args) =>
        {
            asyncGenerator.ThrowAfterDelegation(args.At(0), promiseCapability);
            return JsValue.Undefined;
        }, 1, Runtime.Descriptors.PropertyFlag.Configurable);

        PromiseOperations.PerformPromiseThen(engine, promise, onFulfilled, onRejected, null!);
        return JsValue.Undefined;
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
