using AngleSharp.Dom;
using AngleSharp.Html.Dom;
using Jint.Browser.Dom;
using Jint.Browser.Events;
using Jint.Native.Object;
using Jint.Native;
using Jint.Runtime.Descriptors;
using Jint.Runtime.Interop;
using Jint.Runtime;
using Jint.WebApi.Url.Parsing;
using Jint.WebApi;

namespace Jint.Browser.Runtime;

/// <summary>Installs each child document's global and resolves its parent and indexed frame windows.</summary>
/// <remarks>
/// https://html.spec.whatwg.org/multipage/webappapis.html#realms-settings-objects-global-objects —
/// each document has independent intrinsics and global bindings in the page's engine. WindowProxy
/// navigation and cross-origin access remain unsupported; DomFrameMembers gates exposed windows.
/// </remarks>
internal static class FrameWindows
{
    /// <summary>
    /// The window of <paramref name="frame"/>, built on first use, or <see langword="null"/> when the frame
    /// has no document to be the window of.
    /// </summary>
    internal static JsValue For(PageRuntime runtime, IHtmlInlineFrameElement frame)
    {
        if (frame.ContentDocument is not { } document)
        {
            return JsValue.Null;
        }

        var window = ForDocument(runtime, document);
        AttachDefaultView(runtime, document);
        return window;
    }

    /// <summary>
    /// Gives a frame's document wrapper the <c>defaultView</c> its window is, once.
    /// </summary>
    internal static void AttachDefaultView(PageRuntime runtime, IDocument document)
    {
        var dom = DocumentRealm(runtime, document);
        if (runtime.Dom.WrapNode(document) is not { } wrapper || wrapper.HasOwnProperty("defaultView"))
        {
            return;
        }

        wrapper.DefineOwnPropertyUnchecked(
            "defaultView",
            new GetSetPropertyDescriptor(
                new ClrFunction(runtime.Engine, dom.OwningRealm, "get defaultView", (_, _) => ForDocument(runtime, document), 0),
                set: null,
                PropertyFlag.Configurable));
    }

    /// <summary>The window of the frame at <paramref name="index"/> of the page's document, in tree order.</summary>
    /// <remarks>
    /// https://html.spec.whatwg.org/multipage/nav-history-apis.html#the-window-object — the indexed
    /// properties of a <c>Window</c> are its document's child browsing contexts in tree order, which is what
    /// <c>frames[0]</c> reads. It is computed per access rather than installed, so a frame the parse has not
    /// reached yet appears the moment it does; installing them would be a snapshot of whichever moment the
    /// installer ran.
    /// </remarks>
    internal static JsValue At(PageRuntime runtime, int index, IDocument? document = null)
    {
        document ??= runtime.Document;
        if (index < 0 || document is null)
        {
            return JsValue.Undefined;
        }

        var frames = document.QuerySelectorAll("iframe, frame");

        if (index >= frames.Length || frames[index] is not IHtmlInlineFrameElement frame)
        {
            return JsValue.Undefined;
        }

        var window = For(runtime, frame);
        return window.IsNull() ? JsValue.Undefined : window;
    }

    /// <summary>How many child browsing contexts <paramref name="document"/> has, which is `window.length`.</summary>
    internal static int Count(IDocument? document)
        => document is null ? 0 : document.QuerySelectorAll("iframe, frame").Length;

    internal static DomRealm DocumentRealm(PageRuntime runtime, IDocument document)
    {
        if (runtime.Dom.TryGetDocumentRealm(document, out var existing))
        {
            return existing!;
        }
        var engine = runtime.Engine;
        var realm = engine._host.CreateRealm();
        WebApiRegistration.InstallInRealm(engine, realm);
        DomBindings.Install(engine, realm);
        BrowserEventRealm.Install(engine, realm);
        var dom = DomRealm.Of(engine, realm);
        dom.AssociateWindowDocument(document);
        return dom;
    }

    // https://html.spec.whatwg.org/multipage/webappapis.html#realms-settings-objects-global-objects
    // A child script can arrive before its frame's ContentDocument is published. The document's context
    // already identifies its parent; installation must not depend on an element lookup succeeding yet.
    internal static ObjectInstance ForDocument(PageRuntime runtime, IDocument document)
    {
        if (ReferenceEquals(document, runtime.Document))
        {
            return runtime.Engine._mainRealm.GlobalObject;
        }

        var dom = DocumentRealm(runtime, document);
        var realm = dom.OwningRealm;
        var window = realm.GlobalObject;
        if (dom.WindowTarget is not null)
        {
            return window;
        }

        using var scope = new RealmScope(runtime.Engine, realm);
        dom.ReadyState = "loading";
        WindowInstaller.InstallFrame(runtime, dom, document);
        Own(window, "window", window);
        Own(window, "self", window);
        Own(window, "frames", window);
        Own(window, "document", dom.WrapNodeValue(document));
        Own(window, "top", runtime.Engine._mainRealm.GlobalObject);
        Own(window, "parent", document.Context.Parent?.Active is { } parent
            ? ForDocument(runtime, parent) : runtime.Engine._mainRealm.GlobalObject);
        Accessor("frameElement", () => runtime.Dom.WrapNodeValue(ElementOf(document)));
        Accessor("length", () => JsNumber.Create(Count(document)));
        Accessor("name", () => JsString.Create(ElementOf(document)?.Name ?? ""));
        Own(window, "origin", JsString.Create(PageUrl.OriginOf(document.Url)));
        var location = Location(runtime.Engine, realm, document);
        window.DefineOwnPropertyUnchecked("location", new GetSetPropertyDescriptor(
            new ClrFunction(runtime.Engine, realm, "get location", (_, _) => location, 0),
            new ClrFunction(runtime.Engine, realm, "set location", (_, args) =>
            {
                location.Set("href", args.At(0), throwOnError: true);
                return JsValue.Undefined;
            }, 1), PropertyFlag.OnlyEnumerable));

        var wrapper = dom.WrapNode(document);
        wrapper.DefineOwnPropertyUnchecked("defaultView", new GetSetPropertyDescriptor(
            new ClrFunction(runtime.Engine, realm, "get defaultView", (_, _) => window, 0), null, PropertyFlag.Configurable));
        return window;

        void Accessor(string name, Func<JsValue> read) => window.DefineOwnPropertyUnchecked(name,
            new GetSetPropertyDescriptor(new ClrFunction(runtime.Engine, realm, "get " + name, (_, _) => read(), 0),
                null, PropertyFlag.Configurable | PropertyFlag.Enumerable));
    }

