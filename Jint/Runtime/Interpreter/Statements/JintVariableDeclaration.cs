using Jint.Native;
using Jint.Native.Function;
using Jint.Runtime.Environments;
using Jint.Runtime.Interpreter.Expressions;

namespace Jint.Runtime.Interpreter.Statements;

internal sealed class JintVariableDeclaration : JintStatement<VariableDeclaration>
{
    private readonly ResolvedDeclaration[] _declarations;

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
    }

    protected override Completion ExecuteInternal(EvaluationContext context)
    {
        var engine = context.Engine;
        foreach (var declaration in _declarations)
        {
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
                            laneValue = declaration.Init.GetValue(context).Clone();

                            // Check for generator suspension after evaluating initializer
                            if (context.IsSuspended())
                            {
                                return new Completion(CompletionType.Normal, laneValue, _statement);
                            }
                        }

                        denv.InitializeSlotBinding(lane.Index, laneValue);
                        continue;
                    }
                }

                var lhs = (Reference) declaration.Left.Evaluate(context);
                var value = JsValue.Undefined;
                if (declaration.Init != null)
                {
                    if (declaration.Init is JintClassExpression classExpr && declaration.Init._expression.IsAnonymousFunctionDefinition())
                    {
                        value = classExpr.EvaluateWithName(context, lhs.ReferencedName.ToString()).Clone();
                    }
                    else
                    {
                        value = declaration.Init.GetValue(context).Clone();
                    }

                    // Check for generator suspension after evaluating initializer
                    if (context.IsSuspended())
                    {
                        engine._referencePool.Return(lhs);
                        return new Completion(CompletionType.Normal, value, _statement);
                    }

                    if (declaration.Init._expression.IsFunctionDefinition() && declaration.Init is not JintClassExpression)
                    {
                        ((Function) value).SetFunctionName(lhs.ReferencedName);
                    }
                }

                lhs.InitializeReferencedBinding(value, _statement.Kind.GetDisposeHint());
                engine._referencePool.Return(lhs);
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
                        return new Completion(CompletionType.Normal, value, _statement);
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
                        return new Completion(CompletionType.Normal, JsValue.Undefined, _statement);
                    }
                }
                else if (declaration.LeftIdentifierExpression == null
                         || JintAssignmentExpression.SimpleAssignmentExpression.AssignToIdentifier(
                             context,
                             declaration.LeftIdentifierExpression,
                             declaration.Init,
                             declaration.EvalOrArguments) is null)
                {
                    // slow path
                    var lhs = (Reference) declaration.Left!.Evaluate(context);
                    lhs.AssertValid(engine.Realm);

                    JsValue value;
                    if (declaration.Init is JintClassExpression classExpr && declaration.Init._expression.IsAnonymousFunctionDefinition())
                    {
                        value = classExpr.EvaluateWithName(context, lhs.ReferencedName.ToString()).Clone();
                    }
                    else
                    {
                        value = declaration.Init.GetValue(context).Clone();
                    }

                    // Check for generator suspension after evaluating initializer
                    if (context.IsSuspended())
                    {
                        engine._referencePool.Return(lhs);
                        return new Completion(CompletionType.Normal, value, _statement);
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

        return Completion.Empty();
    }
}
