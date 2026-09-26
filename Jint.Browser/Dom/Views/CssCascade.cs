using Jint.Browser.Styling;
using Jint.HtmlParser;
using Jint.HtmlParser.Css.Selectors;
using Jint.HtmlParser.Css.Values;

namespace Jint.Browser.Dom.Views;

// CSS Cascade 5 §§6–7. All Browser consumers enter the same native, on-demand query.
internal static class CssCascade
{
    internal static NativeCssComputedStyle? Of(Element element, bool resolveInheritance = true)
        => element.OwnerDocument is { } document ? Traversal.For(document)?.Of(element) : null;

    internal static string? ValueOf(NativeCssComputedStyle declaration, string property)
        => declaration.GetPropertyValue(property);

    // Scopes retain the existing caller vocabulary. Lazy getters eliminate eager scope filtering.
    internal enum StyleScope { All, Visibility, Layout }

    internal sealed class Traversal(NativeCssQuery query, SelectorMatchWork matching)
    {
        private readonly Dictionary<Element, NativeCssComputedStyle> _views = new();
        private SelectorMatchWork _matching = matching;

        internal static Traversal? For(Document? document, StyleScope scope = StyleScope.All,
            NativeCssQueryDiagnostics? diagnostics = null, CancellationToken cancellationToken = default,
            Action? checkpoint = null)
        {
            if (document is null) return null;
            var input = NativeCssStyleSheets.RealmOf(document) is { } realm
                ? NativeCssStyleSheets.CreateQuery(document, realm, diagnostics, cancellationToken, checkpoint)
                : NativeCssStyleSheets.CreateInertQuery(document, new CssValueWork(cancellationToken, checkpoint), checkpoint, diagnostics);
            return new(input.Query, input.Matching);
        }

        internal NativeCssComputedStyle Of(Element element)
        {
            _matching.VerifyRead();
            if (_views.TryGetValue(element, out var cached)) return cached;
            var view = new NativeCssComputedStyle(query, element, _matching);
            // Matching is separate from computation; coverage observes real matched rule identities.
            if (CssRuleUsage.IsTracking) CssRuleUsage.Observe(element, view.MatchedRules());
            _views.Add(element, view);
            return view;
        }

        internal NativeCssComputedStyle LayoutOf(Element element) => Of(element);
    }
}
