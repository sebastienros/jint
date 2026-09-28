namespace Jint.HtmlParser.Css;

// Raw slices preserve original UTF-16 coordinates without retaining a tokenizer or syntax tree.
internal readonly struct CssSourceText
{
    internal CssSourceText(string source, CssSourceSpan span)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentOutOfRangeException.ThrowIfNegative(span.Start);
        ArgumentOutOfRangeException.ThrowIfNegative(span.Length);
        if (span.Start > source.Length - span.Length) throw new ArgumentOutOfRangeException(nameof(span));
        Source = source;
        Span = span;
    }

    internal static CssSourceText From(string source)
    {
        ArgumentNullException.ThrowIfNull(source);
        return new(source, new CssSourceSpan(0, source.Length));
    }

    internal string Source { get; }
    internal CssSourceSpan Span { get; }
    internal int End => Span.Start + Span.Length;
    internal string Text => Source.Substring(Span.Start, Span.Length);
    internal CssSourceText Slice(int start, int end) => new(Source, new CssSourceSpan(start, end - start));
}

internal sealed record CssRawRule(CssRuleKind Kind, string Name, CssSourceText Text,
    CssSourceText Prelude, CssSourceText? Body, bool IsClosed);

internal enum CssRuleBodyKind { Group, Style, Keyframes }
internal readonly record struct CssRuleBody(CssSourceText Text, CssRuleBodyKind Kind, bool IsClosed);
