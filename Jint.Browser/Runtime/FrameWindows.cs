using AngleSharp.Dom;
using AngleSharp.Html.Dom;
using Jint.Browser.Dom;
using Jint.Browser.Events;
using Jint.Native.Object;
using Jint.Native;
using Jint.Runtime.Descriptors.Specialized;
using Jint.Runtime.Descriptors;
using Jint.Runtime.Interop;
using Jint.Runtime;
using Jint.WebApi.Url.Parsing;
using Jint.WebApi;

namespace Jint.Browser.Runtime;

/// <summary>
/// A stable facade for each child frame. Its identity and prototype remain on the principal realm;
/// lazy own constructor properties forward to the associated document's realm.
/// </summary>
/// <remarks>
/// https://html.spec.whatwg.org/multipage/webappapis.html#realms-settings-objects-global-objects —
/// the child document has its own DOM and event brands. Child global replacement and script execution
/// remain the next #3771 slice; the facade continues to inherit other page-global properties.
/// </remarks>
internal static class FrameWindows
{
    /// <summary>
    /// The window of <paramref name="frame"/>, built on first use, or <see langword="null"/> when the frame
    /// has no document to be the window of.
    /// </summary>
    /// <remarks>
    /// Cached on the frame element through the binding's own wrapper table, so the same frame answers the
    /// same object every time — <c>frame.contentWindow === frame.contentWindow</c> is what a page compares,
    /// and <c>frames[0] === frame.contentWindow</c> is what wpt does.
    /// </remarks>
    internal static JsValue For(PageRuntime runtime, IHtmlInlineFrameElement frame)
    {
        if (frame.ContentDocument is not { } document)
        {
            return JsValue.Null;
        }

        if (runtime.FrameWindowFor(frame) is { } existing)
        {
            return existing;
        }

        var window = Build(runtime, frame, document);
        runtime.RememberFrameWindow(frame, window);
        AttachDefaultView(runtime, frame, document);
        return window;
    }

