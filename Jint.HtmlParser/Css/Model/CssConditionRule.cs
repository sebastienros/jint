namespace Jint.HtmlParser.Css.Model;

// CSS Conditional 3 §7.2: both media and supports expose a readonly conditionText.
// https://drafts.csswg.org/css-conditional-3/#the-cssconditionrule-interface
internal abstract class CssConditionRule(CssSourceSpan span) : CssGroupingRule(span)
{
    internal abstract string ConditionText { get; }
}
