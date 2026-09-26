using System.Collections;
using Jint.HtmlParser.Css.Serialization;
using Jint.HtmlParser.Css.Syntax;
using Jint.HtmlParser.Css.Values;
using Jint.HtmlParser.Css.Model.Syntax;
using Jint.HtmlParser.Css.Selectors;

namespace Jint.HtmlParser.Css.Model;

internal enum CssRuleType { Style = 1, Import = 3, Media = 4, Keyframes = 7, Keyframe = 8, Supports = 12 }

// CSSOM §6.4: exposed parent links and attachment ownership are deliberately separate.
internal abstract class CssRule
{
    private ulong _version;
    private CssRule? _attachmentParent;
    private CssStyleSheet? _attachmentSheet;

    protected CssRule(CssSourceSpan sourceSpan) => SourceSpan = sourceSpan;

    internal abstract CssRuleType Type { get; }
    internal virtual CssRuleList Rules => CssRuleList.Empty;
    internal CssSourceSpan SourceSpan { get; }
    internal CssRule? ParentRule { get; private set; }
    internal CssStyleSheet? ParentStyleSheet { get; private set; }
    internal CssMutationStamp Stamp => new(_version);
    internal string CssText => CssRuleSerializer.Serialize(this);

    internal void Attach(CssStyleSheet? sheet, CssRule? parent, CssValueWork work)
    {
        var pending = new Stack<(CssRule Rule, CssRule? Parent)>();
        pending.Push((this, parent));
        while (pending.TryPop(out var item))
        {
            work.Charge(1);
            var rule = item.Rule;
            rule.ParentStyleSheet = sheet;
            rule.ParentRule = item.Parent;
            rule._attachmentSheet = item.Parent is null ? sheet : null;
            rule._attachmentParent = item.Parent;
            var children = rule.Rules;
            foreach (var child in children) { work.Charge(1); pending.Push((child, rule)); }
        }
        work.CheckCancellation();
    }

    internal void Detach(CssValueWork? work = null) => PrepareDetach(work ?? new CssValueWork(default)).Commit();

    internal Detachment PrepareDetach(CssValueWork work)
    {
        var descendants = new List<CssRule>();
        var pending = new Stack<CssRule>();
        pending.Push(this);
        while (pending.TryPop(out var rule))
        {
            work.Charge(1);
            descendants.Add(rule);
            var children = rule.Rules;
            foreach (var child in children) { work.Charge(1); pending.Push(child); }
        }
        work.Charge(descendants.Count);
        var detachment = new Detachment(this);
        work.CheckCancellation();
        return detachment;
    }

    // All traversal and allocation precede publication. Commit contains no callbacks or allocation.
    internal sealed class Detachment(CssRule root)
    {
        internal void Commit()
        {
            // CSSOM remove a CSS rule, step 6: only the removed root loses exposed links.
            // Descendants keep historical sheet/parent links; the attachment chain stops here.
            root.ParentStyleSheet = null;
            root.ParentRule = null;
            root._attachmentSheet = null;
            root._attachmentParent = null;
        }
    }

    protected void AdvanceStamp() => CssMutationStamp.Advance(ref _version);

    internal void Changed()
    {
        CssRule? current = this;
        while (current is not null)
        {
            current.AdvanceStamp();
            current._attachmentSheet?.Changed();
            current = current._attachmentParent;
        }
    }
}

internal sealed class CssStyleRule : CssRule
{
    private readonly List<CssRule> _rules = new();
    private readonly ParseLimits? _limits;
    private readonly CssStyleRule? _nestingParent;
    private CompiledSelector _selector;
    private string _selectorText;

    internal CssStyleRule(CompiledSelector selector, string selectorText, CssDeclarationBlock style, CssSourceSpan span,
        ParseLimits? limits = null, CssStyleRule? nestingParent = null)
        : base(span)
    {
        _selector = selector;
        _selectorText = selectorText;
        Style = style;
        _limits = limits;
        _nestingParent = nestingParent;
        Rules = new CssRuleList(_rules);
        style.AttachTo(this);
    }

