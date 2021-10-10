using Esprima.Ast;
using Jint.Runtime.Interpreter.Expressions;

namespace Jint.Runtime.Interpreter.Statements
{
    internal sealed class JintExpressionStatement : JintStatement<ExpressionStatement>
    {
        private JintExpression _expression;

        public JintExpressionStatement(ExpressionStatement statement) : base(statement)
        {
        }

        protected override void Initialize(EvaluationContext context)
        {
            _expression = JintExpression.Build(context.Engine, _statement.Expression);
        }

        protected override Completion ExecuteInternal(EvaluationContext context)
        {
            var value = _expression.GetValue(context);
            return new Completion(CompletionType.Normal, value, Location);
        }
    }
}