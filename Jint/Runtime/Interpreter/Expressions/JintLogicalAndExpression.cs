using Esprima.Ast;
using Jint.Native;

namespace Jint.Runtime.Interpreter.Expressions
{
    internal sealed class JintLogicalAndExpression : JintExpression
    {
        private JintExpression _left = null!;
        private JintExpression _right = null!;

        public JintLogicalAndExpression(BinaryExpression expression) : base(expression)
        {
            _initialized = false;
        }

        protected override void Initialize(EvaluationContext context)
        {
            var expression = (BinaryExpression) _expression;
            _left = Build(expression.Left);
            _right = Build(expression.Right);
        }

        protected override object EvaluateInternal(EvaluationContext context)
        {
            var left = _left.GetValue(context);

            if (left is JsBoolean b && !b._value)
            {
                return b;
            }

            if (!TypeConverter.ToBoolean(left))
            {
                return left;
            }

            return _right.GetValue(context);
        }
    }
}
