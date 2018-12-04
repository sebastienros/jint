using Esprima.Ast;
using Jint.Native;

namespace Jint.Runtime.Interpreter.Expressions
{
    internal sealed class JintSequenceExpression : JintExpression
    {
        private JintExpression[] _expressions;

        public JintSequenceExpression(Engine engine, SequenceExpression expression) : base(engine, expression)
        {
            _initialized = false;
        }

        protected override void Initialize()
        {
            var expression = (SequenceExpression) _expression;
            _expressions = new JintExpression[expression.Expressions.Count];
            for (var i = 0; i < expression.Expressions.Count; i++)
            {
                _expressions[i] = Build(_engine, expression.Expressions[i]);
            }
        }

        protected override object EvaluateInternal()
        {
            var result = Undefined.Instance;
            var expressions = _expressions;
            for (var i = 0; i < (uint) expressions.Length; i++)
            {
                var expression = expressions[i];
                result = expression.GetValue();
            }

            return result;
        }
    }
}