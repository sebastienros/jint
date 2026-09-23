using Jint.HtmlParser.Css;
using Jint.HtmlParser.Css.Syntax;

namespace Jint.HtmlParser;

public static partial class MarkupParser
{
    public static CssRuleSyntax ParseCssRule(string source, CssParseOptions? options = null,
        CancellationToken cancellationToken = default) =>
        new CssSyntaxParser(source, options, cancellationToken).ParseRule();

    public static CssDeclarationSyntax ParseCssDeclaration(string source, CssParseOptions? options = null,
        CancellationToken cancellationToken = default) =>
        new CssSyntaxParser(source, options, cancellationToken).ParseDeclaration();

    public static CssComponentValue ParseCssComponentValue(string source, CssParseOptions? options = null,
        CancellationToken cancellationToken = default) =>
        new CssSyntaxParser(source, options, cancellationToken).ParseComponentValue();

    public static CssComponentValueList ParseCssComponentValues(string source, CssParseOptions? options = null,
        CancellationToken cancellationToken = default) =>
        new CssSyntaxParser(source, options, cancellationToken).ParseComponentValues();
}
