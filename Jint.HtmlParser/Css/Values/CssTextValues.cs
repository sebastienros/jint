namespace Jint.HtmlParser.Css.Values;

internal readonly struct CssIdentifierValue
{
    internal CssIdentifierValue(string text, CssSourceSpan span)
    {
        Text = text;
        Span = span;
    }

    public string Text { get; }
    public CssSourceSpan Span { get; }
}

internal readonly struct CssStringValue
{
    internal CssStringValue(string text, CssSourceSpan span)
    {
        Text = text;
        Span = span;
    }

    public string Text { get; }
    public CssSourceSpan Span { get; }
}
