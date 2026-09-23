namespace Jint.HtmlParser.Css.Values.Math;

internal enum CssMathParseStatus { None, Match, NoMatch, RequiresLaterGrammar }

// CSS Values 4 §10.8, Editor's Draft 20 August 2026: complete finite function census.
internal enum CssMathFunction
{
    None, Calc, Min, Max, Clamp, Round, Mod, Rem, Sin, Cos, Tan, Asin, Acos, Atan, Atan2,
    Pow, Sqrt, Hypot, Log, Exp, Abs, Sign
}

internal readonly struct CssMathParseResult
{
    private readonly CssMathValue? _value;
    private CssMathParseResult(CssMathParseStatus status, CssMathValue? value, CssSourceSpan span,
        CssMathFunction pendingFunction)
    {
        Status = status;
        _value = value;
        Span = span;
        PendingFunction = pendingFunction;
    }

    internal CssMathParseStatus Status { get; }
    internal CssMathValue Value => Status == CssMathParseStatus.Match ? _value! :
        throw new InvalidOperationException("No accepted math value is available.");
    internal CssSourceSpan Span { get; }
    internal CssMathFunction PendingFunction { get; }
    internal static CssMathParseResult Match(CssMathValue value) => new(CssMathParseStatus.Match, value, value.Span, CssMathFunction.None);
    internal static CssMathParseResult NoMatch(CssSourceSpan span) => new(CssMathParseStatus.NoMatch, null, span, CssMathFunction.None);
    internal static CssMathParseResult Pending(CssSourceSpan span, CssMathFunction function) =>
        new(CssMathParseStatus.RequiresLaterGrammar, null, span, function);
}
