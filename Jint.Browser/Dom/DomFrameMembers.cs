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
/// <c>contentDocument</c> is generated from AngleSharp and re-declared over this because the generated body
/// hands out a cross-origin document. Its sibling <c>contentWindow</c> is declared in
/// <c>tools/dom-bindings/overrides.json</c> and answers <see langword="null"/> outright, so it needs no body
/// here — AngleSharp declares it and the conversion table drops it with the rest of
/// <c>AngleSharp.Dom.IWindow</c>, the window being the runtime's.
/// </para>
/// <para>
/// <b>A frame has a document here and no realm of its own</b> (<c>docs/design/headless-browser.md</c> §3):
/// the parser driver fetches a frame's <c>src</c> and AngleSharp opens it into the nested browsing context
/// it already made for the element, so the tree is real and readable, while nothing in it runs and there is
/// no second global object for <c>contentWindow</c> to be.
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
            return realm.WrapNodeValue(nested);
        }

        return string.Equals(here, PageUrl.OriginOf(nested.Url), StringComparison.Ordinal)
            ? realm.WrapNodeValue(nested)
            : JsValue.Null;
    }
}