    /// <summary>
    /// Gives a frame's document wrapper the <c>defaultView</c> its window is, once.
    /// </summary>
    /// <remarks>
    /// <para>
    /// An own accessor on the wrapper, which is where the page's own document gets its <c>defaultView</c>
    /// too (<c>WindowInstaller.AttachDocumentMembers</c>, and it argues there why it is not on
    /// <c>Document.prototype</c>). It is an accessor rather than a value so that reading
    /// <c>contentDocument</c> does not build a window nobody asked for: the frame element is closed over and
    /// the window is made on the first read of <c>defaultView</c>.
    /// </para>
    /// <para>
    /// It is what <c>doc.defaultView.DOMException</c> reaches, which is how a large part of the DOM corpus
    /// gets at the constructor to compare a refusal against.
    /// </para>
    /// </remarks>
    internal static void AttachDefaultView(PageRuntime runtime, IHtmlInlineFrameElement frame, IDocument document)
    {
        DocumentRealm(runtime, document);
        if (runtime.Dom.WrapNode(document) is not { } wrapper || wrapper.HasOwnProperty("defaultView"))
        {
            return;
        }

        wrapper.DefineOwnPropertyUnchecked(
            "defaultView",
            new GetSetPropertyDescriptor(
                new ClrFunction(runtime.Engine, "get defaultView", (_, _) => For(runtime, frame)),
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
    internal static JsValue At(PageRuntime runtime, int index)
    {
        if (index < 0 || runtime.Document is not { } document)
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
        dom.AssociateDocument(document, associatedGlobal: true);
        return dom;
    }

    private static JsObject Build(PageRuntime runtime, IHtmlInlineFrameElement frame, IDocument document)
    {
        var dom = DocumentRealm(runtime, document);
        var engine = runtime.Engine;
        var page = engine._mainRealm.GlobalObject;

        // Preserve the existing facade prototype; only DOM/event constructors are forwarded below.
        var window = new JsObject(engine) { Prototype = page };

        DomBindings.InstallOn(engine, dom.OwningRealm, window);
        BrowserEventRealm.InstallOn(engine, dom.OwningRealm, window);
        foreach (var name in new[] { "Event", "EventTarget", "CustomEvent", "MessageEvent", "DOMException", "QuotaExceededError" })
        {
            var captured = name;
            window.DefineOwnPropertyUnchecked(name, new LazyPropertyDescriptor<Realm>(
                dom.OwningRealm, r => r.GlobalObject.Get(captured), PropertyFlag.NonEnumerable));
        }

        // Itself, for the three names that mean "this window".
        Own(window, "window", window);
        Own(window, "self", window);
        Own(window, "frames", window);

        // The page's, for the two that mean "the one above". A frame nested in a frame still answers the
        // page for both, because a frame's own frames have no window of their own to be a parent: there is
        // one document per frame here and no browsing-context tree above it.
        Own(window, "parent", page);
        Own(window, "top", page);

        Own(window, "frameElement", runtime.Dom.WrapNodeValue(frame));
        Own(window, "document", runtime.Dom.WrapNodeValue(document));
        Own(window, "length", JsNumber.Create(Count(document)));
        Own(window, "name", JsString.Create(frame.Name ?? ""));
        Own(window, "origin", JsString.Create(PageUrl.OriginOf(document.Url)));

        // `location` is shadowed rather than inherited, and that is not the same decision as `DOMException`
        // above. Inheriting a constructor answers the same object either way; inheriting `location` would
        // answer the *page's* URL for a frame that is somewhere else, which is wrong information rather than
        // a shared object.
        Own(window, "location", Location(engine, document));

        return window;
    }

    private static void Own(ObjectInstance window, string name, JsValue value)
        => window.DefineOwnPropertyUnchecked(name, new PropertyDescriptor(value, PropertyFlag.OnlyEnumerable));

    /// <summary>
    /// A frame's <c>location</c>: the components of its document's URL, and nothing that navigates.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Readable, and every write is a loud refusal.</b> HTML's <c>Location</c> setters navigate the
    /// browsing context they belong to, and a frame here has a document rather than a context to navigate.
    /// Of the three possible answers — move the page (the wrong document entirely), do nothing, or refuse —
    /// <b>doing nothing is the one that must not be chosen</b>, and this file learned that from a corpus
    /// document rather than from first principles: <c>event-global-is-still-set-when-coercing-beforeunload-result.html</c>
    /// assigns <c>iframe.contentWindow.location.href</c> and then waits for the frame's <c>load</c>. Against
    /// a silent no-op it waits forever and the whole file <b>times out</b>, where before there was a window
    /// at all it threw at once and reported a failure. A hang is strictly worse than a failure — it is the
    /// one outcome <c>Jint.Tests.Browser/Wpt/AGENTS.md</c> singles out — so a write throws, the page sees the
    /// refusal, and a document that navigates a frame fails fast and says why.
    /// </para>
    /// <para>
    /// The page's own <c>location</c> is unaffected and still navigates; <c>Runtime/LocationInstaller</c>
    /// owns it. <c>assign</c>, <c>replace</c> and <c>reload</c> are simply absent, which is loud in the same
    /// way: calling one is a <c>TypeError</c> on an undefined member.
    /// </para>
    /// </remarks>
    private static JsObject Location(Engine engine, IDocument document)
    {
        var location = new JsObject(engine);
        var href = document.Url ?? "";

        // `href` is the URL as the document carries it, never re-serialized: a document's URL is what it was
        // opened with, and a round trip through the parser would answer a normalized string for a frame that
        // was never navigated anywhere.
        Component(engine, location, "href", href, null);

        Component(engine, location, "protocol", href, static url => url.SerializeProtocol());
        Component(engine, location, "host", href, static url => url.SerializeHostAndPort());
        Component(engine, location, "hostname", href, static url => url.SerializeHost());
        Component(engine, location, "port", href, static url => url.SerializePort());
        Component(engine, location, "pathname", href, static url => url.SerializePath());
        Component(engine, location, "search", href, static url => url.SerializeSearch());
        Component(engine, location, "hash", href, static url => url.SerializeHash());
        Component(engine, location, "origin", href, static url => url.SerializeOrigin());

        location.DefineOwnPropertyUnchecked(
            "toString",
            new PropertyDescriptor(
                new ClrFunction(engine, "toString", (_, _) => JsString.Create(href)),
                PropertyFlag.OnlyEnumerable));

        return location;
    }

    /// <summary>
    /// One component, read once at construction: a frame's document URL cannot move, because nothing here
    /// navigates a frame.
    /// </summary>
    private static void Component(Engine engine, ObjectInstance location, string name, string href, Func<UrlRecord, string>? read)
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
                new ClrFunction(engine, "get " + name, (_, _) => component),
                new ClrFunction(engine, "set " + name, (thisObject, _) =>
                {
                    // Loud, for the reason the class remarks give: a silent no-op turns a document that
                    // navigates a frame into a document that hangs.
                    Throw.TypeError(
                        (thisObject as ObjectInstance)?.Engine.Realm ?? engine.Realm,
                        "Failed to set the '" + name + "' property on 'Location': a child frame's location "
                        + "cannot be navigated in this version, because a frame has a document here and no "
                        + "browsing context of its own.");
                    return JsValue.Undefined;
                }),
                PropertyFlag.Configurable | PropertyFlag.Enumerable));
    }
}
