using Jint.HtmlParser.Css.Syntax;

namespace Jint.HtmlParser.Css.Values.References;

/// <summary>A priority-stripped CSS value and the one C1 parse of its original source.</summary>
internal sealed class CssReferenceInput
{
    private CssReferenceInput(string source, int sourceOffset, CssComponentValueList components, int maxNestingDepth,
        CssSourceSpan serializationSpan, string valueTermination)
    {
        Source = source;
        SourceOffset = sourceOffset;
        Components = components;
        MaxNestingDepth = maxNestingDepth;
        SerializationSpan = serializationSpan;
        ValueTermination = valueTermination;
    }

    internal string Source { get; }
    internal int SourceOffset { get; }
    internal CssComponentValueList Components { get; }
    internal int MaxNestingDepth { get; }
    internal CssSourceSpan SerializationSpan { get; }
    internal string ValueTermination { get; }

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
        if (components.Count == 0) return new CssReferenceInput(string.Empty, 0, components, maxNestingDepth, default, "");
        var first = components[0].Span;
        var last = components[components.Count - 1].Span;
        var end = checked(last.Start + last.Length);
        return FromComponents(source, components, maxNestingDepth,
            new CssSourceSpan(first.Start, checked(end - first.Start)), work);
    }

    internal static CssReferenceInput FromComponents(string source, CssComponentValueList components,
        int maxNestingDepth, CssSourceSpan retainedSourceSpan, CssValueWork work,
        CssSourceSpan? serializationSpan = null, string valueTermination = "")
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(components);
        ArgumentNullException.ThrowIfNull(work);
        ArgumentOutOfRangeException.ThrowIfNegative(maxNestingDepth);
        work.CheckCancellation();
        var start = retainedSourceSpan.Start;
        var length = retainedSourceSpan.Length;
        if (start < 0 || start > source.Length || length < 0 || length > source.Length - start)
            throw new ArgumentOutOfRangeException(nameof(retainedSourceSpan));
        if (components.Count != 0 && (components[0].Span.Start < start ||
            (long) components[components.Count - 1].Span.Start + components[components.Count - 1].Span.Length > (long) start + length))
            throw new ArgumentOutOfRangeException(nameof(retainedSourceSpan));
        var lexicalSpan = serializationSpan ?? retainedSourceSpan;
        if (lexicalSpan.Start < start || lexicalSpan.Length < 0 ||
            (long) lexicalSpan.Start + lexicalSpan.Length > (long) start + length)
            throw new ArgumentOutOfRangeException(nameof(serializationSpan));
        ArgumentNullException.ThrowIfNull(valueTermination);
        work.Charge(valueTermination.Length);
        work.CheckCancellation();
        var slice = source.Substring(start, length);
        work.Charge(length);
        work.CheckCancellation();
        return new CssReferenceInput(slice, start, components, maxNestingDepth, lexicalSpan, valueTermination);
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
        var parser = new CssSyntaxParser(valueText, options, cancellationToken);
        var components = parser.ParseComponentValues();
        cancellationToken.ThrowIfCancellationRequested();
        var work = new CssValueWork(cancellationToken);
        var retained = new CssSourceSpan(0, valueText.Length);
        var input = FromComponents(valueText, components, depth, retained, work,
            valueTermination: parser.ValueTermination(components, retained, work));
        cancellationToken.ThrowIfCancellationRequested();
        return input;
    }
}
