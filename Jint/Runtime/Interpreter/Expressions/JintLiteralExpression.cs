using Esprima;
using Esprima.Ast;
using Jint.Native;

namespace Jint.Runtime.Interpreter.Expressions
{
    internal sealed class JintLiteralExpression : JintExpression<Literal>
    {
        public JintLiteralExpression(Engine engine, Literal expression) : base(engine, expression)
        {
        }

        public override object Evaluate()
        {
            switch (_expression.TokenType)
            {
                case TokenType.BooleanLiteral:
                    // bool is fast enough
                    return _expression.NumericValue > 0.0 ? JsBoolean.True : JsBoolean.False;

                case TokenType.NullLiteral:
                    // and so is null
                    return JsValue.Null;

                case TokenType.NumericLiteral:
                    return (JsValue) (_expression.CachedValue = _expression.CachedValue ?? JsNumber.Create(_expression.NumericValue));

                case TokenType.StringLiteral:
                    return (JsValue) (_expression.CachedValue = _expression.CachedValue ?? JsString.Create((string) _expression.Value));

                case TokenType.RegularExpression:
                    // should not cache
                    return _engine.RegExp.Construct((System.Text.RegularExpressions.Regex) _expression.Value, _expression.Regex.Flags);

                default:
                    // a rare case, above types should cover all
                    return JsValue.FromObject(_engine, _expression.Value);
            }
        }
    }
}