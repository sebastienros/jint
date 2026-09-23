namespace Jint.HtmlParser.Css.Values.References;

internal enum CssReferenceKind { Var, Env }

/// <summary>A slice of an owned component list, with its position in original source.</summary>
internal readonly struct CssReferenceRange
{
    internal CssReferenceRange(CssComponentValueList components, int start, int count, CssSourceSpan span)
    {
        Components = components;
        Start = start;
        Count = count;
        Span = span;
    }

    internal CssComponentValueList Components { get; }
    internal int Start { get; }
    internal int Count { get; }
    internal CssSourceSpan Span { get; }
}

internal readonly struct CssReferenceOccurrence
{
    internal CssReferenceOccurrence(CssReferenceKind kind, CssSourceSpan span, int parentIndex,
        CssReferenceRange header, bool hasFallback, CssReferenceRange fallback,
        string? staticName, bool dynamicHeader, bool hasEarlySubstitution, bool hasNestedFallback)
    {
        Kind = kind;
        Span = span;
        ParentIndex = parentIndex;
        Header = header;
        HasFallback = hasFallback;
        Fallback = fallback;
        StaticName = staticName;
        HasDynamicHeader = dynamicHeader;
        HasEarlySubstitution = hasEarlySubstitution;
        HasNestedFallback = hasNestedFallback;
    }

    internal CssReferenceKind Kind { get; }
    internal CssSourceSpan Span { get; }
    internal int ParentIndex { get; }
    internal CssReferenceRange Header { get; }
    internal bool HasFallback { get; }
    internal CssReferenceRange Fallback { get; }
    internal string? StaticName { get; }
    internal bool HasDynamicHeader { get; }
    internal bool HasEarlySubstitution { get; }
    internal bool HasNestedFallback { get; }

    internal CssReferenceOccurrence WithNestedFallback() => new(Kind, Span, ParentIndex,
        Header, HasFallback, Fallback, StaticName, HasDynamicHeader, HasEarlySubstitution, true);
}
