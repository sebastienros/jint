using Jint.Runtime.Interpreter.Expressions;

namespace Jint.Runtime.Interpreter.Statements;

internal sealed class JintExpressionStatement : JintStatement<ExpressionStatement>
{
    private readonly JintExpression _expression;

    // identifiers are queried the most
    private readonly JintIdentifierExpression? _identifierExpression;

    public JintExpressionStatement(ExpressionStatement statement) : base(statement)
    {
        _expression = JintExpression.Build(statement.Expression);
        _identifierExpression = _expression as JintIdentifierExpression;
    }

    protected override Completion ExecuteInternal(EvaluationContext context)
    {
        var value = _identifierExpression is not null
            ? _identifierExpression.GetValue(context)
            : _expression.GetValue(context);

        return new Completion(context.Completion, value, _statement);
    }
}
