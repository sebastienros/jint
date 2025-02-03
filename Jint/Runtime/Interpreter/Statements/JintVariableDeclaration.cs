using Jint.Native;
using Jint.Native.Function;
using Jint.Runtime.Interpreter.Expressions;

namespace Jint.Runtime.Interpreter.Statements;

internal sealed class JintVariableDeclaration : JintStatement<VariableDeclaration>
{
    private ResolvedDeclaration[] _declarations = Array.Empty<ResolvedDeclaration>();

    private sealed class ResolvedDeclaration
    {
        internal JintExpression? Left;
        internal DestructuringPattern? LeftPattern;
        internal JintExpression? Init;
        internal JintIdentifierExpression? LeftIdentifierExpression;
        internal bool EvalOrArguments;
    }

    public JintVariableDeclaration(VariableDeclaration statement) : base(statement)
    {
    }

    protected override void Initialize(EvaluationContext context)
    {
        _declarations = new ResolvedDeclaration[_statement.Declarations.Count];
        for (var i = 0; i < _declarations.Length; i++)
        {
            var declaration = _statement.Declarations[i];

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
            _declarations[i] = new ResolvedDeclaration
            {
                Left = left,
                LeftPattern = pattern,
                LeftIdentifierExpression = leftIdentifier,
                EvalOrArguments = leftIdentifier?.HasEvalOrArguments == true,
                Init = init
            };
        }
    }

    protected override Completion ExecuteInternal(EvaluationContext context) => ExecuteInternalAsync(context).Preserve().GetAwaiter().GetResult();

    protected override async ValueTask<Completion> ExecuteInternalAsync(EvaluationContext context)
    {
        var engine = context.Engine;
        foreach (var declaration in _declarations)
        {
            if (_statement.Kind != VariableDeclarationKind.Var && declaration.Left != null)
            {
                var lhs = (Reference) (await declaration.Left.EvaluateAsync(context).ConfigureAwait(false));
                var value = JsValue.Undefined;
                if (declaration.Init != null)
                {
                    value = (await declaration.Init.GetValueAsync(context).ConfigureAwait(false)).Clone();
                    if (declaration.Init._expression.IsFunctionDefinition())
                    {
                        ((Function) value).SetFunctionName(lhs.ReferencedName);
                    }
                }

                lhs.InitializeReferencedBinding(value);
                engine._referencePool.Return(lhs);
            }
            else if (declaration.Init != null)
            {
                if (declaration.LeftPattern != null)
                {
                    var environment = _statement.Kind != VariableDeclarationKind.Var
                        ? engine.ExecutionContext.LexicalEnvironment
                        : null;

                    var value = await declaration.Init.GetValueAsync(context).ConfigureAwait(false);

                    DestructuringPatternAssignmentExpression.ProcessPatterns(
                        context,
                        declaration.LeftPattern,
                        value,
                        environment,
                        checkPatternPropertyReference: _statement.Kind != VariableDeclarationKind.Var);
                }
                else if (declaration.LeftIdentifierExpression == null
                         || (await JintAssignmentExpression.SimpleAssignmentExpression.AssignToIdentifierAsync(
                             context,
                             declaration.LeftIdentifierExpression,
                             declaration.Init,
                             declaration.EvalOrArguments).ConfigureAwait(false)) is null)
                {
                    // slow path
                    var lhs = (Reference) (await declaration.Left!.EvaluateAsync(context).ConfigureAwait(false));
                    lhs.AssertValid(engine.Realm);

                    var value = (await declaration.Init.GetValueAsync(context).ConfigureAwait(false)).Clone();

                    if (declaration.Init._expression.IsFunctionDefinition())
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
