using Esprima.Ast;

namespace Jint.Runtime.Interpreter.Expressions
{
    internal sealed class JintThisExpression : JintExpression
    {
        public JintThisExpression(ThisExpression expression) : base(expression)
        {
        }

        protected override ExpressionResult EvaluateInternal(EvaluationContext context)
        {
            return NormalCompletion(context.Engine.ResolveThisBinding());
        }

        public override Completion GetValue(EvaluationContext context)
        {
            // need to notify correct node when taking shortcut
            context.LastSyntaxNode = _expression;

            return Completion.Normal(context.Engine.ResolveThisBinding(), _expression.Location);
        }
    }
}