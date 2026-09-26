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
        var work = new TargetWork(realm);
        Element? target = null;
        if (document.Kind == DocumentKind.Html && UrlParser.Parse(state.Url)?.Fragment is { Length: > 0 } fragment)
        {
            target = FindTarget(document, fragment, work);
            if (target is null)
            {
                var decoded = PercentEncoding.DecodeToString(fragment);
                work.Check();
                if (!ReferenceEquals(decoded, fragment)) target = FindTarget(document, decoded, work);
            }
        }
        realm.Engine.Constraints.Check();
        state.TargetElement = target;
    }

    private static Element? FindTarget(Document document, string fragment, TargetWork work)
    {
        Element? anchor = null;
        // This native walk charges every node/link, including non-element runs and final ascents.
        foreach (var element in NodeTraversal.DescendantElements(document, work.Check, work.Token))
        {
            var isAnchor = anchor is null && work.Equal(element.NamespaceUri, Namespaces.Html) && work.Equal(element.LocalName, "a");
            for (uint i = 0; i < (uint) element.AttributeCount; i++)
            {
                work.Step();
                var attribute = element.GetAttributeAt(i)!;
                if (attribute.NamespaceUri is not null) continue;
                if (work.Equal(attribute.LocalName, "id") && work.Equal(attribute.Value, fragment)) return element;
                if (isAnchor && work.Equal(attribute.LocalName, "name") && work.Equal(attribute.Value, fragment)) anchor = element;
            }
        }
        return anchor;
    }

    private sealed class TargetWork(DomRealm realm)
    {
        private int _work;
        internal CancellationToken Token { get; } = realm.CancellationToken;
        internal void Check()
        {
            Token.ThrowIfCancellationRequested();
            realm.Engine.Constraints.Check();
        }
        internal void Step()
        {
            if ((++_work & 255) == 0) Check();
        }
        internal bool Equal(string? left, string right)
        {
            Step();
            if (left is null || left.Length != right.Length) return false;
            for (var i = 0; i < left.Length; i++)
            {
                Step();
                if (left[i] != right[i]) return false;
            }
            return true;
        }
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