    internal override CssRuleType Type => CssRuleType.Style;
    internal CssDeclarationBlock Style { get; }
    internal override CssRuleList Rules { get; }
    internal void AddProjected(CssRule rule) => _rules.Add(rule);
    internal CompiledSelector Selector => _selector;
    // This stage retains validated author selector text; canonical selector serialization is separate.
    internal string SelectorText => _selectorText;

    internal bool TryMatch(Element element, out SelectorSpecificity specificity, Node? scopingRoot = null,
        CancellationToken cancellationToken = default) =>
        SelectorMatcher.TryMatch(_selector, element, out specificity, scopingRoot, cancellationToken);

    internal bool TryMatch(Element element, out SelectorSpecificity specificity, Node? scopingRoot,
        in SelectorEnvironment environment, ref SelectorMatchWork work) =>
        SelectorMatcher.TryMatch(_selector, element, out specificity, scopingRoot, environment, ref work);

    internal void SetSelectorText(string source, CssParseOptions? options = null,
        CancellationToken cancellationToken = default)
        => SetSelectorText(source, options, new CssValueWork(cancellationToken), cancellationToken);

    internal void SetSelectorText(string source, CssParseOptions? options, CssValueWork work,
        CancellationToken cancellationToken)
    {
        try
        {
            options ??= new CssParseOptions { Limits = _limits ?? ParseLimits.Unbounded };
            var parser = new CssSyntaxParser(source, options, cancellationToken, work.CheckCancellation);
            var values = parser.ParseComponentValues();
            var selector = new SelectorCompiler.Worker(source,
                new SelectorParseContext(limits: options.Limits, nestingParent: _nestingParent?.Selector),
                cancellationToken, work.CheckCancellation).Compile(values);
            var text = CssStyleSheet.SelectorText(source, values, parser, work);
            // Stage the entire subtree so a cancelled mutation cannot publish stale child programs.
            var updates = new List<(CssStyleRule Rule, CompiledSelector Selector)> { (this, selector) };
            var pending = new Stack<(CssStyleRule Rule, CompiledSelector Parent)>();
            foreach (var child in _rules) { work.Charge(1); pending.Push(((CssStyleRule) child, selector)); }
            while (pending.TryPop(out var item))
            {
                work.Charge(1);
                var childRule = item.Rule;
                var childParser = new CssSyntaxParser(childRule._selectorText,
                    new CssParseOptions { Limits = childRule._limits ?? ParseLimits.Unbounded },
                    cancellationToken, work.CheckCancellation);
                var childValues = childParser.ParseComponentValues();
                var childSelector = new SelectorCompiler.Worker(childRule._selectorText,
                    new SelectorParseContext(limits: childRule._limits, nestingParent: item.Parent),
                    cancellationToken, work.CheckCancellation).Compile(childValues);
                updates.Add((childRule, childSelector));
                foreach (var child in childRule._rules)
                { work.Charge(1); pending.Push(((CssStyleRule) child, childSelector)); }
            }
            work.CheckCancellation();
            foreach (var update in updates)
            {
                update.Rule._selector = update.Selector;
                if (!ReferenceEquals(update.Rule, this)) update.Rule.AdvanceStamp();
            }
            _selectorText = text;
            Changed();
        }
        catch (SelectorParseException)
        {
            // CSSOM's selectorText setter preserves the old selector on selector syntax failure.
        }
    }
}

internal sealed class CssRuleList(List<CssRule> items) : IReadOnlyList<CssRule>
{
    internal static readonly CssRuleList Empty = new(new List<CssRule>());
    public int Count => items.Count;
    public CssRule this[int index] => items[index];
    public IEnumerator<CssRule> GetEnumerator() => items.GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}

internal sealed class CssIncompleteRuleGrammarException : NotSupportedException
{
    internal CssIncompleteRuleGrammarException(string ruleName, string blocker, CssSourceSpan span)
        : base("Unimplemented CSS rule grammar: " + blocker)
    { RuleName = ruleName; Blocker = blocker; Span = span; }

    internal string RuleName { get; }
    internal string Blocker { get; }
    internal CssSourceSpan Span { get; }
}
