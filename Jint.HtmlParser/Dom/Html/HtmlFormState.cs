namespace Jint.HtmlParser;

/// <summary>HTML Living Standard §4.10.18.3, form association of built-in elements.</summary>
internal static class HtmlFormState
{
    internal static bool IsFormAssociated(Element element)
    {
        ArgumentNullException.ThrowIfNull(element);
        return element.NamespaceUri == Namespaces.Html && element.LocalName is
            "button" or "fieldset" or "input" or "object" or "output" or "select" or "textarea" or "img";
    }

    internal static bool IsListed(Element element)
    {
        ArgumentNullException.ThrowIfNull(element);
        return element.NamespaceUri == Namespaces.Html && element.LocalName is
            "button" or "fieldset" or "input" or "object" or "output" or "select" or "textarea";
    }

    internal static Element? GetOwner(Element element)
    {
        ArgumentNullException.ThrowIfNull(element);
        return IsFormAssociated(element) ? element.FormAssociationState?.Owner : null;
    }

    internal static IReadOnlyList<Element> SnapshotAssociatedElements(Element form, CancellationToken cancellationToken)
        => Snapshot(form, listedOnly: false, null, cancellationToken);

    // Per-call checkpoint exercises cancellation during the root ascent in tests.
    internal static IReadOnlyList<Element> SnapshotAssociatedElements(Element form,
        Action<int>? rootWorkCheckpoint, CancellationToken cancellationToken)
        => Snapshot(form, listedOnly: false, rootWorkCheckpoint, cancellationToken);

    internal static IReadOnlyList<Element> SnapshotFormControls(Element form, CancellationToken cancellationToken)
        => Snapshot(form, listedOnly: true, null, cancellationToken);

    internal static IReadOnlyList<Element> SnapshotFieldsetControls(Element fieldset, CancellationToken cancellationToken)
    {
        RequireElement(fieldset, "fieldset");
        var result = new List<Element>();
        foreach (var element in NodeTraversal.DescendantElements(fieldset, cancellationToken))
        {
            if (IsListed(element))
            {
                result.Add(element);
            }
        }

        cancellationToken.ThrowIfCancellationRequested();
        return result.AsReadOnly();
    }

    internal static void ResetOwner(Element element) => HtmlFormAssociation.ResetOwner(element);

    internal static void AssociateFromParser(Element element, Element form)
        => HtmlFormAssociation.AssociateFromParser(element, form);

    private static System.Collections.ObjectModel.ReadOnlyCollection<Element> Snapshot(Element form, bool listedOnly,
        Action<int>? rootWorkCheckpoint, CancellationToken cancellationToken)
    {
        RequireElement(form, "form");
        var root = HtmlFormAssociation.OrdinaryRoot(form, rootWorkCheckpoint, cancellationToken);
        var result = new List<Element>();
        if (root is Element rootElement)
        {
            Add(rootElement);
        }

        foreach (var element in NodeTraversal.DescendantElements(root, cancellationToken))
        {
            Add(element);
        }

        cancellationToken.ThrowIfCancellationRequested();
        return result.AsReadOnly();

        void Add(Element element)
        {
            if (!ReferenceEquals(GetOwner(element), form) ||
                listedOnly && (!IsListed(element) || IsImageInput(element)))
            {
                return;
            }

            result.Add(element);
        }
    }

    private static bool IsImageInput(Element element)
        => element.NamespaceUri == Namespaces.Html && element.LocalName == "input" &&
           string.Equals(element.GetAttributeNodeNS(null, "type")?.Value, "image",
               StringComparison.OrdinalIgnoreCase);

    private static void RequireElement(Element element, string localName)
    {
        ArgumentNullException.ThrowIfNull(element);
        if (element.NamespaceUri != Namespaces.Html || element.LocalName != localName)
        {
            throw new ArgumentException($"An HTML {localName} element is required.", nameof(element));
        }
    }
}

internal sealed class HtmlFormAssociationState
{
    internal Element? Owner;
    internal bool ParserInserted;
}
