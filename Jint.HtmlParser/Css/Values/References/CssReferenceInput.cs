using Jint.HtmlParser.Css.Syntax;

namespace Jint.HtmlParser.Css.Values.References;

/// <summary>A priority-stripped CSS value and the one C1 parse of its original source.</summary>
internal sealed class CssReferenceInput
{
    private CssReferenceInput(string source, CssComponentValueList components, int maxNestingDepth)
    {
        Source = source;
        Components = components;
        MaxNestingDepth = maxNestingDepth;
    }

    internal string Source { get; }
    internal CssComponentValueList Components { get; }
    internal int MaxNestingDepth { get; }

    // Consumes C1's priority-stripped immutable components; spans remain in the original source.
    internal static CssReferenceInput FromComponents(string source, CssComponentValueList components,
        int maxNestingDepth)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(components);
        ArgumentOutOfRangeException.ThrowIfNegative(maxNestingDepth);
        return new CssReferenceInput(source, components, maxNestingDepth);
    }

    internal static CssReferenceInput Parse(string valueText, CssParseOptions? options,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(valueText);
        cancellationToken.ThrowIfCancellationRequested();
        var depth = options?.Limits.MaxNestingDepth ?? 0;
        var components = new CssSyntaxParser(valueText, options, cancellationToken).ParseComponentValues();
        cancellationToken.ThrowIfCancellationRequested();
        var input = new CssReferenceInput(valueText, components, depth);
        cancellationToken.ThrowIfCancellationRequested();
        return input;
    }
}
