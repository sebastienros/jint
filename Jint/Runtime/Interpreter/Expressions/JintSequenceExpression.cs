using Esprima.Ast;
using Jint.Native;

namespace Jint.Runtime.Interpreter.Expressions
{
    internal sealed class JintSequenceExpression : JintExpression<SequenceExpression>
    {
        private JintExpression[] _expressions;

        public JintSequenceExpression(Engine engine, SequenceExpression expression) : base(engine, expression)
        {
        }

        protected override void Initialize()
        {
            _expressions = new JintExpression[_expression.Expressions.Count];
            for (var i = 0; i < _expression.Expressions.Count; i++)
            {
                _expressions[i] = Build(_engine, _expression.Expressions[i]);
            }
        }

        protected override object EvaluateInternal()
        {
            var result = Undefined.Instance;
            var expressions = _expressions;
            for (var i = 0; i < (uint) expressions.Length; i++)
            {
                var expression = expressions[i];
                result = _engine.GetValue(expression.Evaluate(), true);
            }

            return result;
        }
    }
}