using Jint.HtmlParser;

namespace Jint.Browser.Dom;

/// <summary>Form-owner lookup for the legacy keygen member retained by the binding contract.</summary>
internal static class DomLegacyKeygenForm
{
    internal static Element? Of(Element element, Action<int>? checkpoint, CancellationToken token)
    {
        if (element is not { NamespaceUri: Namespaces.Html, LocalName: "keygen" }) return null;
        var work = new DomReadWork(checkpoint, token);
        work.Check();
        var id = work.Attribute(element, "form");
        if (id is not null)
        {
            if (id.Length == 0 || work.Root(element) is not Document document)
            {
                work.Check();
                return null;
            }
            foreach (var candidate in NodeTraversal.DescendantElements(document, work.Check, token))
            {
                if (!work.Equal(work.Attribute(candidate, "id"), id)) continue;
                work.Check();
                // The first matching ID wins even when it belongs to a non-form element.
                return candidate is { NamespaceUri: Namespaces.Html, LocalName: "form" } ? candidate : null;
            }
        }
        else
        {
            for (var parent = element.ParentNode; parent is not null; parent = parent.ParentNode)
            {
                work.Step();
                if (parent is not Element { NamespaceUri: Namespaces.Html, LocalName: "form" } form) continue;
                work.Check();
                return form;
            }
        }
        work.Check();
        return null;
    }
}
