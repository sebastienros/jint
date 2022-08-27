using System.Numerics;
using Esprima;
using Esprima.Ast;
using Jint.Native;

namespace Jint.Runtime.Interpreter.Expressions
{
    internal sealed class JintLiteralExpression : JintExpression
    {
        private JintLiteralExpression(Literal expression) : base(expression)
        {
        }

        internal static JintExpression Build(Literal expression)
        {
            var constantValue = ConvertToJsValue(expression);
            if (constantValue is not null)
            {
                return new JintConstantExpression(expression, constantValue);
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

        public override Completion GetValue(EvaluationContext context)
        {
            // need to notify correct node when taking shortcut
            context.LastSyntaxNode = _expression;

            return Completion.Normal(ResolveValue(context), _expression);
        }

        protected override ExpressionResult EvaluateInternal(EvaluationContext context) => NormalCompletion(ResolveValue(context));

        private JsValue ResolveValue(EvaluationContext context)
        {
            var expression = (Literal) _expression;
            if (expression.TokenType == TokenType.RegularExpression)
            {
                return context.Engine.Realm.Intrinsics.RegExp.Construct((System.Text.RegularExpressions.Regex) expression.Value!, expression.Regex!.Pattern, expression.Regex.Flags);
            }

            return JsValue.FromObject(context.Engine, expression.Value);
        }
    }
}
