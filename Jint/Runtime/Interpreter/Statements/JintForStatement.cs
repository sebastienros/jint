using System.Threading;
using Jint.Native;
using Jint.Runtime.Environments;
using Jint.Runtime.Interpreter.Expressions;
using Environment = Jint.Runtime.Environments.Environment;

namespace Jint.Runtime.Interpreter.Statements;

/// <summary>
/// https://tc39.es/ecma262/#sec-forbodyevaluation
/// </summary>
internal sealed class JintForStatement : JintStatement<ForStatement>
{
    private readonly JintVariableDeclaration? _initStatement;
    private readonly JintExpression? _initExpression;

    private readonly JintExpression? _test;
    private readonly JintExpression? _increment;
    private readonly bool _incrementCanDiscard;

    private readonly ProbablyBlockStatement _body;
    private readonly List<Key>? _boundNames;

    // Tight loop: a body of expression statements, var/let/const declarations and if/else chains
    // over such statements cannot break/continue/label/return, so every body completion is
    // structurally Normal and, when the frame additionally cannot suspend, debug or observe
    // completion values, the per-iteration bookkeeping is dead. Bodies with lexical declarations
    // additionally require the flattened pooled environment (their slots live there); without it
    // the generic path creates the per-iteration block environment. The references reuse the
    // body's own handler instances — building duplicates here would allocate a fresh subtree
    // every time this handler is constructed.
    private readonly bool _tightBodyEligible;
    private readonly bool _tightBodyHasLexicalDeclarations;
    private readonly JintStatement? _tightSingleStatement;
    private readonly JintStatementList? _tightBodyList;

    // Body-lexical flattening: when the body block's let/const bindings are slot-eligible
    // (no function/class declarations, no escaping closures — BlockState.SlotNames implies both),
    // contain no using declarations and don't shadow the loop header's names, they fold into the
    // pooled loop environment. The body then runs against that environment directly, eliding the
    // block-env attach/swap/park ceremony per iteration; the body's slot range is re-TDZ'd before
    // each iteration instead — unless the block's conservative scan proved every slot initializes
    // before any reference can evaluate, in which case the previous iteration's leftover values
    // are unobservable and the per-iteration reset is skipped (loop entry still TDZ's all slots).
    private readonly bool _bodyFlattened;
    private readonly bool _flattenedBodySlotsInitBeforeUse;
    private readonly int _flattenedHeaderSlotCount;
    private readonly JintBlockStatement? _flattenedBodyBlock;

    private readonly bool _shouldCreatePerIterationEnvironment;
    private readonly bool _canReuseIterationEnvironment;

    // When the loop env can be reused across iterations AND nothing captures it, it can also be pooled
    // across loop entries: a fixed-slot environment is reset and reused instead of allocated each entry.
    private readonly bool _canPoolLoopEnv;
    private readonly Key[]? _loopSlotNames;
    private readonly Binding[]? _loopSlotTemplates;
    private DeclarativeEnvironment? _cachedLoopEnv;

