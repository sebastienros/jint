using Jint.HtmlParser.Css.Serialization;
using Jint.HtmlParser.Css.Syntax;

namespace Jint.HtmlParser.Css.Model.Syntax;

// CSS Syntax Level 3, §5.4.6: https://drafts.csswg.org/css-syntax/#parse-a-rule
internal sealed class CssSyntaxRule
{
    private CssRuleSyntax _syntax;
    private CssSyntaxStyleSheet? _parentStyleSheet;
    private ulong _version;

    internal CssSyntaxRule(CssRuleSyntax syntax, CssSyntaxStyleSheet parentStyleSheet)
    {
        _syntax = syntax;
        _parentStyleSheet = parentStyleSheet;
    }

    internal CssRuleSyntax Syntax => _syntax;
    internal CssSyntaxStyleSheet? ParentStyleSheet => _parentStyleSheet;
    internal CssMutationStamp Stamp => new(_version);

    internal void ReplaceSyntax(string source, CssParseOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var syntax = new CssSyntaxParser(source, options, cancellationToken).ParseRule();
        cancellationToken.ThrowIfCancellationRequested();
        _syntax = syntax;
        CssMutationStamp.Advance(ref _version);
        _parentStyleSheet?.RuleChanged();
    }

    internal string Serialize() => CssSyntaxSerializer.SerializeRule(_syntax);

    internal void Detach() => _parentStyleSheet = null;
}
