using System.Runtime.CompilerServices;
using Jint.HtmlParser;
using Jint.Native;

namespace Jint.Browser.Dom.Collections;

/// <summary>HTML §3.1's live document collections over native identities.</summary>
internal static class DomDocumentCollections
{
    private static readonly ConditionalWeakTable<Document, Dictionary<string, DomLiveHtmlCollection>> Collections = new();

    internal static JsValue Get(DomRealm realm, Document document, string kind)
    {
        var collections = Collections.GetValue(document, static _ => new(StringComparer.Ordinal));
        if (!collections.TryGetValue(kind, out var collection))
        {
            collection = new DomLiveHtmlCollection(document, new DocumentFilter(kind));
            collections.Add(kind, collection);
        }
        return kind == "all" ? realm.Wrap(collection, DomInterfaces.HTMLAllCollection) : realm.WrapCollection(collection);
    }

    internal static JsValue ByName(DomRealm realm, Document document, JsValue[] arguments)
    {
        var name = DomConvert.RequiredText(arguments, 0, "Document.getElementsByName");
        return realm.Wrap(new NamedNodeList(document, name), DomInterfaces.NodeList);
    }

    private sealed class DocumentFilter(string kind) : DomElementFilter
    {
        internal override bool Matches(Element element)
        {
            if (kind == "all") return true;
            if (element.NamespaceUri != Namespaces.Html) return false;
            return kind switch
            {
                "anchors" => element.LocalName == "a" && element.GetAttributeNS(null, "name") is not null,
                "forms" => element.LocalName == "form",
                "images" => element.LocalName == "img",
                "links" => element.LocalName is "a" or "area" && element.GetAttributeNS(null, "href") is not null,
                "scripts" => element.LocalName == "script",
                "plugins" => element.LocalName == "embed",
                "commands" => element.LocalName is "menuitem" or "button" or "a",
                _ => throw new InvalidOperationException("Unknown document collection: " + kind),
            };
        }
        internal override bool Matches(Element element, DomReadWork work)
        {
            if (kind == "all") return true;
            if (element.NamespaceUri != Namespaces.Html) return false;
            return kind switch
            {
                "anchors" => element.LocalName == "a" && work.Attribute(element, "name") is not null,
                "links" => element.LocalName is "a" or "area" && work.Attribute(element, "href") is not null,
                _ => Matches(element),
            };
        }
    }

    private sealed class NamedNodeList(Document document, string name) : DomNodeList
    {
        internal override int Length
        {
            get
            {
                var count = 0;
                foreach (var unused in Matches()) count++;
                return count;
            }
        }
        internal override Node this[int index]
        {
            get
            {
                ArgumentOutOfRangeException.ThrowIfNegative(index);
                foreach (var element in Matches())
                {
                    if (index-- == 0) return element;
                }
                throw new ArgumentOutOfRangeException(nameof(index));
            }
        }
        internal override int ReadLength(Action<int>? checkpoint, CancellationToken token)
        {
            var count = 0;
            foreach (var unused in Matches(checkpoint, token)) count++;
            return count;
        }
        internal override Node? ReadItem(uint index, Action<int>? checkpoint, CancellationToken token)
        {
            foreach (var element in Matches(checkpoint, token))
            {
                if (index-- != 0) continue;
                token.ThrowIfCancellationRequested();
                checkpoint?.Invoke(0);
                token.ThrowIfCancellationRequested();
                return element;
            }
            return null;
        }
        private IEnumerable<Element> Matches(Action<int>? checkpoint = null, CancellationToken token = default)
        {
            var work = new DomReadWork(checkpoint, token);
            work.Check();
            foreach (var element in NodeTraversal.DescendantElements(document, work.Check, token))
                if (element.NamespaceUri == Namespaces.Html && work.Equal(work.Attribute(element, "name"), name))
                    yield return element;
            work.Check();
        }
    }
}
