namespace Jint.HtmlParser.Css.Values;

internal readonly struct CssPrimitiveResult<T>
{
    private readonly T _value;

    private CssPrimitiveResult(bool isMatch, T value, CssSourceSpan span)
    {
        IsMatch = isMatch;
        _value = value;
        Span = span;
    }

    internal static CssPrimitiveResult<T> Match(T value, CssSourceSpan span) => new(true, value, span);
    internal static CssPrimitiveResult<T> NoMatch(CssSourceSpan span) => new(false, default!, span);

    public bool IsMatch { get; }
    public T Value => IsMatch ? _value : throw new InvalidOperationException("The primitive did not match.");
    public CssSourceSpan Span { get; }
}
