using Jint.HtmlParser.Css.Serialization;
using Jint.HtmlParser.Css.Syntax;

namespace Jint.HtmlParser.Css.Model.Syntax;

// CSS Syntax Level 3, §5.5.6: https://drafts.csswg.org/css-syntax/#consume-a-declaration
internal sealed class CssSyntaxDeclarationBlock
{
    private List<CssDeclarationSyntax> _declarations = new();
    private readonly CssSyntaxListView<CssDeclarationSyntax> _view;
    private ulong _version;

    private CssSyntaxDeclarationBlock() => _view = new CssSyntaxListView<CssDeclarationSyntax>(_declarations);

    internal static CssSyntaxDeclarationBlock Parse(string source, CssParseOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        using var parsedParser = new CssSyntaxParser(source, options, cancellationToken);
        var parsed = parsedParser.ParseDeclarationList();
        var block = new CssSyntaxDeclarationBlock();
        block._declarations.AddRange(parsed);
        cancellationToken.ThrowIfCancellationRequested();
        return block;
    }

    internal IReadOnlyList<CssDeclarationSyntax> Declarations => _view;
    internal CssMutationStamp Stamp => new(_version);

    internal void InsertDeclaration(string source, int index, CssParseOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        if ((uint) index > (uint) _declarations.Count) throw new ArgumentOutOfRangeException(nameof(index));
        using var declarationParser = new CssSyntaxParser(source, options, cancellationToken);
        var declaration = declarationParser.ParseDeclaration();
        cancellationToken.ThrowIfCancellationRequested();
        _declarations.Insert(index, declaration);
        CssMutationStamp.Advance(ref _version);
    }

    internal void ReplaceDeclaration(string source, int index, CssParseOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        if ((uint) index >= (uint) _declarations.Count) throw new ArgumentOutOfRangeException(nameof(index));
        using var declarationParser = new CssSyntaxParser(source, options, cancellationToken);
        var declaration = declarationParser.ParseDeclaration();
        cancellationToken.ThrowIfCancellationRequested();
        _declarations[index] = declaration;
        CssMutationStamp.Advance(ref _version);
    }

    internal void DeleteDeclaration(int index)
    {
        if ((uint) index >= (uint) _declarations.Count) throw new ArgumentOutOfRangeException(nameof(index));
        _declarations.RemoveAt(index);
        CssMutationStamp.Advance(ref _version);
    }

    internal void ReplaceText(string source, CssParseOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        using var declarationsParser = new CssSyntaxParser(source, options, cancellationToken);
        var declarations = declarationsParser.ParseDeclarationList();
        var replacement = new List<CssDeclarationSyntax>(declarations);
        cancellationToken.ThrowIfCancellationRequested();
        _declarations = replacement;
        _view.ReplaceItems(replacement);
        CssMutationStamp.Advance(ref _version);
    }

    internal string Serialize() => CssSyntaxSerializer.SerializeDeclarationList(_view);
}
