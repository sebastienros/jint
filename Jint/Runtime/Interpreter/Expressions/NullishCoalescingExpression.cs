using System.Runtime.CompilerServices;
using Esprima.Ast;
using Jint.Native;

namespace Jint.Runtime.Interpreter.Expressions
{
    internal sealed class NullishCoalescingExpression : JintExpression
    {
        private readonly JintExpression _left;
        private readonly JintExpression _right;
        private readonly JsValue _constant;

        public NullishCoalescingExpression(Engine engine, BinaryExpression expression) : base(expression)
        {
            _left = Build(engine, expression.Left);

            // we can create a fast path for common literal case like variable ?? 0
            if (expression.Right is Literal l)
            {
                _constant = JintLiteralExpression.ConvertToJsValue(l);
            }
            else
            {
                _right = Build(engine, expression.Right);
            }
        }

        public override Completion GetValue(EvaluationContext context)
        {
            // need to notify correct node when taking shortcut
            context.LastSyntaxNode = _expression;
            return Completion.Normal(EvaluateConstantOrExpression(context), _expression.Location);
        }

        protected override ExpressionResult EvaluateInternal(EvaluationContext context)
        {
            return NormalCompletion(EvaluateConstantOrExpression(context));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private JsValue EvaluateConstantOrExpression(EvaluationContext context)
        {
            var left = _left.GetValue(context).Value;

            return !left.IsNullOrUndefined()
                ? left
                : _constant ?? _right.GetValue(context).Value;
        }
    }
}