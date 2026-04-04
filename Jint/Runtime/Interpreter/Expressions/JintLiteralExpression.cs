using System.Text.RegularExpressions;
using Jint.Native;
using Jint.Native.RegExp;
using Jint.Runtime.RegExp;

namespace Jint.Runtime.Interpreter.Expressions;

internal sealed class JintLiteralExpression : JintExpression
{
    private static readonly object _nullMarker = new();

    private JintLiteralExpression(Literal expression) : base(expression)
    {
    }

    internal static JintExpression Build(Literal expression)
    {
        var value = expression.UserData ??= ConvertToJsValue(expression) ?? _nullMarker;

        if (value is JsValue constant)
        {
            return new JintConstantExpression(expression, constant);
        }

        return new JintLiteralExpression(expression);
    }

    internal static JsValue? ConvertToJsValue(Literal literal)
    {
        switch (literal.Kind)
        {
            case TokenKind.BooleanLiteral:
                return ((BooleanLiteral) literal).Value ? JsBoolean.True : JsBoolean.False;
            case TokenKind.NullLiteral:
                return JsValue.Null;
            case TokenKind.NumericLiteral:
                {
                    var numericValue = ((NumericLiteral) literal).Value;
                    var intValue = (int) numericValue;
                    return numericValue == intValue
                           && (intValue != 0 || BitConverter.DoubleToInt64Bits(numericValue) != JsNumber.NegativeZeroBits)
                        ? JsNumber.Create(intValue)
                        : JsNumber.Create(numericValue);
                }
            case TokenKind.StringLiteral:
                return JsString.Create(((StringLiteral) literal).Value);
            case TokenKind.BigIntLiteral:
                return JsBigInt.Create(((BigIntLiteral) literal).Value);
            case TokenKind.RegExpLiteral:
                break;
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
        if (expression is RegExpLiteral regExpLiteral)
        {
            var regExpParseResult = regExpLiteral.ParseResult;
            var pattern = regExpLiteral.RegExp.Pattern;
            var flags = regExpLiteral.RegExp.Flags;
            var regExpConstructor = context.Engine.Realm.Intrinsics.RegExp;
            var userData = regExpLiteral.UserData;

            // Fast path: reuse pre-compiled .NET Regex (from Acornima AdaptToCompiled or cached from first eval)
            if (userData is Regex cachedRegex)
            {
                return regExpConstructor.Construct(cachedRegex, pattern, flags, regExpParseResult);
            }

            // Fast path: reuse pre-compiled custom engine bytecode (cached from first eval)
            if (userData is JintRegExpEngine cachedEngine)
            {
                return regExpConstructor.Construct(cachedEngine, pattern, flags);
            }

            // Check Acornima's parse-time result (AdaptToCompiled mode)
            var regex = regExpParseResult.Regex;
            if (regex is not null && !RegExpConstructor.NeedCustomEngine(pattern, flags))
            {
                regExpLiteral.UserData = regex; // cache for next evaluation
                return regExpConstructor.Construct(regex, pattern, flags, regExpParseResult);
            }

            // First evaluation: compile at runtime, then cache the result
            var jsRegExp = regExpConstructor.RegExpCreate(pattern, flags);
            var result = (JsRegExp) jsRegExp;
            if (result.CustomEngine is not null)
            {
                regExpLiteral.UserData = result.CustomEngine; // cache bytecode
            }
            else if (result.Value is not null)
            {
                regExpLiteral.UserData = result.Value; // cache .NET Regex
            }

            return jsRegExp;
        }

        return JsValue.FromObject(context.Engine, expression.Value);
    }
}
