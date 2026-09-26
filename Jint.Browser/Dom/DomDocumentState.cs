using System.Runtime.CompilerServices;
using Jint.Browser.Runtime;
using Jint.HtmlParser;

namespace Jint.Browser.Dom;

/// <summary>Browser document metadata that does not belong to the native tree or a Realm.</summary>
internal sealed class DomDocumentState
{
    private static readonly ConditionalWeakTable<Document, DomDocumentState> States = new();

    internal static DomDocumentState Of(Document document) => States.GetValue(document, static _ => new DomDocumentState());

    internal string Url { get; set; } = "about:blank";
    internal string Referrer { get; set; } = "";

    // HTML §2.4.3: the first HTML base element with href sets the document base URL.
    internal static string BaseUri(Document document)
    {
        var url = Of(document).Url;
        if (document.Kind != DocumentKind.Html) return url;
        foreach (var element in NodeTraversal.DescendantElements(document, CancellationToken.None))
        {
            if (element.NamespaceUri == Namespaces.Html && element.LocalName == "base" &&
                element.GetAttribute("href") is { } href)
                return PageUrl.Resolve(href, url) ?? url;
        }
        return url;
    }
}
