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

    // HTML §4.16.3: native default checked/selected attributes stay cold; host state owns
    // submit-default selection, placeholder presentation, editing, validity and range applicability.
    private static bool MatchControlState(PredicateKind kind, Element element, ref Work work)
    {
        if (kind == PredicateKind.Default)
        {
            if (element.NamespaceUri != Namespaces.Html) return false;
            if (element.LocalName == "option")
                return StateAttribute(element, "selected", null, ref work) is not null;
            if (element.LocalName == "input")
            {
                var type = HtmlInputTypes.Parse(StateAttribute(element, "type", null, ref work));
                if (type is HtmlInputType.Checkbox or HtmlInputType.Radio)
                    return StateAttribute(element, "checked", null, ref work) is not null;
                if (type is not (HtmlInputType.Submit or HtmlInputType.Image)) return false;
            }
            else if (element.LocalName != "button") return false;
            return work.Shared.ReadControlFacts(element, SelectorControlFactMask.DefaultSubmit).DefaultSubmit;
        }
        // HTML defines :read-only for all other HTML elements. A foreign noneditable
        // element is not made read-only by negating its editing-capability fact.
        if (kind == PredicateKind.ReadOnly && element.NamespaceUri != Namespaces.Html) return false;
        var mask = kind switch
        {
            PredicateKind.PlaceholderShown => SelectorControlFactMask.PlaceholderShown,
            PredicateKind.ReadOnly or PredicateKind.ReadWrite => SelectorControlFactMask.ReadWrite,
            PredicateKind.Valid or PredicateKind.Invalid => SelectorControlFactMask.Validity,
            PredicateKind.InRange or PredicateKind.OutOfRange => SelectorControlFactMask.Range,
            _ => throw new InvalidOperationException("Unknown control-state predicate.")
        };
        var facts = work.Shared.ReadControlFacts(element, mask);
        return kind switch
        {
            PredicateKind.PlaceholderShown => facts.PlaceholderShown,
            PredicateKind.ReadOnly => !facts.ReadWrite,
            PredicateKind.ReadWrite => facts.ReadWrite,
            PredicateKind.Valid => facts.Validity == SelectorControlValidity.Valid,
            PredicateKind.Invalid => facts.Validity == SelectorControlValidity.Invalid,
            PredicateKind.InRange => facts.Range == SelectorControlRange.InRange,
            PredicateKind.OutOfRange => facts.Range == SelectorControlRange.OutOfRange,
            _ => throw new InvalidOperationException("Unknown control-state predicate.")
        };
    }
}