    internal static IHtmlInlineFrameElement? ElementOf(IDocument document)
    {
        var parent = document.Context.Parent?.Active ?? document.Context.Creator;
        if (parent is null)
        {
            return null;
        }
        foreach (var element in parent.QuerySelectorAll("iframe"))
        {
            if (element is IHtmlInlineFrameElement frame && ReferenceEquals(frame.ContentDocument, document))
            {
                return frame;
            }
        }
        return null;
    }

    internal static bool CanRunScripts(PageRuntime runtime, IDocument document)
    {
        // Cross-origin WindowProxy access control and sandboxed globals are separate capabilities.
        // Do not expose the parent's raw global through a child which cannot normally reach it.
        var origin = PageUrl.OriginOf(runtime.DocumentUrl);
        if (origin == PageUrl.OpaqueOrigin || runtime.Document is not { } principal)
        {
            return false;
        }
        for (var current = document; !ReferenceEquals(current.Context, principal.Context);)
        {
            if (!string.Equals(current.Url, "about:blank", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(PageUrl.OriginOf(current.Url), origin, StringComparison.Ordinal))
            {
                return false;
            }
            if (ElementOf(current) is { } frame && frame.HasAttribute("sandbox"))
            {
                return false;
            }
            if (current.Context.Parent?.Active is not { } parent)
            {
                return false;
            }
            current = parent;
        }
        return true;
    }

    private static void Own(ObjectInstance window, string name, JsValue value)
        => window.DefineOwnPropertyUnchecked(name, new PropertyDescriptor(value, PropertyFlag.OnlyEnumerable));

    /// <summary>
    /// A frame's <c>location</c>: the components of its document's URL, and nothing that navigates.
    /// </summary>
    /// <remarks>Setters refuse until frame navigation and WindowProxy replacement are implemented.</remarks>
    private static JsObject Location(Engine engine, Realm realm, IDocument document)
    {
        var location = new JsObject(engine);
        var href = document.Url ?? "";

        // `href` is the URL as the document carries it, never re-serialized: a document's URL is what it was
        // opened with, and a round trip through the parser would answer a normalized string for a frame that
        // was never navigated anywhere.
        Component(engine, realm, location, "href", href, null);

        Component(engine, realm, location, "protocol", href, static url => url.SerializeProtocol());
        Component(engine, realm, location, "host", href, static url => url.SerializeHostAndPort());
        Component(engine, realm, location, "hostname", href, static url => url.SerializeHost());
        Component(engine, realm, location, "port", href, static url => url.SerializePort());
        Component(engine, realm, location, "pathname", href, static url => url.SerializePath());
        Component(engine, realm, location, "search", href, static url => url.SerializeSearch());
        Component(engine, realm, location, "hash", href, static url => url.SerializeHash());
        Component(engine, realm, location, "origin", href, static url => url.SerializeOrigin());

        location.DefineOwnPropertyUnchecked(
            "toString",
            new PropertyDescriptor(
                new ClrFunction(engine, realm, "toString", (_, _) => JsString.Create(href), 0),
                PropertyFlag.OnlyEnumerable));

        return location;
    }

    /// <summary>
    /// One component, read once at construction: a frame's document URL cannot move, because nothing here
    /// navigates a frame.
    /// </summary>
    private static void Component(Engine engine, Realm realm, ObjectInstance location, string name, string href, Func<UrlRecord, string>? read)
    {
        // `href` is the URL as the document carries it, never re-serialized: a document's URL is what it was
        // opened with, and a round trip through the parser would answer a normalized string for a frame that
        // was never navigated anywhere. Every other component is parsed, and answers the empty string for a
        // URL the parser refuses — which is what `about:blank` is, and the same answer
        // `LocationInstaller.Read` gives the page's own location.
        var url = read is null ? null : UrlParser.Parse(href);
        var value = read is null ? href : url is null ? "" : read(url);
        var component = JsString.Create(value);

        location.DefineOwnPropertyUnchecked(
            name,
            new GetSetPropertyDescriptor(
                new ClrFunction(engine, realm, "get " + name, (_, _) => component, 0),
                new ClrFunction(engine, realm, "set " + name, (_, _) =>
                {
                    // Loud, for the reason the class remarks give: a silent no-op turns a document that
                    // navigates a frame into a document that hangs.
                    Throw.TypeError(
                        realm,
                        "Failed to set the '" + name + "' property on 'Location': a child frame's location "
                        + "cannot be navigated in this version.");
                    return JsValue.Undefined;
                }, 1),
                PropertyFlag.Configurable | PropertyFlag.Enumerable));
    }
}
