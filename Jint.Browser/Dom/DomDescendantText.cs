using System.Text;
using Jint.HtmlParser;

namespace Jint.Browser.Dom;

/// <summary>DOM descendant text from actual native light-tree nodes, without wrappers or enhanced HTML state.</summary>
internal static class DomDescendantText
{
    internal static string Read(Node root, Action<int>? checkpoint, CancellationToken cancellationToken)
        => ReadCore(root, checkpoint, cancellationToken, descendants: true);

    internal static string ReadChildren(Node root, Action<int>? checkpoint, CancellationToken cancellationToken)
        => ReadCore(root, checkpoint, cancellationToken, descendants: false);

    private static string ReadCore(Node root, Action<int>? checkpoint, CancellationToken cancellationToken, bool descendants)
    {
        var work = new DomReadWork(checkpoint, cancellationToken);
        work.Check();
        var result = new StringBuilder();
        for (var node = root.FirstChild; node is not null;)
        {
            work.Step();
            if (node is Text text)
            {
                for (var offset = 0; offset < text.DataLength; offset++)
                {
                    work.Step();
                    result.Append(text.DataAt(offset));
                }
            }
            else if (node is CDataSection cdata)
            {
                var data = cdata.Data;
                for (var offset = 0; offset < data.Length; offset++)
                {
                    work.Step();
                    result.Append(data[offset]);
                }
            }
            if (descendants && node.FirstChild is { } child) { node = child; continue; }
            while (descendants && node.NextSibling is null && !ReferenceEquals(node.ParentNode, root))
            {
                work.Step();
                node = node.ParentNode!;
            }
            work.Step();
            node = node.NextSibling;
        }
        work.Check();
        var value = result.ToString();
        work.Check();
        return value;
    }
}
