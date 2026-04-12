using Jint.Native;
using Jint.Native.RegExp;

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
            var pattern = regExpLiteral.RegExp.Pattern;
            var flags = regExpLiteral.RegExp.Flags;
            var regExpConstructor = context.Engine.Realm.Intrinsics.RegExp;
            var userData = regExpLiteral.UserData;

            // Fast path: reuse cached compiled regex from first evaluation.
            // Note: UserData assignment below is not synchronized. This is safe because
            // Engine instances are single-threaded. If a Prepared<Script> is shared across
            // concurrent Engine instances, the worst case is redundant compilation — both
            // engines produce equivalent results for the same pattern.
            if (userData is RegExpParseResult cachedParseResult)
            {
                return regExpConstructor.Construct(cachedParseResult, pattern, flags);
            }

            var conversionOptions = (Engine.RegexConversionOptions) regExpLiteral.ParseResult.AdditionalData!;

            if (!RegExpConstructor.NeedCustomEngine(pattern, flags))
            {
#pragma warning disable CS0618 // Type or member is obsolete
                cachedParseResult = Tokenizer.AdaptRegExp(
#pragma warning restore CS0618 // Type or member is obsolete
                    pattern, flags, conversionOptions.Compiled, conversionOptions.Timeout, throwIfNotAdaptable: false,
                    Engine.BaseParserOptions.EcmaVersion, Engine.BaseParserOptions.ExperimentalESFeatures);

                if (cachedParseResult.Success)
                {
                    regExpLiteral.UserData = cachedParseResult; // cache for next evaluation
                    return regExpConstructor.Construct(cachedParseResult, pattern, flags);
                }
            }

            // Fall back to custom regexp engine.
            var customEngine = RegExpConstructor.TryCompileWithCustomEngine(context.Engine.Realm,
                pattern, flags, conversionOptions.Timeout);

            cachedParseResult = RegExpParseResult.ForSuccess(customEngine);
            regExpLiteral.UserData = cachedParseResult; // cache for next evaluation
            return regExpConstructor.Construct(cachedParseResult, pattern, flags);
        }

        return JsValue.FromObject(context.Engine, expression.Value);
    }
}
