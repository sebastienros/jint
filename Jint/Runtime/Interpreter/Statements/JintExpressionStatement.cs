using Jint.Runtime.Interpreter.Expressions;

namespace Jint.Runtime.Interpreter.Statements;

internal sealed class JintExpressionStatement : JintStatement<ExpressionStatement>
{
    private JintExpression _expression = null!;

    // identifiers are queried the most
    private JintIdentifierExpression? _identifierExpression;

    public JintExpressionStatement(ExpressionStatement statement) : base(statement)
    {
    }

    protected override void Initialize(EvaluationContext context)
    {
        _expression = JintExpression.Build(_statement.Expression);
        _identifierExpression = _expression as JintIdentifierExpression;
    }

    protected override Completion ExecuteInternal(EvaluationContext context) => ExecuteInternalAsync(context).Preserve().GetAwaiter().GetResult();

    protected override async ValueTask<Completion> ExecuteInternalAsync(EvaluationContext context)
    {
        var value = _identifierExpression is not null
            ? await _identifierExpression.GetValueAsync(context).ConfigureAwait(false)
            : await _expression.GetValueAsync(context).ConfigureAwait(false);

        return new Completion(context.Completion, value, _statement);
    }
}
