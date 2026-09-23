using System.Collections;

namespace Jint.HtmlParser.Css;

public readonly struct CssSourceSpan
{
    internal CssSourceSpan(int start, int length)
    {
        Start = start;
        Length = length;
    }

    public int Start { get; }
    public int Length { get; }
}

public enum CssTokenKind
{
    None,
    Ident,
    Function,
    AtKeyword,
    Hash,
    String,
    BadString,
    Url,
    BadUrl,
    Delim,
    Number,
    Percentage,
    Dimension,
    Whitespace,
    Cdo,
    Cdc,
    Colon,
    Semicolon,
    Comma,
    OpenParenthesis,
    CloseParenthesis,
    OpenSquareBracket,
    CloseSquareBracket,
    OpenCurlyBracket,
    CloseCurlyBracket
}

public readonly struct CssToken
{
    private readonly string? _text;
    private readonly string? _numberText;
    private readonly string? _unit;
    private readonly char _delimiter;
    private readonly bool _isInteger;
    private readonly bool _isIdHash;

    internal CssToken(CssTokenKind kind, CssSourceSpan span, string? text = null,
        string? numberText = null, string? unit = null, char delimiter = '\0',
        bool isInteger = false, bool isIdHash = false)
    {
        Kind = kind;
        Span = span;
        _text = text;
        _numberText = numberText;
        _unit = unit;
        _delimiter = delimiter;
        _isInteger = isInteger;
        _isIdHash = isIdHash;
    }

    public CssTokenKind Kind { get; }
    public CssSourceSpan Span { get; }
    public string Text => _text ?? string.Empty;
    public string NumberText => _numberText ?? string.Empty;
    public string Unit => _unit ?? string.Empty;
    public char Delimiter => _delimiter;
    public bool IsInteger => _isInteger;
    public bool IsIdHash => _isIdHash;
}

public enum CssComponentKind { None, Token, Function, SimpleBlock }

public readonly struct CssComponentValue
{
    private readonly CssToken _token;
    private readonly string? _functionName;
    private readonly char _openingDelimiter;
    private readonly CssComponentValueList? _values;

    private CssComponentValue(CssComponentKind kind, CssSourceSpan span, CssToken token,
        string? functionName, char openingDelimiter, CssComponentValueList? values)
    {
        Kind = kind;
        Span = span;
        _token = token;
        _functionName = functionName;
        _openingDelimiter = openingDelimiter;
        _values = values;
    }

    internal static CssComponentValue FromToken(CssToken token) =>
        new(CssComponentKind.Token, token.Span, token, null, '\0', null);

    internal static CssComponentValue FromContainer(CssComponentKind kind, CssSourceSpan span,
        string? functionName, char openingDelimiter, CssComponentValueList values) =>
        new(kind, span, default, functionName, openingDelimiter, values);

    public CssComponentKind Kind { get; }
    public CssSourceSpan Span { get; }
    public CssToken Token => Kind == CssComponentKind.Token ? _token : throw new InvalidOperationException();
    public string FunctionName => Kind == CssComponentKind.Function ? _functionName! : throw new InvalidOperationException();
    public char OpeningDelimiter => Kind == CssComponentKind.SimpleBlock ? _openingDelimiter : throw new InvalidOperationException();
    public CssComponentValueList Values => Kind is CssComponentKind.Function or CssComponentKind.SimpleBlock ? _values! : throw new InvalidOperationException();
}

public sealed class CssComponentValueList : IReadOnlyList<CssComponentValue>
{
    private readonly CssComponentValue[] _values;

    internal CssComponentValueList(CssComponentValue[] values) => _values = values;

    public int Count => _values.Length;
    public CssComponentValue this[int index] => _values[index];
    public IEnumerator<CssComponentValue> GetEnumerator() => ((IEnumerable<CssComponentValue>) _values).GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}

public enum CssRuleKind { AtRule, QualifiedRule }

public sealed class CssRuleSyntax
{
    internal CssRuleSyntax(CssRuleKind kind, string name, CssComponentValueList prelude,
        CssComponentValue? block, CssSourceSpan span)
    {
        Kind = kind;
        Name = name;
        Prelude = prelude;
        Block = block;
        Span = span;
    }

    public CssRuleKind Kind { get; }
    public string Name { get; }
    public CssComponentValueList Prelude { get; }
    public CssComponentValue? Block { get; }
    public CssSourceSpan Span { get; }
}

public sealed class CssDeclarationSyntax
{
    internal CssDeclarationSyntax(string name, CssComponentValueList value, bool isImportant, CssSourceSpan span)
    {
        Name = name;
        Value = value;
        IsImportant = isImportant;
        Span = span;
    }

    public string Name { get; }
    public CssComponentValueList Value { get; }
    public bool IsImportant { get; }
    public CssSourceSpan Span { get; }
}
