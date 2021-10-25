using Esprima.Ast;
using Jint.Runtime.Interpreter.Expressions;
using Jint.Runtime.References;

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
            var result = _expression.Evaluate(context);

            if (result.Type != ExpressionCompletionType.Reference)
            {
                return new Completion(result);
            }

            return new Completion(CompletionType.Normal, context.Engine.GetValue((Reference) result.Value, true), null, Location);
        }
    }
}