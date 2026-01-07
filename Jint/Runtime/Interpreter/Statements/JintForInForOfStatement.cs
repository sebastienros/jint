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

    private ProbablyBlockStatement _body;
    private JintExpression? _expr;
    private DestructuringPattern? _assignmentPattern;
    private JintExpression _right = null!;
    private List<Key>? _tdzNames;
    private bool _destructuring;
    private LhsKind _lhsKind;
    private DisposeHint _disposeHint;

    public JintForInForOfStatement(ForInStatement statement) : base(statement)
    {
        _leftNode = statement.Left;
        _rightExpression = statement.Right;
        _forBody = statement.Body;
        _iterationKind = IterationKind.Enumerate;
    }

    public JintForInForOfStatement(ForOfStatement statement) : base(statement)
    {
        _leftNode = statement.Left;
        _rightExpression = statement.Right;
        _forBody = statement.Body;
        _iterationKind = statement.Await ? IterationKind.AsyncIterate : IterationKind.Iterate;
    }

    protected override void Initialize(EvaluationContext context2)
    {
        _lhsKind = LhsKind.Assignment;
        _disposeHint = DisposeHint.Normal;
        switch (_leftNode)
        {
            case VariableDeclaration variableDeclaration:
                {
                    _lhsKind = variableDeclaration.Kind == VariableDeclarationKind.Var
                        ? LhsKind.VarBinding
                        : LhsKind.LexicalBinding;

                    _disposeHint = variableDeclaration.Kind.GetDisposeHint();

                    var variableDeclarationDeclaration = variableDeclaration.Declarations[0];
                    var id = variableDeclarationDeclaration.Id;
                    if (_lhsKind == LhsKind.LexicalBinding)
                    {
                        _tdzNames = new List<Key>(1);
                        id.GetBoundNames(_tdzNames);
                    }

                    if (id is DestructuringPattern pattern)
                    {
                        _destructuring = true;
                        _assignmentPattern = pattern;
                    }
                    else
                    {
                        var identifier = (Identifier) id;
                        _expr = new JintIdentifierExpression(identifier);
                    }

                    break;
                }
            case DestructuringPattern pattern:
                _destructuring = true;
                _assignmentPattern = pattern;
                break;
            case MemberExpression memberExpression:
                _expr = new JintMemberExpression(memberExpression);
                break;
            default:
                _expr = new JintIdentifierExpression((Identifier) _leftNode);
                break;
        }

        _body = new ProbablyBlockStatement(_forBody);
        _right = JintExpression.Build(_rightExpression);
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
            if (suspendable.TryGetSuspendData<ForOfSuspendData>(this, out suspendData))
            {
                // We're resuming into this for-of loop - use the saved iterator
                keyResult = suspendData!.Iterator;
                resuming = true;
            }
            // Try async for-await-of suspend data
            else if (suspendable.TryGetSuspendData<ForAwaitSuspendData>(this, out forAwaitSuspendData))
            {
                // Check if we're resuming from a rejection - if so, throw the error
                // Note: This requires async-specific handling for _resumeWithThrow
                var asyncFunction = engine.ExecutionContext.AsyncFunction;
                if (asyncFunction is not null && asyncFunction._lastAwaitNode == this && asyncFunction._resumeWithThrow)
                {
                    var error = suspendable.SuspendedValue ?? JsValue.Undefined;
                    suspendable.IsResuming = false;
                    asyncFunction._lastAwaitNode = null;
                    asyncFunction._resumeWithThrow = false;
                    suspendable.ClearSuspendData(this);

                    Throw.JavaScriptException(engine, error, _statement!.Location);
                    return default;
                }

                // We're resuming into this for-await-of loop - use the saved iterator
                keyResult = forAwaitSuspendData!.Iterator;
                resuming = true;
                // Clear the resuming flag since we've handled it
                suspendable.IsResuming = false;
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
                DeclarativeEnvironment? iterationEnv = null;
                JsValue nextValue;

                // Skip TryIteratorStep if we're resuming and already have a current value
                // (this happens when yield occurred during body execution)
                if (resuming && suspendData?.CurrentValue is not null)
                {
                    nextValue = suspendData.CurrentValue;
                    iterationEnv = suspendData.IterationEnv;
                    suspendData.CurrentValue = null; // Clear after use
                    resuming = false; // Only skip step on first iteration after resume

                    // Restore the iteration environment if it was saved
                    if (iterationEnv is not null)
                    {
                        engine.UpdateLexicalEnvironment(iterationEnv);
                    }
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
                        var asyncSuspendData = suspendable?.GetOrCreateSuspendData<ForAwaitSuspendData>(this);

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
                                suspendable?.ClearSuspendData(this);
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

                            // If result is a Promise, we need to await it
                            if (nextPromise is JsPromise promise)
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
                                suspendable?.ClearSuspendData(this);
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
                            suspendable?.ClearSuspendData(this);
                            return new Completion(CompletionType.Normal, v, _statement!);
                        }
                    }

                    nextValue = nextResult.Get(CommonProperties.Value);
                }

                close = true;

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

                var status = CompletionType.Normal;
                if (!destructuring)
                {
                    if (context.IsAbrupt())
                    {
                        close = true;
                        status = context.Completion;
                    }
                    else
                    {
                        var reference = (Reference) lhsRef;
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
                        nextValue,
                        iterationEnv,
                        checkPatternPropertyReference: _lhsKind != LhsKind.VarBinding);

                    // Check for suspension after destructuring
                    if (context.IsSuspended())
                    {
                        close = false; // Don't close iterator, we'll resume later
                        completionType = CompletionType.Return;
                        return new Completion(CompletionType.Return, suspendable?.SuspendedValue ?? nextValue, _statement!);
                    }

                    // Check for return request after destructuring (e.g., generator.return() was called)
                    if (suspendable?.ReturnRequested == true)
                    {
                        completionType = CompletionType.Return;
                        close = false; // Prevent double-close in finally
                        suspendable.ClearSuspendData(this);
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

                if (status != CompletionType.Normal)
                {
                    engine.UpdateLexicalEnvironment(oldEnv);
                    suspendable?.ClearSuspendData(this);
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

                // Before executing body, save state in case of yield (generators only, not async functions)
                // ForOfSuspendData is generator-specific for sync for-of loops
                var generator = engine.ExecutionContext.Generator;
                if (generator is not null)
                {
                    var data = generator.GetOrCreateSuspendData<ForOfSuspendData>(this, iteratorRecord);
                    data.AccumulatedValue = v;
                    data.CurrentValue = nextValue;
                    data.IterationEnv = iterationEnv;
                }

                var result = stmt.Execute(context);

                // Clear current value after successful body execution (not suspended)
                if (generator is not null && !context.IsSuspended())
                {
                    if (generator.TryGetSuspendData<ForOfSuspendData>(this, out var currentData))
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
                    if (generator is not null && generator.TryGetSuspendData<ForOfSuspendData>(this, out var data))
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
                    suspendable.ClearSuspendData(this);
                    iteratorRecord.Close(completionType);
                    var returnValue = suspendable.SuspendedValue ?? result.Value;
                    return new Completion(CompletionType.Return, returnValue, _statement!);
                }

                if (result.Type == CompletionType.Break && (context.Target == null || string.Equals(context.Target, _statement?.LabelSet?.Name, StringComparison.Ordinal)))
                {
                    completionType = CompletionType.Normal;
                    suspendable?.ClearSuspendData(this);
                    return new Completion(CompletionType.Normal, v, _statement!);
                }

                if (result.Type != CompletionType.Continue || (context.Target != null && !string.Equals(context.Target, _statement?.LabelSet?.Name, StringComparison.Ordinal)))
                {
                    completionType = result.Type;
                    if (result.IsAbrupt())
                    {
                        close = true;
                        suspendable?.ClearSuspendData(this);
                        return result;
                    }
                }
            }
        }
        catch
        {
            completionType = CompletionType.Throw;
            suspendable?.ClearSuspendData(this);
            throw;
        }
        finally
        {
            if (close)
            {
                suspendable?.ClearSuspendData(this);
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
            var suspendData = asyncInstance.GetOrCreateSuspendData<ForAwaitSuspendData>(this);
            suspendData.Iterator = iterator;
            suspendData.AccumulatedValue = accumulatedValue;

            // Suspend async function
            asyncInstance._lastAwaitNode = this;
            asyncInstance._state = AsyncFunctionState.SuspendedAwait;
            asyncInstance._savedContext = engine.ExecutionContext;

            // Create resume handlers
            var onFulfilled = new ClrFunction(engine, "", (_, args) =>
            {
                var resolvedValue = args.At(0);

                engine.AddToEventLoop(() =>
                {
                    // Store the resolved iterator result for resume
                    var resumeSuspendData = asyncInstance.GetOrCreateSuspendData<ForAwaitSuspendData>(this);
                    resumeSuspendData.ResolvedIteratorResult = resolvedValue as ObjectInstance;

                    asyncInstance._resumeValue = JsValue.Undefined;
                    asyncInstance._resumeWithThrow = false;
                    JintAwaitExpression.AsyncFunctionResume(engine, asyncInstance);
                });

                return JsValue.Undefined;
            }, 1, PropertyFlag.Configurable);

            var onRejected = new ClrFunction(engine, "", (_, args) =>
            {
                var rejectedValue = args.At(0);

                engine.AddToEventLoop(() =>
                {
                    asyncInstance._resumeValue = rejectedValue;
                    asyncInstance._resumeWithThrow = true;
                    JintAwaitExpression.AsyncFunctionResume(engine, asyncInstance);
                });

                return JsValue.Undefined;
            }, 1, PropertyFlag.Configurable);

            var resultCapability = PromiseConstructor.NewPromiseCapability(engine, engine.Realm.Intrinsics.Promise);
            PromiseOperations.PerformPromiseThen(engine, promise, onFulfilled, onRejected, resultCapability);

            // Return with completion that signals suspension
            return new Completion(CompletionType.Normal, JsValue.Undefined, _statement!);
        }
        else if (asyncGenerator is not null)
        {
            // When iterating over an async generator from within the same async generator,
            // we need to use the async generator's suspension mechanism.
            // Save iterator and state for resume
            var suspendData = asyncGenerator.GetOrCreateSuspendData<ForAwaitSuspendData>(this);
            suspendData.Iterator = iterator;
            suspendData.AccumulatedValue = accumulatedValue;

            // Mark that we're waiting for the iterator result
            asyncGenerator._asyncGeneratorState = Native.AsyncGenerator.AsyncGeneratorState.SuspendedYield;

            // Create resume handlers - just store the result, the async generator's
            // own mechanisms will handle resumption
            var onFulfilled = new ClrFunction(engine, "", (_, args) =>
            {
                var resolvedValue = args.At(0);

                // Store the resolved iterator result for when we resume
                var resumeSuspendData = asyncGenerator.GetOrCreateSuspendData<ForAwaitSuspendData>(this);
                resumeSuspendData.ResolvedIteratorResult = resolvedValue as ObjectInstance;

                // Set up for resumption - mark as resuming so the for-await-of loop
                // knows to use the stored result
                asyncGenerator._isResuming = true;

                // Resume the async generator
                asyncGenerator.AsyncGeneratorResumeNext();

                return JsValue.Undefined;
            }, 1, PropertyFlag.Configurable);

            var onRejected = new ClrFunction(engine, "", (_, args) =>
            {
                var rejectedValue = args.At(0);

                // On rejection, throw in the async generator context
                asyncGenerator._error = rejectedValue;
                asyncGenerator._isResuming = true;
                asyncGenerator._resumeCompletionType = CompletionType.Throw;
                asyncGenerator.AsyncGeneratorResumeNext();

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
