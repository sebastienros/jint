using System.Runtime.CompilerServices;
using Jint.Browser.Runtime;
using Jint.HtmlParser;
using Jint.WebApi.Url.Parsing;

namespace Jint.Browser.Dom;

/// <summary>Browser document metadata that does not belong to the native tree or a Realm.</summary>
internal sealed class DomDocumentState
{
    private static readonly ConditionalWeakTable<Document, DomDocumentState> States = new();

    internal static DomDocumentState Of(Document document) => States.GetValue(document, static _ => new DomDocumentState());

    internal string Url { get; set; } = "about:blank";
    internal string Referrer { get; set; } = "";
    internal Element? TargetElement { get; private set; }

    // HTML §7.4.6.4: navigation selects an identity, initially null. Later ID mutations
    // and history.pushState do not select a different element on a selector read.
    internal static void SelectNavigationTarget(DomRealm realm, Document document)
    {
        realm.Engine.Constraints.Check();
        var state = Of(document);
        Element? target = null;
        if (document.Kind == DocumentKind.Html && UrlParser.Parse(state.Url)?.Fragment is { Length: > 0 } fragment)
        {
            target = FindTarget(realm, document, fragment);
            if (target is null)
            {
                var decoded = PercentEncoding.DecodeToString(fragment);
                if (decoded != fragment) target = FindTarget(realm, document, decoded);
            }
        }
        realm.Engine.Constraints.Check();
        state.TargetElement = target;
    }

    private static Element? FindTarget(DomRealm realm, Document document, string fragment)
    {
        Element? anchor = null;
        // This native walk charges every node/link, including non-element runs and final ascents.
        foreach (var element in NodeTraversal.DescendantElements(document, realm.Engine.Constraints.Check, realm.CancellationToken))
        {
            if (element.GetAttribute("id") == fragment) return element;
            if (anchor is null && element.NamespaceUri == Namespaces.Html && element.LocalName == "a"
                && element.GetAttribute("name") == fragment) anchor = element;
        }
        return anchor;
    }

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
