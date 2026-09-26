using static Jint.HtmlParser.Css.Selectors.CompiledSelector;

namespace Jint.HtmlParser.Css.Selectors;

internal static partial class SelectorMatcher
{
    // HTML Living Standard §4.16.3: https://html.spec.whatwg.org/multipage/semantics-other.html#pseudo-classes
    private static bool MatchFormState(PredicateKind kind, Element element, ref Work work) => kind switch
    {
        PredicateKind.Checked => element is { NamespaceUri: Namespaces.Html, LocalName: "option" }
            ? element.ExistingOptionCore?.Selected ?? StateAttribute(element, "selected", null, ref work) is not null
            : work.Shared.MatchCheckable(element, false),
        PredicateKind.Indeterminate => element is { NamespaceUri: Namespaces.Html, LocalName: "progress" }
            ? StateAttribute(element, "value", null, ref work) is null
            : work.Shared.MatchCheckable(element, true),
        PredicateKind.Enabled => work.DisabledState(element) == HtmlDisabledState.Enabled,
        PredicateKind.Disabled => work.DisabledState(element) == HtmlDisabledState.Disabled,
        PredicateKind.Required => work.RequiredState(element) == HtmlRequiredState.Required,
        PredicateKind.Optional => work.RequiredState(element) == HtmlRequiredState.Optional,
        _ => false
    };
}
