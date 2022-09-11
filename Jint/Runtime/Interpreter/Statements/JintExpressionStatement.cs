using Esprima.Ast;
using Jint.Runtime.Interpreter.Expressions;

namespace Jint.Runtime.Interpreter.Statements;

internal sealed class JintExpressionStatement : JintStatement<ExpressionStatement>
{
    private JintExpression _expression = null!;

    public JintExpressionStatement(ExpressionStatement statement) : base(statement)
    {
    }

    protected override void Initialize(EvaluationContext context)
    {
        _expression = JintExpression.Build(_statement.Expression);
    }

    protected override Completion ExecuteInternal(EvaluationContext context)
    {
        return new Completion(context.Completion, _expression.GetValue(context), _statement);
    }
}
