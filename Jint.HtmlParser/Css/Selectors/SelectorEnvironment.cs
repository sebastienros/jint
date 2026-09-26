namespace Jint.HtmlParser.Css.Selectors;

// Native identities captured by the owning host loop, never predicate providers.
internal readonly record struct SelectorEnvironment(
    Document? Document,
    Element? FocusedElement,
    Element? PointerPressTarget,
    Element? TargetElement);
