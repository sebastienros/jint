using Esprima;
using Esprima.Ast;
using Jint.Native;

namespace Jint.Runtime.Interpreter.Expressions
{
    internal class JintLiteralExpression : JintExpression
    {
        internal readonly JsValue _cachedValue;

        public JintLiteralExpression(Engine engine, Literal expression) : base(engine, expression)
        {
            _cachedValue = ConvertToJsValue(expression);
        }

        internal static JsValue ConvertToJsValue(Literal literal)
        {
            if (literal.TokenType == TokenType.BooleanLiteral)
            {
                return literal.NumericValue > 0.0 ? JsBoolean.True : JsBoolean.False;
            }

            if (literal.TokenType == TokenType.NullLiteral)
            {
                return JsValue.Null;
            }

            if (literal.TokenType == TokenType.NumericLiteral)
            {
                return JsNumber.Create(literal.NumericValue);
            }

            if (literal.TokenType == TokenType.StringLiteral)
            {
                return JsString.Create((string) literal.Value);
            }

            return null;
        }

        public override JsValue GetValue()
        {
            // need to notify correct node when taking shortcut
            _engine._lastSyntaxNode = _expression;
            return _cachedValue ?? ResolveValue();
        }

        protected override object EvaluateInternal()
        {
            return _cachedValue ?? ResolveValue();
        }

        private JsValue ResolveValue()
        {
            var expression = (Literal) _expression;
            if (expression.TokenType == TokenType.RegularExpression)
            {
                return _engine.RegExp.Construct((System.Text.RegularExpressions.Regex) expression.Value, expression.Regex.Flags, _engine);
            }

            return JsValue.FromObject(_engine, expression.Value);
        }
    }
}