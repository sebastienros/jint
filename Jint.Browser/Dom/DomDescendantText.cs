using System.Text;
using Jint.HtmlParser;

namespace Jint.Browser.Dom;

/// <summary>DOM descendant text from actual native light-tree nodes, without wrappers or enhanced HTML state.</summary>
internal static class DomDescendantText
{
    internal static string Read(Node root, Action<int>? checkpoint, CancellationToken cancellationToken)
    {
        var work = new DomReadWork(checkpoint, cancellationToken);
        work.Check();
        var result = new StringBuilder();
        for (var node = root.FirstChild; node is not null;)
        {
            work.Step();
            var data = node switch { Text text => text.Data, CDataSection cdata => cdata.Data, _ => null };
            if (data is not null)
            {
                for (var offset = 0; offset < data.Length;)
                {
                    work.Check();
                    var count = Math.Min(256, data.Length - offset);
                    result.Append(data, offset, count);
                    offset += count;
                }
            }
            if (node.FirstChild is { } child) { node = child; continue; }
            while (node.NextSibling is null && !ReferenceEquals(node.ParentNode, root))
            {
                work.Step();
                node = node.ParentNode!;
            }
            work.Step();
            node = node.NextSibling;
        }
        work.Check();
        return result.ToString();
    }
}
