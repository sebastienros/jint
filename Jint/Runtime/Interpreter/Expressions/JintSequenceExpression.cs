using Esprima.Ast;
using Jint.Native;

namespace Jint.Runtime.Interpreter.Expressions
{
    internal sealed class JintSequenceExpression : JintExpression<SequenceExpression>
    {
        private readonly JintExpression[] _expressions;

        public JintSequenceExpression(Engine engine, SequenceExpression expression) : base(engine, expression)
        {
            _expressions = new JintExpression[_expression.Expressions.Count];
        }

        public override object Evaluate()
        {
            var result = Undefined.Instance;
            var expressionsCount = _expression.Expressions.Count;
            for (var i = 0; i < expressionsCount; i++)
            {
                var expression = _expressions[i] ?? (_expressions[i] = Build(_engine, _expression.Expressions[i]));
                result = _engine.GetValue(expression.Evaluate(), true);
            }

            return result;
        }
    }
}