using static Jint.HtmlParser.Css.Selectors.CompiledSelector;

namespace Jint.HtmlParser.Css.Selectors;

internal static partial class SelectorMatcher
{
    // HTML Living Standard §4.15: https://html.spec.whatwg.org/multipage/semantics-other.html#pseudo-classes
    private static bool MatchFormState(PredicateKind kind, Element element, ref Work work) => kind switch
    {
        PredicateKind.Enabled => work.DisabledState(element) == HtmlDisabledState.Enabled,
        PredicateKind.Disabled => work.DisabledState(element) == HtmlDisabledState.Disabled,
        PredicateKind.Required => work.RequiredState(element) == HtmlRequiredState.Required,
        PredicateKind.Optional => work.RequiredState(element) == HtmlRequiredState.Optional,
        _ => false
    };
}
