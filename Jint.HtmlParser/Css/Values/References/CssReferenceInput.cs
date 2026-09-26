using Jint.HtmlParser.Css.Syntax;

namespace Jint.HtmlParser.Css.Values.References;

/// <summary>A priority-stripped CSS value and the one C1 parse of its original source.</summary>
internal sealed class CssReferenceInput
{
    private CssReferenceInput(string source, int sourceOffset, CssComponentValueList components, int maxNestingDepth)
    {
        Source = source;
        SourceOffset = sourceOffset;
        Components = components;
        MaxNestingDepth = maxNestingDepth;
    }

    internal string Source { get; }
    internal int SourceOffset { get; }
    internal CssComponentValueList Components { get; }
    internal int MaxNestingDepth { get; }

    // Consumes C1's priority-stripped immutable components; spans remain in the original source.
    internal static CssReferenceInput FromComponents(string source, CssComponentValueList components,
        int maxNestingDepth) => FromComponents(source, components, maxNestingDepth, new CssValueWork(default));

    internal static CssReferenceInput FromComponents(string source, CssComponentValueList components,
        int maxNestingDepth, CssValueWork work)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(components);
        ArgumentNullException.ThrowIfNull(work);
        ArgumentOutOfRangeException.ThrowIfNegative(maxNestingDepth);
        work.CheckCancellation();
        if (components.Count == 0) return new CssReferenceInput(string.Empty, 0, components, maxNestingDepth);
        var first = components[0].Span;
        var last = components[components.Count - 1].Span;
        var end = checked(last.Start + last.Length);
        if (first.Start < 0 || end < first.Start || end > source.Length)
            throw new ArgumentOutOfRangeException(nameof(components));
        var length = end - first.Start;
        work.CheckCancellation();
        var slice = source.Substring(first.Start, length);
        work.Charge(length);
        work.CheckCancellation();
        return new CssReferenceInput(slice, first.Start, components, maxNestingDepth);
    }

    internal ReadOnlySpan<char> SourceSlice(CssSourceSpan originalSpan)
    {
        var start = (long) originalSpan.Start - SourceOffset;
        if (start < 0 || start > Source.Length || originalSpan.Length < 0 ||
            originalSpan.Length > Source.Length - start)
            throw new ArgumentOutOfRangeException(nameof(originalSpan));
        return Source.AsSpan((int) start, originalSpan.Length);
    }

    internal static CssReferenceInput Parse(string valueText, CssParseOptions? options,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(valueText);
        cancellationToken.ThrowIfCancellationRequested();
        var depth = options?.Limits.MaxNestingDepth ?? 0;
        var components = new CssSyntaxParser(valueText, options, cancellationToken).ParseComponentValues();
        cancellationToken.ThrowIfCancellationRequested();
        var input = FromComponents(valueText, components, depth, new CssValueWork(cancellationToken));
        cancellationToken.ThrowIfCancellationRequested();
        return input;
    }
}
