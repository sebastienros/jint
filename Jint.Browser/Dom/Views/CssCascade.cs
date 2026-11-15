using System.Runtime.CompilerServices;
using Jint.Browser.Dom;
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
    internal static NativeCssComputedStyle? Of(Element element)
        => element.OwnerDocument is { } document ? Traversal.Current(document).Of(element) : null;

    internal static string? ValueOf(NativeCssComputedStyle declaration, string property)
        => declaration.GetPropertyValue(property);

    // Scopes retain the existing caller vocabulary. Lazy getters eliminate eager scope filtering.
    internal enum StyleScope { All, Visibility, Layout }

    internal sealed class Traversal(NativeCssQuery query, SelectorMatchWork matching, Action? witness = null)
    {
        // One default traversal per document, reused by separate script reads until anything it
        // captured moves: without it every getComputedStyle read re-collected every sheet and
        // rebuilt the rule index, a walk of the whole document per read.
        private static readonly ConditionalWeakTable<Document, Traversal> Shared = new();

        private readonly Dictionary<Element, NativeCssComputedStyle> _views = new();
        private readonly Dictionary<(Element, SelectorPseudoElement), NativeCssComputedStyle> _pseudoViews = new();
        private SelectorMatchWork _matching = matching;
        private DomRealm? _realm;
        private Func<bool>? _witnessIsCurrent;
        internal NativeCssReadContext? ReadContext { get; private set; }

        internal static Traversal Current(Document document)
        {
            var realm = NativeCssStyleSheets.RealmOf(document);
            // Pending control changes are flushed before a query captures their revision, reused or not.
            if (realm is not null)
                BrowserSelectorControlFacts.PrepareControlFactsRead(realm, realm.NativeReadCheckpoint, realm.CancellationToken);
            // Coverage observes matched rules when a view is created; a reused view would skip it.
            // Only this document's coverage needs a fresh view, not a window on another page.
            if (!CssRuleUsage.IsTrackingDocument(document) && Shared.TryGetValue(document, out var cached) && cached.IsReusable(realm))
                return cached;
            var created = For(document)!;
            created._realm = realm;
            Shared.AddOrUpdate(document, created);
            return created;
        }

        private bool IsReusable(DomRealm? realm)
            => ReferenceEquals(_realm, realm) && query.IsReusable() && _witnessIsCurrent?.Invoke() != false;

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
            var hover = events?.MouseHoverTarget;
            var url = DomDocumentState.Of(document).Url;
            var target = DomDocumentState.Of(document).TargetElement;
            var input = NativeCssStyleSheets.RealmOf(document) is { } realm
                ? NativeCssStyleSheets.CreateQuery(document, realm, diagnostics, checkpoint, cancellationToken)
                : NativeCssStyleSheets.CreateInertQuery(document, new CssValueWork(cancellationToken, checkpoint), checkpoint, diagnostics);
            // Child documents share the principal runtime. The witness is their active
            // context association, not equality with the principal runtime's document.
            Func<bool>? witnessIsCurrent = runtime is null ? null : () =>
                ReferenceEquals(DomBrowsingContext.Of(document), context) &&
                ReferenceEquals(DomBrowsingContext.Of(runtime.Document), displayedContext) &&
                ReferenceEquals(PageRuntime.FindBrowsingContext(runtime.Engine, document), runtime) && runtime.Media == media && runtime.Layout.Version == layoutRevision &&
                InteractionsAreCurrent(input.Query, events!, focus, press, hover) &&
                DomDocumentState.Of(document).Url == url && ReferenceEquals(DomDocumentState.Of(document).TargetElement, target);
            Action? readWitness = witnessIsCurrent is null ? null : () =>
            {
                if (!witnessIsCurrent()) throw new InvalidOperationException(NativeCssQuery.Invalidated);
            };
            if (readWitness is not null) input.Query.AttachReadWitness(readWitness);
            var traversal = new Traversal(input.Query, input.Matching, readWitness) { _witnessIsCurrent = witnessIsCurrent };
            // Child CSS remains valid, but principal-page geometry cannot supply its dimensions.
            if (runtime is not null && ReferenceEquals(runtime.Document, document))
                traversal.ReadContext = new NativeCssReadContext(input.Query, traversal, document,
                    runtime.Layout.Visibility, media!.Viewport.Width, readWitness!, input.Query.Work.Token);
            return traversal;
        }

        // A focus, press or hover change can only change a style a rule matches on it; until the rules have
        // been read, any change retires the traversal.
        private static bool InteractionsAreCurrent(NativeCssQuery query, BrowserEventRealm events,
            Element? focus, Element? press, Element? hover)
        {
            var focusChanged = !ReferenceEquals(events.FocusedElement, focus);
            var pressChanged = !ReferenceEquals(events.MousePressTarget, press);
            var hoverChanged = !ReferenceEquals(events.MouseHoverTarget, hover);
            if (!focusChanged && !pressChanged && !hoverChanged) return true;
            query.TryGetInteractionDependencies(out var dependencies);
            return !(focusChanged && (dependencies & CssInteractionDependencies.Focus) != 0
                || pressChanged && (dependencies & CssInteractionDependencies.Active) != 0
                || hoverChanged && (dependencies & CssInteractionDependencies.Hover) != 0);
        }

        internal NativeCssComputedStyle Of(Element element)
        {
            _matching.VerifyRead();
            if (_views.TryGetValue(element, out var cached)) return cached;
            var view = new NativeCssComputedStyle(query, element, _matching, witness, ReadContext);
            // Matching is separate from computation; coverage observes real matched rule identities.
            if (CssRuleUsage.IsTrackingDocument(element.OwnerDocument)) CssRuleUsage.Observe(element, view.MatchedRules());
            _views.Add(element, view);
            return view;
        }

        // CSS Pseudo-Elements 4 §4: the ::before/::after view of element, shared like the element's own view.
        internal NativeCssComputedStyle Of(Element element, SelectorPseudoElement pseudo)
        {
            _matching.VerifyRead();
            if (_pseudoViews.TryGetValue((element, pseudo), out var cached)) return cached;
            var view = new NativeCssComputedStyle(query, element, _matching, witness, ReadContext) { Pseudo = pseudo };
            // A pseudo-element's rules are used once they apply, as the element's are.
            if (CssRuleUsage.IsTrackingDocument(element.OwnerDocument)) CssRuleUsage.Observe(element, view.MatchedRules());
            _pseudoViews.Add((element, pseudo), view);
            return view;
        }

        internal NativeCssComputedStyle LayoutOf(Element element) => Of(element);
    }
}
