using Esprima;
using Esprima.Ast;
using Jint.Native;

namespace Jint.Runtime.Interpreter.Expressions
{
    internal class JintLiteralExpression : JintExpression<Literal>
    {
        internal readonly JsValue _cachedValue;

        public JintLiteralExpression(Engine engine, Literal expression) : base(engine, expression)
        {
            _cachedValue = ConvertToJsValue(expression);
        }

        protected JintLiteralExpression(Engine engine, JsValue cachedValue) : base(engine, null)
        {
            _cachedValue = cachedValue;
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

        protected override object EvaluateInternal()
        {
            return _cachedValue ?? ResolveValue();
        }

        private object ResolveValue()
        {
            if (_expression.TokenType == TokenType.RegularExpression)
            {
                return _engine.RegExp.Construct((System.Text.RegularExpressions.Regex) _expression.Value, _expression.Regex.Flags);
            }

            return JsValue.FromObject(_engine, _expression.Value);
        }
    }
}