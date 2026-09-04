using AngleSharp.Dom;
using AngleSharp.Html.Dom;
using Jint.Browser.Runtime;
using Jint.Native;

namespace Jint.Browser.Dom;

/// <summary>
/// The <c>HTMLIFrameElement</c> member that is about the frame's <i>content</i> rather than about the
/// element: what document a page may reach through it.
/// </summary>
/// <remarks>
/// <para>
/// Both are declared in <c>tools/dom-bindings/overrides.json</c> and answered here. <c>contentDocument</c>
/// is generated from AngleSharp and re-declared over this because the generated body hands out a
/// cross-origin document; <c>contentWindow</c> is added rather than re-declared, because AngleSharp's own
/// declaration is an <c>AngleSharp.Dom.IWindow</c> and the conversion table drops that interface whole — the
/// window a page gets is the runtime's (<c>Runtime/FrameWindows</c>).
/// </para>
/// <para>
/// <b>A frame has a document and a window here and no realm of its own</b>
/// (<c>docs/design/headless-browser.md</c> §3): the parser driver fetches a frame's <c>src</c> and AngleSharp
/// opens it into the nested browsing context it already made for the element, so the tree is real and
/// readable and <c>contentWindow</c> answers an object of the frame's own — while nothing in it runs, and
/// every constructor that window reaches is the page's, there being one realm.
/// </para>
/// </remarks>
internal static class DomFrameMembers
{
    /// <summary>
    /// https://html.spec.whatwg.org/multipage/iframe-embed-object.html#dom-iframe-contentdocument — the
    /// frame's document, or <see langword="null"/> when it is not same origin with the document asking.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The origin compared is the <i>owner document's</i> and not the page's. They are the same for a frame
    /// of the page's own document, and for a frame nested inside another frame the owner is the one whose
    /// script would be reaching in — which is the origin HTML's "current settings object" names.
    /// </para>
    /// <para>
    /// <b><c>about:blank</c> inherits, it does not go opaque.</b>
    /// <a href="https://html.spec.whatwg.org/multipage/browsers.html#determining-the-origin">HTML</a> gives a
    /// document created from <c>about:blank</c> the origin of whatever navigated to it, so an
    /// <c>&lt;iframe src="about:blank"&gt;</c> is same origin with the page that wrote it and readable. A
    /// <c>srcdoc</c> frame's document carries the owner's URL already and needs no rule of its own.
    /// </para>
    /// <para>
    /// Every other opaque origin answers <see langword="null"/>, being same origin with nothing — not even
    /// itself. <c>document.domain</c> is not implemented, so "same origin-domain" and "same origin" are one
    /// question here.
    /// </para>
    /// </remarks>
    internal static JsValue ContentDocument(DomRealm realm, IHtmlInlineFrameElement frame)
    {
        if (frame.ContentDocument is not { } nested)
        {
            return JsValue.Null;
        }

        var here = PageUrl.OriginOf(frame.Owner?.Url);

        if (string.Equals(here, PageUrl.OpaqueOrigin, StringComparison.Ordinal))
        {
            return JsValue.Null;
        }

        if (string.Equals(nested.Url, "about:blank", StringComparison.OrdinalIgnoreCase))
        {
            Attach(realm, frame, nested);
            return realm.WrapNodeValue(nested);
        }

        if (!string.Equals(here, PageUrl.OriginOf(nested.Url), StringComparison.Ordinal))
        {
            return JsValue.Null;
        }

        Attach(realm, frame, nested);
        return realm.WrapNodeValue(nested);
    }

    /// <summary>
    /// https://html.spec.whatwg.org/multipage/iframe-embed-object.html#dom-iframe-contentwindow — the frame's
    /// <c>WindowProxy</c>, or <see langword="null"/> when it has no document to be the window of.
    /// </summary>
    /// <remarks>
    /// <para>
    /// It answers a window on the <i>page's</i> realm rather than a realm of its own —
    /// <c>Runtime/FrameWindows</c> is the whole of what that means and why. Same origin decides whether there
    /// is one to hand back at all, by the same rule <see cref="ContentDocument"/> uses: a window is a door to
    /// a document, so handing one out cross-origin would hand out the document with it.
    /// </para>
    /// <para>
    /// A page's own <c>window</c> is <b>not</b> this: <c>frame.contentWindow !== window</c>, which is the
    /// property the corpus actually tests and the reason a frame gets an object at all.
    /// </para>
    /// </remarks>
    /// <summary>Gives the frame's document its <c>defaultView</c>, whichever member reached it first.</summary>
    private static void Attach(DomRealm realm, IHtmlInlineFrameElement frame, IDocument document)
    {
        if (PageRuntime.Find(realm.Engine) is { } runtime)
        {
            FrameWindows.AttachDefaultView(runtime, frame, document);
        }
    }

    internal static JsValue ContentWindow(DomRealm realm, IHtmlInlineFrameElement frame)
    {
        if (ContentDocument(realm, frame).IsNull() || PageRuntime.Find(realm.Engine) is not { } runtime)
        {
            return JsValue.Null;
        }

        return FrameWindows.For(runtime, frame);
    }
}
