using Esprima.Ast;
using Jint.Native;

namespace Jint.Runtime.Interpreter.Expressions
{
    internal sealed class JintArrayExpression : JintExpression<ArrayExpression>
    {
        private class Pair
        {
            internal JintExpression Expression;
            internal JsValue Value;
        }

        private Pair[] _expressions;

        public JintArrayExpression(Engine engine, ArrayExpression expression) : base(engine, expression)
        {
        }

        protected override void Initialize()
        {
            _expressions = new Pair[_expression.Elements.Count];
            for (var n = 0; n < _expressions.Length; n++)
            {
                var expr = _expression.Elements[n];
                if (expr != null)
                {
                    var expression = Build(_engine, (Expression) expr);
                    _expressions[n] = new Pair
                    {
                        Expression = expression,
                        Value = FastResolve(expression)
                    };
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
                    var value = expr.Value ?? _engine.GetValue(expr.Expression.Evaluate(), true);
                    a.SetIndexValue(i, value, updateLength: false);
                }
            }

            return a;
        }
    }
}