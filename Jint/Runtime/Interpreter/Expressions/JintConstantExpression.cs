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

        /// <summary>
        /// Resolves the underlying value for this expression.
        /// By default uses the Engine for resolving.
        /// </summary>
        /// <param name="context"></param>
        /// <seealso cref="JintLiteralExpression"/>
        public override JsValue GetValue(EvaluationContext context)
        {
            // need to notify correct node when taking shortcut
            context.LastSyntaxNode = _expression;

            return _value;
        }

        protected override object EvaluateInternal(EvaluationContext context) => _value;
    }
}