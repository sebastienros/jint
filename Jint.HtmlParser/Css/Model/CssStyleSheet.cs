using Jint.HtmlParser.Css.Serialization;
using Jint.HtmlParser.Css.Conditions;
using Jint.HtmlParser.Css.Media;
using Jint.HtmlParser.Css.Model.Syntax;
using Jint.HtmlParser.Css.Selectors;
using Jint.HtmlParser.Css.Syntax;
using Jint.HtmlParser.Css.Values;
using Jint.HtmlParser.Css.Values.Properties;

namespace Jint.HtmlParser.Css.Model;

// CSSOM §6.1.2 and §6.4.3. Storage and explicit producers, independent of browser parse caching.
internal sealed class CssStyleSheet
{
    private readonly List<CssRule> _rules = new();
    private ulong _version;
    private bool _disabled;

    internal CssStyleSheet()
    {
        Rules = new CssRuleList(_rules);
        Media = CssMediaList.Parse("");
        Media.AttachTo(this);
    }

    internal CssRuleList Rules { get; }
    internal CssMediaList Media { get; private set; }
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
        using var parser = new CssSyntaxParser(source, options, cancellationToken, work.CheckCancellation);
        var syntax = parser.ParseStyleSheet();
        var sheet = new CssStyleSheet();
        var importsAllowed = true;
        var hasImport = false;
        foreach (var item in syntax)
        {
            work.Charge(1);
            if (!importsAllowed && item.Kind == CssRuleKind.AtRule && CssAscii.EqualsIgnoreCase(item.Name, "import")) continue;
            var rule = BuildRule(source, item, parser, options, work, cancellationToken);
            if (rule is CssImportRule) hasImport = true;
            else if (rule is not null && (rule is not CssLayerStatementRule || hasImport)) importsAllowed = false;
            if (rule is not null) { rule.Attach(sheet, null, work); sheet._rules.Add(rule); }
        }
        work.CheckCancellation();
        return sheet;
    }

    internal void SetAttachment(CssStyleSheetAttachment attachment)
    {
        ArgumentNullException.ThrowIfNull(attachment);
        if (!ReferenceEquals(Attachment.ImportOwner, attachment.ImportOwner))
            throw new InvalidOperationException("Import ownership is assigned only when publishing the child.");
        if (Attachment.ImportOwner is not null && attachment.OwnerNode is not null)
            throw new InvalidOperationException("An imported sheet cannot have an owner node.");
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
        Replace(replacement, work);
    }

    internal void Replace(CssStyleSheet replacement, CssValueWork work)
    {
        var detachments = new List<CssRule.Detachment>();
        foreach (var rule in _rules)
        {
            work.Charge(1);
            detachments.Add(rule.PrepareDetach(work));
        }
        foreach (var rule in replacement._rules) { work.Charge(1); rule.Attach(this, null, work); }
        _rules.EnsureCapacity(replacement._rules.Count);
        work.CheckCancellation();
        foreach (var detachment in detachments) detachment.Commit();
        _rules.Clear();
        _rules.AddRange(replacement._rules);
        Rules.Changed();
        Changed();
    }

    internal int InsertRule(string source, int index, CssParseOptions? options = null,
        CancellationToken cancellationToken = default)
        => InsertRule(source, index, options, new CssValueWork(cancellationToken), cancellationToken);

    internal int InsertRule(string source, int index, CssParseOptions? options, CssValueWork work,
        CancellationToken cancellationToken)
    {
        // CSSOM requires bounds to win over parse and completion failures.
        if ((uint) index > (uint) _rules.Count)
            throw new DomException("IndexSizeError", "The rule index is outside the list.");
        var rule = ParseSingle(source, options, work, cancellationToken);
        // CSSOM insert a CSS rule: parse precedes hierarchy validation.
        var importsAllowed = true;
        var hasImport = false;
        for (var i = 0; i <= _rules.Count; i++)
        {
            work.Charge(1);
            var next = i == index ? rule : _rules[i < index ? i : i - 1];
            if (next is CssImportRule)
            {
                if (!importsAllowed) throw new DomException("HierarchyRequestError", "Imports must precede other rules.");
                hasImport = true;
            }
            else if (next is not CssLayerStatementRule || hasImport) importsAllowed = false;
        }
        rule.Attach(this, null, work);
        work.CheckCancellation();
        _rules.Insert(index, rule);
        Rules.Changed();
        Changed();
        return index;
    }

    internal void DeleteRule(int index, CssValueWork? work = null)
    {
        if ((uint) index >= (uint) _rules.Count)
            throw new DomException("IndexSizeError", "The rule index is outside the list.");
        var rule = _rules[index];
        rule.Detach(work);
        _rules.RemoveAt(index);
        Rules.Changed();
        Changed();
    }

    internal string Serialize() => SerializeWithRanges().Text;
    internal CssSerializationSnapshot SerializeWithRanges(CancellationToken cancellationToken = default) =>
        SerializeWithRanges(new CssValueWork(cancellationToken));

    internal CssSerializationSnapshot SerializeWithRanges(CssValueWork work) =>
        CssRuleSerializer.SerializeSheet(this, work);

    // Enumerate all revisions, including disabled or nonmatching children, without materializing values.
    internal CssStyleSheet[] ImportedStyleSheets(CssValueWork work)
    {
        work.CheckCancellation();
        var result = new List<CssStyleSheet>();
        var seen = new HashSet<CssStyleSheet>(ReferenceEqualityComparer.Instance);
        var pending = new Stack<CssStyleSheet>();
        pending.Push(this);
        while (pending.TryPop(out var sheet))
        {
            work.Charge(1);
            if (!seen.Add(sheet)) continue;
            result.Add(sheet);
            foreach (var rule in sheet.Rules)
            {
                work.Charge(1);
                if (rule is CssImportRule { StyleSheet: { } child }) pending.Push(child);
            }
        }
        work.Charge(result.Count);
        work.CheckCancellation();
        var sheets = result.ToArray();
        work.CheckCancellation();
        return sheets;
    }

    // Imported sheets inherit the root sheet's tree scope, despite having no owner node themselves.
    internal Node? EffectiveOwnerNode(CssValueWork work)
    {
        work.CheckCancellation();
        var seen = new HashSet<CssStyleSheet>(ReferenceEqualityComparer.Instance);
        CssStyleSheet? sheet = this;
        while (sheet is not null && seen.Add(sheet))
        {
            work.Charge(1);
            if (sheet.Attachment.OwnerNode is { } owner) { work.CheckCancellation(); return owner; }
            sheet = sheet.Attachment.ImportOwner?.ParentStyleSheet;
        }
        work.CheckCancellation();
        return null;
    }

    internal CssStyleRule[] ApplicableStyleRules(CssMediaEnvironment environment, CssValueWork work)
    {
        var styles = new List<CssStyleRule>();
        foreach (var rule in ApplicableRules(environment, work))
        {
            work.Charge(1);
            if (rule is CssStyleRule style) styles.Add(style);
        }
        work.CheckCancellation();
        return styles.ToArray();
    }

    internal CssRule[] ApplicableRules(CssMediaEnvironment environment, CssValueWork work)
    {
        work.CheckCancellation();
        if (Disabled || !Media.Matches(environment, work)) return [];
        var result = new List<CssRule>();
        var active = new HashSet<CssStyleSheet>(ReferenceEqualityComparer.Instance) { this };
        var frames = new Stack<(CssRuleList Rules, int Index, CssStyleSheet? Sheet)>();
        frames.Push((Rules, 0, this));
        while (frames.TryPop(out var frame))
        {
            work.Charge(1);
            if (frame.Index == frame.Rules.Count)
            {
                if (frame.Sheet is { } finished) active.Remove(finished);
                continue;
            }
            var rule = frame.Rules[frame.Index];
            frames.Push((frame.Rules, frame.Index + 1, frame.Sheet));
            result.Add(rule);
            if (rule is CssStyleRule style)
            {
                frames.Push((style.Rules, 0, null));
            }
            else if (rule is CssLayerBlockRule layer)
                frames.Push((layer.Rules, 0, null));
            else if (rule is CssMediaRule media && media.Media.Matches(environment, work))
                frames.Push((media.Rules, 0, null));
            else if (rule is CssSupportsRule { Matches: true } supports)
                frames.Push((supports.Rules, 0, null));
            else if (rule is CssImportRule { StyleSheet: { } child } && !child.Disabled &&
                child.Media.Matches(environment, work) && active.Add(child))
                frames.Push((child.Rules, 0, child));
        }
        work.CheckCancellation();
        var rules = result.ToArray();
        work.CheckCancellation();
        return rules;
    }

    internal ImportAttachment PrepareImportAttachment(CssImportRule owner, CssMediaList media, Uri? sourceUrl, Uri? baseUrl)
    {
        if (Attachment.ImportOwner is not null || Attachment.OwnerNode is not null ||
            owner.StyleSheet is not null || !ReferenceEquals(owner.Media, media))
            throw new InvalidOperationException("The child must be published by an unassociated import owner.");
        return new ImportAttachment(this, media,
            new CssStyleSheetAttachment { ImportOwner = owner, SourceUrl = sourceUrl, BaseUrl = baseUrl });
    }

    // Prepared ownership record; caller verifies revisions after its last checkpoint.
    internal sealed class ImportAttachment(CssStyleSheet sheet, CssMediaList media, CssStyleSheetAttachment attachment)
    {
        internal void Commit()
        {
            sheet.Attachment = attachment;
            sheet.Media = media;
            media.AttachTo(sheet);
            sheet.Changed();
        }
    }

    internal void Changed() => CssMutationStamp.Advance(ref _version);

    internal static CssRule ParseSingle(string source, CssParseOptions? options, CancellationToken cancellationToken)
        => ParseSingle(source, options, new CssValueWork(cancellationToken), cancellationToken);

    internal static CssRule ParseSingle(string source, CssParseOptions? options, CssValueWork work,
        CancellationToken cancellationToken)
    {
        using var parser = new CssSyntaxParser(source, options, cancellationToken, work.CheckCancellation);
        CssRuleSyntax syntax;
        try { syntax = parser.ParseRule(); }
        catch (CssParseException) { throw new DomException("SyntaxError", "Exactly one valid CSS rule is required."); }
        var rule = BuildRule(source, syntax, parser, options, work, cancellationToken) ??
            throw new DomException("SyntaxError", "The CSS rule is invalid or unknown.");
        work.CheckCancellation();
        return rule;
    }

    private static CssRule? BuildRule(string source, CssRuleSyntax syntax, CssSyntaxParser parser,
        CssParseOptions? options, CssValueWork work, CancellationToken cancellationToken)
    {
        var root = BuildShallow(source, syntax, parser, options, work, cancellationToken, out var rootBody);
        if (root is not (CssGroupingRule or CssStyleRule) || syntax.Block is not { } rootBlock) return root;
        // A style rule's body was already parsed for its declarations; grouping rules parse theirs here.
        Stack<PendingBlock>? pending = null;
        var item = new PendingBlock(root, rootBlock, rootBody);
        while (true)
        {
            work.Charge(1);
            foreach (var entry in item.Body ?? parser.ParseBlockContents(item.Block))
            {
                work.Charge(1);
                if (entry.Kind != CssBlockItemKind.Rule) continue;
                if (entry.Rule.Kind == CssRuleKind.AtRule && CssAscii.EqualsIgnoreCase(entry.Rule.Name, "import")) continue;
                var child = BuildShallow(source, entry.Rule, parser, options, work, cancellationToken,
                    out var childBody, item.Owner as CssStyleRule);
                if (child is null) continue;
                if (item.Owner is CssGroupingRule group) group.AddProjected(child);
                else ((CssStyleRule) item.Owner).AddProjected(child);
                if (child is CssGroupingRule or CssStyleRule && entry.Rule.Block is { } childBlock)
                    (pending ??= new()).Push(new(child, childBlock, childBody));
            }
            if (pending is null || !pending.TryPop(out item)) return root;
        }
    }

    private readonly record struct PendingBlock(CssRule Owner, CssComponentValue Block, CssBlockSyntax? Body);

    internal static CssRule? BuildShallow(string source, CssRuleSyntax syntax, CssSyntaxParser parser,
        CssParseOptions? options, CssValueWork work, CancellationToken cancellationToken,
        CssStyleRule? nestingParent = null) =>
        BuildShallow(source, syntax, parser, options, work, cancellationToken, out _, nestingParent);

    private static CssRule? BuildShallow(string source, CssRuleSyntax syntax, CssSyntaxParser parser,
        CssParseOptions? options, CssValueWork work, CancellationToken cancellationToken,
        out CssBlockSyntax? body, CssStyleRule? nestingParent = null)
    {
        body = null;
        if (syntax.Kind == CssRuleKind.AtRule)
        {
            var name = CssPropertyRegistry.NormalizeName(syntax.Name, work);
            var kind = CssAtRuleLookup.Match(name);
            switch (kind)
            {
                case CssAtRuleKind.Import:
                    return CssImportRule.Parse(source, syntax, parser, work);
                case CssAtRuleKind.Layer:
                    {
                        if (nestingParent is not null)
                            throw new CssIncompleteRuleGrammarException("nested-layer", "C2:nesting-selector-context", syntax.Span);
                        var names = CssLayerName.Parse(syntax.Prelude, work);
                        if (names is null) return null;
                        if (syntax.Block is null)
                            return names.Length == 0 ? null : new CssLayerStatementRule(names, syntax.Span);
                        return names.Length switch
                        {
                            0 => new CssLayerBlockRule(null, syntax.Span),
                            1 => new CssLayerBlockRule(names[0], syntax.Span),
                            _ => null
                        };
                    }
                case CssAtRuleKind.FontFace:
                    {
                        if (syntax.Block is not { } descriptorBlock || nestingParent is not null) return null;
                        foreach (var value in syntax.Prelude)
                        {
                            work.Charge(1);
                            if (value.Kind != CssComponentKind.Token || value.Token.Kind != CssTokenKind.Whitespace) return null;
                        }
                        var descriptors = new List<CssDeclarationSyntax>();
                        foreach (var item in parser.ParseBlockContents(descriptorBlock))
                        {
                            work.Charge(1);
                            if (item.Kind == CssBlockItemKind.Declarations)
                                foreach (var declaration in item.Declarations) { work.Charge(1); descriptors.Add(declaration); }
                        }
                        return new CssFontFaceRule(CssDeclarationBlock.FromDeclarations(source, descriptors,
                            CssDeclarationContext.FontFace, options?.Limits.MaxNestingDepth ?? 0, work), syntax.Span);
                    }
                case CssAtRuleKind.Media:
                    return syntax.Block is null ? null : new CssMediaRule(CssMediaList.FromComponents(source, syntax.Prelude, parser, work), syntax.Span);
                case CssAtRuleKind.Supports:
                    {
                        if (syntax.Block is null || !CssSupports.TryParseCondition(source, syntax.Prelude, options, work, out var matches))
                            return null;
                        var condition = SelectorText(source, syntax.Prelude, parser, work);
                        return new CssSupportsRule(condition, matches, syntax.Span);
                    }
            }
            work.Charge(syntax.Span.Length);
            return new CssGenericRule(source.Substring(syntax.Span.Start, syntax.Span.Length), syntax.Span);
        }
        if (syntax.Block is not { } block) return null;
        var selector = CompileSelector(source, syntax, options, work, cancellationToken, nestingParent);
        if (selector is null) return null;
        var text = SelectorText(source, syntax.Prelude, parser, work);
        body = parser.ParseBlockContents(block);
        var declarations = new List<CssDeclarationSyntax>();
        var afterNestedRule = false;
        foreach (var item in body)
        {
            work.Charge(1);
            if (item.Kind == CssBlockItemKind.Rule)
            {
                if (item.Rule.Kind == CssRuleKind.QualifiedRule)
                {
                    afterNestedRule = true;
                    continue;
                }
                // Unknown at-rules recover; known nested grammars must remain completion blockers.
                if (CssAscii.EqualsIgnoreCase(item.Rule.Name, "media") || CssAscii.EqualsIgnoreCase(item.Rule.Name, "supports") ||
                    CssAscii.EqualsIgnoreCase(item.Rule.Name, "layer"))
                    throw new CssIncompleteRuleGrammarException("nested-" + item.Rule.Name, "C2:nesting-selector-context", item.Rule.Span);
                if (!CssAscii.EqualsIgnoreCase(item.Rule.Name, "import") && !CssAscii.EqualsIgnoreCase(item.Rule.Name, "property"))
                    BuildShallow(source, item.Rule, parser, options, work, cancellationToken);
                continue;
            }
            if (afterNestedRule)
                throw new CssIncompleteRuleGrammarException("nested-declarations", "C2:interleaved-declarations", syntax.Span);
            foreach (var declaration in item.Declarations) { work.Charge(1); declarations.Add(declaration); }
        }
        var style = CssDeclarationBlock.FromDeclarations(source, declarations, CssDeclarationContext.Style,
            options?.Limits.MaxNestingDepth ?? 0, work);
        return new CssStyleRule(selector, text, style, syntax.Span, options?.Limits, nestingParent);
    }

    internal static CompiledSelector? CompileSelector(string source, CssRuleSyntax syntax,
        CssParseOptions? options, CssValueWork work, CancellationToken cancellationToken,
        CssStyleRule? nestingParent = null)
    {
        try
        {
            return new SelectorCompiler.Worker(source,
                new SelectorParseContext(limits: options?.Limits, nestingParent: nestingParent?.Selector),
                cancellationToken, work.CheckCancellation).Compile(syntax.Prelude);
        }
        catch (SelectorParseException) { return null; }
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
