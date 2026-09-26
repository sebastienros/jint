using Jint.HtmlParser.Css.Serialization;
using Jint.HtmlParser.Css.Media;
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

    private CssStyleSheet()
    {
        Rules = new CssRuleList(_rules);
        Media = CssMediaList.Parse("");
        Media.AttachTo(this);
    }

    internal CssRuleList Rules { get; }
    internal CssMediaList Media { get; }
    internal CssStyleSheetAttachment Attachment { get; private set; } = new();
    internal CssMutationStamp Stamp => new(_version);
    internal bool Disabled
    {
        get => _disabled;
        set { if (_disabled != value) { _disabled = value; Changed(); } }
    }

    internal static CssStyleSheet Parse(string source, CssParseOptions? options = null,
        CancellationToken cancellationToken = default) =>
        Parse(source, options, new CssValueWork(cancellationToken), cancellationToken);

    internal static CssStyleSheet Parse(string source, CssParseOptions? options, CssValueWork work,
        CancellationToken cancellationToken)
    {
        var parser = new CssSyntaxParser(source, options, cancellationToken);
        var syntax = parser.ParseStyleSheet();
        var sheet = new CssStyleSheet();
        foreach (var item in syntax)
        {
            work.Charge(1);
            var rule = BuildRule(source, item, parser, options, work, cancellationToken);
            if (rule is not null) { rule.Attach(sheet, null, work); sheet._rules.Add(rule); }
        }
        work.CheckCancellation();
        return sheet;
    }

    internal void SetAttachment(CssStyleSheetAttachment attachment)
    {
        ArgumentNullException.ThrowIfNull(attachment);
        if (Attachment == attachment) return;
        Attachment = attachment;
        Changed();
    }

    internal void ReplaceText(string source, CssParseOptions? options = null,
        CancellationToken cancellationToken = default) =>
        ReplaceText(source, options, new CssValueWork(cancellationToken), cancellationToken);

    internal void ReplaceText(string source, CssParseOptions? options, CssValueWork work,
        CancellationToken cancellationToken)
    {
        var replacement = Parse(source, options, work, cancellationToken);
        foreach (var rule in replacement._rules) { work.Charge(1); rule.Attach(this, null, work); }
        _rules.EnsureCapacity(replacement._rules.Count);
        work.CheckCancellation();
        foreach (var rule in _rules) rule.Detach();
        _rules.Clear();
        _rules.AddRange(replacement._rules);
        Changed();
    }

    internal int InsertRule(string source, int index, CssParseOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        // CSSOM requires bounds to win over parse and completion failures.
        if ((uint) index > (uint) _rules.Count)
            throw new DomException("IndexSizeError", "The rule index is outside the list.");
        var rule = ParseSingle(source, options, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        rule.Attach(this, null, new CssValueWork(cancellationToken));
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
        SerializeWithRanges(new CssValueWork(cancellationToken));

    internal CssSerializationSnapshot SerializeWithRanges(CssValueWork work) =>
        CssRuleSerializer.SerializeSheet(this, work);

    internal CssStyleRule[] ApplicableStyleRules(CssMediaEnvironment environment, CssValueWork work)
    {
        work.CheckCancellation();
        if (Disabled || !Media.Matches(environment, work)) return [];
        var result = new List<CssStyleRule>();
        var frames = new Stack<(CssRuleList Rules, int Index)>();
        frames.Push((Rules, 0));
        while (frames.TryPop(out var frame))
        {
            work.Charge(1);
            if (frame.Index == frame.Rules.Count) continue;
            var rule = frame.Rules[frame.Index];
            frames.Push((frame.Rules, frame.Index + 1));
            if (rule is CssStyleRule style) result.Add(style);
            else if (rule is CssMediaRule media && media.Media.Matches(environment, work))
                frames.Push((media.Rules, 0));
        }
        work.CheckCancellation();
        var rules = result.ToArray();
        work.CheckCancellation();
        return rules;
    }

    internal void Changed() => CssMutationStamp.Advance(ref _version);

    internal static CssRule ParseSingle(string source, CssParseOptions? options, CancellationToken cancellationToken)
    {
        var parser = new CssSyntaxParser(source, options, cancellationToken);
        CssRuleSyntax syntax;
        try { syntax = parser.ParseRule(); }
        catch (CssParseException) { throw new DomException("SyntaxError", "Exactly one valid CSS rule is required."); }
        var work = new CssValueWork(cancellationToken);
        var rule = BuildRule(source, syntax, parser, options, work, cancellationToken) ??
            throw new DomException("SyntaxError", "The CSS rule is invalid or unknown.");
        work.CheckCancellation();
        return rule;
    }

    private static CssRule? BuildRule(string source, CssRuleSyntax syntax, CssSyntaxParser parser,
        CssParseOptions? options, CssValueWork work, CancellationToken cancellationToken)
    {
        var root = BuildShallow(source, syntax, parser, options, work, cancellationToken);
        if (root is not CssMediaRule group) return root;
        var pending = new Stack<(CssMediaRule Group, CssComponentValue Block)>();
        pending.Push((group, syntax.Block!.Value));
        while (pending.TryPop(out var item))
        {
            work.Charge(1);
            foreach (var entry in parser.ParseBlockContents(item.Block))
            {
                work.Charge(1);
                if (entry.Kind != CssBlockItemKind.Rule) continue;
                var child = BuildShallow(source, entry.Rule, parser, options, work, cancellationToken);
                if (child is null) continue;
                item.Group.AddProjected(child);
                if (child is CssMediaRule childGroup) pending.Push((childGroup, entry.Rule.Block!.Value));
            }
        }
        return root;
    }

    private static CssRule? BuildShallow(string source, CssRuleSyntax syntax, CssSyntaxParser parser,
        CssParseOptions? options, CssValueWork work, CancellationToken cancellationToken)
    {
        if (syntax.Kind == CssRuleKind.AtRule)
        {
            var name = CssPropertyRegistry.NormalizeName(syntax.Name, work);
            if (name == "media")
                return syntax.Block is null ? null : new CssMediaRule(CssMediaList.FromComponents(source, syntax.Prelude, parser, work), syntax.Span);
            var group = name switch
            {
                "import" or "namespace" => "R1",
                "supports" or "container" or "scope" or "starting-style" or "layer" => "R2",
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
        var text = SelectorText(source, syntax.Prelude, parser, work);
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
                if (CssAscii.EqualsIgnoreCase(item.Rule.Name, "media"))
                    throw new CssIncompleteRuleGrammarException("nested-media", "C2:nesting-selector-context", item.Rule.Span);
                BuildShallow(source, item.Rule, parser, options, work, cancellationToken);
                continue;
            }
            foreach (var declaration in item.Declarations) { work.Charge(1); declarations.Add(declaration); }
        }
        var style = CssDeclarationBlock.FromDeclarations(source, declarations, CssDeclarationContext.Style,
            options?.Limits.MaxNestingDepth ?? 0, work);
        return new CssStyleRule(selector, text, style, syntax.Span);
    }

    internal static string SelectorText(string source, CssComponentValueList values, CssSyntaxParser parser, CssValueWork work)
    {
        var first = 0;
        var last = values.Count - 1;
        while (first <= last && Whitespace(values[first])) { work.Charge(1); first++; }
        while (last >= first && Whitespace(values[last])) { work.Charge(1); last--; }
        if (first > last) return string.Empty;
        var start = values[first].Span.Start;
        var end = values[last].Span.Start + values[last].Span.Length;
        work.CheckCancellation();
        var termination = parser.ValueTermination(values, new CssSourceSpan(start, end - start), work);
        var text = string.Concat(source.AsSpan(start, end - start), termination.AsSpan());
        work.Charge(text.Length);
        work.CheckCancellation();
        return text;
    }

    private static bool Whitespace(CssComponentValue value) =>
        value.Kind == CssComponentKind.Token && value.Token.Kind == CssTokenKind.Whitespace;
}
