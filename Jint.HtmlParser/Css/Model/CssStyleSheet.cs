using Jint.HtmlParser.Css.Serialization;
using Jint.HtmlParser.Css.Model.Syntax;
using Jint.HtmlParser.Css.Selectors;
using Jint.HtmlParser.Css.Syntax;
using Jint.HtmlParser.Css.Values;
using Jint.HtmlParser.Css.Values.Properties;

namespace Jint.HtmlParser.Css.Model;

// CSSOM §6.1.2 and §6.4.3. This is a validated producer, independent of the syntax editors.
internal sealed class CssStyleSheet
{
    private readonly List<CssRule> _rules = new();
    private ulong _version;
    private bool _disabled;

    private CssStyleSheet() => Rules = new CssRuleList(_rules);

    internal CssRuleList Rules { get; }
    internal CssMutationStamp Stamp => new(_version);
    internal bool Disabled
    {
        get => _disabled;
        set { if (_disabled != value) { _disabled = value; Changed(); } }
    }

    internal static CssStyleSheet Parse(string source, CssParseOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var parser = new CssSyntaxParser(source, options, cancellationToken);
        var syntax = parser.ParseStyleSheet();
        var work = new CssValueWork(cancellationToken);
        var sheet = new CssStyleSheet();
        foreach (var item in syntax)
        {
            work.Charge(1);
            var rule = BuildRule(source, item, parser, options, work, cancellationToken);
            if (rule is not null) { rule.Attach(sheet, null); sheet._rules.Add(rule); }
        }
        work.CheckCancellation();
        return sheet;
    }

    internal int InsertRule(string source, int index, CssParseOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        // CSSOM requires bounds to win over parse and completion failures.
        if ((uint) index > (uint) _rules.Count)
            throw new DomException("IndexSizeError", "The rule index is outside the list.");
        var parser = new CssSyntaxParser(source, options, cancellationToken);
        CssRuleSyntax syntax;
        try { syntax = parser.ParseRule(); }
        catch (CssParseException) { throw new DomException("SyntaxError", "Exactly one valid CSS rule is required."); }
        var work = new CssValueWork(cancellationToken);
        var rule = BuildRule(source, syntax, parser, options, work, cancellationToken) ??
            throw new DomException("SyntaxError", "The CSS rule is invalid or unknown.");
        work.CheckCancellation();
        rule.Attach(this, null);
        _rules.Insert(index, rule);
        Changed();
        return index;
    }

    internal void DeleteRule(int index)
    {
        if ((uint) index >= (uint) _rules.Count)
            throw new DomException("IndexSizeError", "The rule index is outside the list.");
        var rule = _rules[index];
        _rules.RemoveAt(index);
        rule.Detach();
        Changed();
    }

    internal string Serialize() => SerializeWithRanges().Text;
    internal CssSerializationSnapshot SerializeWithRanges(CancellationToken cancellationToken = default) =>
        CssRuleSerializer.SerializeSheet(this, new CssValueWork(cancellationToken));

    internal void Changed() => CssMutationStamp.Advance(ref _version);

    private static CssStyleRule? BuildRule(string source, CssRuleSyntax syntax, CssSyntaxParser parser,
        CssParseOptions? options, CssValueWork work, CancellationToken cancellationToken)
    {
        if (syntax.Kind == CssRuleKind.AtRule)
        {
            var name = CssPropertyRegistry.NormalizeName(syntax.Name, work);
            var group = name switch
            {
                "import" or "namespace" or "charset" => "R1",
                "media" or "supports" or "container" or "scope" or "starting-style" or "layer" => "R2",
                "keyframes" => "R3",
                "font-face" or "font-feature-values" or "font-palette-values" => "R4",
                "page" or "counter-style" => "R5",
                "property" or "view-transition" or "position-try" or "color-profile" => "R6",
                "document" or "viewport" => "R7",
                _ => null
            };
            if (group is not null) throw new CssIncompleteRuleGrammarException(name, group + ":" + name, syntax.Span);
            return null;
        }
        if (syntax.Block is not { } block) return null;
        CompiledSelector selector;
        try
        {
            selector = new SelectorCompiler.Worker(source,
                new SelectorParseContext(limits: options?.Limits), cancellationToken).Compile(syntax.Prelude);
        }
        catch (SelectorParseException) { return null; }
        var text = SelectorText(source, syntax.Prelude, work);
        var body = parser.ParseBlockContents(block);
        var declarations = new List<CssDeclarationSyntax>();
        foreach (var item in body)
        {
            work.Charge(1);
            if (item.Kind == CssBlockItemKind.Rule)
            {
                if (item.Rule.Kind == CssRuleKind.QualifiedRule)
                    throw new CssIncompleteRuleGrammarException("nested-style", "C2:nesting-selector-context", item.Rule.Span);
                // Unknown at-rules recover; known nested grammars must remain completion blockers.
                BuildRule(source, item.Rule, parser, options, work, cancellationToken);
                continue;
            }
            foreach (var declaration in item.Declarations) { work.Charge(1); declarations.Add(declaration); }
        }
        var style = CssDeclarationBlock.FromDeclarations(source, declarations, CssDeclarationContext.Style,
            options?.Limits.MaxNestingDepth ?? 0, work);
        return new CssStyleRule(selector, text, style, syntax.Span);
    }

    internal static string SelectorText(string source, CssComponentValueList values, CssValueWork work)
    {
        var first = 0;
        var last = values.Count - 1;
        while (first <= last && Whitespace(values[first])) { work.Charge(1); first++; }
        while (last >= first && Whitespace(values[last])) { work.Charge(1); last--; }
        if (first > last) return string.Empty;
        var start = values[first].Span.Start;
        var end = values[last].Span.Start + values[last].Span.Length;
        work.CheckCancellation();
        var text = source.Substring(start, end - start);
        work.Charge(text.Length);
        work.CheckCancellation();
        return text;
    }

    private static bool Whitespace(CssComponentValue value) =>
        value.Kind == CssComponentKind.Token && value.Token.Kind == CssTokenKind.Whitespace;
}
