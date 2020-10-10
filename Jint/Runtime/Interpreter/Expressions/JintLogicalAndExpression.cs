using Esprima.Ast;
using Jint.Native;

namespace Jint.Runtime.Interpreter.Expressions
{
    internal sealed class JintLogicalAndExpression : JintExpression
    {
        private JintExpression _left;
        private JintExpression _right;

        public JintLogicalAndExpression(BinaryExpression expression) : base(expression)
        {
            _initialized = false;
        }

        protected override void Initialize(EvaluationContext context)
        {
            var expression = (BinaryExpression) _expression;
            _left = Build(context.Engine, expression.Left);
            _right = Build(context.Engine, expression.Right);
        }

        protected override ExpressionResult EvaluateInternal(EvaluationContext context)
        {
            var left = _left.GetValue(context).Value;

            if (left is JsBoolean b && !b._value)
            {
                return NormalCompletion(b);
            }

            if (!TypeConverter.ToBoolean(left))
            {
                return NormalCompletion(left);
            }

            return _right.GetValue(context);
        }
    }
}