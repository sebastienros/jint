using AngleSharp.Dom;
using AngleSharp.Html.Dom;

namespace Jint.Browser.Dom;

/// <summary>
/// HTML's two named elements of a document — <a href="https://html.spec.whatwg.org/multipage/dom.html#the-html-element-2">the
/// html element</a> and <a href="https://html.spec.whatwg.org/multipage/dom.html#the-body-element">the body
/// element</a> — which several members are defined in terms of and no AngleSharp member answers.
/// </summary>
/// <remarks>
/// <para>
/// <b>Both are gated on the same sentence</b>: "The html element of a document is its document element, if it
/// is an <c>html</c> element, and null otherwise", an <c>html</c> element being one in the HTML namespace with
/// that local name. AngleSharp's <c>IDocument.Body</c> walks <c>DocumentElement.ChildNodes</c> without asking
/// what the document element is, so a document rooted at an XHTML <c>div</c> answers a nested <c>body</c> —
/// the standard's own counter-example. <c>Dom/divergences.md</c> records it.
/// </para>
/// <para>
/// <b>It is here rather than on <see cref="DomHostHooks"/> because two unrelated members need it.</b>
/// <c>document.body</c> is a hook; HTML §3.2.6.4's <c>document.dir</c> and §16.3.3's five obsolete colours are
/// <see cref="ReflectedAttribute"/> rows that reflect an attribute of <em>another</em> element
/// (<see cref="ReflectedTarget"/>), and each of those names one of these two elements in as many words. A gate
/// on one and not the other is how <c>document.bgColor</c> came to read a body the document does not have.
/// </para>
/// </remarks>
internal static class DomDocumentElements
{
    /// <summary>
    /// https://html.spec.whatwg.org/multipage/dom.html#the-html-element-2 — the document element when it is an
    /// <c>html</c> element in the HTML namespace, and <see langword="null"/> otherwise.
    /// </summary>
    internal static IElement? Html(IDocument document)
    {
        var root = document.DocumentElement;

        return root is not null
            && string.Equals(root.LocalName, "html", StringComparison.Ordinal)
            && string.Equals(root.NamespaceUri, NamespaceNames.HtmlUri, StringComparison.Ordinal)
            ? root
            : null;
    }

    /// <summary>
    /// https://html.spec.whatwg.org/multipage/dom.html#the-body-element — the first <c>body</c> or
    /// <c>frameset</c> child of <see cref="Html"/>, and <see langword="null"/> when there is no html element.
    /// </summary>
    /// <remarks>
    /// Past the gate the search is AngleSharp's, because that half already matches the standard: it takes the
    /// first child that is a body or a frameset and looks no deeper.
    /// </remarks>
    internal static IHtmlElement? Body(IDocument document) => Html(document) is null ? null : document.Body;
}
