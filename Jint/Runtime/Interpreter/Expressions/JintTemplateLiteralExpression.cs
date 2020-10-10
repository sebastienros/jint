using Esprima.Ast;
using Jint.Native;
using Jint.Pooling;

namespace Jint.Runtime.Interpreter.Expressions
{
    internal sealed class JintTemplateLiteralExpression : JintExpression
    {
        internal readonly TemplateLiteral _templateLiteralExpression;
        internal JintExpression[] _expressions;

        public JintTemplateLiteralExpression(TemplateLiteral expression) : base(expression)
        {
            _templateLiteralExpression = expression;
            _initialized = false;
        }

        protected override void Initialize(EvaluationContext context)
        {
            DoInitialize(context);
        }

        internal void DoInitialize(EvaluationContext context)
        {
            var engine = context.Engine;
            _expressions = new JintExpression[_templateLiteralExpression.Expressions.Count];
            for (var i = 0; i < _templateLiteralExpression.Expressions.Count; i++)
            {
                var exp = _templateLiteralExpression.Expressions[i];
                _expressions[i] = Build(engine, exp);
            }

            _initialized = true;
        }

        private JsString BuildString(EvaluationContext context)
        {
            using var sb = StringBuilderPool.Rent();
            for (var i = 0; i < _templateLiteralExpression.Quasis.Count; i++)
            {
                var quasi = _templateLiteralExpression.Quasis[i];
                sb.Builder.Append(quasi.Value.Cooked);
                if (i < _expressions.Length)
                {
                    var completion = _expressions[i].GetValue(context);
                    sb.Builder.Append(completion.Value);
                }
            }

            return JsString.Create(sb.ToString());
        }

        protected override ExpressionResult EvaluateInternal(EvaluationContext context)
        {
            return NormalCompletion(BuildString(context));
        }
    }
}