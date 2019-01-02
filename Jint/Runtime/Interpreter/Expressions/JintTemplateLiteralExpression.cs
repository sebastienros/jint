using Esprima.Ast;
using Jint.Native;
using Jint.Pooling;

namespace Jint.Runtime.Interpreter.Expressions
{
    internal sealed class JintTemplateLiteralExpression : JintExpression
    {
        internal readonly TemplateLiteral _templateLiteralExpression;
        internal JintExpression[] _expressions;

        public JintTemplateLiteralExpression(Engine engine, TemplateLiteral expression) : base(engine, expression)
        {
            _templateLiteralExpression = expression;
            _initialized = false;
        }

        protected override void Initialize()
        {
            DoInitialize();
        }

        internal void DoInitialize()
        {
            _expressions = new JintExpression[_templateLiteralExpression.Expressions.Count];
            for (var i = 0; i < _templateLiteralExpression.Expressions.Count; i++)
            {
                var exp = _templateLiteralExpression.Expressions[i];
                _expressions[i] = Build(_engine, exp);
            }

            _initialized = true;
        }

        private JsString BuildString()
        {
            using (var sb = StringBuilderPool.Rent())
            {
                for (var i = 0; i < _templateLiteralExpression.Quasis.Count; i++)
                {
                    var quasi = _templateLiteralExpression.Quasis[i];
                    sb.Builder.Append(quasi.Value.Cooked);
                    if (i < _expressions.Length)
                    {
                        sb.Builder.Append(_expressions[i].GetValue());
                    }
                }

                return JsString.Create(sb.ToString());
            }
        }

        protected override object EvaluateInternal()
        {
            return BuildString();
        }
    }
}