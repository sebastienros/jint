namespace Jint.HtmlParser.Css.Selectors;

// Native identities and a value-only host-facts seed captured by the owning host loop.
internal readonly record struct SelectorEnvironment(
    Document? Document,
    Element? FocusedElement,
    Element? PointerPressTarget,
    Element? TargetElement,
    ISelectorControlFactsFactory? ControlFactsFactory = null,
    object? ControlFactsContext = null,
    ulong ControlFactsRevision = 0);
