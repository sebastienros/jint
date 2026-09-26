using Jint.HtmlParser.Css.Values;

namespace Jint.HtmlParser.Css.Model;

// CSSOM §6.4.5. Stable children and MediaList identities, including detached retained groups.
internal sealed class CssMediaRule : CssRule
{
    private readonly List<CssRule> _rules = new();

    internal CssMediaRule(CssMediaList media, CssSourceSpan span) : base(span)
    {
        Media = media;
        media.AttachTo(this);
        Rules = new CssRuleList(_rules);
    }

    internal override CssRuleType Type => CssRuleType.Media;
    internal CssMediaList Media { get; }
    internal string ConditionText => Media.MediaText;
    internal CssRuleList Rules { get; }
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
        rule.Attach(ParentStyleSheet, this, work);
        work.CheckCancellation();
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
}
