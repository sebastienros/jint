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
    private readonly CssSourceSpan[]? _earlySubstitutions;

    internal CssReferenceOccurrence(CssReferenceKind kind, CssSourceSpan span, int parentIndex,
        CssReferenceRange header, bool hasFallback, CssReferenceRange fallback,
        string? staticName, bool dynamicHeader, CssSourceSpan[]? earlySubstitutions,
        bool hasNestedFallback)
    {
        Kind = kind;
        Span = span;
        ParentIndex = parentIndex;
        Header = header;
        HasFallback = hasFallback;
        Fallback = fallback;
        StaticName = staticName;
        HasDynamicHeader = dynamicHeader;
        _earlySubstitutions = earlySubstitutions;
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
    internal bool HasEarlySubstitution => _earlySubstitutions is { Length: > 0 };
    internal int EarlySubstitutionCount => _earlySubstitutions?.Length ?? 0;
    internal CssSourceSpan EarlySubstitutionSpan(int index) =>
        _earlySubstitutions is { } spans ? spans[index] : throw new ArgumentOutOfRangeException(nameof(index));
    internal bool HasNestedFallback { get; }

    internal CssReferenceOccurrence WithNestedFallback() => new(Kind, Span, ParentIndex,
        Header, HasFallback, Fallback, StaticName, HasDynamicHeader, _earlySubstitutions, true);

    internal CssReferenceOccurrence WithDynamicHeader() => new(Kind, Span, ParentIndex,
        Header, HasFallback, Fallback, StaticName, true, _earlySubstitutions, HasNestedFallback);

    internal CssReferenceOccurrence WithEarlySubstitutions(CssSourceSpan[]? spans, bool inHeader) =>
        new(Kind, Span, ParentIndex, Header, HasFallback, Fallback, StaticName,
            HasDynamicHeader || inHeader, spans, HasNestedFallback);
}