    public JintForStatement(ForStatement statement) : base(statement)
    {
        _body = new ProbablyBlockStatement(statement.Body);

        if (statement.Init != null)
        {
            if (statement.Init.Type == NodeType.VariableDeclaration)
            {
                var d = (VariableDeclaration) statement.Init;
                if (d.Kind != VariableDeclarationKind.Var)
                {
                    _boundNames = new List<Key>();
                    d.GetBoundNames(_boundNames);
                }
                _initStatement = new JintVariableDeclaration(d);
                _shouldCreatePerIterationEnvironment = d.Kind == VariableDeclarationKind.Let;

                // If no closures in the loop body/test/update capture the iteration environment,
                // we can reuse the same environment each iteration instead of allocating a new one
                if (_shouldCreatePerIterationEnvironment)
                {
                    _canReuseIterationEnvironment = !ForLoopMayCapture(statement);

                    // ...and pool that environment across loop entries via fixed slots (let bindings,
                    // 1-16 names). Re-entries reset and reuse one DeclarativeEnvironment.
                    if (_canReuseIterationEnvironment && _boundNames!.Count is > 0 and <= 16)
                    {
                        var slotNames = new Key[_boundNames.Count];
                        var slotTemplates = new Binding[_boundNames.Count];
                        for (var i = 0; i < _boundNames.Count; i++)
                        {
                            slotNames[i] = _boundNames[i];
                            slotTemplates[i] = new Binding(null!, canBeDeleted: false, mutable: true, strict: false);
                        }

                        _loopSlotNames = slotNames;
                        _loopSlotTemplates = slotTemplates;
                        _canPoolLoopEnv = true;
                    }
                }
            }
            else
            {
                _initExpression = JintExpression.Build((Expression) statement.Init);
            }
        }

        if (statement.Test != null)
        {
            _test = JintExpression.Build(statement.Test);
        }

        if (statement.Update != null)
        {
            _increment = JintExpression.Build(statement.Update);
            // restricted to types whose Evaluate() result is a plain value (never a Reference)
            // so the discard path matches today's semantics exactly
            _incrementCanDiscard = _increment.HasDiscardFastPath;
        }

        if (_canPoolLoopEnv && _body.BlockStatement is { } flattenCandidate)
        {
            var bodyState = flattenCandidate.State;
            if (bodyState.SlotNames is { } bodyNames
                && _boundNames!.Count + bodyNames.Length <= 16
                && !HasUsingDeclarations(bodyState)
                && !NamesOverlap(_loopSlotNames!, bodyNames)
                && !HeaderReferencesAnyBodyName(statement, bodyNames))
            {
                var headerCount = _loopSlotNames!.Length;
                var combinedNames = new Key[headerCount + bodyNames.Length];
                var combinedTemplates = new Binding[combinedNames.Length];
                System.Array.Copy(_loopSlotNames, combinedNames, headerCount);
                System.Array.Copy(_loopSlotTemplates!, combinedTemplates, headerCount);
                System.Array.Copy(bodyNames, 0, combinedNames, headerCount, bodyNames.Length);
                System.Array.Copy(bodyState.SlotTemplates!, 0, combinedTemplates, headerCount, bodyNames.Length);

                _loopSlotNames = combinedNames;
                _loopSlotTemplates = combinedTemplates;
                _flattenedHeaderSlotCount = headerCount;
                _flattenedBodyBlock = flattenCandidate;
                _flattenedBodySlotsInitBeforeUse = bodyState.AllBodySlotsInitBeforeUse;
                _bodyFlattened = true;
            }
        }

        if (_test is not null && IsTightBodyShape(statement.Body))
        {
            _tightBodyEligible = true;
            if (_body.BlockStatement is { } bodyBlock)
            {
                // single-statement blocks live in SingleStatement, larger (and empty) ones in the list
                _tightSingleStatement = bodyBlock.SingleStatement;
                _tightBodyList = _tightSingleStatement is null ? bodyBlock.StatementList : null;
                _tightBodyHasLexicalDeclarations = bodyBlock.State.Declarations.Count > 0;
            }
            else if (_body.Statement is not JintEmptyStatement)
            {
                // an EmptyStatement body leaves both references null (nothing to run per iteration)
                _tightSingleStatement = _body.Statement;
            }
        }
    }

    /// <summary>
    /// A tight-loop body contains only statements whose completions are structurally Normal:
    /// expression statements, empty statements, var/let/const declarations, and if/else chains
    /// over such statements (including declaration-free brace blocks). Control flow that routes
    /// completions (break/continue/return/labels/loops/switch/try), other declarations, and
    /// AnnexB function-declaration branches all disqualify the body. Shared with the while and
    /// do-while tight lanes, which apply the same predicate to their bodies.
    /// </summary>
    internal static bool IsTightBodyShape(Statement body)
    {
        if (body is NestedBlockStatement block)
        {
            foreach (var statement in block.Body)
            {
                if (!IsTightStatement(statement))
                {
                    return false;
                }
            }

            return true;
        }

        return IsTightStatement(body);
    }

    private static bool IsTightStatement(Statement statement)
    {
        switch (statement.Type)
        {
            case NodeType.ExpressionStatement:
            case NodeType.EmptyStatement:
                return true;

            case NodeType.VariableDeclaration:
                // using/await using declarations register dispose resources — not tight
                return ((VariableDeclaration) statement).Kind
                    is VariableDeclarationKind.Var
                    or VariableDeclarationKind.Let
                    or VariableDeclarationKind.Const;

            case NodeType.IfStatement:
                var ifStatement = (IfStatement) statement;
                return IsTightBranch(ifStatement.Consequent)
                    && (ifStatement.Alternate is null || IsTightBranch(ifStatement.Alternate));

            default:
                return false;
        }
    }

