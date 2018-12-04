using Esprima.Ast;

namespace Jint.Runtime.Interpreter.Expressions
{
    internal sealed class JintArrayExpression : JintExpression
    {
        private JintExpression[] _expressions;

        public JintArrayExpression(Engine engine, ArrayExpression expression) : base(engine, expression)
        {
            _initialized = false;
        }

        protected override void Initialize()
        {
            var node = (ArrayExpression) _expression;
            _expressions = new JintExpression[node.Elements.Count];
            for (var n = 0; n < _expressions.Length; n++)
            {
                var expr = node.Elements[n];
                if (expr != null)
                {
                    var expression = Build(_engine, (Expression) expr);
                    _expressions[n] = expression;
                }
            }
        }

        protected override object EvaluateInternal()
        {
            var a = _engine.Array.ConstructFast((uint) _expressions.Length);
            var expressions = _expressions;
            for (uint i = 0; i < (uint) expressions.Length; i++)
            {
                var expr = expressions[i];
                if (expr != null)
                {
                    var value = expr.GetValue();
                    a.SetIndexValue(i, value, updateLength: false);
                }
            }

            return a;
        }
    }
}