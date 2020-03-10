using System;
using System.Globalization;
using Esprima;
using Esprima.Ast;
using Jint.Native;

namespace Jint.Runtime.Interpreter.Expressions
{
    internal class JintLiteralExpression : JintExpression
    {
        private JintLiteralExpression(Engine engine, Literal expression) : base(engine, expression)
        {
        }

        internal static JintExpression Build(Engine engine, Literal expression)
        {
            var constantValue = ConvertToJsValue(expression);
            if (!(constantValue is null))
            {
                return new JintConstantExpression(engine, expression, constantValue);
            }
            
            return new JintLiteralExpression(engine, expression);
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
                var isIntValue = int.TryParse(literal.Raw, out var intValue);
                if (!isIntValue && literal.Raw.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                {
                    // TODO spanify
                    var hex = literal.Raw.Substring(2);
                    if (uint.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var hexIntValue))
                    {
                        return JsNumber.Create(hexIntValue);
                    }
                }

                return isIntValue
                       && (intValue != 0 || BitConverter.DoubleToInt64Bits(literal.NumericValue) != JsNumber.NegativeZeroBits)
                    ? JsNumber.Create(intValue)
                    : JsNumber.Create(literal.NumericValue);
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
            
            return ResolveValue();
        }

        protected override object EvaluateInternal() => ResolveValue();

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