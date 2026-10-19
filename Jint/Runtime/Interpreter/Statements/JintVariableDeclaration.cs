using Jint.Native;
using Jint.Native.Function;
using Jint.Runtime.Environments;
using Jint.Runtime.Interpreter.Expressions;

namespace Jint.Runtime.Interpreter.Statements;

internal sealed class JintVariableDeclaration : JintStatement<VariableDeclaration>
{
    private readonly ResolvedDeclaration[] _declarations;

    /// <summary>
    /// Whether a suspension can leave the list part-way through: more than one declarator, and one of
    /// them contains an <c>await</c> or a <c>yield</c> of the enclosing frame, in its initializer or in
    /// a default inside its binding pattern. Only such a list parks the declarator it suspended at. Every
    /// other list runs the walk with no resume bookkeeping at all; a single declarator that suspends
    /// restarts at index 0, which is the declarator it suspended in.
    /// </summary>
    private readonly bool _tracksDeclaratorIndex;

    private sealed class ResolvedDeclaration
    {
        internal JintExpression? Left;
        internal DestructuringPattern? LeftPattern;
        internal JintExpression? Init;
        internal JintIdentifierExpression? LeftIdentifierExpression;
        internal bool EvalOrArguments;
        internal bool CanUseSlotLane;
        internal SlotLane? Lane;
    }

    /// <summary>
    /// Monomorphic cache mapping a slotted environment's layout (by <see cref="Key"/>-array
    /// identity, stable across engines for shared prepared scripts) to the declared name's slot
    /// index, so repeated lexical initialization skips the Reference rent/return and name scan.
    /// A single reference field keeps publication atomic on shared handler trees; Index -1
    /// records "no slot in this layout" so misses don't rescan.
    /// </summary>
    private sealed class SlotLane
    {
        internal readonly Key[] SlotNames;
        internal readonly int Index;

        internal SlotLane(Key[] slotNames, int index)
        {
            SlotNames = slotNames;
            Index = index;
        }
    }

    public JintVariableDeclaration(VariableDeclaration statement) : base(statement)
    {
        _declarations = new ResolvedDeclaration[statement.Declarations.Count];
        for (var i = 0; i < _declarations.Length; i++)
        {
            var declaration = statement.Declarations[i];

            JintExpression? left = null;
            JintExpression? init = null;
            DestructuringPattern? pattern = null;

            if (declaration.Id is DestructuringPattern bp)
            {
                pattern = bp;
            }
            else
            {
                left = JintExpression.Build((Identifier) declaration.Id);
            }

            if (declaration.Init != null)
            {
                init = JintExpression.Build(declaration.Init);
            }

            var leftIdentifier = left as JintIdentifierExpression;

            // let/const identifier targets whose initializer needs no naming side channel
            // (function definitions and class expressions call SetFunctionName/EvaluateWithName
            // with the reference's name) can initialize straight into a resolved slot.
            var canUseSlotLane = statement.Kind is VariableDeclarationKind.Let or VariableDeclarationKind.Const
                && leftIdentifier is not null
                && leftIdentifier.HasEvalOrArguments == false
                && (declaration.Init is null
                    || (declaration.Init is not ClassExpression && !declaration.Init.IsFunctionDefinition()));

            _declarations[i] = new ResolvedDeclaration
            {
                Left = left,
                LeftPattern = pattern,
                LeftIdentifierExpression = leftIdentifier,
                EvalOrArguments = leftIdentifier?.HasEvalOrArguments == true,
                Init = init,
                CanUseSlotLane = canUseSlotLane,
            };
        }

        if (_declarations.Length > 1)
        {
            foreach (var declaration in statement.Declarations)
            {
                if (MaySuspendVisitor.MaySuspend(declaration))
                {
                    _tracksDeclaratorIndex = true;
                    break;
                }
            }
        }
    }

