namespace Jint.HtmlParser.Css.Values.References;

internal enum CssCustomPropertyKind { Uninitialized, Value, WideKeyword, InvalidSyntax, PendingFeature }

internal readonly struct CssCustomPropertyValue
{
    internal CssCustomPropertyValue(string decodedName, CssReferenceProgram program)
    {
        DecodedName = decodedName;
        Program = program;
    }

    internal string DecodedName { get; }
    internal CssReferenceProgram Program { get; }
    internal CssReferenceInput Input => Program.Input;
}

internal readonly struct CssCustomPropertyResult
{
    private readonly CssCustomPropertyValue _value;
    private readonly CssWideKeyword _keyword;
    private readonly CssReferenceInput? _input;
    private readonly CssSourceSpan _span;
    private readonly string? _pendingFunction;

    private CssCustomPropertyResult(CssCustomPropertyKind kind, CssCustomPropertyValue value,
        CssWideKeyword keyword, CssReferenceInput? input, CssSourceSpan span, string? pendingFunction)
    {
        Kind = kind;
        _value = value;
        _keyword = keyword;
        _input = input;
        _span = span;
        _pendingFunction = pendingFunction;
    }

    internal CssCustomPropertyKind Kind { get; }
    internal CssCustomPropertyValue Value => Kind == CssCustomPropertyKind.Value
        ? _value : throw new InvalidOperationException();
    internal CssWideKeyword WideKeyword => Kind == CssCustomPropertyKind.WideKeyword
        ? _keyword : throw new InvalidOperationException();
    internal CssReferenceInput Input => Kind == CssCustomPropertyKind.WideKeyword
        ? _input! : throw new InvalidOperationException();
    internal CssSourceSpan Span => Kind is CssCustomPropertyKind.InvalidSyntax or
        CssCustomPropertyKind.PendingFeature ? _span : throw new InvalidOperationException();
    internal string? PendingFunction => Kind == CssCustomPropertyKind.PendingFeature
        ? _pendingFunction : throw new InvalidOperationException();

    internal static CssCustomPropertyResult FromValue(string name, CssReferenceProgram program) =>
        new(CssCustomPropertyKind.Value, new CssCustomPropertyValue(name, program), default, null, default, null);
    internal static CssCustomPropertyResult FromKeyword(CssWideKeyword keyword, CssReferenceInput input) =>
        new(CssCustomPropertyKind.WideKeyword, default, keyword, input, default, null);
    internal static CssCustomPropertyResult Invalid(CssSourceSpan span) =>
        new(CssCustomPropertyKind.InvalidSyntax, default, default, null, span, null);
    internal static CssCustomPropertyResult Pending(CssSourceSpan span, string function) =>
        new(CssCustomPropertyKind.PendingFeature, default, default, null, span, function);
}
