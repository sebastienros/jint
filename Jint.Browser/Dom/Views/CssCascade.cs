using Jint.Browser.Styling;
using Jint.Browser.Runtime;
using Jint.Browser.Events;
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

    internal sealed class Traversal(NativeCssQuery query, SelectorMatchWork matching, Action? witness = null)
    {
        private readonly Dictionary<Element, NativeCssComputedStyle> _views = new();
        private SelectorMatchWork _matching = matching;
        internal NativeCssReadContext? ReadContext { get; private set; }

        internal static Traversal? For(Document? document, StyleScope scope = StyleScope.All,
            NativeCssQueryDiagnostics? diagnostics = null, Action? checkpoint = null,
            CancellationToken cancellationToken = default)
        {
            if (document is null) return null;
            var runtime = NativeCssStyleSheets.RealmOf(document) is { } host
                ? PageRuntime.FindBrowsingContext(host.Engine, document) : null;
            var context = DomBrowsingContext.Of(document);
            var displayedContext = DomBrowsingContext.Of(runtime?.Document);
            var media = runtime?.Media;
            var layoutRevision = runtime?.Layout.Version;
            var events = runtime is null ? null : BrowserEventRealm.Of(runtime.Engine);
            var focus = events?.FocusedElement;
            var press = events?.MousePressTarget;
            var url = DomDocumentState.Of(document).Url;
            var target = DomDocumentState.Of(document).TargetElement;
            var input = NativeCssStyleSheets.RealmOf(document) is { } realm
                ? NativeCssStyleSheets.CreateQuery(document, realm, diagnostics, checkpoint, cancellationToken)
                : NativeCssStyleSheets.CreateInertQuery(document, new CssValueWork(cancellationToken, checkpoint), checkpoint, diagnostics);
            Action? readWitness = runtime is null ? null : () =>
            {
                // Child documents share the principal runtime. The witness is their active
                // context association, not equality with the principal runtime's document.
                if (!ReferenceEquals(DomBrowsingContext.Of(document), context) ||
                    !ReferenceEquals(DomBrowsingContext.Of(runtime.Document), displayedContext) ||
                    !ReferenceEquals(PageRuntime.FindBrowsingContext(runtime.Engine, document), runtime) || runtime.Media != media || runtime.Layout.Version != layoutRevision ||
                    !ReferenceEquals(events!.FocusedElement, focus) || !ReferenceEquals(events.MousePressTarget, press) ||
                    DomDocumentState.Of(document).Url != url || !ReferenceEquals(DomDocumentState.Of(document).TargetElement, target))
                    throw new InvalidOperationException(NativeCssQuery.Invalidated);
            };
            if (readWitness is not null) input.Query.AttachReadWitness(readWitness);
            var traversal = new Traversal(input.Query, input.Matching, readWitness);
            // Child CSS remains valid, but principal-page geometry cannot supply its dimensions.
            if (runtime is not null && ReferenceEquals(runtime.Document, document))
                traversal.ReadContext = new NativeCssReadContext(input.Query, traversal, document,
                    runtime.Layout.Visibility, media!.Viewport.Width, readWitness!, input.Query.Work.Token);
            return traversal;
        }

        internal NativeCssComputedStyle Of(Element element)
        {
            _matching.VerifyRead();
            if (_views.TryGetValue(element, out var cached)) return cached;
            var view = new NativeCssComputedStyle(query, element, _matching, witness, ReadContext);
            // Matching is separate from computation; coverage observes real matched rule identities.
            if (CssRuleUsage.IsTracking) CssRuleUsage.Observe(element, view.MatchedRules());
            _views.Add(element, view);
            return view;
        }

        internal NativeCssComputedStyle LayoutOf(Element element) => Of(element);
    }
}
