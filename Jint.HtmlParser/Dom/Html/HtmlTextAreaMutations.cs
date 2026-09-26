namespace Jint.HtmlParser;

/// <summary>DOM child-text-content projection used by HTML textarea state.</summary>
internal static class HtmlTextAreaMutations
{
    internal static void ChildrenChanged(Node parent, bool mayShorten = true, bool markDocument = true,
        uint? knownApiLength = null)
    {
        if (parent is Element { NamespaceUri: Namespaces.Html, LocalName: "textarea" } element)
        {
            element.ExistingTextAreaState?.ChildrenChanged(mayShorten, markDocument, knownApiLength);
        }
    }

    // Replace-all removes direct children from first to last. A backwards pass
    // gives the normalized length after each removal, including CRLF split over
    // adjacent text nodes. This retains each clamp step without rescanning every
    // remaining prefix of a long textarea.
    internal static uint[]? RemovalSuffixLengths(Node parent)
    {
        if (parent is not Element { NamespaceUri: Namespaces.Html, LocalName: "textarea" } element ||
            element.ExistingTextAreaState is not { NeedsRemovalLengths: true })
        {
            return null;
        }

        var lengths = new uint[parent.ChildCount];
        long count = 0;
        var nextIsLf = false;
        var index = parent.ChildCount;
        for (var child = parent.LastChild; child is not null; child = child.PreviousSibling)
        {
            var length = ChildLength(child);
            for (var i = length - 1; i >= 0; i--)
            {
                var character = ChildCharAt(child, i);
                if (character != '\r' || !nextIsLf) count++;
                nextIsLf = character == '\n';
            }

            lengths[--index] = (uint) Math.Min(count, int.MaxValue);
        }

        return lengths;
    }

    internal static string CollectChildText(Element element, CancellationToken cancellationToken)
        => CollectChildText(element, null, cancellationToken);

    internal static string CollectChildText(Element element, Action<int>? checkpoint,
        CancellationToken cancellationToken)
    {
        var work = new HtmlTextWork(cancellationToken, checkpoint);
        return CollectChildText(element, ref work);
    }

    internal static string CollectChildText(Element element, ref HtmlTextWork work)
    {
        work.Check();
        long length = 0;
        for (var child = element.FirstChild; child is not null; child = child.NextSibling)
        {
            work.Step();
            var count = ChildLength(child);
            length = checked(length + count);
        }

        work.Check();
        var result = new char[checked((int) length)];
        var offset = 0;
        for (var child = element.FirstChild; child is not null; child = child.NextSibling)
        {
            work.Step();
            var count = ChildLength(child);
            for (var i = 0; i < count; i++)
            {
                work.Step();
                result[offset++] = ChildCharAt(child, i);
            }
        }

        work.Check();
        var text = new string(result);
        work.Check();
        return text;
    }

    private static int ChildLength(Node child) => child switch
    {
        Text text => text.DataLength,
        CDataSection cdata => cdata.Data.Length,
        _ => 0
    };

    private static char ChildCharAt(Node child, int index) => child switch
    {
        Text text => text.DataAt(index),
        CDataSection cdata => cdata.Data[index],
        _ => throw new InvalidOperationException("Only text children are copied.")
    };
}
