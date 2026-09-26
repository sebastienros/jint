using System.Runtime.CompilerServices;
using Jint.Browser.Dom;
using Jint.Browser.Events;
using Jint.Browser.Runtime;
using Jint.HtmlParser;
using Jint.HtmlParser.Css.Media;
using Jint.HtmlParser.Css.Selectors;
using Jint.HtmlParser.Css.Values;

namespace Jint.Browser.Styling;

internal static partial class NativeCssStyleSheets
{
    private static readonly ConditionalWeakTable<Document, WeakReference<DomRealm>> Hosts = new();
    private static readonly ConditionalWeakTable<Node, NativeCssStyleSheetList> Lists = new();

    internal static NativeCssStyleSheetList ListOf(DomRealm realm, Node document) =>
        Lists.GetValue(document, owner => new(realm, owner));

    internal static Jint.HtmlParser.Css.Model.CssStyleSheet? SheetOf(DomRealm realm, Element owner)
    {
        if (owner.OwnerDocument is not { } document) return null;
        var work = new CssValueWork(realm.CancellationToken, realm.Engine.Constraints.Check);
        foreach (var input in Get(document, work, includeShadow: true))
        {
            work.Charge(1);
            if (ReferenceEquals(input.Sheet.Attachment.OwnerNode, owner)) return input.Sheet;
        }
        work.CheckCancellation();
        return null;
    }

    internal static void Associate(DomRealm realm, Document document)
    {
        Hosts.Remove(document);
        Hosts.Add(document, new(realm));
    }

    internal static void AssociateOwner(DomRealm realm, Element owner)
    {
        if (owner.OwnerDocument is { } document)
            AssociateOwner(document, owner, new CssValueWork(realm.CancellationToken, realm.Engine.Constraints.Check));
    }

    internal static void PrepareOwner(DomRealm realm, Element owner)
    {
        if (owner.OwnerDocument is { } document)
            PrepareOwner(document, owner, new CssValueWork(realm.CancellationToken, realm.Engine.Constraints.Check));
    }

    internal static void DisassociateOwner(DomRealm realm, Document document, Element owner) =>
        DisassociateOwner(document, owner, new CssValueWork(realm.CancellationToken, realm.Engine.Constraints.Check));

    internal static void SetDefaultStyle(DomRealm realm, Document document, string name) =>
        SetDefaultStyle(document, name, new CssValueWork(realm.CancellationToken, realm.Engine.Constraints.Check));

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

    internal static (NativeCssQuery Query, SelectorMatchWork Matching) CreateQuery(Document document, DomRealm realm,
        NativeCssQueryDiagnostics? diagnostics = null, Action? checkpoint = null,
        CancellationToken cancellationToken = default)
    {
        var token = cancellationToken.CanBeCanceled ? cancellationToken : realm.CancellationToken;
        void Check()
        {
            realm.CancellationToken.ThrowIfCancellationRequested();
            realm.Engine.Constraints.Check();
            checkpoint?.Invoke();
            realm.CancellationToken.ThrowIfCancellationRequested();
        }
        var work = new CssValueWork(token, Check);
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
            AnyPointer = features["any-pointer"] switch
            {
                "none" => CssPointerCapabilities.None,
                "coarse" => CssPointerCapabilities.Coarse,
                "fine" => CssPointerCapabilities.Fine,
                _ => CssPointerCapabilities.Unknown
            },
            AnyHover = features["any-hover"],
            DisplayMode = features["display-mode"],
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
        return CreateQuery(document, media, selectors, work, Check, diagnostics);
    }

    internal static (NativeCssQuery Query, SelectorMatchWork Matching) CreateInertQuery(Document document,
        CssValueWork work, Action? selectorCheckpoint = null, NativeCssQueryDiagnostics? diagnostics = null)
    {
        // Inert content uses the existing headless screen/viewport and initial-font policy.
        // Scripting is unavailable; no focus, pointer activation or glyph metrics are invented.
        var media = new CssMediaEnvironment { Scripting = "none" };
        var target = DomDocumentState.Of(document).TargetElement;
        var selectors = new SelectorEnvironment(document, null, null,
            target?.OwnerDocument == document ? target : null);
        return CreateQuery(document, media, selectors, work, selectorCheckpoint, diagnostics);
    }
}

// CSSOM §6.2: stable list identity, with its members reconciled only when read.
internal sealed class NativeCssStyleSheetList(DomRealm realm, Node root)
{
    private IReadOnlyList<NativeCssSheet> Read()
    {
        var document = root as Document ?? root.OwnerDocument;
        var host = document is not null ? NativeCssStyleSheets.RealmOf(document) ?? realm : realm;
        var work = new CssValueWork(host.CancellationToken, host.Engine.Constraints.Check);
        return root switch
        {
            Document owner => NativeCssStyleSheets.Get(owner, work),
            ShadowRoot shadow => NativeCssStyleSheets.Get(shadow, work),
            _ => throw new ArgumentException("A stylesheet list needs a Document or ShadowRoot.", nameof(root))
        };
    }
    internal int Length => Read().Count;
    internal Jint.HtmlParser.Css.Model.CssStyleSheet? Item(int index)
    {
        var sheets = Read();
        return (uint) index < (uint) sheets.Count ? sheets[index].Sheet : null;
    }
}
