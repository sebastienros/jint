using System.Runtime.CompilerServices;
using Jint.Browser.Runtime;
using Jint.HtmlParser;
using Jint.Native;
using Jint.WebApi.Url.Parsing;

namespace Jint.Browser.Dom;

/// <summary>Browser document metadata that does not belong to the native tree or a Realm.</summary>
internal sealed class DomDocumentState
{
    private static readonly ConditionalWeakTable<Document, DomDocumentState> States = new();

    internal static DomDocumentState Of(Document document) => States.GetValue(document, static _ => new DomDocumentState());

    // HTML §6.8.2. Read-only predicate consumers do not allocate an editing sidecar.
    internal static bool IsDesignModeEnabled(Document document)
        => States.TryGetValue(document, out var state) && state.DesignModeEnabled;

    internal bool DesignModeEnabled { get; set; }
    internal DomDocumentOrigin Origin { get; set; } = DomDocumentOrigin.Opaque();
    internal DateTimeOffset? SourceLastModified { get; set; }

    internal string Url { get; set; } = "about:blank";
    internal string Referrer { get; set; } = "";
    internal string ReadyState { get; set; } = "complete";
    internal string CharacterSet { get; set; } = Jint.WebApi.Encoding.EncodingLabels.Utf8Name;
    internal Element? TargetElement { get; private set; }

    // HTML §7.4.6.4: navigation selects an identity, initially null. Later ID mutations
    // and history.pushState do not select a different element on a selector read.
    internal static void SelectNavigationTarget(DomRealm realm, Document document)
    {
        realm.Engine.Constraints.Check();
        var state = Of(document);
        var work = new TargetWork(realm.Engine.Constraints.Check, realm.CancellationToken);
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

    private sealed class TargetWork(Action? checkpoint, CancellationToken token)
    {
        private int _work;
        internal CancellationToken Token { get; } = token;
        internal void Check()
        {
            Token.ThrowIfCancellationRequested();
            checkpoint?.Invoke();
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

    // HTML §2.4.3: about:blank/srcdoc can carry the creator's about base URL.
    // The parser/navigation entry point sets this when creating that document.
    internal string? AboutBaseUrl { get; set; }

    internal static string FallbackBaseUri(Document document)
    {
        var state = Of(document);
        return state.Url is "about:blank" or "about:srcdoc" && state.AboutBaseUrl is { } aboutBase
            ? aboutBase : state.Url;
    }

    // https://html.spec.whatwg.org/multipage/semantics.html#dom-base-href
    // This getter deliberately ignores every base element, including its receiver.
    internal static JsValue BaseHref(DomRealm realm, Element element)
    {
        realm.Engine.Constraints.Check();
        var value = element.GetAttribute("href") ?? "";
        var href = PageUrl.Resolve(value, FallbackBaseUri(element.OwnerDocument!)) ?? value;
        realm.Engine.Constraints.Check();
        return JsString.Create(href);
    }

    // HTML §2.4.3: the first HTML base element with href sets the document base URL.
    internal static string BaseUri(Document document, Action? checkpoint = null, CancellationToken token = default)
    {
        var fallback = FallbackBaseUri(document);
        if (document.Kind != DocumentKind.Html) return fallback;
        var work = new TargetWork(checkpoint, token);
        work.Check();
        foreach (var element in NodeTraversal.DescendantElements(document, work.Check, token))
        {
            if (!work.Equal(element.NamespaceUri, Namespaces.Html) || !work.Equal(element.LocalName, "base")) continue;
            for (uint i = 0; i < (uint) element.AttributeCount; i++)
            {
                work.Step();
                var attribute = element.GetAttributeAt(i)!;
                if (attribute.NamespaceUri is not null || !work.Equal(attribute.LocalName, "href")) continue;
                var url = PageUrl.Parse(attribute.Value, fallback);
                work.Check();
                return url is null || url.Scheme is "data" or "javascript" ? fallback : url.Serialize();
            }
        }
        work.Check();
        return fallback;
    }
}
