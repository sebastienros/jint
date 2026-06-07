using Jint.Native;
using Jint.Runtime.Interpreter.Expressions;

namespace Jint.Runtime.Interpreter.Statements;

internal sealed class JintExpressionStatement : JintStatement<ExpressionStatement>
{
    private readonly JintExpression _expression;

    // identifiers are queried the most
    private readonly JintIdentifierExpression? _identifierExpression;

    // only these node types have a non-materializing discard path; gating here keeps
    // every other statement on the exact same call sequence as before
    private readonly bool _expressionCanDiscard;

    public JintExpressionStatement(ExpressionStatement statement) : base(statement)
    {
        _expression = JintExpression.Build(statement.Expression);
        _identifierExpression = _expression as JintIdentifierExpression;
        _expressionCanDiscard = _expression is JintUpdateExpression or JintAssignmentExpression;
    }

    protected override Completion ExecuteInternal(EvaluationContext context)
    {
        // generators/async functions surface suspended values through completions,
        // so elision only applies in plain synchronous frames
        if (!_expressionCanDiscard
            || context.CompletionValuesObservable
            || context.Engine.ExecutionContext.Suspendable is not null)
        {
            var value = _identifierExpression is not null
                ? _identifierExpression.GetValue(context)
                : _expression.GetValue(context);

            return new Completion(context.Completion, value, _statement);
        }

        _expression.EvaluateAndDiscard(context);
        return new Completion(context.Completion, JsValue.Undefined, _statement);
    }
}
