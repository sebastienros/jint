using System.Diagnostics.CodeAnalysis;
using Jint.Native;
using Jint.Native.AsyncFunction;
using Jint.Native.Iterator;
using Jint.Native.Object;
using Jint.Native.Promise;
using Jint.Runtime.Descriptors;
using Jint.Runtime.Environments;
using Jint.Runtime.Interop;
using Jint.Runtime.Interpreter.Expressions;
using Environment = Jint.Runtime.Environments.Environment;

namespace Jint.Runtime.Interpreter.Statements;

/// <summary>
/// https://tc39.es/ecma262/#sec-for-in-and-for-of-statements
/// </summary>
internal sealed class JintForInForOfStatement : JintStatement<Statement>
{
    private readonly Node _leftNode;
    private readonly Statement _forBody;
    private readonly Expression _rightExpression;
    private readonly IterationKind _iterationKind;

    private readonly ProbablyBlockStatement _body;
    private readonly JintExpression? _expr;
    private readonly DestructuringPattern? _assignmentPattern;
    private readonly JintExpression _right;
    private readonly List<Key>? _tdzNames;
    private readonly bool _destructuring;
    private readonly LhsKind _lhsKind;
    private readonly DisposeHint _disposeHint;

    // AnnexB B.3.6: for-in initializer expression (e.g., `for (var a = expr in obj)`)
    private readonly JintExpression? _forInVarInitializer;
    private readonly string? _forInVarName;

    public JintForInForOfStatement(ForInStatement statement) : base(statement)
    {
        _leftNode = statement.Left;
        _rightExpression = statement.Right;
        _forBody = statement.Body;
        _iterationKind = IterationKind.Enumerate;
        InitializeLhs(out _lhsKind, out _disposeHint, out _tdzNames, out _destructuring, out _assignmentPattern, out _expr);
        _body = new ProbablyBlockStatement(_forBody);
        _right = JintExpression.Build(_rightExpression);

        // AnnexB B.3.6: for-in with initializer
        if (_leftNode is VariableDeclaration { Kind: VariableDeclarationKind.Var } varDecl
            && varDecl.Declarations[0] is { Init: not null, Id: Identifier id })
        {
            _forInVarInitializer = JintExpression.Build(varDecl.Declarations[0].Init!);
            _forInVarName = id.Name;
        }
    }

    public JintForInForOfStatement(ForOfStatement statement) : base(statement)
    {
        _leftNode = statement.Left;
        _rightExpression = statement.Right;
        _forBody = statement.Body;
        _iterationKind = statement.Await ? IterationKind.AsyncIterate : IterationKind.Iterate;
        InitializeLhs(out _lhsKind, out _disposeHint, out _tdzNames, out _destructuring, out _assignmentPattern, out _expr);
        _body = new ProbablyBlockStatement(_forBody);
        _right = JintExpression.Build(_rightExpression);
    }

    private void InitializeLhs(
        out LhsKind lhsKind,
        out DisposeHint disposeHint,
        out List<Key>? tdzNames,
        out bool destructuring,
        out DestructuringPattern? assignmentPattern,
        out JintExpression? expr)
    {
        lhsKind = LhsKind.Assignment;
        disposeHint = DisposeHint.Normal;
        tdzNames = null;
        destructuring = false;
        assignmentPattern = null;
        expr = null;
        switch (_leftNode)
        {
            case VariableDeclaration variableDeclaration:
                {
                    lhsKind = variableDeclaration.Kind == VariableDeclarationKind.Var
                        ? LhsKind.VarBinding
                        : LhsKind.LexicalBinding;

                    disposeHint = variableDeclaration.Kind.GetDisposeHint();

                    var variableDeclarationDeclaration = variableDeclaration.Declarations[0];
                    var id = variableDeclarationDeclaration.Id;
                    if (lhsKind == LhsKind.LexicalBinding)
                    {
                        tdzNames = new List<Key>(1);
                        id.GetBoundNames(tdzNames);
                    }

                    if (id is DestructuringPattern pattern)
                    {
                        destructuring = true;
                        assignmentPattern = pattern;
                    }
                    else
                    {
                        var identifier = (Identifier) id;
                        expr = new JintIdentifierExpression(identifier);
                    }

                    break;
                }
            case DestructuringPattern pattern:
                destructuring = true;
                assignmentPattern = pattern;
                break;
            case MemberExpression memberExpression:
                expr = new JintMemberExpression(memberExpression);
                break;
            default:
                expr = _leftNode is Expression expression
                    ? JintExpression.Build(expression)
                    : new JintIdentifierExpression((Identifier) _leftNode);
                break;
        }
    }

