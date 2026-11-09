using System.Runtime.CompilerServices;
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

        GeneratorReturnException.Raise(value);
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
            // https://tc39.es/ecma262/#sec-yield: AsyncGeneratorYield(? Await(value)). Await's PromiseResolve reads a
            // promise's constructor, which may throw: that is this yield's throw, so it comes before anything marks the
            // generator suspended here.
            var promise = PromiseConstructor.ResolveForAwaitToPromise(context.Engine, value);
            asyncGenerator._lastYieldNode = _expression;
            return asyncGenerator.AsyncGeneratorYield(value, promise);
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

        // Step 7.a.i calls the record's [[NextMethod]], which GetIteratorFromMethod
        // (https://tc39.es/ecma262/#sec-getiteratorfrommethod) read off the iterator once. An ObjectIterator holds what
        // was read, a callable or null. A record Jint steps natively holds null too: its [[NextMethod]] is the built-in
        // `next` the read found, and that function is the native step (IteratorInstance.CallNextMethod). Reading `next`
        // again ran a getter twice and called a `next` replaced in between (sebastienros/jint#4424).
        var nextMethod = iterator.NextMethod;

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
    /// <remarks>
    /// An exception leaving the loop ends the delegation, whatever raised it: the delegate's <c>next</c>, <c>throw</c> or
    /// <c>return</c>, a non-object answer, a <c>done</c> or <c>value</c> getter, a constraint or a host exception. The
    /// generator then forgets it (<see cref="GeneratorInstance.EndDelegation"/>), since nothing resumes into it and the
    /// next evaluation of this yield* calls GetIterator afresh; a delegation it kept was resumed by the same node in a
    /// loop around a <c>try</c>/<c>catch</c> instead (sebastienros/jint#4440). The <c>catch</c> runs only when a step
    /// throws. The return completion a <c>return()</c> ends the delegation with is delivered after it, so the
    /// <see cref="GeneratorReturnException"/> that may carry it is never caught and rethrown.
    /// </remarks>
    private JsValue RunYieldDelegateLoop(
        EvaluationContext context,
        GeneratorInstance generator,
        IteratorInstance iterator,
        CompletionType receivedType,
        JsValue receivedValue)
    {
        var engine = context.Engine;
        var generatorKind = engine.ExecutionContext.GetGeneratorKind();

        JsValue returnValue;
        try
        {
            while (true)
            {
                if (receivedType == CompletionType.Normal)
                {
                    // Step 7.a.i: Call(iteratorRecord.[[NextMethod]], iteratorRecord.[[Iterator]], « received.[[Value]] »),
                    // through the method YieldDelegate took off the record. Without one the record takes the call: a native
                    // step, whose built-in `next` ignores the argument, or the TypeError of a `next` that was not callable.
                    var nextMethod = generator._delegatingNextMethod;
                    var innerResultValue = nextMethod is not null
                        ? nextMethod.Call(iterator.Instance, new[] { receivedValue })
                        : iterator.CallNextMethod();
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
                        generator.EndDelegation();
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
                    // Only a generator runs this loop; an async generator's throw() is DelegateThrow's.
                    var innerThrowObj = ForwardThrowToDelegate(engine.Realm, iterator, receivedValue, out var throwValue);
                    if (innerThrowObj is null)
                    {
                        // Step 7.b.ii.6: the delegate's `throw` answered done, and the yield* evaluates to its value.
                        generator.EndDelegation();
                        return throwValue;
                    }

                    // Step 7.b.ii.8: GeneratorYield(innerResult), and the delegation goes on.
                    SuspendForDelegation(context, generator, innerThrowObj, CompletionType.Normal);

                    // Check if suspended - if so, return to propagate suspension up the call stack
                    if (context.IsSuspended())
                    {
                        return JsValue.Undefined;
                    }
                }
                else // receivedType == CompletionType.Return
                {
                    // Only a generator runs this loop; an async generator's return() is DelegateReturn's.
                    var innerReturnObj = ForwardReturnToDelegate(engine.Realm, iterator, receivedValue, out returnValue);
                    if (innerReturnObj is null)
                    {
                        // Steps 7.c.iii.3 and 7.c.viii.2: Return ReturnCompletion(value). The generator completes with it
                        // once the statement has run the finally blocks, which _returnRequested asks it to; see DeliverReturn.
                        generator.EndDelegation();
                        generator._returnRequested = true;
                        generator._suspendedValue = returnValue;
                        break;
                    }

                    // Step 7.c.x: GeneratorYield(innerReturnResult), and the delegation goes on.
                    SuspendForDelegation(context, generator, innerReturnObj, CompletionType.Normal);

                    // Check if suspended - if so, return to propagate suspension up the call stack
                    if (context.IsSuspended())
                    {
                        return JsValue.Undefined;
                    }
                }
            }
        }
        catch
        {
            generator.EndDelegation();
            throw;
        }

        return DeliverReturn(returnValue, syncGenerator: true);
    }

    /// <summary>
    /// The throw branch of https://tc39.es/ecma262/#sec-generator-function-definitions-runtime-semantics-evaluation
    /// (step 7.b) in a generator: forwards a throw() resumption to the delegate's own <c>throw</c>. Answers
    /// <see langword="null"/> when that answered done, the delegation ending with <paramref name="returnValue"/> as the
    /// yield*'s value (7.b.ii.6), and otherwise what it answered, which the generator yields (7.b.ii.8) while the
    /// delegation goes on. A delegate with no <c>throw</c> is closed, and the yield* throws a <c>TypeError</c> (7.b.iii).
    /// <para>
    /// Every way the delegation ends here, a throw included, releases the native state of a built-in iterator the delegate
    /// is, as <see cref="ForwardReturnToDelegate"/> does for a <c>return</c>: a CLR collection's iterator disposes its host
    /// enumerator (sebastienros/jint#4440). An answer that is not done keeps the iterator for the steps still to come, and a
    /// delegate with no <c>throw</c> is released by its IteratorClose. The release is told a throw completion whenever one
    /// is leaving, so that a host's <c>Dispose</c> cannot replace it. Out of line: only a throw() reaches it.
    /// </para>
    /// </summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static ObjectInstance? ForwardThrowToDelegate(
        Realm realm,
        IteratorInstance iterator,
        JsValue receivedValue,
        out JsValue returnValue)
    {
        var iteratorInstance = iterator.Instance;

        // The completion leaving the delegation, as the native release is told it: a throw until a step below is known
        // to have completed.
        var leaving = CompletionType.Throw;
        var delegationEnds = true;
        try
        {
            // Step 7.b.i: Let throw be ? GetMethod(iterator, "throw").
            var throwMethod = iteratorInstance.GetMethod(CommonProperties.Throw);
            if (throwMethod is null)
            {
                // Step 7.b.iii: IteratorClose(iteratorRecord, NormalCompletion(empty)), which releases the native state
                // itself, after a `return` or in place of one, and then the TypeError.
                delegationEnds = false;
                iterator.Close(CompletionType.Normal);
                Throw.TypeError(realm, "Iterator does not have throw method");
            }

            // Steps 7.b.ii.1-4.
            var innerResult = throwMethod.Call(iteratorInstance, new[] { receivedValue });
            if (innerResult is not ObjectInstance innerObj)
            {
                Throw.TypeError(realm, "Iterator result is not an object");
                returnValue = null!;
                return null;
            }

            // Steps 7.b.ii.5-6.
            if (IteratorComplete(innerObj))
            {
                returnValue = IteratorValue(innerObj);
                leaving = CompletionType.Normal;
                return null;
            }

            delegationEnds = false;
            returnValue = JsValue.Undefined;
            return innerObj;
        }
        finally
        {
            if (delegationEnds)
            {
                iterator.CloseNativeStateAfterReturn(leaving);
            }
        }
    }

    /// <summary>
    /// The return branch of https://tc39.es/ecma262/#sec-generator-function-definitions-runtime-semantics-evaluation
    /// (step 7.c) in a generator: forwards a return() resumption to the delegate. Answers <see langword="null"/> when the
    /// delegation ends with a return completion of <paramref name="returnValue"/>, because the delegate has no
    /// <c>return</c> (7.c.iii) or its <c>return</c> answered done (7.c.viii), and otherwise what that <c>return</c>
    /// answered, which the generator yields (7.c.x) while the delegation goes on.
    /// <para>
    /// Every way the delegation ends here, a throw included, releases the native state of a built-in iterator the
    /// delegate is, after its <c>return</c> or in place of one, as IteratorClose does: a CLR collection's iterator disposes
    /// its host enumerator (sebastienros/jint#4429). An answer that is not done keeps the iterator for the steps still to
    /// come. The release is told a throw completion whenever one is leaving, so that a host's <c>Dispose</c> cannot
    /// replace it. Out of line: only a return() reaches it.
    /// </para>
    /// </summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static ObjectInstance? ForwardReturnToDelegate(
        Realm realm,
        IteratorInstance iterator,
        JsValue receivedValue,
        out JsValue returnValue)
    {
        var iteratorInstance = iterator.Instance;

        // The completion leaving the delegation, as the native release is told it: a throw until a step below is known
        // to have completed.
        var leaving = CompletionType.Throw;
        var delegationEnds = true;
        try
        {
            // Step 7.c.ii: Let return be ? GetMethod(iterator, "return").
            var returnMethod = iteratorInstance.GetMethod(CommonProperties.Return);
            if (returnMethod is null)
            {
                // Step 7.c.iii: the value received is the return completion's.
                returnValue = receivedValue;
                leaving = CompletionType.Return;
                return null;
            }

            // Steps 7.c.iv and 7.c.vi.
            var innerReturnResult = returnMethod.Call(iteratorInstance, new[] { receivedValue });
            if (innerReturnResult is not ObjectInstance innerReturnObj)
            {
                Throw.TypeError(realm, "Iterator result is not an object");
                returnValue = null!;
                return null;
            }

            // Steps 7.c.vii-viii.
            if (IteratorComplete(innerReturnObj))
            {
                returnValue = IteratorValue(innerReturnObj);
                leaving = CompletionType.Return;
                return null;
            }

            delegationEnds = false;
            returnValue = JsValue.Undefined;
            return innerReturnObj;
        }
        finally
        {
            if (delegationEnds)
            {
                iterator.CloseNativeStateAfterReturn(leaving);
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
    /// Runs one step of the yield* delegation for async generators: it calls the delegate and awaits what it answered,
    /// and the reactions to that await go on from there.
    /// </summary>
    /// <remarks>
    /// A throw out of the step itself, before anything is awaited, ends the delegation, as it does in
    /// <see cref="RunYieldDelegateLoop"/>: the delegate's <c>next</c>, <c>throw</c> or <c>return</c> threw, reading one of
    /// them did, or the PromiseResolve of the Await did. The generator forgets the delegation, node and all, so that the
    /// next evaluation of this yield* calls GetIterator afresh rather than resuming the delegate that failed
    /// (sebastienros/jint#4440). A step that fails once the answer has settled goes through
    /// <see cref="AsyncGeneratorInstance.ThrowAfterDelegation"/> instead, whose replay re-enters at the yield* and forgets
    /// it there.
    /// </remarks>
    private static JsValue RunAsyncYieldDelegateLoop(
        EvaluationContext context,
        AsyncGeneratorInstance asyncGenerator,
        IteratorInstance iterator,
        CompletionType receivedType,
        JsValue receivedValue)
    {
        try
        {
            if (receivedType == CompletionType.Normal)
            {
                // Step 7.a.i, as RunYieldDelegateLoop takes it: the record's [[NextMethod]], never a fresh read.
                var nextMethod = asyncGenerator._delegatingNextMethod;
                var innerResultValue = nextMethod is not null
                    ? nextMethod.Call(iterator.Instance, new[] { receivedValue })
                    : iterator.CallNextMethod();

                // Per spec step 7.a.vi: if generatorKind is async, set innerResult to Await(innerResult).
                // Await wraps via PromiseResolve, which handles both Promises and thenables.
                asyncGenerator._delegationResumeType = CompletionType.Normal;
                var innerPromise = EnsurePromise(context.Engine, innerResultValue);
                return AwaitAndYieldDelegation(context, asyncGenerator, iterator, innerPromise);
            }

            if (receivedType == CompletionType.Throw)
            {
                return DelegateThrow(context, asyncGenerator, iterator, receivedValue);
            }

            return DelegateReturn(context, asyncGenerator, iterator, receivedValue);
        }
        catch
        {
            asyncGenerator.EndDelegation();
            throw;
        }
    }

    /// <summary>
    /// The PromiseResolve(%Promise%, value) step of the <c>? Await(innerResult)</c> async generator yield* delegation
    /// performs on each answer of its delegate. It reads a promise's constructor, which may throw: that throw is the
    /// yield*'s own and ends the delegation, without closing the delegate.
    /// </summary>
    private static JsPromise EnsurePromise(Engine engine, JsValue value) => PromiseConstructor.ResolveForAwaitToPromise(engine, value);

    /// <summary>
    /// The throw branch of https://tc39.es/ecma262/#sec-generator-function-definitions-runtime-semantics-evaluation
    /// (step 7.b) in an async generator: forwards a throw() resumption to the delegate's own <c>throw</c>, and awaits and
    /// steps through what it answered as a next() result is. A delegate with no <c>throw</c> is closed, and the yield*
    /// throws a <c>TypeError</c> (7.b.iii, <see cref="CloseDelegateWithoutThrow"/>).
    /// <para>
    /// Every way the delegation ends releases the native state of a built-in iterator the delegate is, as
    /// <see cref="DelegateReturn"/> does for a <c>return</c>: a CLR collection's iterator disposes its host enumerator
    /// (sebastienros/jint#4440). That is at once when reading, calling or awaiting the <c>throw</c> throws here, a
    /// constraint or a host exception included, and otherwise once its answer has settled done, or failed
    /// (<see cref="ReleaseAfterForwardedResumption"/>). A delegate with no <c>throw</c> is released by its
    /// AsyncIteratorClose.
    /// </para>
    /// </summary>
    private static JsValue DelegateThrow(
        EvaluationContext context,
        AsyncGeneratorInstance asyncGenerator,
        IteratorInstance iterator,
        JsValue receivedValue)
    {
        var engine = context.Engine;
        var iteratorInstance = iterator.Instance;

        JsPromise? innerPromise = null;
        try
        {
            // Step 7.b.i: Let throw be ? GetMethod(iterator, "throw").
            var throwMethod = iteratorInstance.GetMethod(CommonProperties.Throw);
            if (throwMethod is not null)
            {
                var innerResult = throwMethod.Call(iteratorInstance, new[] { receivedValue });

                // Step 7.b.ii.2: if generatorKind is async, set innerResult to ? Await(innerResult).
                asyncGenerator._delegationResumeType = CompletionType.Throw;
                innerPromise = EnsurePromise(engine, innerResult);
            }
        }
        catch
        {
            // The throw ends the delegation here and now. Whatever it is, it propagates as it is: the release is told a
            // throw completion, so a host's Dispose cannot replace it unless MustPropagateHostException says so.
            iterator.CloseNativeStateAfterReturn(CompletionType.Throw);
            throw;
        }

        if (innerPromise is null)
        {
            // Step 7.b.iii: AsyncIteratorClose(iteratorRecord, NormalCompletion(empty)), and then the TypeError.
            return CloseDelegateWithoutThrow(context, asyncGenerator, iterator);
        }

        return AwaitAndYieldDelegation(context, asyncGenerator, iterator, innerPromise);
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
                ReleaseAfterForwardedResumption(asyncGenerator, delegationResumeType, CompletionType.Throw);
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
                ReleaseAfterForwardedResumption(asyncGenerator, delegationResumeType, CompletionType.Throw);
                asyncGenerator.ThrowAfterDelegation(e.Error, promiseCapability);
                return JsValue.Undefined;
            }
            catch
            {
                // A constraint that tripped or a host exception, which no script can catch: it leaves the job as it is,
                // and the delegation it ends still owes the release, and is forgotten.
                ReleaseAfterForwardedResumption(asyncGenerator, delegationResumeType, CompletionType.Throw);
                asyncGenerator.EndDelegation();
                throw;
            }

            if (done)
            {
                // Delegation complete - clean up and resume the outer generator with the final value
                ReleaseAfterForwardedResumption(asyncGenerator, delegationResumeType, CompletionType.Normal);
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
            ReleaseAfterForwardedResumption(asyncGenerator, delegationResumeType, CompletionType.Throw);
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
    /// <para>
    /// Every way the delegation ends releases the native state of a built-in iterator the delegate is, a CLR
    /// collection's iterator disposing its host enumerator (sebastienros/jint#4429): at once without a <c>return</c>
    /// or when reading, calling or awaiting it throws here, a constraint or a host exception included, and otherwise once
    /// its answer has settled done, or failed (<see cref="ReleaseAfterForwardedResumption"/>). The CreateAsyncFromSyncIterator
    /// wrapper of a sync delegate has a <c>return</c> of its own, and the release reaches the sync iterator behind it.
    /// </para>
    /// </summary>
    private static JsValue DelegateReturn(
        EvaluationContext context,
        AsyncGeneratorInstance asyncGenerator,
        IteratorInstance iterator,
        JsValue receivedValue)
    {
        var engine = context.Engine;

        JsPromise innerPromise;
        try
        {
            var returnMethod = iterator.Instance.GetMethod(CommonProperties.Return);
            if (returnMethod is null)
            {
                // Step 7.c.iii: the delegation is over, so the release comes now, as IteratorClose's step 4.b does.
                iterator.CloseWithoutReturn(CompletionType.Return);
                return AwaitDelegationReturnValue(engine, asyncGenerator, receivedValue);
            }

            var innerReturnResult = returnMethod.Call(iterator.Instance, new[] { receivedValue });

            // Await and step through the delegate's answer exactly as a next() result is, with Return as the
            // completion a done result resumes the body with.
            asyncGenerator._delegationResumeType = CompletionType.Return;
            innerPromise = EnsurePromise(engine, innerReturnResult);
        }
        catch
        {
            // The throw ends the delegation here and now, and RunAsyncYieldDelegateLoop forgets it. Whatever it is - a
            // script throw, a constraint that tripped inside the `return`, a host exception - it propagates as it is: the
            // release is told a throw completion, so a host's Dispose cannot replace it unless MustPropagateHostException
            // says so.
            iterator.CloseNativeStateAfterReturn(CompletionType.Throw);
            throw;
        }

        return AwaitAndYieldDelegation(context, asyncGenerator, iterator, innerPromise);
    }

    /// <summary>
    /// The answer of the <c>return</c> or <c>throw</c> that <see cref="DelegateReturn"/> or <see cref="DelegateThrow"/>
    /// forwarded a resumption to has settled, and ended the delegation: done, or as a throw, which is
    /// <paramref name="leaving"/>. The delegate's native state is released now, as IteratorClose releases it after a
    /// <c>return</c>, and only for a delegation <paramref name="delegationResumeType"/> says was forwarding one: a next()
    /// that ends a delegation leaves a delegate that ran out or failed a step, and a CLR collection's iterator releases
    /// itself at either. The iterator is read off the generator while the delegation still holds it, so the reactions
    /// capture nothing more for it.
    /// </summary>
    private static void ReleaseAfterForwardedResumption(
        AsyncGeneratorInstance asyncGenerator,
        CompletionType delegationResumeType,
        CompletionType leaving)
    {
        if (delegationResumeType != CompletionType.Normal)
        {
            asyncGenerator._delegatingIterator?.CloseNativeStateAfterReturn(leaving);
        }
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

    /// <summary>
    /// Step 7.b.iii of https://tc39.es/ecma262/#sec-generator-function-definitions-runtime-semantics-evaluation in an
    /// async generator, whose delegate has no <c>throw</c>: <c>? AsyncIteratorClose(iteratorRecord,
    /// NormalCompletion(empty))</c> (https://tc39.es/ecma262/#sec-asynciteratorclose), and then the <c>TypeError</c>.
    /// Steps 3 to 4.c of the close run here, and step 4.d's Await of what <c>return</c> answered suspends the generator
    /// in the yield*; <see cref="DelegateCloseContinuation"/> takes steps 6 and 7 once it settles. The close's completion
    /// is a normal one, so whatever is abrupt in it is what the yield* throws: reading or calling <c>return</c>, the
    /// Await's PromiseResolve and a rejection (step 6), and an answer that is not an object (step 7). Only a close that
    /// completes leaves the <c>TypeError</c> for the missing <c>throw</c>. Without a <c>return</c> nothing is awaited,
    /// and that <c>TypeError</c> is thrown at once (step 4.b). The close used to call <c>return</c> and throw the
    /// <c>TypeError</c> without awaiting anything (sebastienros/jint#4498).
    /// <para>
    /// A CLR collection's iterator the delegate is disposes its host enumerator in its native close: in place of a
    /// missing <c>return</c>, at once when reading, calling or awaiting it throws, and otherwise once what it answered has
    /// settled, as the other AsyncIteratorCloses release it (<see cref="IteratorInstance.CloseNativeStateAfterReturn"/>).
    /// A throw leaves the yield* whichever way the close ends, so the release is told one, and a host's
    /// <c>Dispose</c> does not replace it. Out of line: only a throw() into a delegate with no <c>throw</c> reaches it.
    /// </para>
    /// </summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static JsValue CloseDelegateWithoutThrow(
        EvaluationContext context,
        AsyncGeneratorInstance asyncGenerator,
        IteratorInstance iteratorRecord)
    {
        var engine = context.Engine;
        var iterator = iteratorRecord.Instance;

        JsPromise? promise = null;
        try
        {
            // Step 3: Let innerResult be Completion(GetMethod(iterator, "return")).
            var returnMethod = iterator.GetMethod(CommonProperties.Return);
            if (returnMethod is not null)
            {
                // Step 4.c, and step 4.d's Await: its PromiseResolve(%Promise%, value) reads a promise's constructor,
                // which may throw, and that is an abrupt innerResult step 6 propagates as it is.
                var innerResult = returnMethod.Call(iterator, Arguments.Empty);
                promise = EnsurePromise(engine, innerResult);
            }
        }
        catch
        {
            // Step 6: the close is over, and RunAsyncYieldDelegateLoop forgets the delegation.
            iteratorRecord.CloseNativeStateAfterReturn(CompletionType.Throw);
            throw;
        }

        if (promise is null)
        {
            // Step 4.b: If return is undefined, return ? completion, which is normal; then step 7.b.iii.6 of yield*.
            iteratorRecord.CloseNativeStateAfterReturn(CompletionType.Throw);
            Throw.TypeError(engine.Realm, "Iterator does not have throw method");
        }

        // Step 4.d awaits. Clear the iterator but keep _delegatingYieldNode, so the resume re-enters at the yield* and
        // throws there what the close decided. The request stays the current one, so a request made meanwhile queues.
        asyncGenerator.ClearDelegationIterator();
        asyncGenerator._asyncGeneratorState = AsyncGeneratorState.SuspendedYield;

        PromiseOperations.PerformPromiseThen(
            engine,
            promise,
            new DelegateCloseContinuation(asyncGenerator, iteratorRecord, engine.Realm, asyncGenerator._currentPromiseCapability));

        return JsValue.Undefined;
    }

    /// <summary>
    /// The reaction to the Await of <see cref="CloseDelegateWithoutThrow"/>: steps 6 to 8 of
    /// https://tc39.es/ecma262/#sec-asynciteratorclose, and then step 7.b.iii.6 of yield*. A rejection is what the
    /// yield* throws (step 6), an answer that is not an object is a <c>TypeError</c> (step 7), and otherwise the close
    /// completes normally and the <c>TypeError</c> for the missing <c>throw</c> follows. Each is thrown at the yield*,
    /// where a <c>catch</c> around it sees it (<see cref="AsyncGeneratorInstance.ThrowAfterDelegation"/>), and is created
    /// in the realm the generator was running in when it suspended.
    /// </summary>
    private sealed class DelegateCloseContinuation : IPromiseContinuation
    {
        private readonly AsyncGeneratorInstance _asyncGenerator;
        private readonly IteratorInstance _iteratorRecord;
        private readonly Realm _realm;
        private readonly PromiseCapability? _capability;

        public DelegateCloseContinuation(
            AsyncGeneratorInstance asyncGenerator,
            IteratorInstance iteratorRecord,
            Realm realm,
            PromiseCapability? capability)
        {
            _asyncGenerator = asyncGenerator;
            _iteratorRecord = iteratorRecord;
            _realm = realm;
            _capability = capability;
        }

        public void Invoke(Engine engine, JsValue value, ReactionType type)
        {
            // The return has settled, so the native state goes now; a throw leaves the yield* whatever it settled to.
            _iteratorRecord.CloseNativeStateAfterReturn(CompletionType.Throw);

            JsValue error;
            if (type == ReactionType.Reject)
            {
                error = value;
            }
            else if (!value.IsObject())
            {
                error = _realm.Intrinsics.TypeError.Construct("Iterator returned non-object");
            }
            else
            {
                error = _realm.Intrinsics.TypeError.Construct("Iterator does not have throw method");
            }

            _asyncGenerator.ThrowAfterDelegation(error, _capability);
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
