using Jint.HtmlParser.Css.Model;
using Jint.HtmlParser.Css.Syntax;
using Jint.HtmlParser.Css.Values;
using Jint.HtmlParser.Css.Values.Properties;

namespace Jint.HtmlParser.Css;

internal readonly record struct CssParsedRule(CssRule Rule, CssRuleBody? Children, CssSourceText? Media);

// Explicit interpretation entrypoints. Results never invoke another parser from a getter.
internal static partial class CssParser
{
    internal static CssMediaList ParseMediaQueryList(string source, CssValueWork work, CssParseOptions? options = null) =>
        ParseMediaQueryList(CssSourceText.From(source), work, options);

    internal static CssMediaList ParseMediaQueryList(CssSourceText input, CssValueWork work, CssParseOptions? options = null)
    {
        using var parser = new CssSyntaxParser(input, options, work.Token, work.CheckCancellation);
        var values = parser.ParseComponentValues();
        return CssMediaList.FromComponents(input.Source, values, parser, work);
    }

    internal static CssDeclarationBlock ParseDeclarationList(string source, CssValueWork work,
        CssDeclarationContext context = CssDeclarationContext.Style, CssParseOptions? options = null) =>
        CssDeclarationBlock.Parse(source, context, options, work, work.Token);

    internal static bool HasValidSelector(CssRawRule raw, CssValueWork work, CssParseOptions? options = null)
    {
        using var parser = new CssSyntaxParser(raw.Prelude, options, work.Token, work.CheckCancellation);
        return CssStyleSheet.CompileSelector(raw.Text.Source, Header(raw, parser), options, work, work.Token) is not null;
    }

    internal static CssParsedRule? ParseRule(CssRawRule raw, CssValueWork work,
        CssStyleRule? nestingParent = null, CssParseOptions? options = null)
    {
        var name = raw.Kind == CssRuleKind.AtRule ? CssPropertyRegistry.NormalizeName(raw.Name, work) : null;
        var kind = name is null ? default : CssAtRuleLookup.Match(name);
        // CSS Syntax §3.2: an encoding declaration only looks like an at-rule; "no such rule actually exists".
        if (name == "charset") return null;
        // Same renderless nesting boundary as CssStyleSheet.BuildShallow; do this before
        // the media fast path can expose descendants without their parent selector context.
        if (nestingParent is not null && raw.Kind == CssRuleKind.AtRule &&
            kind is CssAtRuleKind.Media or CssAtRuleKind.Supports or CssAtRuleKind.Layer)
        {
            options?.Diagnostics?.Add("css/unsupported-nested-at-rule", raw.Text.Span.Start);
            work.Charge(raw.Text.Span.Length);
            return new(new CssGenericRule(raw.Text.Text, raw.Text.Span), null, null);
        }
        if (raw.Kind == CssRuleKind.AtRule && kind is not (CssAtRuleKind.Media or CssAtRuleKind.Import or
            CssAtRuleKind.FontFace or CssAtRuleKind.Supports or CssAtRuleKind.Layer))
        {
            work.Charge(raw.Text.Span.Length);
            return new(new CssGenericRule(raw.Text.Text, raw.Text.Span), null, null);
        }
        if (raw.Kind == CssRuleKind.AtRule && kind == CssAtRuleKind.Media)
        {
            if (raw.Body is not { } mediaBody) return null;
            work.CheckCancellation();
            return new(new CssMediaRule(CssMediaList.Empty(), raw.Text.Span),
                new CssRuleBody(mediaBody, CssRuleBodyKind.Group, raw.IsClosed), raw.Prelude);
        }
        // A style rule's declaration-vs-nesting grammar, and descriptor rules, demand their own
        // body. A grouping rule needs only its prelude; its body stays a raw source slice.
        var full = raw.Kind == CssRuleKind.QualifiedRule || kind is CssAtRuleKind.FontFace;
        using var parser = new CssSyntaxParser(full ? raw.Text : raw.Prelude, options, work.Token, work.CheckCancellation);
        var syntax = full ? parser.ParseRule() : Header(raw, parser);
        CssRule? rule;
        CssSourceText? media = null;
        if (raw.Kind == CssRuleKind.AtRule && kind == CssAtRuleKind.Import)
        {
            var header = CssImportRule.ParseHeader(syntax, work);
            if (header is null) return null;
            var start = header.Value.MediaIndex < syntax.Prelude.Count
                ? syntax.Prelude[header.Value.MediaIndex].Span.Start : raw.Prelude.End;
            media = raw.Prelude.Slice(start, raw.Prelude.End);
            rule = new CssImportRule(header.Value.Href, CssMediaList.Empty(), raw.Text.Span);
        }
        else
            rule = CssStyleSheet.BuildShallow(raw.Text.Source, syntax, parser, options, work, work.Token, nestingParent);
        if (rule is null) return null;
        CssRuleBody? children = null;
        if (raw.Body is { } body && rule is CssGroupingRule or CssStyleRule)
            children = new CssRuleBody(body, rule switch
            {
                CssStyleRule => CssRuleBodyKind.Style,
                _ => CssRuleBodyKind.Group
            }, raw.IsClosed);
        work.CheckCancellation();
        return new(rule, children, media);
    }

    private static CssRuleSyntax Header(CssRawRule raw, CssSyntaxParser parser)
    {
        var prelude = parser.ParseComponentValues();
        var block = raw.Body is { } body
            ? CssComponentValue.FromContainer(CssComponentKind.SimpleBlock,
                new CssSourceSpan(body.Span.Start - 1, body.Span.Length + (raw.IsClosed ? 2 : 1)),
                null, '{', new CssComponentValueList([]), raw.IsClosed)
            : (CssComponentValue?) null;
        return new CssRuleSyntax(raw.Kind, raw.Name, prelude, block, raw.Text.Span);
    }
}
