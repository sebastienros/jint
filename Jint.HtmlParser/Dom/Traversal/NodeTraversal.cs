namespace Jint.HtmlParser;

/// <summary>Iterative walks over ordinary native tree links.</summary>
internal static class NodeTraversal
{
    internal static IEnumerable<Element> DescendantElements(Node root, CancellationToken cancellationToken)
        => DescendantElementsCore(root, null, cancellationToken);

    // The per-enumeration checkpoint makes cancellation during a final, yield-free
    // ascent observable in tests without adding a shared hook to the tree.
    internal static IEnumerable<Element> DescendantElements(
        Node root, Action? checkpoint, CancellationToken cancellationToken)
        => DescendantElementsCore(root, checkpoint, cancellationToken);

    private static IEnumerable<Element> DescendantElementsCore(
        Node root, Action? checkpoint, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(root);
        cancellationToken.ThrowIfCancellationRequested();

        var work = 0;
        Node? current = root.FirstChild;
        CheckWork(ref work, checkpoint, cancellationToken);

        while (current is not null)
        {
            if (current is Element element)
            {
                yield return element;
            }

            var child = current.FirstChild;
            CheckWork(ref work, checkpoint, cancellationToken);
            if (child is not null)
            {
                current = child;
                continue;
            }

            while (true)
            {
                var sibling = current.NextSibling;
                CheckWork(ref work, checkpoint, cancellationToken);
                if (sibling is not null)
                {
                    current = sibling;
                    break;
                }

                current = current.ParentNode!;
                CheckWork(ref work, checkpoint, cancellationToken);
                if (ReferenceEquals(current, root))
                {
                    current = null;
                    break;
                }
            }
        }

        cancellationToken.ThrowIfCancellationRequested();
    }

    internal static Element? PreviousElementSibling(Node node, CancellationToken cancellationToken)
        => FindElementSibling(node, previous: true, null, cancellationToken);

    internal static Element? NextElementSibling(Node node, CancellationToken cancellationToken)
        => FindElementSibling(node, previous: false, null, cancellationToken);

    internal static Element? PreviousElementSibling(Node node, Action? checkpoint, CancellationToken cancellationToken)
        => FindElementSibling(node, previous: true, checkpoint, cancellationToken);

    internal static Element? NextElementSibling(Node node, Action? checkpoint, CancellationToken cancellationToken)
        => FindElementSibling(node, previous: false, checkpoint, cancellationToken);

    private static Element? FindElementSibling(
        Node node, bool previous, Action? checkpoint, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(node);
        cancellationToken.ThrowIfCancellationRequested();

        var work = 0;
        for (var current = previous ? node.PreviousSibling : node.NextSibling;
             current is not null;
             current = previous ? current.PreviousSibling : current.NextSibling)
        {
            CheckWork(ref work, checkpoint, cancellationToken);
            if (current is Element element)
            {
                cancellationToken.ThrowIfCancellationRequested();
                return element;
            }
        }

        cancellationToken.ThrowIfCancellationRequested();
        return null;
    }

    private static void CheckWork(ref int work, Action? checkpoint, CancellationToken cancellationToken)
    {
        if ((++work & 255) == 0)
        {
            checkpoint?.Invoke();
            cancellationToken.ThrowIfCancellationRequested();
        }
    }
}
