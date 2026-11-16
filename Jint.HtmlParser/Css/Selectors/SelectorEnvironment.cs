namespace Jint.HtmlParser.Css.Selectors;

// Native identities and a value-only host-facts seed captured by the owning host loop.
// FocusVisible is the host's Selectors 4 §9.4 decision that FocusedElement indicates focus; the
// heuristics are the user agent's, so the parser only applies it to the focused element itself.
internal readonly record struct SelectorEnvironment(
    Document? Document,
    Element? FocusedElement,
    Element? PointerPressTarget,
    Element? TargetElement,
    ISelectorControlFactsFactory? ControlFactsFactory = null,
    object? ControlFactsContext = null,
    ulong ControlFactsRevision = 0,
    Element? HoverTarget = null,
    bool FocusVisible = false);
