using Jint.HtmlParser.Css.Values;

namespace Jint.HtmlParser.Css.Model;

// CSSOM §6.4.7: a grouping rule owns one ordered, live child list.
// https://drafts.csswg.org/cssom/#the-cssgroupingrule-interface
internal abstract class CssGroupingRule : CssRule
{
    private readonly List<CssRule> _rules = new();

    protected CssGroupingRule(CssSourceSpan span) : base(span) => Rules = new CssRuleList(_rules);

    internal override CssRuleList Rules { get; }
    internal void AddProjected(CssRule rule) => _rules.Add(rule);

    internal int InsertRule(string source, int index, CssParseOptions? options = null,
        CancellationToken cancellationToken = default)
        => InsertRule(source, index, options, new CssValueWork(cancellationToken), cancellationToken);

    internal int InsertRule(string source, int index, CssParseOptions? options, CssValueWork work,
        CancellationToken cancellationToken)
    {
        if ((uint) index > (uint) _rules.Count)
            throw new DomException("IndexSizeError", "The rule index is outside the list.");
        var rule = CssStyleSheet.ParseSingle(source, options, work, cancellationToken);
        if (rule is CssImportRule)
            throw new DomException("HierarchyRequestError", "Imports cannot be nested.");
        rule.Attach(ParentStyleSheet, this, work);
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
}
