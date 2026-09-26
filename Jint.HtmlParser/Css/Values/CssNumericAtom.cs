namespace Jint.HtmlParser.Css.Values;

internal enum CssNumericKind { Number, Percentage, Dimension }

internal readonly struct CssNumericAtom
{
    internal CssNumericAtom(CssNumericKind kind, CssNumber number, CssUnit unit, bool isIntegerToken, CssSourceSpan span)
    {
        Kind = kind;
        Number = number;
        Unit = unit;
        IsIntegerToken = isIntegerToken;
        Span = span;
    }

    public CssNumericKind Kind { get; }
    public CssNumber Number { get; }
    public CssUnit Unit { get; }
    public bool IsIntegerToken { get; }
    public CssSourceSpan Span { get; }
}
