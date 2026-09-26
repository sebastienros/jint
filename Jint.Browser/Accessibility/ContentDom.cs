using System.Text;
using Jint.HtmlParser;
using Jint.HtmlParser.Html;

namespace Jint.Browser.Accessibility;

/// <summary>Engine-free content queries over the document's ordinary native links.</summary>
internal static partial class ContentDom
{
    /// <summary>Parses inert HTML for the engine-free content algorithms.</summary>
    internal static Document Parse(string html, HtmlParseOptions? options = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(html);
        var document = Document.CreateHtml();
        var session = new HtmlParserSession(document, options ?? new HtmlParseOptions { ScriptingEnabled = false },
            scriptingMode: HtmlParserScriptingMode.Inert);
        session.AppendInput(html, isFinal: true);
        while (true)
        {
            var step = session.Drive(4096, cancellationToken);
            if (step.Kind == HtmlParseStepKind.Complete) return document;
            if (step.Kind != HtmlParseStepKind.Yielded)
                throw new InvalidOperationException("The native inert HTML parser could not complete: " + step.Kind + ".");
        }
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

    internal static string InputType(Element element) => HtmlInputTypes.Info(HtmlInputTypes.Get(element)).Keyword;
}
