using System.Diagnostics.CodeAnalysis;
using Jint.Native;
using Jint.Native.AsyncFunction;
using Jint.Native.Disposable;
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

    // Per-iteration environment reuse: for-of/for-in create a fresh binding per iteration with
    // no copy step, so when nothing in the body (or a destructuring default) captures the
    // environment, one fixed-slot environment reset per iteration is unobservable — and its
    // stable identity keeps per-node slot caches in the body hot. The pooled instance lives on
    // this handler (per statement list); the Interlocked + engine-identity discipline mirrors
    // JintForStatement._cachedLoopEnv (a cached env must never pin a foreign engine, #2560).
    private readonly bool _canReuseIterationEnv;
    private readonly Key[]? _iterationSlotNames;
    private readonly Binding[]? _iterationSlotTemplates;
    private DeclarativeEnvironment? _cachedIterationEnv;

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

        InitializeIterationEnvReuse(out _canReuseIterationEnv, out _iterationSlotNames, out _iterationSlotTemplates);
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

        InitializeIterationEnvReuse(out _canReuseIterationEnv, out _iterationSlotNames, out _iterationSlotTemplates);
    }

    private void InitializeIterationEnvReuse(out bool canReuse, out Key[]? slotNames, out Binding[]? slotTemplates)
    {
        canReuse = false;
        slotNames = null;
        slotTemplates = null;

        // Only plain let/const heads qualify (using/await-using register per-iteration dispose
        // resources on the environment), with 1-16 bindings, and nothing in the body — or in a
        // destructuring pattern's default-value expressions — may capture or escape the
        // per-iteration environment (closures, direct eval).
        if (_lhsKind != LhsKind.LexicalBinding
            || _disposeHint != DisposeHint.Normal
            || _tdzNames is null
            || _tdzNames.Count is 0 or > 16)
        {
            return;
        }

        if (JintFunctionDefinition.EnvironmentEscapeAstVisitor.IsCapturing(_forBody)
            || JintFunctionDefinition.EnvironmentEscapeAstVisitor.MayEscape(_forBody))
        {
            return;
        }

        if (_destructuring
            && (JintFunctionDefinition.EnvironmentEscapeAstVisitor.IsCapturing(_leftNode)
                || JintFunctionDefinition.EnvironmentEscapeAstVisitor.MayEscape(_leftNode)))
        {
            return;
        }

        var kind = ((VariableDeclaration) _leftNode).Kind;
        var names = new Key[_tdzNames.Count];
        var templates = new Binding[_tdzNames.Count];
        for (var i = 0; i < _tdzNames.Count; i++)
        {
            names[i] = _tdzNames[i];
            templates[i] = kind == VariableDeclarationKind.Const
                ? new Binding(null!, canBeDeleted: false, mutable: false, strict: true)
                : new Binding(null!, canBeDeleted: false, mutable: true, strict: false);
        }

        slotNames = names;
        slotTemplates = templates;
        canReuse = true;
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
                if (suspendData!.DisposeInProgress)
                {
                    return ResumeFromDispose(context, suspendable, suspendData);
                }
                // We're resuming into this for-of loop - use the saved iterator
                keyResult = suspendData.Iterator;
                resuming = true;
            }
            // Try async for-await-of suspend data
            else if (suspendable.Data.TryGet(this, out forAwaitSuspendData))
            {
                if (forAwaitSuspendData!.DisposeInProgress)
                {
                    return ResumeFromDispose(context, suspendable, forAwaitSuspendData);
                }

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
                if (forAwaitSuspendData.RejectedValue is { } rejectedValue)
                {
                    suspendable.IsResuming = false;
                    suspendable.Data.Clear(this);

                    Throw.JavaScriptException(engine, rejectedValue, _statement!.Location);
                    return default;
                }

                // We're resuming into this for-await-of loop - use the saved iterator
                keyResult = forAwaitSuspendData.Iterator;
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

        // Spec only requires the TDZ environment when there are TDZ names to protect (lexical
        // heads); var/assignment forms evaluate the right-hand side in the current environment.
        if (_tdzNames != null)
        {
            var tdz = JintEnvironment.NewDeclarativeEnvironment(engine, oldEnv);
            foreach (var name in _tdzNames)
            {
                tdz.CreateMutableBinding(name);
            }

            engine.UpdateLexicalEnvironment(tdz);
        }

        // AnnexB B.3.6: evaluate for-in initializer before the right-hand expression
        if (_forInVarInitializer is not null)
        {
            var lhs = engine.ResolveBinding(_forInVarName!);
            var value = _forInVarInitializer.GetValue(context);
            engine.PutValue(lhs, value);
        }

        var exprValue = _right.GetValue(context);
        if (_tdzNames != null)
        {
            engine.UpdateLexicalEnvironment(oldEnv);
        }

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
            result = new IteratorInstance.ForInIterator(engine, obj);
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

        // Reusable fixed-slot iteration environment: gated on a non-suspendable context so a
        // pooled env never round-trips through suspend/resume save-and-restore. ResetSlots at
        // each iteration start re-establishes TDZ; the stable identity keeps body slot caches hot.
        DeclarativeEnvironment? reusableEnv = null;
        if (_canReuseIterationEnv && suspendable is null)
        {
            var cachedEnv = System.Threading.Interlocked.Exchange(ref _cachedIterationEnv, null);
            if (cachedEnv is not null && ReferenceEquals(cachedEnv._engine, engine))
            {
                cachedEnv._outerEnv = oldEnv;
                reusableEnv = cachedEnv;
            }
            else
            {
                reusableEnv = JintEnvironment.NewDeclarativeEnvironment(engine, oldEnv);
                reusableEnv._slotNames = _iterationSlotNames;
                reusableEnv._slots = (Binding[]) _iterationSlotTemplates!.Clone();
            }
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
                else if (iteratorKind == IteratorKind.Async)
                {
                    ObjectInstance nextResult;
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

                    nextValue = nextResult.Get(CommonProperties.Value);
                }
                else
                {
                    // Sync iteration; TryStepValue skips the per-step IteratorResult for
                    // iterators that can (for-in keys) and is the same TryIteratorStep +
                    // Get(value) sequence for everything else.
                    if (!iteratorRecord.TryStepValue(out var steppedValue))
                    {
                        close = true;
                        // Clean up suspend data on normal completion
                        suspendable?.Data.Clear(this);
                        return new Completion(CompletionType.Normal, v, _statement!);
                    }

                    nextValue = steppedValue;
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
                    else if (reusableEnv is not null)
                    {
                        // Fresh per-iteration binding via slot reset (spec has no copy step for
                        // for-in/of, so reuse is unobservable without captures). The single
                        // identifier binding initializes straight into its slot below.
                        ResetSlots(reusableEnv._slots!, _iterationSlotTemplates!);
                        iterationEnv = reusableEnv;
                        engine.UpdateLexicalEnvironment(iterationEnv);
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
                        if (reusableEnv is not null)
                        {
                            // single bound name -> slot 0; ChangeValue preserves the const/let flags
                            iterationEnv!.InitializeSlotBinding(0, nextValue);
                        }
                        else if (context.IsAbrupt())
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

                // Dispose iteration env's resources. If the env has async-dispose
                // resources and we're in an async function (and the body didn't
                // suspend), drive the spec-mandated Await(...) suspensions via the
                // state machine and suspend the async function on each pending
                // promise — same pattern as JintBlockStatement. Sync/already-suspended
                // contexts use the legacy drive (which sync-waits via UnwrapIfPromise).
                if (iterationEnv?.HasDisposeResources == true
                    && !context.IsSuspended()
                    && engine.ExecutionContext.AsyncFunction is { } disposeAsyncFn)
                {
                    var disposeStep = iterationEnv.BeginDisposeResources(result);
                    var suspendedCompletion = DriveDispose(
                        context,
                        suspendable,
                        disposeAsyncFn,
                        iterationEnv,
                        oldEnv,
                        v,
                        iteratorRecord,
                        iteratorKind,
                        disposeStep,
                        out var disposeFinal);
                    if (suspendedCompletion is { } suspended)
                    {
                        // Prevent the finally block from clearing the suspend data we
                        // just stored and from closing the iterator — we'll resume.
                        close = false;
                        return suspended;
                    }
                    result = disposeFinal;
                }
                else
                {
                    result = iterationEnv?.DisposeResources(result) ?? result;
                }
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

            // Park the reusable iteration environment for the next loop entry; reset at park
            // time so the cached env doesn't root the completed loop's values or scope chain.
            // Reuse is gated on non-suspendable contexts, so the loop cannot exit suspended.
            if (reusableEnv is not null)
            {
                reusableEnv._outerEnv = null;
                ResetSlots(reusableEnv._slots!, _iterationSlotTemplates!);
                System.Threading.Interlocked.Exchange(ref _cachedIterationEnv, reusableEnv);
            }

            engine.UpdateLexicalEnvironment(oldEnv);
        }
    }

    /// <summary>
    /// Reset the slots of a reused iteration environment to the pre-computed templates (every
    /// binding back to uninitialized/TDZ). Hand-rolled small-array fast path: for-of/for-in
    /// heads hold 1-2 bindings.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private static void ResetSlots(Binding[] slots, Binding[] templates)
    {
        var len = slots.Length;
        if (len == templates.Length && len <= 4)
        {
            for (var i = 0; i < len; i++)
            {
                slots[i] = templates[i];
            }
        }
        else
        {
            templates.AsSpan().CopyTo(slots);
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

    /// <summary>
    /// Drives the iteration env's dispose state machine. If the next step is a
    /// suspend (await), suspends the surrounding async function on the pending
    /// promise — same machinery as a JS <c>await</c> — and returns a completion
    /// indicating the for-of has handed control back to the async runtime. On
    /// resume, <see cref="ResumeFromDispose"/> picks up where we left off.
    /// Returns null when the state machine completes synchronously; the caller
    /// then uses <paramref name="finalCompletion"/> as the post-dispose result.
    /// </summary>
    private Completion? DriveDispose(
        EvaluationContext context,
        ISuspendable? suspendable,
        AsyncFunctionInstance asyncFn,
        DeclarativeEnvironment iterationEnv,
        Environment oldEnv,
        JsValue v,
        IteratorInstance iteratorRecord,
        IteratorKind iteratorKind,
        DisposeStepResult step,
        out Completion finalCompletion)
    {
        var engine = context.Engine;
        if (step.IsDone)
        {
            finalCompletion = step.CompletedResult;
            return null;
        }

        SetupDisposeSuspension(engine, asyncFn, step.PendingPromise!);
        SaveDisposeSuspendState(suspendable, iterationEnv, oldEnv, v, iteratorRecord, iteratorKind);
        engine.UpdateLexicalEnvironment(oldEnv);
        finalCompletion = default;
        return new Completion(CompletionType.Normal, JsValue.Undefined, _statement!);
    }

    /// <summary>
    /// Resume entry point when the async function was suspended mid-dispose.
    /// Advances the iteration env's dispose state machine with the awaited result.
    /// If the state machine suspends again, re-suspends the function. When the
    /// state machine completes, applies the same post-dispose handling the main
    /// loop applies (update accumulator, propagate abrupt completions) and either
    /// continues with the next iteration via <see cref="BodyEvaluation"/>, or
    /// returns the abrupt completion.
    /// </summary>
    private Completion ResumeFromDispose(EvaluationContext context, ISuspendable suspendable, SuspendData data)
    {
        var engine = context.Engine;
        var asyncFn = engine.ExecutionContext.AsyncFunction;
        var resumeValue = asyncFn?._resumeValue ?? JsValue.Undefined;
        var resumeThrew = asyncFn?._resumeWithThrow ?? false;
        if (asyncFn is not null)
        {
            asyncFn._resumeValue = null;
            asyncFn._resumeWithThrow = false;
            asyncFn._lastAwaitNode = null;
        }
        suspendable.IsResuming = false;

        DeclarativeEnvironment iterationEnv;
        Environment oldEnv;
        JsValue v;
        IteratorInstance iteratorRecord;
        IteratorKind iteratorKind;
        if (data is ForOfSuspendData syncData)
        {
            iterationEnv = syncData.IterationEnv!;
            oldEnv = syncData.OuterEnv!;
            v = syncData.AccumulatedValue;
            iteratorRecord = syncData.Iterator!;
            iteratorKind = IteratorKind.Sync;
            syncData.DisposeInProgress = false;
        }
        else if (data is ForAwaitSuspendData asyncData)
        {
            iterationEnv = asyncData.IterationEnv!;
            oldEnv = asyncData.OuterEnv!;
            v = asyncData.AccumulatedValue;
            iteratorRecord = asyncData.Iterator!;
            iteratorKind = IteratorKind.Async;
            asyncData.DisposeInProgress = false;
        }
        else
        {
            Throw.InvalidOperationException("Unexpected suspend data type for dispose resume.");
            return default;
        }

        engine.UpdateLexicalEnvironment(iterationEnv);
        var step = iterationEnv.ContinueDisposeResources(resumeValue, resumeThrew);

        // The state machine may suspend again — handle that with the same Pattern A
        // hand-off. We can only re-suspend on AsyncFunctionInstance; if for some
        // reason it's gone, sync-wait via UnwrapIfPromise as a fallback.
        while (!step.IsDone)
        {
            if (asyncFn is not null)
            {
                SetupDisposeSuspension(engine, asyncFn, step.PendingPromise!);
                SaveDisposeSuspendState(suspendable, iterationEnv, oldEnv, v, iteratorRecord, iteratorKind);
                engine.UpdateLexicalEnvironment(oldEnv);
                return new Completion(CompletionType.Normal, JsValue.Undefined, _statement!);
            }
            try
            {
                var resolved = step.PendingPromise!.UnwrapIfPromise(engine.Options.Constraints.PromiseTimeout);
                step = iterationEnv.ContinueDisposeResources(resolved, false);
            }
            catch (PromiseRejectedException e)
            {
                step = iterationEnv.ContinueDisposeResources(e.RejectedValue, true);
            }
            catch (JavaScriptException e)
            {
                step = iterationEnv.ContinueDisposeResources(e.Error, true);
            }
        }

        var result = step.CompletedResult;
        engine.UpdateLexicalEnvironment(oldEnv);
        if (!result.Value.IsEmpty)
        {
            v = result.Value;
        }

        // Post-dispose abrupt handling — mirrors the inline code in BodyEvaluation.
        if (result.Type == CompletionType.Throw)
        {
            suspendable.Data.Clear(this);
            TryCloseIterator(iteratorRecord, CompletionType.Throw);
            Throw.JavaScriptException(engine, result.Value, _statement!.Location);
            return default;
        }

        if (result.Type == CompletionType.Break
            && (context.Target is null || string.Equals(context.Target, _statement?.LabelSet?.Name, StringComparison.Ordinal)))
        {
            suspendable.Data.Clear(this);
            TryCloseIterator(iteratorRecord, CompletionType.Normal);
            return new Completion(CompletionType.Normal, v, _statement!);
        }

        if (result.Type == CompletionType.Return)
        {
            suspendable.Data.Clear(this);
            TryCloseIterator(iteratorRecord, CompletionType.Return);
            return result;
        }

        if (result.IsAbrupt() && result.Type != CompletionType.Continue)
        {
            suspendable.Data.Clear(this);
            TryCloseIterator(iteratorRecord, result.Type);
            return result;
        }

        // Normal / Continue → next iteration. Clear dispose-specific state but
        // pass the accumulator forward via a fresh ForOfSuspendData (read by
        // BodyEvaluation's `v` init).
        suspendable.Data.Clear(this);
        var carrier = new ForOfSuspendData { Iterator = iteratorRecord, AccumulatedValue = v };
        return BodyEvaluation(context, _expr, _body, iteratorRecord, _iterationKind, _lhsKind, carrier, resuming: false, iteratorKind);
    }

    /// <summary>
    /// Mirror of <see cref="JintAwaitExpression.SuspendForAwait"/> for the dispose
    /// path: suspends the async function on the pending dispose promise so the
    /// next event-loop tick resumes us via <see cref="ResumeFromDispose"/>.
    /// </summary>
    private void SetupDisposeSuspension(Engine engine, AsyncFunctionInstance asyncFn, JsValue pendingPromise)
    {
        var promise = pendingPromise as JsPromise
            ?? (JsPromise) engine.Realm.Intrinsics.Promise.PromiseResolve(pendingPromise);

        asyncFn._lastAwaitNode = this;
        asyncFn._state = AsyncFunctionState.SuspendedAwait;
        asyncFn._savedContext = engine.ExecutionContext;

        var onFulfilled = new ClrFunction(engine, "", (_, args) =>
        {
            asyncFn._resumeValue = args.At(0);
            asyncFn._resumeWithThrow = false;
            JintAwaitExpression.AsyncFunctionResume(engine, asyncFn);
            return JsValue.Undefined;
        }, 1, PropertyFlag.Configurable);

        var onRejected = new ClrFunction(engine, "", (_, args) =>
        {
            asyncFn._resumeValue = args.At(0);
            asyncFn._resumeWithThrow = true;
            JintAwaitExpression.AsyncFunctionResume(engine, asyncFn);
            return JsValue.Undefined;
        }, 1, PropertyFlag.Configurable);

        PromiseOperations.PerformPromiseThen(engine, promise, onFulfilled, onRejected, null!);
    }

    private void SaveDisposeSuspendState(
        ISuspendable? suspendable,
        DeclarativeEnvironment iterationEnv,
        Environment oldEnv,
        JsValue v,
        IteratorInstance iteratorRecord,
        IteratorKind iteratorKind)
    {
        if (suspendable is null)
        {
            return;
        }

        // Clear any pre-existing suspend data for this statement so the dispose
        // resume isn't ambiguous with a body-await resume of a different shape.
        suspendable.Data.Clear(this);

        if (iteratorKind == IteratorKind.Async)
        {
            var data = suspendable.Data.GetOrCreate<ForAwaitSuspendData>(this, iteratorRecord);
            data.Iterator = iteratorRecord;
            data.IterationEnv = iterationEnv;
            data.OuterEnv = oldEnv;
            data.AccumulatedValue = v;
            data.DisposeInProgress = true;
        }
        else
        {
            var data = suspendable.Data.GetOrCreate<ForOfSuspendData>(this, iteratorRecord);
            data.Iterator = iteratorRecord;
            data.IterationEnv = iterationEnv;
            data.OuterEnv = oldEnv;
            data.AccumulatedValue = v;
            data.DisposeInProgress = true;
        }
    }

    private static void TryCloseIterator(IteratorInstance iterator, CompletionType completionType)
    {
        try
        {
            iterator.Close(completionType);
        }
        catch
        {
            // Best-effort close on abrupt — main path already has its own completion.
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
