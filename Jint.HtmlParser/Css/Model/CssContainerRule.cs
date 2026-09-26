using Jint.HtmlParser.Css.Conditions;

namespace Jint.HtmlParser.Css.Model;

internal sealed record CssContainerQuery(string Name, string Query, CssContainerCondition Condition);

// https://drafts.csswg.org/css-conditional-5/#the-csscontainerrule-interface
internal sealed class CssContainerRule(string name, string query, string conditionText,
    CssContainerCondition condition, CssSourceSpan span, CssContainerQuery[]? alternatives = null) : CssConditionRule(span)
{
    internal override CssRuleType Type => CssRuleType.Container;
    internal string ContainerName { get; } = name;
    internal string ContainerQuery { get; } = query;
    internal override string ConditionText { get; } = conditionText;
    internal CssContainerCondition Condition { get; } = condition;
    internal IReadOnlyList<CssContainerQuery> Conditions { get; } = Array.AsReadOnly(alternatives ?? [new(name, query, condition)]);
}
