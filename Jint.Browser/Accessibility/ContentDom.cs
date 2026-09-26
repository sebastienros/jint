using System.Text;
using Jint.HtmlParser;
using Jint.HtmlParser.Html;
using Jint.Browser.Dom;
using Jint.Browser.Styling;
using Jint.HtmlParser.Css.Values;

namespace Jint.Browser.Accessibility;

/// <summary>Engine-free content queries over the document's ordinary native links.</summary>
internal static partial class ContentDom
{
    /// <summary>Parses inert HTML for the engine-free content algorithms.</summary>
    internal static Document Parse(string html, HtmlParseOptions? options = null, CancellationToken cancellationToken = default,
        Action? checkpoint = null)
    {
        ArgumentNullException.ThrowIfNull(html);
        var document = Document.CreateHtml();
        var session = new HtmlParserSession(document, options ?? new HtmlParseOptions { ScriptingEnabled = false },
            scriptingMode: HtmlParserScriptingMode.Inert);
        session.AppendInput(html, isFinal: true);
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            checkpoint?.Invoke();
            var step = session.Drive(4096, cancellationToken);
            if (step.Kind == HtmlParseStepKind.Complete)
            {
                InstallCompletedStyles(document, new CssValueWork(cancellationToken, checkpoint));
                return document;
            }
            if (step.Kind != HtmlParseStepKind.Yielded)
                throw new InvalidOperationException("The native inert HTML parser could not complete: " + step.Kind + ".");
        }
    }

    // The inert parser has completed. Register only connected ordinary-tree styles, never fetch links
    // or parse CSS here. This is a completion boundary, not a query-time repair or a live mutation hook.
    private static void InstallCompletedStyles(Document document, CssValueWork work)
    {
        var pending = new Stack<Node>();
        pending.Push(document);
        while (pending.TryPop(out var node))
        {
            work.Charge(1);
            for (var child = node.LastChild; child is not null; child = child.PreviousSibling)
            { work.Charge(1); pending.Push(child); }
            if (node is not Element { LocalName: "style", NamespaceUri: Namespaces.Html or Namespaces.Svg } owner) continue;
            var text = DomDescendantText.Read(owner, work.Charge, work.Token);
            NativeCssStyleSheets.Install(document, owner, text, "", "", work);
        }
        work.CheckCancellation();
    }

    internal static bool HasAttribute(this Element element, string name) => element.GetAttributeNode(name) is not null;

    internal static IEnumerable<Element> Children(Node node)
    {
        for (var child = node.FirstChild; child is not null; child = child.NextSibling)
        {
            if (child is Element element) yield return element;
        }
    }

    internal static IEnumerable<Element> Descendants(Node root)
        => NodeTraversal.DescendantElements(root, CancellationToken.None);

    internal static Element? NextElementSibling(Node node)
        => NodeTraversal.NextElementSibling(node, CancellationToken.None);

    internal static Element? ElementById(Node root, string id)
        => id.Length == 0 ? null : Descendants(root).FirstOrDefault(element => element.GetAttribute("id") == id);

    internal static Element? First(Node root, string localName)
        => Descendants(root).FirstOrDefault(element => element.LocalName == localName);

    // HTML §3.1.7: an embedded SVG title is not the HTML document's title.
    // https://html.spec.whatwg.org/multipage/dom.html#document.title
    internal static string DocumentTitle(Document document)
    {
        var title = document.DocumentElement is { NamespaceUri: Namespaces.Svg, LocalName: "svg" } svg
            ? Children(svg).FirstOrDefault(element => element is { NamespaceUri: Namespaces.Svg, LocalName: "title" })
            : Descendants(document).FirstOrDefault(element => HtmlName(element) == "title");
        if (title is null) return string.Empty;

        var text = new StringBuilder();
        foreach (var child in title.ChildNodes)
        {
            if (child is Text data) text.Append(data.Data);
            else if (child is CDataSection cdata) text.Append(cdata.Data);
        }
        return text.ToString();
    }

    // DOM §4.4: descendant Text data, including CDATASection, in tree order.
    internal static string TextContent(Node root)
    {
        var text = new StringBuilder();
        var current = root.FirstChild;
        while (current is not null)
        {
            if (current is Text data) text.Append(data.Data);
            else if (current is CDataSection cdata) text.Append(cdata.Data);
            if (current.FirstChild is { } child)
            {
                current = child;
                continue;
            }
            while (current.NextSibling is null && !ReferenceEquals(current.ParentNode, root))
                current = current.ParentNode!;
            current = current.NextSibling;
        }
        return text.ToString();
    }

    internal static IEnumerable<string> ClassNames(Element element)
        => (element.GetAttribute("class") ?? string.Empty).Split([' ', '\t', '\n', '\r', '\f'], StringSplitOptions.RemoveEmptyEntries);

    internal static string? HtmlName(Element element) => element.NamespaceUri == Namespaces.Html ? element.LocalName : null;

    internal static string InputType(Element element) => HtmlInputTypes.Info(HtmlInputTypes.Get(element)).Keyword;
}