    protected override Completion ExecuteInternal(EvaluationContext context)
    {
        var engine = context.Engine;
        var suspendable = engine.ExecutionContext.Suspendable;

        // Check if we're resuming from a yield/await inside this for-of/for-await-of loop
        IteratorInstance? keyResult = null;
        ForOfSuspendData? suspendData = null;
        ForAwaitSuspendData? forAwaitSuspendData = null;
        var resuming = false;

        if (suspendable is { IsResuming: true })
        {
            // Try sync for-of suspend data first (generators)
            if (suspendable.Data.TryGet(this, out suspendData))
            {
                // We're resuming into this for-of loop - use the saved iterator
                keyResult = suspendData!.Iterator;
                resuming = true;
            }
            // Try async for-await-of suspend data
            else if (suspendable.Data.TryGet(this, out forAwaitSuspendData))
            {
                // Check if we're resuming from a rejection in an async function - if so, throw the error
                var asyncFunction = engine.ExecutionContext.AsyncFunction;
                if (asyncFunction is not null && asyncFunction._lastAwaitNode == this && asyncFunction._resumeWithThrow)
                {
                    var error = suspendable.SuspendedValue ?? JsValue.Undefined;
                    suspendable.IsResuming = false;
                    asyncFunction._lastAwaitNode = null;
                    asyncFunction._resumeWithThrow = false;
                    suspendable.Data.Clear(this);

                    Throw.JavaScriptException(engine, error, _statement!.Location);
                    return default;
                }

                // Check if we're resuming from a rejection in an async generator - if so, throw the error
                if (forAwaitSuspendData!.RejectedValue is { } rejectedValue)
                {
                    suspendable.IsResuming = false;
                    suspendable.Data.Clear(this);

                    Throw.JavaScriptException(engine, rejectedValue, _statement!.Location);
                    return default;
                }

                // We're resuming into this for-await-of loop - use the saved iterator
                keyResult = forAwaitSuspendData!.Iterator;
                resuming = true;
                // Only clear IsResuming if NOT resuming from yield inside destructuring
                // (yield needs IsResuming to be true to return the resume value)
                if (forAwaitSuspendData.CurrentValue is null)
                {
                    suspendable.IsResuming = false;
                }
            }
        }

        if (!resuming)
        {
            // Normal execution - create new iterator via HeadEvaluation
            if (!HeadEvaluation(context, out keyResult))
            {
                return new Completion(CompletionType.Normal, JsValue.Undefined, _statement);
            }
        }

        var iteratorKind = _iterationKind == IterationKind.AsyncIterate ? IteratorKind.Async : IteratorKind.Sync;
        return BodyEvaluation(context, _expr, _body, keyResult!, _iterationKind, _lhsKind, suspendData, resuming, iteratorKind);
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-runtime-semantics-forin-div-ofheadevaluation-tdznames-expr-iterationkind
    /// </summary>
    private bool HeadEvaluation(EvaluationContext context, [NotNullWhen(true)] out IteratorInstance? result)
    {
        var engine = context.Engine;
        var oldEnv = engine.ExecutionContext.LexicalEnvironment;
        var tdz = JintEnvironment.NewDeclarativeEnvironment(engine, oldEnv);
        if (_tdzNames != null)
        {
            var TDZEnvRec = tdz;
            foreach (var name in _tdzNames)
            {
                TDZEnvRec.CreateMutableBinding(name);
            }
        }

        engine.UpdateLexicalEnvironment(tdz);

        // AnnexB B.3.6: evaluate for-in initializer before the right-hand expression
        if (_forInVarInitializer is not null)
        {
            var lhs = engine.ResolveBinding(_forInVarName!);
            var value = _forInVarInitializer.GetValue(context);
            engine.PutValue(lhs, value);
        }

        var exprValue = _right.GetValue(context);
        engine.UpdateLexicalEnvironment(oldEnv);

        // Check if execution suspended during the right-hand-side evaluation (e.g., await in array)
        if (context.IsSuspended())
        {
            // Return false with null - the for-await statement will return normally and the
            // statement list's suspension check will handle saving the index for resume.
            result = null;
            return false;
        }

        if (_iterationKind == IterationKind.Enumerate)
        {
            if (exprValue.IsNullOrUndefined())
            {
                result = null;
                return false;
            }

            var obj = TypeConverter.ToObject(engine.Realm, exprValue);
            result = new IteratorInstance.EnumerableIterator(engine, obj.GetKeys());
        }
        else if (_iterationKind == IterationKind.AsyncIterate)
        {
            // For await-of uses async iteration
            result = exprValue as IteratorInstance ?? exprValue.GetIterator(engine.Realm, Native.Generator.GeneratorKind.Async);
        }
        else
        {
            result = exprValue as IteratorInstance ?? exprValue.GetIterator(engine.Realm);
        }

        return true;
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-runtime-semantics-forin-div-ofbodyevaluation-lhs-stmt-iterator-lhskind-labelset
    /// </summary>
    private Completion BodyEvaluation(
        EvaluationContext context,
        JintExpression? lhs,
        in ProbablyBlockStatement stmt,
        IteratorInstance iteratorRecord,
        IterationKind iterationKind,
        LhsKind lhsKind,
        ForOfSuspendData? suspendData = null,
        bool resuming = false,
        IteratorKind iteratorKind = IteratorKind.Sync)
    {
        var engine = context.Engine;
        var suspendable = engine.ExecutionContext.Suspendable;
        var oldEnv = engine.ExecutionContext.LexicalEnvironment;

        // When resuming from await/yield inside a body with let declarations,
        // the saved execution context has a block-scoped environment. Restore
        // the correct outer env from suspend data.
        if (resuming && suspendData?.OuterEnv is not null)
        {
            oldEnv = suspendData.OuterEnv;
            engine.UpdateLexicalEnvironment(oldEnv);
        }

        // Restore accumulated value if resuming
        var v = suspendData?.AccumulatedValue ?? JsValue.Undefined;
        var destructuring = _destructuring;
        string? lhsName = null;

        var completionType = CompletionType.Normal;
        var close = false;

        try
        {
            while (true)
            {
                engine.ExecutionContext.ClearCompletedAwaitsIfNotResuming();

                DeclarativeEnvironment? iterationEnv = null;
                JsValue nextValue;
                var skipLhsSetup = false;

                // Skip TryIteratorStep if we're resuming and already have a current value
                // (this happens when yield occurred during body execution or destructuring)
                if (resuming && suspendData?.CurrentValue is not null)
                {
                    nextValue = suspendData.CurrentValue;
                    iterationEnv = suspendData.IterationEnv;
                    skipLhsSetup = suspendData.LhsBindingComplete;
                    suspendData.CurrentValue = null; // Clear after use
                    suspendData.LhsBindingComplete = false; // Save block re-sets if body re-suspends
                    resuming = false; // Only skip step on first iteration after resume

                    // Restore the iteration environment if it was saved
                    if (iterationEnv is not null)
                    {
                        engine.UpdateLexicalEnvironment(iterationEnv);
                    }
                }
                else if (resuming && iteratorKind == IteratorKind.Async
                         && suspendable?.Data.TryGet<ForAwaitSuspendData>(this, out var asyncResumeData) == true
                         && asyncResumeData?.CurrentValue is not null)
                {
                    // Resuming from yield inside destructuring in for-await-of
                    nextValue = asyncResumeData.CurrentValue;
                    asyncResumeData.CurrentValue = null;
                    resuming = false;
                }
                else
                {
                    ObjectInstance nextResult;

                    if (iteratorKind == IteratorKind.Async)
                    {
                        // For async iteration, we need to await the Promise from next()
                        // Note: We need direct access to async instances for state manipulation in SuspendForAsyncIteration
                        var asyncInstance = engine.ExecutionContext.AsyncFunction;
                        var asyncGenerator = engine.ExecutionContext.AsyncGenerator;
                        var asyncSuspendData = suspendable?.Data.GetOrCreate<ForAwaitSuspendData>(this);

                        // Check if we're resuming from awaiting next() with a successful result
                        if (asyncSuspendData?.ResolvedIteratorResult is not null)
                        {
                            nextResult = asyncSuspendData.ResolvedIteratorResult;
                            asyncSuspendData.ResolvedIteratorResult = null;
                            v = asyncSuspendData.AccumulatedValue;

                            // Check if iterator is done
                            var doneVal = nextResult.Get(CommonProperties.Done);
                            if (!doneVal.IsUndefined() && TypeConverter.ToBoolean(doneVal))
                            {
                                close = true;
                                suspendable?.Data.Clear(this);
                                return new Completion(CompletionType.Normal, v, _statement!);
                            }
                        }
                        else
                        {
                            // Call next() on the iterator - for async iterators this returns a Promise
                            var nextMethod = iteratorRecord.Instance.Get(CommonProperties.Next) as ICallable;
                            if (nextMethod is null)
                            {
                                Throw.TypeError(engine.Realm, "Iterator does not have a next method");
                                return default;
                            }

                            var nextPromise = nextMethod.Call(iteratorRecord.Instance, Arguments.Empty);

                            // Per spec 13.7.5.13 step 5.b.c: Await(nextResult)
                            // Await step 1: PromiseResolve(%Promise%, nextResult)
                            // This makes constructor lookups observable per spec.
                            var promiseResolved = engine.Realm.Intrinsics.Promise.PromiseResolve(nextPromise);

                            // If result is a Promise, we need to await it
                            if (promiseResolved is JsPromise promise)
                            {
                                // Save current state for resume (including iterator)
                                if (asyncSuspendData is not null)
                                {
                                    asyncSuspendData.AccumulatedValue = v;
                                    asyncSuspendData.Iterator = iteratorRecord;
                                }

                                // Don't close the iterator when suspending - we'll resume later
                                close = false;

                                // Suspend and await the promise
                                return SuspendForAsyncIteration(context, promise, asyncInstance, asyncGenerator, iteratorRecord, v);
                            }

                            // Not a promise - treat as sync iterator result
                            nextResult = (nextPromise as ObjectInstance)!;
                            if (nextResult is null)
                            {
                                Throw.TypeError(engine.Realm, "Iterator result is not an object");
                                return default;
                            }

                            // Check if iterator is done
                            var doneVal = nextResult.Get(CommonProperties.Done);
                            if (!doneVal.IsUndefined() && TypeConverter.ToBoolean(doneVal))
                            {
                                close = true;
                                suspendable?.Data.Clear(this);
                                return new Completion(CompletionType.Normal, v, _statement!);
                            }
                        }
                    }
                    else
                    {
                        // Sync iteration - use existing TryIteratorStep
                        if (!iteratorRecord.TryIteratorStep(out nextResult))
                        {
                            close = true;
                            // Clean up suspend data on normal completion
                            suspendable?.Data.Clear(this);
                            return new Completion(CompletionType.Normal, v, _statement!);
                        }
                    }

                    nextValue = nextResult.Get(CommonProperties.Value);
                }

                close = true;

                var valueForResume = nextValue;
                var status = CompletionType.Normal;

                // Skip lhs setup (env creation, BindingInstantiation, destructuring/init) on body
                // resume — bindings already exist in the restored iterationEnv. Re-running
                // destructuring against `valueForResume` would consume a one-shot iterator twice.
                if (!skipLhsSetup)
                {
                    object lhsRef = null!;
                    if (lhsKind != LhsKind.LexicalBinding)
                    {
                        if (!destructuring)
                        {
                            lhsRef = lhs!.Evaluate(context);
                        }
                    }
                    else
                    {
                        iterationEnv = JintEnvironment.NewDeclarativeEnvironment(engine, oldEnv);
                        if (_tdzNames != null)
                        {
                            BindingInstantiation(iterationEnv);
                        }
                        engine.UpdateLexicalEnvironment(iterationEnv);

                        if (!destructuring)
                        {
                            var identifier = (Identifier) ((VariableDeclaration) _leftNode).Declarations[0].Id;
                            lhsName ??= identifier.Name;
                            lhsRef = engine.ResolveBinding(lhsName);
                        }
                    }

                    if (context.DebugMode)
                    {
                        context.Engine.Debugger.OnStep(_leftNode);
                    }

                    if (!destructuring)
                    {
                        if (context.IsAbrupt())
                        {
                            close = true;
                            status = context.Completion;
                        }
                        else
                        {
                            var reference = lhsRef as Reference;
                            if (reference is null)
                            {
                                Throw.ReferenceError(engine.Realm, "Invalid left-hand side in assignment");
                            }
                            if (lhsKind == LhsKind.LexicalBinding || _leftNode.Type == NodeType.Identifier && !reference.IsUnresolvableReference)
                            {
                                reference.InitializeReferencedBinding(nextValue, _disposeHint);
                            }
                            else
                            {
                                engine.PutValue(reference, nextValue);
                            }
                        }
                    }
                    else
                    {
                        nextValue = DestructuringPatternAssignmentExpression.ProcessPatterns(
                            context,
                            _assignmentPattern!,
                            valueForResume,
                            iterationEnv,
                            checkPatternPropertyReference: _lhsKind != LhsKind.VarBinding);

                        // Check for suspension after destructuring (yield inside pattern)
                        if (context.IsSuspended())
                        {
                            close = false; // Don't close iterator, we'll resume later
                            // Save the ORIGINAL iterator value for replay when resuming
                            if (_iterationKind == IterationKind.AsyncIterate && suspendable is not null)
                            {
                                var asyncSD = suspendable.Data.GetOrCreate<ForAwaitSuspendData>(this);
                                asyncSD.CurrentValue = valueForResume;
                                asyncSD.AccumulatedValue = v;
                            }
                            completionType = CompletionType.Return;
                            return new Completion(CompletionType.Return, suspendable?.SuspendedValue ?? nextValue, _statement!);
                        }

                        // Check for return request after destructuring (e.g., generator.return() was called)
                        if (suspendable?.ReturnRequested == true)
                        {
                            completionType = CompletionType.Return;
                            close = false; // Prevent double-close in finally
                            suspendable.Data.Clear(this);
                            iteratorRecord.Close(completionType);
                            var returnValue = suspendable.SuspendedValue ?? nextValue;
                            return new Completion(CompletionType.Return, returnValue, _statement!);
                        }

                        status = context.Completion;

                        if (lhsKind == LhsKind.Assignment)
                        {
                            // DestructuringAssignmentEvaluation of assignmentPattern using nextValue as the argument.
                        }
#pragma warning disable MA0140
                        else if (lhsKind == LhsKind.VarBinding)
                        {
                            // BindingInitialization for lhs passing nextValue and undefined as the arguments.
                        }
                        else
                        {
                            // BindingInitialization for lhs passing nextValue and iterationEnv as arguments
                        }
#pragma warning restore MA0140
                    }
                }

                if (status != CompletionType.Normal)
                {
                    engine.UpdateLexicalEnvironment(oldEnv);
                    suspendable?.Data.Clear(this);
                    if (_iterationKind == IterationKind.AsyncIterate)
                    {
                        iteratorRecord.Close(status);
                        return new Completion(status, nextValue, context.LastSyntaxElement);
                    }

                    if (iterationKind == IterationKind.Enumerate)
                    {
                        return new Completion(status, nextValue, context.LastSyntaxElement);
                    }

                    iteratorRecord.Close(status);
                    return new Completion(status, nextValue, context.LastSyntaxElement);
                }

                // Before executing body, save state in case of yield/await suspension.
                var generator = engine.ExecutionContext.Generator;
                if (generator is not null)
                {
                    var data = generator.Data.GetOrCreate<ForOfSuspendData>(this, iteratorRecord);
                    data.AccumulatedValue = v;
                    data.CurrentValue = valueForResume;
                    data.IterationEnv = iterationEnv;
                    data.OuterEnv = oldEnv;
                    data.LhsBindingComplete = true;
                }

                // For async functions with sync iterators, save state so that if an await
                // in the body suspends execution, we can resume at the correct iteration
                // without restarting the whole loop from scratch.
                var asyncFnBody = engine.ExecutionContext.AsyncFunction;
                if (iteratorKind == IteratorKind.Sync && asyncFnBody is not null)
                {
                    var asyncData = asyncFnBody.Data.GetOrCreate<ForOfSuspendData>(this, iteratorRecord);
                    asyncData.AccumulatedValue = v;
                    asyncData.CurrentValue = valueForResume;
                    asyncData.IterationEnv = iterationEnv;
                    asyncData.OuterEnv = oldEnv;
                    asyncData.LhsBindingComplete = true;
                }

                var result = stmt.Execute(context);

                // Clear current value after successful body execution (not suspended)
                if (generator is not null && !context.IsSuspended())
                {
                    if (generator.Data.TryGet<ForOfSuspendData>(this, out var currentData))
                    {
                        currentData!.CurrentValue = null;
                    }
                }

                result = iterationEnv?.DisposeResources(result) ?? result;
                engine.UpdateLexicalEnvironment(oldEnv);

                if (!result.Value.IsEmpty)
                {
                    v = result.Value;
                    // Update accumulated value in suspend data
                    if (generator is not null && generator.Data.TryGet<ForOfSuspendData>(this, out var data))
                    {
                        data!.AccumulatedValue = v;
                    }
                }

                // Check for suspension - if suspended, we need to exit the loop
                if (context.IsSuspended())
                {
                    // Iterator is already saved in suspend data, just exit
                    close = false; // Don't close - we'll resume
                    var suspendedValue = suspendable?.SuspendedValue ?? result.Value;
                    completionType = CompletionType.Return;
                    return new Completion(CompletionType.Return, suspendedValue, _statement!);
                }

                // Check for return request (e.g., generator.return() was called)
                if (suspendable?.ReturnRequested == true)
                {
                    // Close iterator with Return completion
                    completionType = CompletionType.Return;
                    close = false; // Prevent double-close in finally
                    suspendable.Data.Clear(this);
                    iteratorRecord.Close(completionType);
                    var returnValue = suspendable.SuspendedValue ?? result.Value;
                    return new Completion(CompletionType.Return, returnValue, _statement!);
                }

                if (result.Type == CompletionType.Break && (context.Target == null || string.Equals(context.Target, _statement?.LabelSet?.Name, StringComparison.Ordinal)))
                {
                    completionType = CompletionType.Normal;
                    suspendable?.Data.Clear(this);
                    return new Completion(CompletionType.Normal, v, _statement!);
                }

                if (result.Type != CompletionType.Continue || (context.Target != null && !string.Equals(context.Target, _statement?.LabelSet?.Name, StringComparison.Ordinal)))
                {
                    completionType = result.Type;
                    if (result.IsAbrupt())
                    {
                        close = true;
                        suspendable?.Data.Clear(this);
                        return result;
                    }
                }
            }
        }
        catch
        {
            completionType = CompletionType.Throw;
            suspendable?.Data.Clear(this);
            throw;
        }
        finally
        {
            if (close)
            {
                suspendable?.Data.Clear(this);
                try
                {
                    iteratorRecord.Close(completionType);
                }
                catch
                {
                    // if we already have and exception, use it
                    if (completionType != CompletionType.Throw)
                    {
#pragma warning disable CA2219
#pragma warning disable MA0072
                        throw;
#pragma warning restore MA0072
#pragma warning restore CA2219
                    }
                }
            }
            engine.UpdateLexicalEnvironment(oldEnv);
        }
    }

    private void BindingInstantiation(Environment environment)
    {
        var envRec = (DeclarativeEnvironment) environment;
        var variableDeclaration = (VariableDeclaration) _leftNode;
        var boundNames = new List<Key>();
        variableDeclaration.GetBoundNames(boundNames);
        for (var i = 0; i < boundNames.Count; i++)
        {
            var name = boundNames[i];
            // const, using, and await using all create immutable bindings
            if (variableDeclaration.Kind is VariableDeclarationKind.Const or VariableDeclarationKind.Using or VariableDeclarationKind.AwaitUsing)
            {
                envRec.CreateImmutableBinding(name, strict: true);
            }
            else
            {
                envRec.CreateMutableBinding(name, canBeDeleted: false);
            }
        }
    }

    /// <summary>
    /// Suspends the current async function/generator to await the iterator's next() Promise.
    /// </summary>
    private Completion SuspendForAsyncIteration(
        EvaluationContext context,
        JsPromise promise,
        AsyncFunctionInstance? asyncInstance,
        Native.AsyncGenerator.AsyncGeneratorInstance? asyncGenerator,
        IteratorInstance iterator,
        JsValue accumulatedValue)
    {
        var engine = context.Engine;

        if (asyncInstance is not null)
        {
            // Save iterator and state for resume
            var suspendData = asyncInstance.Data.GetOrCreate<ForAwaitSuspendData>(this);
            suspendData.Iterator = iterator;
            suspendData.AccumulatedValue = accumulatedValue;

            // Suspend async function
            asyncInstance._lastAwaitNode = this;
            asyncInstance._state = AsyncFunctionState.SuspendedAwait;
            asyncInstance._savedContext = engine.ExecutionContext;

            // Create resume handlers - resume directly inside the reaction job (no extra AddToEventLoop hop)
            var onFulfilled = new ClrFunction(engine, "", (_, args) =>
            {
                var resolvedValue = args.At(0);

                // Store the resolved iterator result for resume
                var resumeSuspendData = asyncInstance.Data.GetOrCreate<ForAwaitSuspendData>(this);
                resumeSuspendData.ResolvedIteratorResult = resolvedValue as ObjectInstance;

                asyncInstance._resumeValue = JsValue.Undefined;
                asyncInstance._resumeWithThrow = false;
                JintAwaitExpression.AsyncFunctionResume(engine, asyncInstance);

                return JsValue.Undefined;
            }, 1, PropertyFlag.Configurable);

            var onRejected = new ClrFunction(engine, "", (_, args) =>
            {
                var rejectedValue = args.At(0);

                asyncInstance._resumeValue = rejectedValue;
                asyncInstance._resumeWithThrow = true;
                JintAwaitExpression.AsyncFunctionResume(engine, asyncInstance);

                return JsValue.Undefined;
            }, 1, PropertyFlag.Configurable);

            // Per spec Await step 3: PerformPromiseThen(promise, onFulfilled, onRejected) with no resultCapability
            PromiseOperations.PerformPromiseThen(engine, promise, onFulfilled, onRejected, null!);

            // Return with completion that signals suspension
            return new Completion(CompletionType.Normal, JsValue.Undefined, _statement!);
        }
        else if (asyncGenerator is not null)
        {
            // Save iterator and state for resume
            var suspendData = asyncGenerator.Data.GetOrCreate<ForAwaitSuspendData>(this);
            suspendData.Iterator = iterator;
            suspendData.AccumulatedValue = accumulatedValue;

            // Capture the current promise capability before suspending —
            // the request was already dequeued by AsyncGeneratorResumeNext() before
            // reaching here, so the queue is now empty. On resume we must continue
            // THIS request's execution, not start a new one via AsyncGeneratorResumeNext().
            var currentCapability = asyncGenerator._currentPromiseCapability!;

            // Mark that we're waiting for the iterator result
            asyncGenerator._asyncGeneratorState = Native.AsyncGenerator.AsyncGeneratorState.SuspendedYield;

            // Create resume handlers. Use AddToEventLoop (like the async-function path) so
            // the actual resumption happens in a distinct event-loop turn, matching spec
            // microtask ordering.
            var onFulfilled = new ClrFunction(engine, "", (_, args) =>
            {
                var resolvedValue = args.At(0);

                engine.AddToEventLoop(() =>
                {
                    // Store the resolved iterator result so the for-await-of loop can use it
                    var resumeSuspendData = asyncGenerator.Data.GetOrCreate<ForAwaitSuspendData>(this);
                    resumeSuspendData.ResolvedIteratorResult = resolvedValue as ObjectInstance;

                    // Resume the current request's execution (queue is empty – cannot use AsyncGeneratorResumeNext)
                    asyncGenerator.AsyncGeneratorContinueForAwait(currentCapability);
                });

                return JsValue.Undefined;
            }, 1, PropertyFlag.Configurable);

            var onRejected = new ClrFunction(engine, "", (_, args) =>
            {
                var rejectedValue = args.At(0);

                engine.AddToEventLoop(() =>
                {
                    // Store the rejection so ExecuteInternal can propagate it as a throw
                    var resumeSuspendData = asyncGenerator.Data.GetOrCreate<ForAwaitSuspendData>(this);
                    resumeSuspendData.RejectedValue = rejectedValue;

                    // Resume the current request's execution so the throw can be handled
                    asyncGenerator.AsyncGeneratorContinueForAwait(currentCapability);
                });

                return JsValue.Undefined;
            }, 1, PropertyFlag.Configurable);

            PromiseOperations.PerformPromiseThen(engine, promise, onFulfilled, onRejected, null!);

            // Return with completion that signals suspension
            return new Completion(CompletionType.Normal, JsValue.Undefined, _statement!);
        }
        else
        {
            // Fallback: synchronously unwrap the promise (blocking)
            try
            {
                var resolvedResult = promise.UnwrapIfPromise(engine.Options.Constraints.PromiseTimeout);
                // Continue normally with the resolved result
                // This won't work correctly for truly async promises
                Throw.TypeError(engine.Realm, "for-await-of requires an async context");
                return default;
            }
            catch (PromiseRejectedException e)
            {
                Throw.JavaScriptException(engine, e.RejectedValue, _statement!.Location);
                return default;
            }
        }
    }

    private enum LhsKind
    {
        Assignment,
        VarBinding,
        LexicalBinding
    }

    private enum IteratorKind
    {
        Sync,
        Async
    }

    private enum IterationKind
    {
        Enumerate,
        Iterate,
        AsyncIterate
    }
}
