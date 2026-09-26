namespace Jint.HtmlParser.Css.Values.References;

internal enum CssReferenceUse { PropertyValue, CustomPropertyValue, DescriptorValue }
internal enum CssReferenceAnalysisKind { Uninitialized, Literal, Deferred, InvalidSyntax, PendingFeature }

internal readonly struct CssReferenceAnalysis
{
    private readonly CssReferenceProgram? _program;
    private readonly CssSourceSpan _span;
    private readonly string? _pendingFunction;

    private CssReferenceAnalysis(CssReferenceAnalysisKind kind, CssReferenceProgram? program,
        CssSourceSpan span, string? pendingFunction)
    {
        Kind = kind;
        _program = program;
        _span = span;
        _pendingFunction = pendingFunction;
    }

    internal CssReferenceAnalysisKind Kind { get; }
    internal CssReferenceProgram Program => Kind is CssReferenceAnalysisKind.Literal or
        CssReferenceAnalysisKind.Deferred ? _program! : throw new InvalidOperationException();
    internal CssSourceSpan Span => Kind is CssReferenceAnalysisKind.InvalidSyntax or
        CssReferenceAnalysisKind.PendingFeature ? _span : throw new InvalidOperationException();
    internal string? PendingFunction => Kind == CssReferenceAnalysisKind.PendingFeature
        ? _pendingFunction : throw new InvalidOperationException();

    internal static CssReferenceAnalysis Success(CssReferenceProgram program, bool deferred) =>
        new(deferred ? CssReferenceAnalysisKind.Deferred : CssReferenceAnalysisKind.Literal,
            program, default, null);
    internal static CssReferenceAnalysis Invalid(CssSourceSpan span) =>
        new(CssReferenceAnalysisKind.InvalidSyntax, null, span, null);
    internal static CssReferenceAnalysis Pending(CssSourceSpan span, string function) =>
        new(CssReferenceAnalysisKind.PendingFeature, null, span, function);
}

/// <summary>Owns the input and a private, publication-time copy of ordered occurrences.</summary>
internal sealed class CssReferenceProgram
{
    private readonly CssReferenceOccurrence[] _occurrences;

    internal CssReferenceProgram(CssReferenceInput input, CssReferenceOccurrence[] occurrences)
    {
        Input = input;
        _occurrences = occurrences;
    }

    internal CssReferenceInput Input { get; }
    internal int Count => _occurrences.Length;
    internal CssReferenceOccurrence this[int index] => _occurrences[index];
}
