namespace Jint.HtmlParser.Css.Model;

// CSS Conditional 3 §§3, 7.4: retain the specified condition without logical simplification.
// https://drafts.csswg.org/css-conditional-3/#the-csssupportsrule-interface
internal sealed class CssSupportsRule(string conditionText, bool matches, CssSourceSpan span) : CssConditionRule(span)
{
    internal override CssRuleType Type => CssRuleType.Supports;
    internal override string ConditionText { get; } = conditionText;
    internal bool Matches { get; } = matches;
}
