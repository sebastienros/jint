using Esprima.Ast;

namespace Jint.Runtime.Interpreter.Expressions
{
    internal sealed class JintConditionalExpression : JintExpression<ConditionalExpression>
    {
        private readonly JintExpression _test;
        private readonly JintExpression _consequent;
        private readonly JintExpression _alternate;

        public JintConditionalExpression(Engine engine, ConditionalExpression expression) : base(engine, expression)
        {
            _test = Build(engine, expression.Test);
            _consequent = Build(engine, expression.Consequent);
            _alternate = Build(engine, expression.Alternate);
        }

        public override object Evaluate()
        {
            var lref = _test.Evaluate();
            if (TypeConverter.ToBoolean(_engine.GetValue(lref, true)))
            {
                var trueRef = _consequent.Evaluate();
                return _engine.GetValue(trueRef, true);
            }
            else
            {
                var falseRef = _alternate.Evaluate();
                return _engine.GetValue(falseRef, true);
            }
        }
    }
}