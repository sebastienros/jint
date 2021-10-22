using Esprima.Ast;
using Jint.Native;

namespace Jint.Runtime.Interpreter.Expressions
{
    /// <summary>
    /// Constant JsValue returning expression.
    /// </summary>
    internal sealed class JintConstantExpression : JintExpression
    {
        private readonly JsValue _value;

        public JintConstantExpression(Expression expression, JsValue value) : base(expression)
        {
            _value = value;
        }

        public override Completion GetValue(EvaluationContext context)
        {
            // need to notify correct node when taking shortcut
            context.LastSyntaxNode = _expression;

            return Completion.Normal(_value, _expression.Location);
        }

        protected override ExpressionResult EvaluateInternal(EvaluationContext context) => NormalCompletion(_value);
    }
}