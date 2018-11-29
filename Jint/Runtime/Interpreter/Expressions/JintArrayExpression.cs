using Esprima.Ast;

namespace Jint.Runtime.Interpreter.Expressions
{
    internal sealed class JintArrayExpression : JintExpression<ArrayExpression>
    {
        private readonly JintExpression[] _expressions;

        public JintArrayExpression(Engine engine, ArrayExpression expression) : base(engine, expression)
        {
            _expressions = new JintExpression[expression.Elements.Count];
        }

        protected override object EvaluateInternal()
        {
            var elements = _expression.Elements;
            var count = elements.Count;

            var a = _engine.Array.ConstructFast((uint) count);
            for (var n = 0; n < count; n++)
            {
                var expr = elements[n];
                if (expr != null)
                {
                    var jintExpr = _expressions[n] ?? (_expressions[n] = JintExpression.Build(_engine, (Expression) expr));
                    var value = _engine.GetValue(jintExpr.Evaluate(), true);
                    a.SetIndexValue((uint) n, value, updateLength: false);
                }
            }

            return a;
        }
    }
}