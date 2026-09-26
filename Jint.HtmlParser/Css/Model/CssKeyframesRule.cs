using Jint.HtmlParser.Css.Serialization;
using Jint.HtmlParser.Css.Values;

namespace Jint.HtmlParser.Css.Model;

// CSS Animations 1 §6.3: keyframes are CSSRule, not CSSGroupingRule.
// https://drafts.csswg.org/css-animations-1/#interface-csskeyframesrule
internal sealed class CssKeyframesRule : CssRule
{
    private readonly List<CssRule> _rules = new();
    private string _name;

    internal CssKeyframesRule(string name, CssSourceSpan span) : base(span)
    {
        _name = name;
        Rules = new CssRuleList(_rules);
    }

    internal override CssRuleType Type => CssRuleType.Keyframes;
    internal override CssRuleList Rules { get; }
    internal string Name => _name;
    internal void AddProjected(CssKeyframeRule rule, CssValueWork work)
    {
        PrepareAppend(work);
        _rules.Add(rule);
    }

    private void PrepareAppend(CssValueWork work)
    {
        work.Charge(1);
        if (_rules.Count == _rules.Capacity) work.Charge(_rules.Count);
        _rules.EnsureCapacity(_rules.Count + 1);
        work.CheckCancellation();
    }

    // name is a DOMString setter, not a second invocation of the at-rule prelude grammar.
    internal void SetName(string name, CssValueWork? work = null)
    {
        work ??= new CssValueWork(default);
        work.Charge(name.Length);
        work.CheckCancellation();
        _name = name;
        Changed();
    }

    internal string SerializeName(CssValueWork work) =>
        _name.Length == 0 || CssKeyframeParser.ReservedName(_name)
            ? CssSyntaxSerializer.SerializeString(_name, work)
            : CssSyntaxSerializer.SerializeIdentifier(_name, work);

    internal void AppendRule(string source, CssParseOptions? options = null, CssValueWork? work = null)
    {
        work ??= new CssValueWork(default);
        var rule = CssKeyframeParser.ParseSingle(source, options, work);
        if (rule is null) return;
        rule.Attach(ParentStyleSheet, this, work);
        PrepareAppend(work); // capacity copies and callbacks precede publication
        _rules.Add(rule);
        Changed();
    }

    internal CssKeyframeRule? FindRule(string selector, CssParseOptions? options = null, CssValueWork? work = null)
    {
        work ??= new CssValueWork(default);
        var index = FindIndex(selector, options, work);
        work.CheckCancellation();
        return index < 0 ? null : (CssKeyframeRule) _rules[index];
    }

    internal void DeleteRule(string selector, CssParseOptions? options = null, CssValueWork? work = null)
    {
        work ??= new CssValueWork(default);
        var index = FindIndex(selector, options, work);
        if (index < 0) return;
        var detachment = _rules[index].PrepareDetach(work);
        work.Charge(_rules.Count - index); // shifts must be paid before the atomic commit
        work.CheckCancellation();
        detachment.Commit();
        _rules.RemoveAt(index);
        Changed();
    }

    private int FindIndex(string selector, CssParseOptions? options, CssValueWork work)
    {
        var keys = CssKeyframeKeys.Parse(selector, options, work);
        if (keys is null) return -1;
        for (var i = _rules.Count - 1; i >= 0; i--)
        {
            work.Charge(1);
            if (((CssKeyframeRule) _rules[i]).Matches(keys, work)) return i;
        }
        work.CheckCancellation();
        return -1;
    }
}