    private static bool IsTightBranch(Statement branch)
    {
        // sloppy-mode `if (c) function f() {}` takes the AnnexB var-scope copy path at runtime
        if (branch.Type == NodeType.FunctionDeclaration)
        {
            return false;
        }

        if (branch is NestedBlockStatement block)
        {
            foreach (var statement in block.Body)
            {
                // a lexical declaration would require the nested block's own environment
                if (statement.Type is NodeType.ClassDeclaration or NodeType.FunctionDeclaration
                    || statement is VariableDeclaration { Kind: not VariableDeclarationKind.Var })
                {
                    return false;
                }

                if (!IsTightStatement(statement))
                {
                    return false;
                }
            }

            return true;
        }

        return IsTightStatement(branch);
    }

    protected override Completion ExecuteInternal(EvaluationContext context)
    {
        Environment? oldEnv = null;
        DeclarativeEnvironment? loopEnv = null;
        var engine = context.Engine;

        // Check if we're resuming from a yield/await inside this for statement
        // If resuming from body, test, or update, skip initialization to avoid resetting loop variables
        // If resuming from init expression, we must re-execute init to complete pending nested awaits
        var suspendable = engine.ExecutionContext.Suspendable;

        var resumeNode = GetSuspensionNode(suspendable);

        // Only skip init when resuming from body/test/update, NOT from init
        var resumingInLoop = resumeNode is not null && IsNodeInsideForStatementExcludingInit(resumeNode);
        var resumingInBody = resumeNode is not null && IsNodeInsideRange(resumeNode, _statement.Body.Range);
        var resumingInUpdate = resumeNode is not null && _statement.Update is not null && IsNodeInsideRange(resumeNode, _statement.Update.Range);

        ForLoopSuspendData? suspendData = null;
        if (resumingInLoop)
        {
            suspendable?.Data.TryGet(this, out suspendData);
        }

        if (_boundNames != null)
        {
            oldEnv = engine.ExecutionContext.LexicalEnvironment;

            if (_canPoolLoopEnv && suspendable is null)
            {
                // Pooled fixed-slot environment, reset and reused across loop entries. ResetSlots leaves
                // every binding uninitialized (TDZ re-established); the for-init then initializes them.
                // Gated on a non-suspendable context so a pooled env never has to round-trip through the
                // async/generator suspend/resume save-and-restore machinery.
                var cachedEnv = Interlocked.Exchange(ref _cachedLoopEnv, null);
                if (cachedEnv is not null && ReferenceEquals(cachedEnv._engine, engine))
                {
                    cachedEnv._outerEnv = oldEnv;
                    ResetSlots(cachedEnv._slots!, _loopSlotTemplates!);
                    loopEnv = cachedEnv;
                }
                else
                {
                    loopEnv = JintEnvironment.NewDeclarativeEnvironment(engine, oldEnv);
                    loopEnv._slotNames = _loopSlotNames;
                    loopEnv._slots = (Binding[]) _loopSlotTemplates!.Clone();
                }

                engine.UpdateLexicalEnvironment(loopEnv);
            }
            else
            {
                loopEnv = JintEnvironment.NewDeclarativeEnvironment(engine, oldEnv);
                var loopEnvRec = loopEnv;
                var kind = _initStatement!._statement.Kind;
                for (var i = 0; i < _boundNames.Count; i++)
                {
                    var name = _boundNames[i];
                    // const, using, and await using all create immutable bindings
                    if (kind is VariableDeclarationKind.Const or VariableDeclarationKind.Using or VariableDeclarationKind.AwaitUsing)
                    {
                        loopEnvRec.CreateImmutableBinding(name);
                    }
                    else
                    {
                        loopEnvRec.CreateMutableBinding(name);
                    }
                }

                engine.UpdateLexicalEnvironment(loopEnv);

                // Restore loop variable values if resuming
                if (resumingInLoop && suspendData?.BoundValues is not null)
                {
                    foreach (var kvp in suspendData.BoundValues)
                    {
                        loopEnvRec.InitializeBinding(kvp.Key, kvp.Value, DisposeHint.Normal);
                    }
                }
            }
        }

        var completion = Completion.Empty();
        try
        {
            // Skip initialization if resuming from inside the loop (body, test, or update)
            if (!resumingInLoop)
            {
                if (_initExpression != null)
                {
                    _initExpression?.GetValue(context);

                    // Check for async suspension in init expression
                    if (context.IsSuspended())
                    {
                        return new Completion(CompletionType.Return, JsValue.Undefined, _statement);
                    }
                }
                else
                {
                    _initStatement?.Execute(context);

                    // Check for async suspension in init statement
                    if (context.IsSuspended())
                    {
                        return new Completion(CompletionType.Return, JsValue.Undefined, _statement);
                    }
                }
            }

            // body flattening engages only when the pooled combined-slot environment is live
            var flattenActive = _bodyFlattened && loopEnv is not null && _canPoolLoopEnv && suspendable is null;
            completion = ForBodyEvaluation(context, suspendData?.AccumulatedValue ?? JsValue.Undefined, skipTestOnce: resumingInBody, resumeUpdateOnce: resumingInUpdate, flattenActive);
            return completion;
        }
        finally
        {
            if (oldEnv is not null)
            {
                // Save loop variable values if generator/async function is suspended (don't save on normal completion)
                if (context.IsSuspended() && _boundNames != null && suspendable is not null)
                {
                    // Use the CURRENT lexical environment, not loopEnv, because
                    // CreatePerIterationEnvironment may have created new environments during the loop
                    var currentEnv = engine.ExecutionContext.LexicalEnvironment;

                    var data = suspendable.Data.GetOrCreate<ForLoopSuspendData>(this);
                    data.BoundValues ??= new Dictionary<Key, JsValue>();
                    for (var i = 0; i < _boundNames.Count; i++)
                    {
                        var name = _boundNames[i];
                        var value = currentEnv.GetBindingValue(name, strict: false);
                        data.BoundValues[name] = value;
                    }
                }
                else if (!context.IsSuspended())
                {
                    // Clear suspend data on normal completion
                    suspendable?.Data.Clear(this);
                }

                loopEnv!.DisposeResources(completion);

                // Cache the pooled environment for the next loop entry (only on clean, non-suspended
                // completion; a suspended loop may still reference it).
                if (_canPoolLoopEnv && !context.IsSuspended() && loopEnv._slots is not null)
                {
                    Interlocked.Exchange(ref _cachedLoopEnv, loopEnv);
                }

                engine.UpdateLexicalEnvironment(oldEnv);
            }
        }
    }

