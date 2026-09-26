using System.Runtime.CompilerServices;
using Jint.Browser.Dom;
using Jint.Browser.Events;
using Jint.Browser.Runtime;
using Jint.HtmlParser;
using Jint.HtmlParser.Css.Media;
using Jint.HtmlParser.Css.Selectors;
using Jint.HtmlParser.Css.Values;
using Jint.HtmlParser.Css.Values.References;

namespace Jint.Browser.Styling;

internal static partial class NativeCssStyleSheets
{
    private static readonly ConditionalWeakTable<Document, WeakReference<DomRealm>> Hosts = new();

    internal static void Associate(DomRealm realm, Document document)
    {
        Hosts.Remove(document);
        Hosts.Add(document, new(realm));
    }

    internal static DomRealm? RealmOf(Document document) =>
        Hosts.TryGetValue(document, out var reference) && reference.TryGetTarget(out var realm) ? realm : null;

    internal static void Install(DomRealm realm, Element owner, string text, string sourceUrl)
    {
        var document = owner.OwnerDocument ?? throw new ArgumentException("A stylesheet owner needs a document.", nameof(owner));
        var work = new CssValueWork(realm.CancellationToken, realm.Engine.Constraints.Check);
        var baseUrl = DomDocumentState.BaseUri(document, realm.Engine.Constraints.Check, realm.CancellationToken);
        Associate(realm, document);
        Install(document, owner, text, sourceUrl, owner.LocalName == "link" ? sourceUrl : baseUrl, work);
    }

    internal static (NativeCssQuery Query, SelectorMatchWork Matching) CreateQuery(Document document, DomRealm realm)
    {
        var work = new CssValueWork(realm.CancellationToken, realm.Engine.Constraints.Check);
        var page = PageRuntime.FindBrowsingContext(realm.Engine, document)?.Media ?? PageMediaEnvironment.Default;
        IReadOnlyDictionary<string, string> features = page;
        var media = new CssMediaEnvironment
        {
            Type = page.MediaType,
            Width = page.Viewport.Width,
            Height = page.Viewport.Height,
            Resolution = page.Viewport.DeviceScaleFactor,
            Pointer = features["pointer"],
            Hover = features["hover"],
            Scripting = features["scripting"],
            ColorScheme = features["prefers-color-scheme"],
            ReducedMotion = features["prefers-reduced-motion"],
            ReducedTransparency = features["prefers-reduced-transparency"],
            ReducedData = features["prefers-reduced-data"],
            Contrast = features["prefers-contrast"],
            ForcedColors = features["forced-colors"]
        };
        var events = BrowserEventRealm.Of(realm.Engine);
        var focus = events.FocusedElement;
        var press = events.MousePressTarget;
        var target = DomDocumentState.Of(document).TargetElement;
        var selectors = new SelectorEnvironment(document,
            focus?.OwnerDocument == document ? focus : null,
            press?.OwnerDocument == document ? press : null,
            target?.OwnerDocument == document ? target : null);
        var sheets = Get(document, work);
        var query = new NativeCssQuery(document, sheets, [], media, selectors,
            CssEnvironmentSnapshot.Create([], work), work, readInlineAttributes: true);
        var matching = new SelectorMatchWork(document, realm.CancellationToken, realm.Engine.Constraints.Check);
        return (query, matching);
    }
}
