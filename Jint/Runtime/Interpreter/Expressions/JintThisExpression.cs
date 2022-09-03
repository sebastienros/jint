using Esprima.Ast;
using Jint.Native;

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
            context.LastSyntaxElement = _expression;

            JsValue value = context.Engine.ResolveThisBinding();
            return new(CompletionType.Normal, value, _expression);
        }
    }
}
