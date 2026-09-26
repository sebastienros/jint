using Jint.HtmlParser.Css;
using Jint.HtmlParser.Css.Syntax;

namespace Jint.HtmlParser;

public static partial class MarkupParser
{
    /// <summary>Parses a whole CSS stylesheet into immutable syntax with standard CSS Syntax recovery.</summary>
    /// <remarks>
    /// <para>Unknown rules and unvalidated property contents remain accessible; parsing does not compile selectors or compute styles.</para>
    /// </remarks>
    public static CssStyleSheetSyntax ParseCss(string source, CssParseOptions? options = null,
        CancellationToken cancellationToken = default) =>
        ParseCssCore(source, options, cancellationToken, checkpoint: null);

    // CSS Syntax Level 3, §5.5.1: https://drafts.csswg.org/css-syntax/#parse-stylesheet
    internal static CssStyleSheetSyntax ParseCssCore(string source, CssParseOptions? options,
        CancellationToken cancellationToken, Action? checkpoint)
    {
        var rules = new CssSyntaxParser(source, options, cancellationToken, checkpoint).ParseStyleSheet();
        var result = new CssStyleSheetSyntax(source, rules);
        cancellationToken.ThrowIfCancellationRequested();
        return result;
    }

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
