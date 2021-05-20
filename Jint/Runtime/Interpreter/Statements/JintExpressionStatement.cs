using Esprima.Ast;
using Jint.Runtime.Interpreter.Expressions;

namespace Jint.Runtime.Interpreter.Statements
{
    internal sealed class JintExpressionStatement : JintStatement<ExpressionStatement>
    {
        private readonly JintExpression _expression;
        private readonly CompletionType _completionType;

        public JintExpressionStatement(Engine engine, ExpressionStatement statement) : base(engine, statement)
        {
            _expression = JintExpression.Build(engine, statement.Expression);
            _completionType = statement.Expression.Type == Nodes.YieldExpression
                ? CompletionType.Return
                : CompletionType.Normal;
        }

        protected override Completion ExecuteInternal()
        {
            var value = _expression.GetValue();
            return new Completion(_completionType, value, null, Location);
        }
    }
}