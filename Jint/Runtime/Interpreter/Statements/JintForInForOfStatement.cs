using System.Diagnostics.CodeAnalysis;
using Jint.Native;
using Jint.Native.AsyncFunction;
using Jint.Native.Disposable;
using Jint.Native.Function;
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

    // Per-statement pooled for-in key iterator (Enumerate kind only). Each loop entry otherwise allocates
    // a fresh ForInIterator plus, on the first prototype-chain descent, a List<CompletedLevel>. Reused only
    // in a non-suspendable frame (the loop then runs to completion, freeing the instance by the finally) and
    // taken via Interlocked.Exchange, so a nested/recursive enumeration of the SAME statement finds the cache
    // emptied and allocates its own — pooling never shares live enumeration state. Engine-identity checked,
    // mirroring _cachedIterationEnv (a cached instance must never pin/serve a foreign engine, #2560).
    private IteratorInstance.ForInIterator? _cachedForInIterator;

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

                // Resuming from AsyncIteratorClose's Await (step 4.d). The settlement is read from
                // the suspend data, never from _resumeWithThrow below: a rejected close is only
                // sometimes a throw, and steps 5-8 are what decide.
                if (forAwaitSuspendData.CloseInProgress)
                {
                    return ResumeFromAsyncClose(context, suspendable, forAwaitSuspendData);
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
        return BodyEvaluation(context, _expr, in _body, keyResult!, _iterationKind, _lhsKind, suspendData, resuming, iteratorKind);
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

            // The head is one of NamedEvaluation's positions: "If IsAnonymousFunctionDefinition(Initializer)
            // is true, let value be ? NamedEvaluation of Initializer with argument bindingId."
            // https://tc39.es/ecma262/#sec-runtime-semantics-forinofloopevaluation
            JsValue value;
            if (_forInVarInitializer is JintClassExpression classExpression
                && _forInVarInitializer._expression.IsAnonymousFunctionDefinition())
            {
                value = classExpression.EvaluateWithName(context, _forInVarName!);
            }
            else
            {
                value = _forInVarInitializer.GetValue(context);
                if (_forInVarInitializer._expression.IsFunctionDefinition())
                {
                    // A no-op when the definition already carries its own name.
                    ((Function) value).SetFunctionName(_forInVarName!);
                }
            }

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

            // Reuse a parked iterator when not in a suspendable (generator/async) frame — the loop then
            // runs start-to-finish, so the instance is free again by BodyEvaluation's finally. Interlocked
            // take empties the cache, so a nested/recursive enumeration of the same statement allocates its
            // own instance and never shares live state.
            IteratorInstance.ForInIterator? pooled = null;
            if (engine.ExecutionContext.Suspendable is null)
            {
                pooled = System.Threading.Interlocked.Exchange(ref _cachedForInIterator, null);
            }

            if (pooled is not null && pooled.BelongsTo(engine))
            {
                pooled.ResetForReuse(obj);
                result = pooled;
            }
            else
            {
                result = new IteratorInstance.ForInIterator(engine, obj);
            }
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
        else if (resuming && iteratorKind == IteratorKind.Async
                 && suspendable?.Data.TryGet<ForAwaitSuspendData>(this, out var forAwaitEntryData) == true
                 && forAwaitEntryData is { CurrentValue: not null, OuterEnv: not null })
        {
            // Same restoration for a for-await-of resuming from a suspension INSIDE the
            // loop body (CurrentValue set): the saved execution context's lexical env is
            // the env at the await itself (e.g. the body block's env). Leaving oldEnv
            // pointing there would parent the NEXT iteration's environment under the
            // previous iteration's body block — and the block's env-reuse cache then
            // re-attaches that block env under the new iteration env, creating a CYCLIC
            // environment chain that turns the next identifier lookup into an infinite
            // walk. (A resume from the awaited next() suspends at loop level, where the
            // context env already is the outer env — nothing to restore.)
            oldEnv = forAwaitEntryData.OuterEnv;
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
                // Steps 8.a-8.f (next(), the await, the non-object check, the "done" read and the
                // "value" read) all propagate with ?, and step 8.e returns iterationResult when done
                // — none of them reaches IteratorClose. So the loop enters every iteration with
                // nothing to close, and only a completed step (below) arms it. Re-arming per
                // iteration is what keeps a next() that throws on the SECOND step from closing on
                // the strength of the first step's success.
                close = false;

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
                    // Resuming from a yield/await inside the loop body (LhsBindingComplete,
                    // saved before the body ran) or from a yield inside destructuring
                    // (LhsBindingComplete false) in for-await-of — re-enter the CURRENT
                    // iteration instead of awaiting the iterator's next result.
                    nextValue = asyncResumeData.CurrentValue;
                    iterationEnv = asyncResumeData.IterationEnv;
                    skipLhsSetup = asyncResumeData.LhsBindingComplete;
                    asyncResumeData.CurrentValue = null; // Clear after use
                    asyncResumeData.LhsBindingComplete = false; // Save block re-sets if body re-suspends
                    resuming = false;

                    // Restore the iteration environment if it was saved — but never rewind a
                    // FINER-grained environment the resume machinery already restored: an async
                    // function's saved execution context holds the env at the await itself
                    // (e.g. the body block's environment, a descendant of the iteration env when
                    // the body declares let/const). Clobbering it would detach the body block's
                    // fast-forward from its own bindings. Only update when the current env is
                    // not already the iteration env or nested inside it.
                    if (iterationEnv is not null)
                    {
                        var currentEnv = engine.ExecutionContext.LexicalEnvironment;
                        var withinIterationEnv = false;
                        for (var env = currentEnv; env is not null; env = env._outerEnv)
                        {
                            if (ReferenceEquals(env, iterationEnv))
                            {
                                withinIterationEnv = true;
                                break;
                            }
                        }

                        if (!withinIterationEnv)
                        {
                            engine.UpdateLexicalEnvironment(iterationEnv);
                        }
                    }
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
                                // step 8.e: "If done is true, return iterationResult" — no close
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
                                // step 8.e: "If done is true, return iterationResult" — no close
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
                        // step 8.e: "If done is true, return iterationResult" — no close. An
                        // iterator that runs out is not closed; only an abrupt completion of the
                        // binding or the body (below) reaches IteratorClose.
                        // Clean up suspend data on normal completion
                        suspendable?.Data.Clear(this);
                        return new Completion(CompletionType.Normal, v, _statement!);
                    }

                    nextValue = steppedValue;
                }

                // The step produced a value, so from here on an abrupt completion is the loop's own
                // (steps 8.i and 8.m) and does reach IteratorClose.
                close = true;

                var valueForResume = nextValue;

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

                            // The lhs reference is rented fresh from the pool each iteration by
                            // lhs.Evaluate(context) (identifier/member targets); neither
                            // InitializeReferencedBinding nor PutValue returns it, so a var/assignment
                            // for-in/of target leaked one Reference per key-step. Return it now — the
                            // reference is not read again this iteration.
                            engine._referencePool.Return(reference);
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

                // Async generators iterating a SYNC iterable (a plain for...of in an async
                // generator body) need the same treatment: a yield/await in the body suspends
                // and later re-enters this statement from the top, and without saved state
                // the resume path restarts the loop with a fresh iterator — replaying the
                // first step into the consumed resume value and re-yielding the second
                // element forever. for-await-of (async iterables) is handled separately via
                // ForAwaitSuspendData in SuspendForAsyncIteration.
                var asyncGenBody = engine.ExecutionContext.AsyncGenerator;
                if (iteratorKind == IteratorKind.Sync && asyncGenBody is not null)
                {
                    var asyncGenData = asyncGenBody.Data.GetOrCreate<ForOfSuspendData>(this, iteratorRecord);
                    asyncGenData.AccumulatedValue = v;
                    asyncGenData.CurrentValue = valueForResume;
                    asyncGenData.IterationEnv = iterationEnv;
                    asyncGenData.OuterEnv = oldEnv;
                    asyncGenData.LhsBindingComplete = true;
                }

                // for-await-of (async iterators) needs the same save: an await/yield in the
                // body suspends and re-enters this statement from the top, and without the
                // saved current value the resume path would await the iterator's NEXT result
                // instead of re-entering the current iteration — dropping the in-flight item
                // and, once the stream ends, silently completing the loop.
                if (iteratorKind == IteratorKind.Async && suspendable is not null
                    && (asyncFnBody is not null || asyncGenBody is not null))
                {
                    var forAwaitData = suspendable.Data.GetOrCreate<ForAwaitSuspendData>(this);
                    forAwaitData.Iterator = iteratorRecord;
                    forAwaitData.AccumulatedValue = v;
                    forAwaitData.CurrentValue = valueForResume;
                    forAwaitData.IterationEnv = iterationEnv;
                    forAwaitData.OuterEnv = oldEnv;
                    forAwaitData.LhsBindingComplete = true;
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
                else if (asyncGenBody is not null && !context.IsSuspended())
                {
                    if (asyncGenBody.Data.TryGet<ForOfSuspendData>(this, out var currentData))
                    {
                        currentData!.CurrentValue = null;
                    }
                }

                // The for-await-of current value must clear too once the body has run
                // without suspending — a stale value would shadow the awaited-next()
                // resume on a LATER suspension and replay this iteration's item.
                if (iteratorKind == IteratorKind.Async && !context.IsSuspended()
                    && suspendable?.Data.TryGet<ForAwaitSuspendData>(this, out var completedAwaitData) == true)
                {
                    completedAwaitData!.CurrentValue = null;
                    completedAwaitData.LhsBindingComplete = false;
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
                    else if (asyncGenBody is not null && asyncGenBody.Data.TryGet<ForOfSuspendData>(this, out var asyncGenAccData))
                    {
                        asyncGenAccData!.AccumulatedValue = v;
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

                if (result.Type == CompletionType.Break && (result.Target == null || string.Equals(result.Target, _statement?.LabelSet?.Name, StringComparison.Ordinal)))
                {
                    completionType = CompletionType.Normal;
                    if (iteratorKind == IteratorKind.Async)
                    {
                        // step 8.j.ii.3: "If iteratorKind is async, return ? AsyncIteratorClose(
                        // iteratorRecord, status)". The close awaits, so it may suspend, and its
                        // rejection outranks this (non-throw) completion — see CloseAsyncIterator.
                        close = false;
                        return CloseAsyncIterator(context, iteratorRecord, new Completion(CompletionType.Normal, v, _statement!));
                    }

                    suspendable?.Data.Clear(this);
                    return new Completion(CompletionType.Normal, v, _statement!);
                }

                if (result.Type != CompletionType.Continue || (result.Target != null && !string.Equals(result.Target, _statement?.LabelSet?.Name, StringComparison.Ordinal)))
                {
                    completionType = result.Type;
                    if (result.IsAbrupt())
                    {
                        // Same step, for a return or a jump naming an enclosing label. A throw
                        // completion stays on the synchronous close below: step 5 returns it
                        // whatever the close does.
                        if (iteratorKind == IteratorKind.Async && result.Type != CompletionType.Throw)
                        {
                            close = false;
                            return CloseAsyncIterator(context, iteratorRecord, result);
                        }

                        close = true;
                        suspendable?.Data.Clear(this);
                        return result;
                    }
                }
            }
        }
        catch when (LeavingOnException(suspendable, out completionType))
        {
            // Unreachable: the filter always declines. Kept as a rethrow so that a filter which one day
            // sometimes accepts still leaves this site behaving exactly as it did before it had one.
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

            // Park the for-in key iterator for the next entry. Gated on a non-suspendable frame (the loop
            // ran to completion, so the instance is no longer referenced by suspend data or the body) and
            // on engine identity. Cleared first so the cache never roots the finished enumeration's object.
            if (iterationKind == IterationKind.Enumerate
                && suspendable is null
                && iteratorRecord is IteratorInstance.ForInIterator forInIterator
                && forInIterator.BelongsTo(engine))
            {
                forInIterator.ClearForPark();
                System.Threading.Interlocked.Exchange(ref _cachedForInIterator, forInIterator);
            }

            engine.UpdateLexicalEnvironment(oldEnv);
        }
    }

    /// <summary>
    /// Exception filter for <see cref="BodyEvaluation"/>. It performs the bookkeeping every exception
    /// leaving the loop owes — recording the throw completion the <c>finally</c> below closes the iterator
    /// with, and dropping the loop's suspend data — and then <em>always declines</em>, so the frame never
    /// enters the unwind of an exception it could only rethrow.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Declining is the point, and it is the point <see cref="JintStatementList.ShouldCatch"/> already
    /// makes for statement lists: a throw from inside a catch handler restarts the two-pass dispatch, so
    /// the runtime cannot collapse the frames it has already searched and each level costs roughly 10 KB
    /// that stays live for the rest of the unwind. A <c>for-of</c> on the recursion path contributes one
    /// such frame per JavaScript call, which is why a constraint exception raised underneath one unwound a
    /// fraction as far as the same exception raised anywhere else — far enough short of the forward
    /// ceiling that <c>Options.LimitRecursion</c> went on killing the process for that shape after the
    /// statement lists had been fixed. A filter is answered in the first pass and leaves nothing behind.
    /// </para>
    /// <para>
    /// Both halves of the bookkeeping are safe to do from that first pass.
    /// <paramref name="completionType"/> is a local of the invocation being unwound, and its only reader is
    /// that same invocation's <c>finally</c>, which runs later in the second pass either way — the catch
    /// handler this replaces also assigned it before that <c>finally</c> could see it. The suspend data
    /// keyed by this statement is written and read only by <see cref="BodyEvaluation"/> itself, on entry
    /// and resume paths that cannot be running while an exception is in flight through it; and no script
    /// code runs in between to reach them, because Jint evaluates a JavaScript <c>finally</c> block as
    /// forward interpretation of a parked completion (<c>JintTryStatement.ExecuteFinalizer</c>) rather than
    /// from a CLR <c>finally</c>, so a CLR unwind through the interpreter carries interpreter bookkeeping
    /// only. Clearing it is a dictionary removal: it does not recurse, does not allocate and cannot throw,
    /// which is the contract a filter owes here — see
    /// <see cref="ExceptionDataHelper.HasJavaScriptLocation"/>.
    /// </para>
    /// <para>
    /// What the loop still does on the way out is unchanged. The <c>finally</c> reads the very
    /// <see cref="CompletionType.Throw"/> written here, so IteratorClose is still performed with a throw
    /// completion — which is what makes a failing <c>return()</c> swallowed rather than allowed to replace
    /// the exception in flight, per step 5 of
    /// <see href="https://tc39.es/ecma262/#sec-iteratorclose">IteratorClose</see>.
    /// </para>
    /// </remarks>
    private bool LeavingOnException(ISuspendable? suspendable, out CompletionType completionType)
    {
        completionType = CompletionType.Throw;
        suspendable?.Data.Clear(this);
        return false;
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

            // Per spec Await step 3: PerformPromiseThen(promise, onFulfilled, onRejected) with no
            // resultCapability. The continuation resumes directly inside the reaction job (no
            // extra AddToEventLoop hop) and needs no JS-callable handler pair.
            PromiseOperations.PerformPromiseThen(engine, promise, new ForAwaitFunctionContinuation(this, asyncInstance));

            // Return with completion that signals suspension
            return new Completion(CompletionType.Normal, JsValue.Undefined, _statement!);
        }
        else if (asyncGenerator is not null)
        {
            // Save iterator and state for resume
            var suspendData = asyncGenerator.Data.GetOrCreate<ForAwaitSuspendData>(this);
            suspendData.Iterator = iterator;
            suspendData.AccumulatedValue = accumulatedValue;

            // Mark that we're waiting for the iterator result
            asyncGenerator._asyncGeneratorState = Native.AsyncGenerator.AsyncGeneratorState.SuspendedYield;

            // Capture the current promise capability before suspending —
            // the request was already dequeued by AsyncGeneratorResumeNext() before
            // reaching here, so the queue is now empty. On resume we must continue
            // THIS request's execution, not start a new one via AsyncGeneratorResumeNext().
            // The continuation re-enqueues via AddToEventLoop (like the async-function path)
            // so the actual resumption happens in a distinct event-loop turn, matching spec
            // microtask ordering.
            PromiseOperations.PerformPromiseThen(
                engine,
                promise,
                new ForAwaitGeneratorContinuation(this, asyncGenerator, asyncGenerator._currentPromiseCapability!));

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
    /// Await continuation for a for-await-of running inside an async function: stores the
    /// resolved iterator result and resumes directly inside the reaction job.
    /// </summary>
    private sealed class ForAwaitFunctionContinuation : IPromiseContinuation
    {
        private readonly JintForInForOfStatement _statement;
        private readonly AsyncFunctionInstance _asyncInstance;

        public ForAwaitFunctionContinuation(JintForInForOfStatement statement, AsyncFunctionInstance asyncInstance)
        {
            _statement = statement;
            _asyncInstance = asyncInstance;
        }

        public void Invoke(Engine engine, JsValue value, ReactionType type)
        {
            if (type == ReactionType.Fulfill)
            {
                // Store the resolved iterator result for resume
                var resumeSuspendData = _asyncInstance.Data.GetOrCreate<ForAwaitSuspendData>(_statement);
                resumeSuspendData.ResolvedIteratorResult = value as ObjectInstance;

                _asyncInstance._resumeValue = JsValue.Undefined;
                _asyncInstance._resumeWithThrow = false;
            }
            else
            {
                _asyncInstance._resumeValue = value;
                _asyncInstance._resumeWithThrow = true;
            }

            JintAwaitExpression.AsyncFunctionResume(engine, _asyncInstance);
        }
    }

    /// <summary>
    /// Await continuation for a for-await-of running inside an async generator: hands the
    /// settled value to the suspend data and resumes the current request in a distinct
    /// event-loop turn (see the enqueueing comment at the PerformPromiseThen call site).
    /// </summary>
    private sealed class ForAwaitGeneratorContinuation : IPromiseContinuation
    {
        private readonly JintForInForOfStatement _statement;
        private readonly Native.AsyncGenerator.AsyncGeneratorInstance _asyncGenerator;
        private readonly PromiseCapability _capability;

        public ForAwaitGeneratorContinuation(
            JintForInForOfStatement statement,
            Native.AsyncGenerator.AsyncGeneratorInstance asyncGenerator,
            PromiseCapability capability)
        {
            _statement = statement;
            _asyncGenerator = asyncGenerator;
            _capability = capability;
        }

        public void Invoke(Engine engine, JsValue value, ReactionType type)
        {
            engine.AddToEventLoop(() =>
            {
                var resumeSuspendData = _asyncGenerator.Data.GetOrCreate<ForAwaitSuspendData>(_statement);
                if (type == ReactionType.Fulfill)
                {
                    // Store the resolved iterator result so the for-await-of loop can use it
                    resumeSuspendData.ResolvedIteratorResult = value as ObjectInstance;
                }
                else
                {
                    // Store the rejection so ExecuteInternal can propagate it as a throw
                    resumeSuspendData.RejectedValue = value;
                }

                // Resume the current request's execution (queue is empty – cannot use AsyncGeneratorResumeNext)
                _asyncGenerator.AsyncGeneratorContinueForAwait(_capability);
            });
        }
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-asynciteratorclose
    /// AsyncIteratorClose for a for-await-of leaving its loop with a completion that is <em>not</em>
    /// a throw completion — a break, a return, or a jump naming an enclosing label. Steps 3 and
    /// 4.a-4.c run here; step 4.d's Await suspends the async function or async generator, and
    /// <see cref="ResumeFromAsyncClose"/> performs steps 5-8 once it settles.
    /// <para>
    /// The suspension is the whole point. Jint used to perform the synchronous IteratorClose here,
    /// which calls <c>return()</c>, sees the promise it answers with, finds it <em>is</em> an object
    /// and stops — so a <c>return()</c> that rejects was dropped on the floor and
    /// <c>for await (…) { break; }</c> completed normally where step 6 requires the rejection to
    /// become the loop's completion (#3098).
    /// </para>
    /// <para>
    /// A throw completion deliberately does not come here, and keeps taking the synchronous close in
    /// <see cref="BodyEvaluation"/>'s <c>finally</c>. Step 5 returns that completion whatever the
    /// close does, so the outcome is already the spec's; and the throw reaches that <c>finally</c> as
    /// a CLR unwind, which could only be suspended by catching it — the very thing
    /// <see cref="LeavingOnException"/> exists to avoid. What is given up is confined to the
    /// microtask the discarded Await would have taken.
    /// </para>
    /// </summary>
    private Completion CloseAsyncIterator(EvaluationContext context, IteratorInstance iteratorRecord, Completion completion)
    {
        var engine = context.Engine;
        var suspendable = engine.ExecutionContext.Suspendable;

        // The loop is over either way: whatever happens from here, it never resumes iterating.
        suspendable?.Data.Clear(this);

        // Step 5 folded forward for a throw completion, so that every caller may hand one over. The
        // close still happens, but its abrupt completion, its awaited value and therefore the Await
        // itself are all discarded — which is precisely the synchronous IteratorClose.
        if (completion.Type == CompletionType.Throw)
        {
            TryCloseIterator(iteratorRecord, CompletionType.Throw);
            return completion;
        }

        // Steps 3 and 4.a-4.c. An abrupt completion of either propagates as the loop's own, which
        // is step 6 — step 5, the one that would swallow it, needs a throw completion and this
        // method never sees one.
        if (!iteratorRecord.TryStartAsyncClose(out var innerResult))
        {
            // Step 4.b: If return is undefined, return ? completion.
            return completion;
        }

        // Step 4.d: Await(innerResult).
        var suspended = SuspendForAsyncClose(context, iteratorRecord, innerResult, completion);
        if (suspended is { } suspension)
        {
            return suspension;
        }

        // No async context to suspend in. for-await-of outside one is an early error, so this is the
        // same unreachable fallback SuspendForAsyncIteration keeps: unwrap synchronously and finish
        // the algorithm inline rather than lose the completion.
        try
        {
            var resolved = innerResult.UnwrapIfPromise(engine.Options.Constraints.PromiseTimeout);
            return FinishAsyncIteratorClose(context, completion, resolved, rejected: false);
        }
        catch (PromiseRejectedException e)
        {
            return FinishAsyncIteratorClose(context, completion, e.RejectedValue, rejected: true);
        }
    }

    /// <summary>
    /// Suspends the surrounding async function or async generator on AsyncIteratorClose's Await
    /// (step 4.d), parking the completion steps 5 and 8 still owe. Returns <see langword="null"/>
    /// when there is no async context to suspend in.
    /// </summary>
    private Completion? SuspendForAsyncClose(
        EvaluationContext context,
        IteratorInstance iteratorRecord,
        JsValue innerResult,
        Completion completion)
    {
        var engine = context.Engine;
        var suspendable = engine.ExecutionContext.Suspendable;
        var asyncInstance = engine.ExecutionContext.AsyncFunction;
        var asyncGenerator = engine.ExecutionContext.AsyncGenerator;

        if (suspendable is null || (asyncInstance is null && asyncGenerator is null))
        {
            return null;
        }

        // Await step 1: PromiseResolve(%Promise%, value).
        var promise = innerResult as JsPromise
            ?? (JsPromise) engine.Realm.Intrinsics.Promise.PromiseResolve(innerResult);

        var data = suspendable.Data.GetOrCreate<ForAwaitSuspendData>(this, iteratorRecord);
        data.Iterator = iteratorRecord;
        data.CloseInProgress = true;
        data.CloseCompletion = completion;
        data.CloseSettledValue = null;
        data.CloseSettledRejected = false;

        if (asyncInstance is not null)
        {
            asyncInstance._lastAwaitNode = this;
            asyncInstance._state = AsyncFunctionState.SuspendedAwait;
            asyncInstance._savedContext = engine.ExecutionContext;
            PromiseOperations.PerformPromiseThen(engine, promise, new AsyncCloseFunctionContinuation(this, asyncInstance));
        }
        else
        {
            asyncGenerator!._lastYieldNode = this;
            asyncGenerator._awaitSuspended = true;
            PromiseOperations.PerformPromiseThen(
                engine,
                promise,
                new AsyncCloseGeneratorContinuation(this, asyncGenerator, asyncGenerator._currentPromiseCapability!));
        }

        return new Completion(CompletionType.Normal, JsValue.Undefined, _statement!);
    }

    /// <summary>
    /// Resume entry point for a for-await-of suspended on AsyncIteratorClose's Await, reached from
    /// <see cref="ExecuteInternal"/> when the parked data says the close is what is in flight.
    /// </summary>
    private Completion ResumeFromAsyncClose(EvaluationContext context, ISuspendable suspendable, ForAwaitSuspendData data)
    {
        var engine = context.Engine;
        var completion = data.CloseCompletion;
        var settled = data.CloseSettledValue ?? JsValue.Undefined;
        var rejected = data.CloseSettledRejected;

        suspendable.IsResuming = false;
        suspendable.Data.Clear(this);

        // Undo exactly the suspension that was armed, and nothing else: the async-function branch is
        // preferred there (both fields can be set on one context — UpdateAsyncFunction carries the
        // generator's over), so clearing an async generator's yield node here when a function was
        // what suspended would discard a suspension point that is still live.
        var asyncFn = engine.ExecutionContext.AsyncFunction;
        if (asyncFn is not null)
        {
            asyncFn._resumeValue = null;
            asyncFn._resumeWithThrow = false;
            asyncFn._lastAwaitNode = null;
        }
        else if (engine.ExecutionContext.AsyncGenerator is { } asyncGenerator)
        {
            asyncGenerator._resumeWithThrow = false;
            asyncGenerator._lastYieldNode = null;
        }

        return FinishAsyncIteratorClose(context, completion, settled, rejected);
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-asynciteratorclose steps 5-8, applied once the Await of step 4.d
    /// has settled.
    /// </summary>
    private Completion FinishAsyncIteratorClose(EvaluationContext context, Completion completion, JsValue settled, bool rejected)
    {
        var engine = context.Engine;

        // Step 5: If completion is a throw completion, return ? completion. Kept whole even though
        // CloseAsyncIterator now answers a throw completion before it can reach here, so this
        // method reads as the algorithm's tail rather than as the half of it that happens to remain.
        if (completion.Type == CompletionType.Throw)
        {
            Throw.JavaScriptException(engine, completion.Value, _statement!.Location);
        }

        // Step 6: If innerResult is a throw completion, return ? innerResult.
        if (rejected)
        {
            Throw.JavaScriptException(engine, settled, _statement!.Location);
        }

        // Step 7: If innerResult.[[Value]] is not an Object, throw a TypeError exception. The Await
        // is what makes this bite for an async iterator: the check is against the *settled* value,
        // so a return() answering Promise.resolve(42) fails it exactly as a sync one answering 42.
        if (!settled.IsObject())
        {
            Throw.TypeError(engine.Realm, "Iterator returned non-object");
        }

        // Step 8: Return ? completion.
        return completion;
    }

    /// <summary>
    /// Await continuation for AsyncIteratorClose step 4.d inside an async function: records how the
    /// close settled and resumes the function, which re-enters this statement at
    /// <see cref="ResumeFromAsyncClose"/>.
    /// </summary>
    private sealed class AsyncCloseFunctionContinuation : IPromiseContinuation
    {
        private readonly JintForInForOfStatement _statement;
        private readonly AsyncFunctionInstance _asyncInstance;

        public AsyncCloseFunctionContinuation(JintForInForOfStatement statement, AsyncFunctionInstance asyncInstance)
        {
            _statement = statement;
            _asyncInstance = asyncInstance;
        }

        public void Invoke(Engine engine, JsValue value, ReactionType type)
        {
            var data = _asyncInstance.Data.GetOrCreate<ForAwaitSuspendData>(_statement);
            data.CloseSettledValue = value;
            data.CloseSettledRejected = type == ReactionType.Reject;

            // The generic resume path must stay neutral: steps 5-8, not _resumeWithThrow, decide
            // whether a rejected close becomes a throw.
            _asyncInstance._resumeValue = JsValue.Undefined;
            _asyncInstance._resumeWithThrow = false;

            JintAwaitExpression.AsyncFunctionResume(engine, _asyncInstance);
        }
    }

    /// <summary>
    /// Await continuation for AsyncIteratorClose step 4.d inside an async generator. Mirrors
    /// <see cref="AsyncCloseFunctionContinuation"/>, resuming the current request the way a plain
    /// <c>await</c> in a generator body does.
    /// </summary>
    private sealed class AsyncCloseGeneratorContinuation : IPromiseContinuation
    {
        private readonly JintForInForOfStatement _statement;
        private readonly Native.AsyncGenerator.AsyncGeneratorInstance _asyncGenerator;
        private readonly PromiseCapability _capability;

        public AsyncCloseGeneratorContinuation(
            JintForInForOfStatement statement,
            Native.AsyncGenerator.AsyncGeneratorInstance asyncGenerator,
            PromiseCapability capability)
        {
            _statement = statement;
            _asyncGenerator = asyncGenerator;
            _capability = capability;
        }

        public void Invoke(Engine engine, JsValue value, ReactionType type)
        {
            var data = _asyncGenerator.Data.GetOrCreate<ForAwaitSuspendData>(_statement);
            data.CloseSettledValue = value;
            data.CloseSettledRejected = type == ReactionType.Reject;

            _asyncGenerator._nextValue = JsValue.Undefined;
            _asyncGenerator._resumeWithThrow = false;
            _asyncGenerator._awaitSuspended = false;
            _asyncGenerator.AsyncGeneratorContinueForAwait(_capability);
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
            && (result.Target is null || string.Equals(result.Target, _statement?.LabelSet?.Name, StringComparison.Ordinal)))
        {
            suspendable.Data.Clear(this);
            var breakCompletion = new Completion(CompletionType.Normal, v, _statement!);
            if (iteratorKind == IteratorKind.Async)
            {
                return CloseAsyncIterator(context, iteratorRecord, breakCompletion);
            }

            TryCloseIterator(iteratorRecord, CompletionType.Normal);
            return breakCompletion;
        }

        if (result.Type == CompletionType.Return)
        {
            suspendable.Data.Clear(this);
            if (iteratorKind == IteratorKind.Async)
            {
                return CloseAsyncIterator(context, iteratorRecord, result);
            }

            TryCloseIterator(iteratorRecord, CompletionType.Return);
            return result;
        }

        if (result.IsAbrupt() && result.Type != CompletionType.Continue)
        {
            suspendable.Data.Clear(this);
            if (iteratorKind == IteratorKind.Async)
            {
                return CloseAsyncIterator(context, iteratorRecord, result);
            }

            TryCloseIterator(iteratorRecord, result.Type);
            return result;
        }

        // Normal / Continue → next iteration. Clear dispose-specific state but
        // pass the accumulator forward via a fresh ForOfSuspendData (read by
        // BodyEvaluation's `v` init).
        suspendable.Data.Clear(this);
        var carrier = new ForOfSuspendData { Iterator = iteratorRecord, AccumulatedValue = v };
        return BodyEvaluation(context, _expr, in _body, iteratorRecord, _iterationKind, _lhsKind, carrier, resuming: false, iteratorKind);
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

        // The async instance is its own await continuation (same as a plain await).
        PromiseOperations.PerformPromiseThen(engine, promise, asyncFn);
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
