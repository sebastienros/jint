using Jint.HtmlParser.Css.Conditions;

namespace Jint.HtmlParser.Css.Model;

// https://drafts.csswg.org/css-conditional-5/#the-csscontainerrule-interface
internal sealed class CssContainerRule(string name, string query, string conditionText,
    CssContainerCondition condition, CssSourceSpan span) : CssConditionRule(span)
{
    internal override CssRuleType Type => CssRuleType.Container;
    internal string ContainerName { get; } = name;
    internal string ContainerQuery { get; } = query;
    internal override string ConditionText { get; } = conditionText;
    internal CssContainerCondition Condition { get; } = condition;
}
