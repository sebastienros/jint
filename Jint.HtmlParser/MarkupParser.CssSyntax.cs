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
        ParseCssCore(source, options, checkpoint: null, cancellationToken);

    // CSS Syntax Level 3, §5.5.1: https://drafts.csswg.org/css-syntax/#parse-stylesheet
    internal static CssStyleSheetSyntax ParseCssCore(string source, CssParseOptions? options,
        Action? checkpoint, CancellationToken cancellationToken)
    {
        CssRuleSyntax[] rules;
        using (var parser = new CssSyntaxParser(source, options, cancellationToken, checkpoint))
        {
            rules = parser.ParseStyleSheet();
        }
        var result = new CssStyleSheetSyntax(source, rules);
        cancellationToken.ThrowIfCancellationRequested();
        return result;
    }

    public static CssRuleSyntax ParseCssRule(string source, CssParseOptions? options = null,
        CancellationToken cancellationToken = default) =>
        Parse(source, options, static parser => parser.ParseRule(), cancellationToken);

    public static CssDeclarationSyntax ParseCssDeclaration(string source, CssParseOptions? options = null,
        CancellationToken cancellationToken = default) =>
        Parse(source, options, static parser => parser.ParseDeclaration(), cancellationToken);

    public static CssComponentValue ParseCssComponentValue(string source, CssParseOptions? options = null,
        CancellationToken cancellationToken = default) =>
        Parse(source, options, static parser => parser.ParseComponentValue(), cancellationToken);

    public static CssComponentValueList ParseCssComponentValues(string source, CssParseOptions? options = null,
        CancellationToken cancellationToken = default) =>
        Parse(source, options, static parser => parser.ParseComponentValues(), cancellationToken);

    private static T Parse<T>(string source, CssParseOptions? options, Func<CssSyntaxParser, T> parse,
        CancellationToken cancellationToken)
    {
        using var parser = new CssSyntaxParser(source, options, cancellationToken);
        return parse(parser);
    }
}
