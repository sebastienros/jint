using Esprima.Ast;

namespace Jint.Runtime.Interpreter.Expressions
{
    internal sealed class JintLogicalOrExpression : JintExpression<BinaryExpression>
    {
        private readonly JintExpression _left;
        private readonly JintExpression _right;

        public JintLogicalOrExpression(Engine engine, BinaryExpression expression) : base(engine, expression)
        {
            _left = Build(engine, _expression.Left);
            _right = Build(engine, _expression.Right);
        }

        protected override object EvaluateInternal()
        {
            var left = _engine.GetValue(_left.Evaluate(), true);

            if (TypeConverter.ToBoolean(left))
            {
                return left;
            }

            return _engine.GetValue(_right.Evaluate(), true);
        }
    }
}