    /// <summary>
    /// Reset the slots of a reused loop environment to the pre-computed templates (every binding
    /// uninitialized). Hand-rolled small-array fast path: loop headers hold 1-3 bindings.
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

    /// <summary>
    /// Re-establishes the TDZ of the flattened body's slot range before an iteration; the header
    /// range is left untouched (a reused iteration environment keeps its header bindings).
    /// Skipped entirely when <see cref="JintBlockStatement.BlockState.AllBodySlotsInitBeforeUse"/>
    /// holds for the body: no reference can evaluate before its slot's declaration re-initializes
    /// it, so the previous iteration's values are dead and loop entry's full reset covers the
    /// first iteration.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private static void ResetBodySlotRange(Binding[] slots, Binding[] templates, int headerCount)
    {
        for (var i = headerCount; i < templates.Length; i++)
        {
            slots[i] = templates[i];
        }
    }

    private static bool HasUsingDeclarations(JintBlockStatement.BlockState state)
    {
        foreach (var declaration in state.Declarations)
        {
            if (declaration.Declaration is VariableDeclaration { Kind: VariableDeclarationKind.Using or VariableDeclarationKind.AwaitUsing })
            {
                return true;
            }
        }

        return false;
    }

    private static bool NamesOverlap(Key[] headerNames, Key[] bodyNames)
    {
        foreach (var bodyName in bodyNames)
        {
            foreach (var headerName in headerNames)
            {
                if (bodyName == headerName)
                {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>
    /// Whether the loop header (init, test, or update) contains an identifier matching a body-block
    /// lexical name. Flattening folds the body's let/const slots into the loop environment, so such a
    /// name resolves there — but the header scope excludes the body block's declarations, so the
    /// reference targets an <b>outer</b> binding the body shadows. Folding it in resolves the header
    /// reference against the still-uninitialized body slot, a spurious TDZ ("x has not been
    /// initialized"). Over-approximates in the safe direction (a matching property name or a
    /// nested-scope declaration only forgoes the optimization), mirroring
    /// <c>EnvironmentEscapeAstVisitor.ClosureReferencesAny</c>.
    /// </summary>
    private static bool HeaderReferencesAnyBodyName(ForStatement statement, Key[] bodyNames)
    {
        return (statement.Init is not null && ReferencesAnyName(statement.Init, bodyNames))
            || (statement.Test is not null && ReferencesAnyName(statement.Test, bodyNames))
            || (statement.Update is not null && ReferencesAnyName(statement.Update, bodyNames));
    }

    private static bool ReferencesAnyName(Node node, Key[] names)
    {
        if (node.Type == NodeType.Identifier)
        {
            var name = ((Identifier) node).Name;
            foreach (var candidate in names)
            {
                if (string.Equals(candidate.Name, name, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        foreach (var childNode in node.ChildNodes)
        {
            if (ReferencesAnyName(childNode, names))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Checks if the given node is inside this for statement's body, test, or update (but NOT init).
    /// Used to determine if we're resuming from a yield/await inside the loop.
    /// When resuming from init, we must re-execute init to complete nested awaits.
    /// When resuming from body/test/update, we skip init to avoid resetting variables.
    /// </summary>
    private bool IsNodeInsideForStatementExcludingInit(Node node)
    {
        var nodeRange = node.Range;

        // Check if inside body
        var bodyRange = _statement.Body.Range;
        if (bodyRange.Start <= nodeRange.Start && nodeRange.End <= bodyRange.End)
        {
            return true;
        }

        // Check if inside test expression
        if (_statement.Test != null)
        {
            var testRange = _statement.Test.Range;
            if (testRange.Start <= nodeRange.Start && nodeRange.End <= testRange.End)
            {
                return true;
            }
        }

        // Check if inside update expression
        if (_statement.Update != null)
        {
            var updateRange = _statement.Update.Range;
            if (updateRange.Start <= nodeRange.Start && nodeRange.End <= updateRange.End)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-forbodyevaluation
    /// </summary>
    private Completion ForBodyEvaluation(EvaluationContext context, JsValue initialValue, bool skipTestOnce, bool resumeUpdateOnce, bool flattenActive = false)
    {
        var v = initialValue;

        // Tight loop: with an expression-statements-only body, a non-suspendable frame, no
        // per-statement (exact) constraint/debug checks, dead completion values and no fresh
        // per-iteration environment, nothing per iteration remains observable but test, body
        // expressions and update. Amortized constraints (timeout, cancellation, memory limit)
        // do not disarm the lane: TightForBody drives the shared amortized countdown once per
        // iteration, keeping their detection latency bounded.
        // The DebugMode probe is live (same coarseness as the debugHandler hoisting below).
        if (_tightBodyEligible
            && !skipTestOnce
            && !resumeUpdateOnce
            && !context.CompletionValuesObservable
            && !context.ShouldRunPerStatementChecks
            && !context.DebugMode
            && (!_shouldCreatePerIterationEnvironment || _canReuseIterationEnvironment)
            && (!_tightBodyHasLexicalDeclarations || flattenActive)
            && context.Engine.ExecutionContext.Suspendable is null)
        {
            return TightForBody(context, flattenActive);
        }

        if (!resumeUpdateOnce && _shouldCreatePerIterationEnvironment && !_canReuseIterationEnvironment)
        {
            CreatePerIterationEnvironment(context);
        }

        var debugHandler = context.DebugMode ? context.Engine.Debugger : null;

        while (true)
        {
            context.Engine.ExecutionContext.ClearCompletedAwaitsIfNotResuming();

            if (!resumeUpdateOnce && !skipTestOnce && _test != null)
            {
                debugHandler?.OnStep(_test._expression);

                if (!_test.GetBooleanValue(context))
                {
                    // Check for async suspension in test expression
                    if (context.IsSuspended())
                    {
                        SaveAccumulatedValue(context, v);
                        return new Completion(CompletionType.Return, JsValue.Undefined, ((JintStatement) this)._statement);
                    }

                    context.Engine.ExecutionContext.Suspendable?.Data.Clear(this);
                    return new Completion(CompletionType.Normal, v, ((JintStatement) this)._statement);
                }
            }

            skipTestOnce = false;

            var suspendable = context.Engine.ExecutionContext.Suspendable;
            if (!resumeUpdateOnce)
            {
                Completion result;
                if (flattenActive && !context.DebugMode)
                {
                    // the pooled loop environment carries the body's slots: re-establish their TDZ
                    // and run the block contents in place. Under a debugger the normal block path
                    // runs instead — its fresh env shadows the (uninitialized) flattened slots.
                    // When the block scan proved every slot initializes before any use, the
                    // re-TDZ is unobservable (loop entry already reset the whole slot array for
                    // the first iteration) and is skipped.
                    if (!_flattenedBodySlotsInitBeforeUse)
                    {
                        var env = (DeclarativeEnvironment) context.Engine.ExecutionContext.LexicalEnvironment;
                        ResetBodySlotRange(env._slots!, _loopSlotTemplates!, _flattenedHeaderSlotCount);
                    }
                    result = _flattenedBodyBlock!.ExecuteFlattenedContents(context);
                }
                else
                {
                    result = _body.Execute(context);
                }
                if (!result.Value.IsEmpty)
                {
                    v = result.Value;
                }

                // Check for suspension - if suspended, we need to exit the loop
                if (context.IsSuspended())
                {
                    SaveAccumulatedValue(context, v);
                    var suspendedValue = suspendable?.SuspendedValue ?? result.Value;
                    return new Completion(CompletionType.Return, suspendedValue, ((JintStatement) this)._statement);
                }

                if (result.Type == CompletionType.Break && (context.Target == null || string.Equals(context.Target, _statement?.LabelSet?.Name, StringComparison.Ordinal)))
                {
                    suspendable?.Data.Clear(this);
                    return new Completion(CompletionType.Normal, result.Value, ((JintStatement) this)._statement);
                }

                if (result.Type != CompletionType.Continue || (context.Target != null && !string.Equals(context.Target, _statement?.LabelSet?.Name, StringComparison.Ordinal)))
                {
                    if (result.Type != CompletionType.Normal)
                    {
                        return result;
                    }
                }

                if (_shouldCreatePerIterationEnvironment && !_canReuseIterationEnvironment)
                {
                    CreatePerIterationEnvironment(context);
                }
            }
            resumeUpdateOnce = false;

            if (_increment != null)
            {
                debugHandler?.OnStep(_increment._expression);
                if (_incrementCanDiscard)
                {
                    // update/compound assignment results are unobservable here and these
                    // node types have non-materializing fast paths
                    _increment.EvaluateAndDiscard(context);
                }
                else
                {
                    _increment.Evaluate(context);
                }

                // Check for suspension in update expression (e.g., yield in the update)
                if (context.IsSuspended())
                {
                    SaveAccumulatedValue(context, v);
                    var suspendedValue = suspendable?.SuspendedValue ?? JsValue.Undefined;
                    return new Completion(CompletionType.Return, suspendedValue, ((JintStatement) this)._statement);
                }

                // Check for return request (e.g., generator.return() was called)
                if (suspendable?.ReturnRequested == true)
                {
                    var returnValue = suspendable.SuspendedValue ?? JsValue.Undefined;
                    return new Completion(CompletionType.Return, returnValue, ((JintStatement) this)._statement);
                }
            }
        }
    }

    /// <summary>
    /// The bare test → body-statements → update cycle. Exceptions propagate to the enclosing
    /// statement list exactly as on the generic path (ForBodyEvaluation catches nothing); the
    /// loop's Normal completion value is dead by the caller's gate, so Undefined stands in.
    /// Deferred errors surface as Engine._error instead of a .NET throw; the statement list
    /// converts them per statement, and so must the tight loop. Under flattening the pooled
    /// loop environment carries the body's slots: their TDZ is re-established per iteration
    /// before the body statements run, mirroring the generic flattened arm — skipped when the
    /// block scan proved every slot initializes before any use (leftovers are unobservable).
    /// Amortized constraints stay live through the context's shared countdown, driven once per
    /// iteration (a tripped constraint throws and propagates like any other exception); exact
    /// constraints and debug mode never reach this lane by the caller's gate.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private Completion TightForBody(EvaluationContext context, bool flattenActive)
    {
        var test = _test!;
        var increment = _increment;
        var single = _tightSingleStatement;
        var list = _tightBodyList;
        var engine = context.Engine;
        var resetBodySlotsPerIteration = flattenActive && !_flattenedBodySlotsInitBeforeUse;

        while (test.GetBooleanValue(context))
        {
            context.RunAmortizedConstraintChecks();

            if (resetBodySlotsPerIteration)
            {
                var env = (DeclarativeEnvironment) engine.ExecutionContext.LexicalEnvironment;
                ResetBodySlotRange(env._slots!, _loopSlotTemplates!, _flattenedHeaderSlotCount);
            }

            if (single is not null)
            {
                single.ExecuteDiscarded(context);
                if (engine._error is not null)
                {
                    return JintStatementList.HandleError(engine, single);
                }
            }
            else if (list is not null)
            {
                for (var i = 0; i < list.Count; i++)
                {
                    var statement = list.GetStatement(i);
                    statement.ExecuteDiscarded(context);
                    if (engine._error is not null)
                    {
                        return JintStatementList.HandleError(engine, statement);
                    }
                }
            }

            if (increment is not null)
            {
                if (_incrementCanDiscard)
                {
                    increment.EvaluateAndDiscard(context);
                }
                else
                {
                    increment.Evaluate(context);
                }
            }
        }

        return new Completion(CompletionType.Normal, JsValue.Undefined, ((JintStatement) this)._statement);
    }

    private void SaveAccumulatedValue(EvaluationContext context, JsValue value)
    {
        var suspendable = context.Engine.ExecutionContext.Suspendable;
        if (suspendable is not null)
        {
            suspendable.Data.GetOrCreate<ForLoopSuspendData>(this).AccumulatedValue = value;
        }
    }

    /// <summary>
    /// Checks if any part of the for-loop (init, test, update, body) contains closures
    /// that could capture the per-iteration environment.
    /// </summary>
    private static bool ForLoopMayCapture(ForStatement statement)
    {
        // Check init declarators (e.g., for (let i = 0, f = function() { return i }; ...))
        if (statement.Init is VariableDeclaration vd)
        {
            foreach (var decl in vd.Declarations)
            {
                if (decl.Init is not null)
                {
                    if (JintFunctionDefinition.EnvironmentEscapeAstVisitor.IsCapturing(decl.Init)
                        || JintFunctionDefinition.EnvironmentEscapeAstVisitor.MayEscape(decl.Init))
                    {
                        return true;
                    }
                }
            }
        }

        if (statement.Test is not null
            && (JintFunctionDefinition.EnvironmentEscapeAstVisitor.IsCapturing(statement.Test)
                || JintFunctionDefinition.EnvironmentEscapeAstVisitor.MayEscape(statement.Test)))
        {
            return true;
        }

        if (statement.Update is not null
            && (JintFunctionDefinition.EnvironmentEscapeAstVisitor.IsCapturing(statement.Update)
                || JintFunctionDefinition.EnvironmentEscapeAstVisitor.MayEscape(statement.Update)))
        {
            return true;
        }

        return JintFunctionDefinition.EnvironmentEscapeAstVisitor.IsCapturing(statement.Body)
            || JintFunctionDefinition.EnvironmentEscapeAstVisitor.MayEscape(statement.Body);
    }

    private void CreatePerIterationEnvironment(EvaluationContext context)
    {
        var engine = context.Engine;
        var lastIterationEnv = (DeclarativeEnvironment) engine.ExecutionContext.LexicalEnvironment;
        var thisIterationEnv = JintEnvironment.NewDeclarativeEnvironment(engine, lastIterationEnv._outerEnv);

        lastIterationEnv.TransferTo(_boundNames!, thisIterationEnv);

        engine.UpdateLexicalEnvironment(thisIterationEnv);
    }
}
