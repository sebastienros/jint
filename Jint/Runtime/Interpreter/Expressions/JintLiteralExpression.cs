using System.Numerics;
using Esprima;
using Esprima.Ast;
using Jint.Native;

namespace Jint.Runtime.Interpreter.Expressions
{
    internal sealed class JintLiteralExpression : JintExpression
    {
        private static readonly object _nullMarker = new();

        private JintLiteralExpression(Literal expression) : base(expression)
        {
        }

        internal static JintExpression Build(Literal expression)
        {
            var value = expression.AssociatedData ??= ConvertToJsValue(expression) ?? _nullMarker;

            if (value is JsValue constant)
            {
                return new JintConstantExpression(expression, constant);
            }

            return new JintLiteralExpression(expression);
        }

        internal static JsValue? ConvertToJsValue(Literal literal)
        {
            if (literal.TokenType == TokenType.BooleanLiteral)
            {
                return literal.BooleanValue!.Value ? JsBoolean.True : JsBoolean.False;
            }

            if (literal.TokenType == TokenType.NullLiteral)
            {
                return JsValue.Null;
            }

            if (literal.TokenType == TokenType.NumericLiteral)
            {
                // unbox only once
                var numericValue = (double) literal.Value!;
                var intValue = (int) numericValue;
                return numericValue == intValue
                       && (intValue != 0 || BitConverter.DoubleToInt64Bits(numericValue) != JsNumber.NegativeZeroBits)
                    ? JsNumber.Create(intValue)
                    : JsNumber.Create(numericValue);
            }

            if (literal.TokenType == TokenType.StringLiteral)
            {
                return JsString.Create((string) literal.Value!);
            }

            if (literal.TokenType == TokenType.BigIntLiteral)
            {
                return JsBigInt.Create((BigInteger) literal.Value!);
            }

            return null;
        }

        public override JsValue GetValue(EvaluationContext context)
        {
            // need to notify correct node when taking shortcut
            context.LastSyntaxElement = _expression;
            return ResolveValue(context);
        }

        protected override object EvaluateInternal(EvaluationContext context) => ResolveValue(context);

        private JsValue ResolveValue(EvaluationContext context)
        {
            var expression = (Literal) _expression;
            if (expression.TokenType == TokenType.RegularExpression)
            {
                var regExpLiteral = (RegExpLiteral) _expression;
                var regExpParseResult = regExpLiteral.ParseResult;
                if (regExpParseResult.Success)
                {
                    return context.Engine.Realm.Intrinsics.RegExp.Construct(regExpParseResult.Regex!, regExpLiteral.Regex.Pattern, regExpLiteral.Regex.Flags, regExpParseResult);
                }

                ExceptionHelper.ThrowSyntaxError(context.Engine.Realm, $"Unsupported regular expression. {regExpParseResult.ConversionError!.Description}");
            }

            return JsValue.FromObject(context.Engine, expression.Value);
        }
    }
}
