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
            var pattern = regExpLiteral.RegExp.Pattern;
            var flags = regExpLiteral.RegExp.Flags;
            var engine = context.Engine;
            var regExpConstructor = engine.Realm.Intrinsics.RegExp;

            // The matcher is memoized on the AST node. It embeds no timeout - compiling it ran under the engine's
            // budget, and every match reads the budget in force from the conversion options or the engine - and its
            // compiled program is immutable and thread-safe (its one mutable field, a frame-count hint, is benign to
            // race), so the memo is engine-neutral and serves every engine sharing a Prepared<Script>, as the AST
            // UserData invariant requires. The unsynchronized write is the usual last-writer-wins AST publication: two engines
            // racing it compile the same pattern into equivalent matchers.
            var conversionOptions = regExpLiteral.ParseResult.AdditionalData as Engine.RegexConversionOptions;
            if (regExpLiteral.UserData is not JintRegExpEngine matcher)
            {
                // Process-wide cache: a fresh-parse (source-mode) literal would otherwise recompile the same
                // pattern every run, because the node memo only survives in a reused Prepared<Script>.
                matcher = RegExpParseCache.GetOrCompile(engine.Realm, pattern, flags, Engine.RegexConversionOptions.ResolveTimeout(conversionOptions, engine));
                regExpLiteral.UserData = matcher;
            }

            return regExpConstructor.Construct(matcher, conversionOptions, pattern, flags);
        }

        return JsValue.FromObject(context.Engine, expression.Value);
    }
}
