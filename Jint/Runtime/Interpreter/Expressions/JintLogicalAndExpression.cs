using Esprima.Ast;
using Jint.Native;

namespace Jint.Runtime.Interpreter.Expressions
{
    internal sealed class JintLogicalAndExpression : JintExpression
    {
        private JintExpression _left;
        private JintExpression _right;

        public JintLogicalAndExpression(Engine engine, BinaryExpression expression) : base(engine, expression)
        {
            _initialized = false;
        }

        protected override void Initialize()
        {
            var expression = (BinaryExpression) _expression;
            _left = Build(_engine, expression.Left);
            _right = Build(_engine, expression.Right);
        }

        protected override object EvaluateInternal()
        {
            var left = _left.GetValue();

            if (left is JsBoolean b && !b._value)
            {
                return b;
            }

            if (!TypeConverter.ToBoolean(left))
            {
                return left;
            }

            return _right.GetValue();
        }
    }
}