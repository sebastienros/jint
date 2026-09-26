using Jint.HtmlParser.Css.Syntax;
using Jint.HtmlParser.Css.Values;
using Jint.HtmlParser.Css.Values.Properties;

namespace Jint.HtmlParser.Css.Model;

// CSS Animations 1 §3, finite classic grammar. No selector compiler or typed value parser.
// https://drafts.csswg.org/css-animations-1/#keyframes
internal static class CssKeyframeParser
{
    internal static bool ReservedName(string name) =>
        CssAscii.EqualsIgnoreCase(name, "none") || CssAscii.EqualsIgnoreCase(name, "default") ||
        CssWideKeywords.Recognize(name.AsSpan()) != CssWideKeyword.None;

    internal static string? Name(CssComponentValueList values, CssValueWork work)
    {
        CssToken? name = null;
        foreach (var value in values)
        {
            work.Charge(1);
            if (value.Kind != CssComponentKind.Token) return null;
            var token = value.Token;
            if (token.Kind == CssTokenKind.Whitespace) continue;
            if (name is not null || token.Kind is not (CssTokenKind.String or CssTokenKind.Ident)) return null;
            if (token.Kind == CssTokenKind.Ident && ReservedName(token.Text)) return null;
            work.Charge(token.Text.Length);
            name = token;
        }
        work.CheckCancellation();
        return name?.Text;
    }

    internal static CssKeyframeRule? ParseSingle(string source, CssParseOptions? options, CssValueWork work)
    {
        var parser = new CssSyntaxParser(source, options, work.Token, work.CheckCancellation);
        CssRuleSyntax syntax;
        try { syntax = parser.ParseRule(); }
        catch (CssParseException) { return null; }
        // Invalid append syntax is a no-op, including selectors outside the classic grammar.
        return Build(source, syntax, parser, options, work, reportPending: false);
    }

    internal static CssKeyframeRule? Build(string source, CssRuleSyntax syntax, CssSyntaxParser parser,
        CssParseOptions? options, CssValueWork work, bool reportPending = true)
    {
        if (syntax.Kind != CssRuleKind.QualifiedRule || syntax.Block is not { } block) return null;
        var keys = CssKeyframeKeys.FromComponents(syntax.Prelude, work, out var timeline);
        if (keys is null)
        {
            if (reportPending && timeline)
                throw new CssIncompleteRuleGrammarException("keyframes-timeline-range-selectors",
                    "R3:keyframes-timeline-range-selectors", syntax.Span);
            return null;
        }
        var declarations = new List<CssDeclarationSyntax>();
        foreach (var item in parser.ParseBlockContents(block))
        {
            work.Charge(1);
            if (item.Kind != CssBlockItemKind.Declarations) continue;
            foreach (var declaration in item.Declarations)
            {
                work.Charge(1);
                if (!declaration.IsImportant) declarations.Add(declaration);
            }
        }
        var style = CssDeclarationBlock.FromDeclarations(source, declarations, CssDeclarationContext.Keyframe,
            options?.Limits.MaxNestingDepth ?? 0, work);
        work.CheckCancellation();
        return new CssKeyframeRule(keys, style, syntax.Span);
    }
}
