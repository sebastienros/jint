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
    UnicodeRange,
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
    // Numeric tokens keep their source spelling in _text; every other kind keeps decoded text there.
    private readonly string? _text;
    private readonly string? _unit;
    private readonly CssSourceSpan _span;
    private readonly int _unicodeStart;
    private readonly int _unicodeEnd;
    private readonly CssTokenKind _kind;
    private readonly char _delimiter;
    private readonly Flags _flags;

    internal CssToken(CssTokenKind kind, CssSourceSpan span, string? text = null,
        string? numberText = null, string? unit = null, char delimiter = '\0',
        bool isInteger = false, bool isIdHash = false, int unicodeStart = 0,
        int unicodeEnd = 0)
    {
        _kind = kind;
        _span = span;
        _text = IsNumeric(kind) ? numberText : text;
        _unit = unit;
        _delimiter = delimiter;
        _flags = (isInteger ? Flags.Integer : Flags.None) | (isIdHash ? Flags.IdHash : Flags.None);
        _unicodeStart = unicodeStart;
        _unicodeEnd = unicodeEnd;
    }

    private CssToken(CssTokenKind kind, CssSourceSpan span, string? text, char delimiter, Flags flags)
    {
        _kind = kind;
        _span = span;
        _text = text;
        _delimiter = delimiter;
        _flags = flags;
    }

    // The header of a function or simple block: its span covers the whole container.
    internal static CssToken Container(bool function, CssSourceSpan span, string? functionName,
        char openingDelimiter, bool closed) =>
        new(function ? CssTokenKind.Function : CssTokenKind.OpenCurlyBracket, span, functionName,
            openingDelimiter, closed ? Flags.Closed : Flags.None);

    public CssTokenKind Kind => _kind;
    public CssSourceSpan Span => _span;
    // Unicode ranges retain their short source spelling; other text is decoded.
    public string Text => IsNumeric(_kind) ? string.Empty : _text ?? string.Empty;
    public string NumberText => IsNumeric(_kind) ? _text! : string.Empty;
    public string Unit => _unit ?? string.Empty;
    public char Delimiter => _delimiter;
    public bool IsInteger => (_flags & Flags.Integer) != 0;
    public bool IsIdHash => (_flags & Flags.IdHash) != 0;
    public int UnicodeRangeStart => _unicodeStart;
    public int UnicodeRangeEnd => _unicodeEnd;
    internal bool IsClosed => (_flags & Flags.Closed) != 0;

    private static bool IsNumeric(CssTokenKind kind) => (uint) (kind - CssTokenKind.Number) <= CssTokenKind.Dimension - CssTokenKind.Number;

    [Flags]
    private enum Flags : byte
    {
        None = 0,
        Integer = 1,
        IdHash = 2,
        Closed = 4
    }
}

public enum CssComponentKind { None, Token, Function, SimpleBlock }

public readonly struct CssComponentValue
{
    // A token component has no list; a container keeps its header in _token.
    private readonly CssToken _token;
    private readonly CssComponentValueList? _values;

    private CssComponentValue(CssToken token, CssComponentValueList? values)
    {
        _token = token;
        _values = values;
    }

    internal static CssComponentValue FromToken(CssToken token) => new(token, null);

    internal static CssComponentValue FromContainer(CssComponentKind kind, CssSourceSpan span,
        string? functionName, char openingDelimiter, CssComponentValueList values, bool closed) =>
        new(CssToken.Container(kind == CssComponentKind.Function, span, functionName, openingDelimiter, closed), values);

    public CssComponentKind Kind => _values is not null
        ? _token.Kind == CssTokenKind.Function ? CssComponentKind.Function : CssComponentKind.SimpleBlock
        : _token.Kind == CssTokenKind.None ? CssComponentKind.None : CssComponentKind.Token;
    public CssSourceSpan Span => _token.Span;
    public CssToken Token => _values is null && _token.Kind != CssTokenKind.None ? _token : throw new InvalidOperationException();
    public string FunctionName => Kind == CssComponentKind.Function ? _token.Text : throw new InvalidOperationException();
    public char OpeningDelimiter => Kind == CssComponentKind.SimpleBlock ? _token.Delimiter : throw new InvalidOperationException();
    public CssComponentValueList Values => _values ?? throw new InvalidOperationException();
    internal bool IsClosed => _values is not null && _token.IsClosed;
    // The token kind of a token component, otherwise None; avoids copying the token out.
    internal CssTokenKind TokenKind => _values is null ? _token.Kind : CssTokenKind.None;
}

public sealed class CssComponentValueList : IReadOnlyList<CssComponentValue>
{
    // Lists parsed out of an immutable parent list share its storage instead of copying.
    private readonly CssComponentValue[] _values;
    private readonly int _start;

    internal CssComponentValueList(CssComponentValue[] values) : this(values, 0, values.Length) { }

    internal CssComponentValueList(CssComponentValue[] values, int start, int count)
    {
        _values = values;
        _start = start;
        Count = count;
    }

    internal ReadOnlySpan<CssComponentValue> AsSpan() => new(_values, _start, Count);
    internal CssComponentValue[] Storage => _values;

    public int Count { get; }
    public CssComponentValue this[int index] => (uint) index < (uint) Count
        ? _values[_start + index]
        : throw new ArgumentOutOfRangeException(nameof(index));

    public IEnumerator<CssComponentValue> GetEnumerator()
    {
        for (var i = 0; i < Count; i++) yield return _values[_start + i];
    }

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
    internal CssDeclarationSyntax(string name, CssComponentValueList value, bool isImportant, CssSourceSpan span,
        CssSourceSpan valueSourceSpan, CssSourceSpan valueSerializationSpan, string valueTermination)
    {
        Name = name;
        Value = value;
        IsImportant = isImportant;
        Span = span;
        ValueSourceSpan = valueSourceSpan;
        ValueSerializationSpan = valueSerializationSpan;
        ValueTermination = valueTermination;
    }

    public string Name { get; }
    public CssComponentValueList Value { get; }
    public bool IsImportant { get; }
    public CssSourceSpan Span { get; }
    internal CssSourceSpan ValueSourceSpan { get; }
    internal CssSourceSpan ValueSerializationSpan { get; }
    internal string ValueTermination { get; }
}
