using Jint.HtmlParser;
using Jint.Browser.Runtime;
using Jint.Native;

namespace Jint.Browser.Dom;

/// <summary>
/// Projects child-frame documents and windows through the Browser frame runtime.
/// </summary>
/// <remarks>
/// The parser driver loads frame documents. These bindings enforce same-origin access and return the
/// Browser-owned window wrapper instead of exposing a native browsing context to script.
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
    /// An opaque origin is compared by identity: an inherited blank or srcdoc origin remains same origin
    /// with its creator, while independently created opaque origins are different. <c>document.domain</c> is not implemented, so "same origin-domain" and "same origin" are one
    /// question here.
    /// </para>
    /// </remarks>
    internal static JsValue ContentDocument(DomRealm realm, Element frame)
    {
        if (DomBrowsingContext.OfFrame(frame)?.Active is not { } nested)
        {
            return JsValue.Null;
        }

        if (frame.OwnerDocument is not { } owner
            || !DomDocumentState.Of(owner).Origin.IsSameOrigin(DomDocumentState.Of(nested).Origin))
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
    /// It answers the child document's realm global —
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
    private static void Attach(DomRealm realm, Element frame, Document document)
    {
        if (PageRuntime.Find(realm.Engine) is { } runtime)
        {
            FrameWindows.AttachDefaultView(runtime, document);
        }
    }

    internal static JsValue ContentWindow(DomRealm realm, Element frame)
    {
        if (ContentDocument(realm, frame).IsNull() || PageRuntime.Find(realm.Engine) is not { } runtime)
        {
            return JsValue.Null;
        }

        return FrameWindows.For(runtime, frame);
    }
}
