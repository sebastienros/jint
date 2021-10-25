using Esprima.Ast;

namespace Jint.Runtime.Interpreter.Expressions
{
    internal sealed class JintConditionalExpression : JintExpression
    {
        private readonly JintExpression _test;
        private readonly JintExpression _consequent;
        private readonly JintExpression _alternate;

        public JintConditionalExpression(Engine engine, ConditionalExpression expression) : base(expression)
        {
            _test = Build(engine, expression.Test);
            _consequent = Build(engine, expression.Consequent);
            _alternate = Build(engine, expression.Alternate);
        }

        protected override ExpressionResult EvaluateInternal(EvaluationContext context)
        {
            return TypeConverter.ToBoolean(_test.GetValue(context).Value)
                ? _consequent.GetValue(context)
                : _alternate.GetValue(context);
        }
    }
}