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

        public NullishCoalescingExpression(Engine engine, BinaryExpression expression) : base(engine, expression)
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

        public override JsValue GetValue()
        {
            // need to notify correct node when taking shortcut
            _engine._lastSyntaxNode = _expression;
            return EvaluateConstantOrExpression();
        }

        protected override object EvaluateInternal()
        {
            return EvaluateConstantOrExpression();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private JsValue EvaluateConstantOrExpression()
        {
            var left = _left.GetValue();

            return !left.IsNullOrUndefined()
                ? left
                : _constant ?? _right.GetValue();
        }
    }
}