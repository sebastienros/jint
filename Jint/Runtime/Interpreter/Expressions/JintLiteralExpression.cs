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
                    var numericValue = ((NumericLiteral) literal).NearestDouble();
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

            // Fast path: reuse the adaptation memoized on an earlier evaluation, but only when it serves the
            // budget in force here. Most memos serve every engine and answer on the first comparison; the
            // engine's own constraint is read only for one adapted under a deferred timeout, which is what a
            // prepared program that chose none carries (see AdaptedRegExp).
            // Note: UserData assignment below is not synchronized. This is safe because
            // Engine instances are single-threaded. If a Prepared<Script> is shared across
            // concurrent Engine instances, the worst case is redundant adaptation — both
            // engines produce equivalent results for the same pattern and timeout.
            if (regExpLiteral.UserData is AdaptedRegExp memo
                && (memo.TimeoutTicks == AdaptedRegExp.EngineIndependent
                    || memo.TimeoutTicks == EngineTimeoutTicks(engine)))
            {
                return regExpConstructor.Construct(memo.ParseResult, pattern, flags);
            }

            var conversionOptions = (Engine.RegexConversionOptions) regExpLiteral.ParseResult.AdditionalData!;
            var timeout = conversionOptions.ResolveTimeout(engine);

            if (!RegExpConstructor.NeedCustomEngine(pattern, flags))
            {
                // Process-wide cache: a fresh-parse (source-mode) literal recompiles the same Regex every
                // run because the per-node UserData cache above only survives in a reused Prepared<Script>.
                // Its key covers the timeout as well as the pattern, so re-adapting a node whose memo was
                // built for another engine's budget costs a dictionary lookup rather than a compilation.
                var parseResult = RegExpParseCache.GetOrAdapt(
                    pattern, flags, conversionOptions.Compilation, timeout);

                if (parseResult.Success)
                {
                    // A .NET Regex embeds its MatchTimeout, so this adaptation is only good for the value it
                    // was built with — unless the source chose that value itself, in which case every engine
                    // resolves the same one and the memo serves them all.
                    regExpLiteral.UserData = new AdaptedRegExp(
                        in parseResult,
                        conversionOptions.Timeout is null ? timeout.Ticks : AdaptedRegExp.EngineIndependent);
                    return regExpConstructor.Construct(parseResult, pattern, flags);
                }
            }

            // Fall back to custom regexp engine.
            var customEngine = RegExpConstructor.TryCompileWithCustomEngine(engine.Realm, pattern, flags, timeout);

            // Carry the conversion options forward so CustomEngineBuiltinExec can honor the same budget
            // (the .NET Regex path embeds the timeout in MatchTimeout, the custom engine has no such carrier
            // and re-reads it per match) — which is also why this memo serves every engine: what it holds is
            // a compiled pattern with no timeout in it.
            var customResult = RegExpParseResult.ForSuccess(customEngine, additionalData: conversionOptions);
            regExpLiteral.UserData = new AdaptedRegExp(in customResult, AdaptedRegExp.EngineIndependent);
            return regExpConstructor.Construct(customResult, pattern, flags);
        }

        return JsValue.FromObject(context.Engine, expression.Value);
    }

    /// <summary>
    /// The effective <see cref="Options.ConstraintOptions.RegexTimeout"/> of the engine about to run the
    /// pattern, in ticks. Only reached for a memo adapted under a deferred timeout.
    /// </summary>
    private static long EngineTimeoutTicks(Engine engine)
        => Options.ConstraintOptions.NormalizeRegexTimeout(engine.Options.Constraints.RegexTimeout).Ticks;

    /// <summary>
    /// One adapted regex literal memoized on the AST node, together with the match timeout it embeds.
    /// </summary>
    /// <remarks>
    /// The memo is published onto the AST, which a <c>Prepared&lt;Script&gt;</c> shares across engines, so it
    /// may hold nothing engine-affine. A tick count is a value rather than a reference to the engine that
    /// supplied it, which is what lets the memo be keyed on the one input two engines can disagree about.
    /// Two engines with different budgets sharing one prepared script make the node ping-pong, last writer
    /// winning as every AST publication does; the loser pays a <see cref="RegExpParseCache"/> lookup, not a
    /// regex compilation.
    /// </remarks>
    private sealed class AdaptedRegExp
    {
        /// <summary>
        /// <see cref="TimeoutTicks"/> for an adaptation no engine's budget can invalidate: one built under a
        /// timeout the source itself chose, which is fixed on the node so every engine resolves the same
        /// value, and any custom-engine one, which embeds no timeout at all.
        /// </summary>
        public const long EngineIndependent = long.MinValue;

        public AdaptedRegExp(in RegExpParseResult parseResult, long timeoutTicks)
        {
            ParseResult = parseResult;
            TimeoutTicks = timeoutTicks;
        }

        public readonly RegExpParseResult ParseResult;

        /// <summary>Ticks of the timeout this adaptation embeds, or <see cref="EngineIndependent"/>.</summary>
        public readonly long TimeoutTicks;
    }
}
