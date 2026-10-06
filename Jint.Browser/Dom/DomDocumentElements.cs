using Jint.HtmlParser;

namespace Jint.Browser.Dom;

/// <summary>
/// HTML's html and body elements, selected using their namespace, parent and local name.
/// </summary>
/// <remarks>
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
    internal static Element? Html(Document document)
    {
        var root = document.DocumentElement;

        return root is not null
            && string.Equals(root.LocalName, "html", StringComparison.Ordinal)
            && string.Equals(root.NamespaceUri, Namespaces.Html, StringComparison.Ordinal)
            ? root
            : null;
    }

    internal static Element? Html(Document document, DomReadWork work)
    {
        for (var child = document.FirstChild; child is not null; child = child.NextSibling)
        {
            work.Step();
            if (child is not Element element) continue;
            return element is { NamespaceUri: Namespaces.Html, LocalName: "html" } ? element : null;
        }
        return null;
    }

    /// <summary>HTML §3.1: the first HTML head child of the html element.</summary>
    internal static Element? Head(Document document)
    {
        if (Html(document) is not { } html) return null;
        for (var child = html.FirstChild; child is not null; child = child.NextSibling)
        {
            if (child is Element { NamespaceUri: Namespaces.Html, LocalName: "head" } head) return head;
        }
        return null;
    }

    /// <summary>
    /// https://html.spec.whatwg.org/multipage/dom.html#the-body-element — the first <c>body</c> or
    /// <c>frameset</c> child of <see cref="Html(Document)"/>, and <see langword="null"/> when there is no html element.
    /// </summary>
    internal static Element? Body(Document document)
    {
        if (Html(document) is not { } html) return null;
        for (var child = html.FirstChild; child is not null; child = child.NextSibling)
        {
            if (child is Element { NamespaceUri: Namespaces.Html, LocalName: "body" or "frameset" } element)
                return element;
        }
        return null;
    }

    internal static Element? Body(Document document, DomReadWork work)
    {
        if (Html(document, work) is not { } html) return null;
        for (var child = html.FirstChild; child is not null; child = child.NextSibling)
        {
            work.Step();
            if (child is Element { NamespaceUri: Namespaces.Html, LocalName: "body" or "frameset" } element)
                return element;
        }
        return null;
    }
}
