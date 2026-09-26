using System.Collections;

namespace Jint.HtmlParser.Css;

internal enum CssBlockItemKind { None, Rule, Declarations }

internal readonly struct CssBlockItemSyntax
{
    private readonly CssRuleSyntax? _rule;
    private readonly CssDeclarationSyntaxList? _declarations;

    private CssBlockItemSyntax(CssRuleSyntax rule)
    {
        Kind = CssBlockItemKind.Rule;
        _rule = rule;
        _declarations = null;
    }

    private CssBlockItemSyntax(CssDeclarationSyntaxList declarations)
    {
        Kind = CssBlockItemKind.Declarations;
        _rule = null;
        _declarations = declarations;
    }

    internal static CssBlockItemSyntax FromRule(CssRuleSyntax rule) =>
        new(rule ?? throw new ArgumentNullException(nameof(rule)));

    internal static CssBlockItemSyntax FromDeclarations(CssDeclarationSyntax[] declarations) =>
        declarations is { Length: > 0 }
            ? new(new CssDeclarationSyntaxList(declarations))
            : throw new ArgumentException("A declaration run cannot be empty.", nameof(declarations));

    internal CssBlockItemKind Kind { get; }
    internal CssRuleSyntax Rule => Kind == CssBlockItemKind.Rule ? _rule! : throw new InvalidOperationException();
    internal CssDeclarationSyntaxList Declarations => Kind == CssBlockItemKind.Declarations
        ? _declarations! : throw new InvalidOperationException();
}

internal sealed class CssBlockSyntax : IReadOnlyList<CssBlockItemSyntax>
{
    private readonly CssBlockItemSyntax[] _items;

    internal CssBlockSyntax(CssBlockItemSyntax[] items) => _items = items;

    public int Count => _items.Length;
    public CssBlockItemSyntax this[int index] => _items[index];
    public IEnumerator<CssBlockItemSyntax> GetEnumerator() =>
        ((IEnumerable<CssBlockItemSyntax>) _items).GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}

internal sealed class CssDeclarationSyntaxList : IReadOnlyList<CssDeclarationSyntax>
{
    private readonly CssDeclarationSyntax[] _items;

    internal CssDeclarationSyntaxList(CssDeclarationSyntax[] items) => _items = items;

    public int Count => _items.Length;
    public CssDeclarationSyntax this[int index] => _items[index];
    public IEnumerator<CssDeclarationSyntax> GetEnumerator() =>
        ((IEnumerable<CssDeclarationSyntax>) _items).GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