    /// <summary>
    /// Tight-loop entry; see <see cref="JintStatement.ExecuteDiscarded"/>. Declarations complete
    /// with an Empty Normal value on every path, so only the Execute wrapper ceremony is skipped;
    /// the current node still needs tracking for error locations. Skipping PrepareFor is safe
    /// because PrepareFor no longer does anything else: it only tracks the node, which this lane
    /// does itself below.
    /// </summary>
    internal override void ExecuteDiscarded(EvaluationContext context)
    {
        context.ChargeStatement();
        context.LastSyntaxElement = _statement;
        ExecuteInternal(context);
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-let-and-const-declarations-runtime-semantics-evaluation and
    /// https://tc39.es/ecma262/#sec-variable-statement-runtime-semantics-evaluation: each declarator of the
    /// list is evaluated once, left to right.
    /// </summary>
    /// <remarks>
    /// A suspension inside a declarator's initializer makes the statement list replay this statement on
    /// resume, and a replay that restarted at the first declarator re-ran every declarator before the
    /// suspended one: the completed-await memo hands back the awaits, so what repeated was exactly the
    /// declarators that do not await — their side effects, and a let/const initialized again with the second
    /// value. The index the walk suspended at is parked on the suspendable, keyed by this handler
    /// (handler trees are shared, so it cannot be a field here), read back only while resuming, and cleared
    /// when a resumed walk completes; <see cref="Expressions.JintSequenceExpression"/> keeps its operand
    /// index the same way. Only a list that can suspend part-way through pays for any of it
    /// (<see cref="_tracksDeclaratorIndex"/>): this handler runs for every declaration statement, so the
    /// others walk with a null suspendable and no bookkeeping.
    /// </remarks>
    protected override Completion ExecuteInternal(EvaluationContext context)
    {
        var engine = context.Engine;
        var declarations = _declarations;

        ISuspendable? suspendable = null;
        var resumed = false;
        var i = 0;
        if (_tracksDeclaratorIndex)
        {
            suspendable = engine.ExecutionContext.Suspendable;
            if (suspendable is { IsResuming: true }
                && suspendable.Data.TryGet(this, out VariableDeclarationSuspendData? resumeData))
            {
                i = resumeData!.DeclarationIndex;
                resumed = true;
            }
        }

        for (; (uint) i < (uint) declarations.Length; i++)
        {
            var declaration = declarations[i];
            if (_statement.Kind != VariableDeclarationKind.Var && declaration.Left != null)
            {
                // Slot lane: the declaration's own binding lives in the current environment by
                // construction (blocks/loops instantiate their env before executing statements),
                // so when that env stores bindings in slots we can initialize the slot directly
                // and skip the Reference rent/evaluate/return round-trip and the name scan.
                if (declaration.CanUseSlotLane
                    && engine.ExecutionContext.LexicalEnvironment is DeclarativeEnvironment denv
                    && denv._slotNames is not null)
                {
                    var lane = declaration.Lane;
                    if (lane is null || !ReferenceEquals(lane.SlotNames, denv._slotNames))
                    {
                        declaration.Lane = lane = new SlotLane(
                            denv._slotNames,
                            denv.FindSlotIndex(declaration.LeftIdentifierExpression!.Identifier.Key));
                    }

                    if (lane.Index >= 0)
                    {
                        JsValue laneValue;
                        if (declaration.Init is null)
                        {
                            laneValue = JsValue.Undefined;
                        }
                        else
                        {
                            laneValue = declaration.Init.GetValue(context);

                            // Check for generator suspension after evaluating initializer
                            if (context.IsSuspended())
                            {
                                return Suspended(suspendable, i, laneValue);
                            }
                        }

                        denv.InitializeSlotBinding(lane.Index, laneValue);
                        continue;
                    }
                }

                // Slow arm kept out-of-line so this method stays compact for the var path.
                var suspendedValue = InitializeLexicalBindingSlow(context, engine, declaration);
                if (suspendedValue is not null)
                {
                    return Suspended(suspendable, i, suspendedValue);
                }
            }
            else if (declaration.Init != null)
            {
                if (declaration.LeftPattern != null)
                {
                    var environment = _statement.Kind != VariableDeclarationKind.Var
                        ? engine.ExecutionContext.LexicalEnvironment
                        : null;

                    var value = declaration.Init.GetValue(context);

                    // Check for generator suspension after evaluating initializer
                    if (context.IsSuspended())
                    {
                        return Suspended(suspendable, i, value);
                    }

                    DestructuringPatternAssignmentExpression.ProcessPatterns(
                        context,
                        declaration.LeftPattern,
                        value,
                        environment,
                        checkPatternPropertyReference: _statement.Kind != VariableDeclarationKind.Var);

                    // Check for async/generator suspension after processing patterns
                    if (context.IsSuspended())
                    {
                        return Suspended(suspendable, i, JsValue.Undefined);
                    }
                }
                else
                {
                    if (declaration.LeftIdentifierExpression is not null)
                    {
                        var assigned = JintAssignmentExpression.SimpleAssignmentExpression.AssignToIdentifier(
                            context,
                            declaration.LeftIdentifierExpression,
                            declaration.Init,
                            declaration.EvalOrArguments);
                        if (assigned is not null)
                        {
                            // The helper hands the suspension placeholder back without assigning it. A
                            // list that can suspend part-way through stops here too, or the declarators
                            // after this one run while the frame is already suspended; any other list
                            // has nothing after this declarator that it could run too early.
                            if (suspendable is not null && context.IsSuspended())
                            {
                                return Suspended(suspendable, i, (JsValue) assigned);
                            }

                            continue;
                        }
                    }

                    // slow path
                    var lhs = (Reference) declaration.Left!.Evaluate(context);
                    lhs.AssertValid(engine.Realm);

                    JsValue value;
                    if (declaration.Init is JintClassExpression classExpr && declaration.Init._expression.IsAnonymousFunctionDefinition())
                    {
                        value = classExpr.EvaluateWithName(context, lhs.ReferencedName.ToString());
                    }
                    else
                    {
                        value = declaration.Init.GetValue(context);
                    }

                    // Check for generator suspension after evaluating initializer
                    if (context.IsSuspended())
                    {
                        engine._referencePool.Return(lhs);
                        return Suspended(suspendable, i, value);
                    }

                    if (declaration.Init._expression.IsFunctionDefinition() && declaration.Init is not JintClassExpression)
                    {
                        ((Function) value).SetFunctionName(lhs.ReferencedName);
                    }

                    engine.PutValue(lhs, value);
                    engine._referencePool.Return(lhs);
                }
            }
        }

        if (resumed)
        {
            suspendable!.Data.Clear(this);
        }

        return Completion.Empty();
    }

    /// <summary>
    /// Parks the declarator the walk suspended at, for a tracked list, and completes with the suspension
    /// placeholder.
    /// </summary>
    private Completion Suspended(ISuspendable? suspendable, int index, JsValue value)
    {
        if (suspendable is not null)
        {
            suspendable.Data.GetOrCreate<VariableDeclarationSuspendData>(this).DeclarationIndex = index;
        }

        return new Completion(CompletionType.Normal, value, _statement);
    }

    /// <summary>
    /// Initializes a lexical (let/const/using) identifier binding through the Reference-based
    /// path (targets the slot lane can't serve). Returns the placeholder only when the initializer
    /// suspended (generator/async); null means the binding was initialized and the caller
    /// continues with the next declarator.
    /// </summary>
    private JsValue? InitializeLexicalBindingSlow(EvaluationContext context, Engine engine, ResolvedDeclaration declaration)
    {
        var lhs = (Reference) declaration.Left!.Evaluate(context);
        var value = JsValue.Undefined;
        if (declaration.Init != null)
        {
            if (declaration.Init is JintClassExpression classExpr && declaration.Init._expression.IsAnonymousFunctionDefinition())
            {
                value = classExpr.EvaluateWithName(context, lhs.ReferencedName.ToString());
            }
            else
            {
                value = declaration.Init.GetValue(context);
            }

            // Check for generator suspension after evaluating initializer
            if (context.IsSuspended())
            {
                engine._referencePool.Return(lhs);
                return value;
            }

            if (declaration.Init._expression.IsFunctionDefinition() && declaration.Init is not JintClassExpression)
            {
                ((Function) value).SetFunctionName(lhs.ReferencedName);
            }
        }

        lhs.InitializeReferencedBinding(value, _statement.Kind.GetDisposeHint());
        engine._referencePool.Return(lhs);
        return null;
    }
}
