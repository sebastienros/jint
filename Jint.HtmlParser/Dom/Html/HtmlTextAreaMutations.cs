namespace Jint.HtmlParser;

/// <summary>DOM child-text-content projection used by HTML textarea state.</summary>
internal static class HtmlTextAreaMutations
{
    internal static void ChildrenChanged(Node parent, bool mayShorten = true, bool markDocument = true)
    {
        if (parent is Element { NamespaceUri: Namespaces.Html, LocalName: "textarea" } element)
        {
            element.ExistingTextAreaState?.ChildrenChanged(mayShorten, markDocument);
        }
    }

    internal static string CollectChildText(Element element, CancellationToken cancellationToken)
        => CollectChildText(element, null, cancellationToken);

    internal static string CollectChildText(Element element, Action<int>? checkpoint,
        CancellationToken cancellationToken)
    {
        var work = new HtmlTextWork(cancellationToken, checkpoint);
        work.Check();
        long length = 0;
        for (var child = element.FirstChild; child is not null; child = child.NextSibling)
        {
            work.Step();
            var count = ChildLength(child);
            length = checked(length + count);
            for (var i = 0; i < count; i++) work.Step();
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
