using Jint.Browser.Accessibility;
using Jint.Browser.Extraction;
using Jint.HtmlParser;
using Jint.Native;

namespace Jint.Browser.Dom;

/// <summary>HTML innerText through Browser's documented rendering approximation and native replace-all.</summary>
internal static class DomInnerText
{
    internal static string Get(DomRealm realm, Element element)
    {
        var work = new DomReadWork(realm.NativeReadCheckpoint, realm.CancellationToken);
        work.Check();
        var visibility = new ElementVisibility(useComputedStyle: true, work);
        var rendered = !ImplicitRole.IsMetadataContent(element);
        Document? connectedDocument = null;
        // A hidden ancestor can answer without demanding stylesheet syntax or computed values.
        for (Node? node = element; node is not null; node = RenderingParent(node))
        {
            work.Step();
            if (node is Document document) connectedDocument = document;
            if (node is Element ancestor && work.Attribute(ancestor, "hidden") is not null) rendered = false;
        }
        rendered &= connectedDocument is not null;
        var traversal = rendered ? visibility.CreateTraversal(element.OwnerDocument) : null;
        for (Node? node = element; rendered && node is not null; node = RenderingParent(node))
        {
            work.Step();
            if (node is Element ancestor && visibility.RenderingReasonFor(ancestor, traversal) is AxIgnoredReason.Hidden or AxIgnoredReason.NotRendered)
                rendered = false;
        }
        work.Check();
        return rendered
            ? TextExtractor.InnerText(element, useComputedStyle: true, realm.NativeReadCheckpoint, realm.CancellationToken)
            : DomDescendantText.Read(element, realm.NativeReadCheckpoint, realm.CancellationToken);
    }

    private static Node? RenderingParent(Node node) => node.ParentNode ?? (node as ShadowRoot)?.Host;

    // HTML §3.2.7: build the text/br fragment before replacing the target's children once.
    internal static JsValue Set(DomRealm realm, Element element, string value)
    {
        using var mutation = realm.MutateLayout();
        var work = new DomReadWork(realm.NativeReadCheckpoint, realm.CancellationToken);
        work.Check();
        var document = element.OwnerDocument!;
        var fragment = document.CreateDocumentFragment();
        var start = 0;
        for (var i = 0; i < value.Length; i++)
        {
            work.Step();
            if (value[i] is not ('\r' or '\n')) continue;
            AppendText(i);
            work.Check();
            fragment.AppendChild(document.CreateElementNS(Namespaces.Html, "br"));
            if (value[i] == '\r' && i + 1 < value.Length && value[i + 1] == '\n') { work.Step(); i++; }
            start = i + 1;
        }
        AppendText(value.Length);
        // Cancellation before publication preserves the existing child tree. The native atomic boundary
        // may exceed a checkpoint quantum; cancellation after publication propagates without rollback.
        for (var child = element.FirstChild; child is not null; child = child.NextSibling) work.Step();
        work.Check();
        element.ReplaceChildren(fragment);
        work.Check();
        return JsValue.Undefined;

        void AppendText(int end)
        {
            if (end == start) return;
            for (var offset = start; offset < end; offset++) work.Step();
            work.Check();
            var text = value.Substring(start, end - start);
            work.Check();
            fragment.AppendChild(document.CreateTextNode(text));
        }
    }
}
