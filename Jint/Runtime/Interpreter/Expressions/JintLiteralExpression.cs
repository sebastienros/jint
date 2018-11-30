using Esprima;
using Esprima.Ast;
using Jint.Native;

namespace Jint.Runtime.Interpreter.Expressions
{
    internal sealed class JintLiteralExpression : JintExpression<Literal>
    {
        internal readonly JsValue _cachedValue;

        public JintLiteralExpression(Engine engine, Literal expression) : base(engine, expression)
        {
            _cachedValue = ConvertToJsValue(expression);
        }

        internal static JsValue ConvertToJsValue(Literal literal)
        {
            switch (literal.TokenType)
            {
                case TokenType.BooleanLiteral:
                    return literal.NumericValue > 0.0 ? JsBoolean.True : JsBoolean.False;

                case TokenType.NullLiteral:
                    // and so is null
                    return  JsValue.Null;

                case TokenType.NumericLiteral:
                    return JsNumber.Create(literal.NumericValue);

                case TokenType.StringLiteral:
                    return JsString.Create((string) literal.Value);

                default:
                    return null;
            }
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