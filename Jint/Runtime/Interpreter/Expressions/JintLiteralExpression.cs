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
            switch (_expression.TokenType)
            {
                case TokenType.BooleanLiteral:
                    // bool is fast enough
                    _cachedValue = _expression.NumericValue > 0.0 ? JsBoolean.True : JsBoolean.False;
                    break;

                case TokenType.NullLiteral:
                    // and so is null
                    _cachedValue = JsValue.Null;
                    break;

                case TokenType.NumericLiteral:
                    _cachedValue = JsNumber.Create(_expression.NumericValue);
                    break;

                case TokenType.StringLiteral:
                    _cachedValue = JsString.Create((string) _expression.Value);
                    break;
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