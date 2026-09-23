namespace Jint.HtmlParser;

/// <summary>Stored form ownership and synchronous HTML insertion/removal hooks.</summary>
internal static class HtmlFormAssociation
{
    internal static Node OrdinaryRoot(Node node)
    {
        while (node.ParentNode is { } parent)
        {
            node = parent;
        }

        return node;
    }

    internal static void ResetOwner(Element element)
    {
        ArgumentNullException.ThrowIfNull(element);
        if (!HtmlFormState.IsFormAssociated(element))
        {
            throw new InvalidOperationException("Only form-associated elements have a form owner.");
        }

        var state = element.FormAssociationState ??= new HtmlFormAssociationState();
        state.ParserInserted = false;
        var nearest = NearestAncestorForm(element);
        var formAttribute = HtmlFormState.IsListed(element)
            ? element.GetAttributeNodeNS(null, "form")
            : null;
        var hasExplicitForm = formAttribute is not null;
        if (state.Owner is not null && !hasExplicitForm && ReferenceEquals(state.Owner, nearest))
        {
            return;
        }

        state.Owner = null;
        if (hasExplicitForm && ShadowTree.IsConnected(element, default))
        {
            var id = formAttribute!.Value;
            if (id.Length != 0 && HtmlFormIndex.GetOrCreate(OrdinaryRoot(element)).FirstWithId(id) is
                { NamespaceUri: Namespaces.Html, LocalName: "form" } form)
            {
                state.Owner = form;
            }

            return;
        }

        state.Owner = nearest;
    }

    internal static void AssociateFromParser(Element element, Element form)
    {
        ArgumentNullException.ThrowIfNull(element);
        ArgumentNullException.ThrowIfNull(form);
        if (!HtmlFormState.IsFormAssociated(element) ||
            form.NamespaceUri != Namespaces.Html || form.LocalName != "form" ||
            (HtmlFormState.IsListed(element) && element.GetAttributeNodeNS(null, "form") is not null) ||
            element.WasInserted || element.ParentNode is not null || element.FirstChild is not null ||
            element.PreviousSibling is not null || element.NextSibling is not null ||
            element.MutationRegistrations is not null ||
            !ReferenceEquals(element.OwnerDocument, form.OwnerDocument) ||
            element.FormAssociationState is not null)
        {
            throw new InvalidOperationException("Parser association requires a fresh built-in control and a form in its node document.");
        }

        var state = element.FormAssociationState ??= new HtmlFormAssociationState();
        state.Owner = form;
        state.ParserInserted = true;
    }

    internal static void AttributeChanged(Element element, string? namespaceUri, string localName,
        string? oldValue, string? newValue)
    {
        if (namespaceUri is not null || oldValue == newValue)
        {
            return;
        }

        if (localName == "form" && HtmlFormState.IsListed(element))
        {
            OrdinaryRoot(element).FormIndex?.ChangeReference(element, oldValue, newValue);
            ResetOwner(element);
        }
        else if (localName == "id")
        {
            var index = OrdinaryRoot(element).FormIndex;
            if (index is null)
            {
                return;
            }

            index.ChangeId(element, oldValue, newValue);
            ResetReferences(index, oldValue);
            if (newValue != oldValue)
            {
                ResetReferences(index, newValue);
            }
        }
    }

    internal static FormRemoval BeforeRemoval(Node node, Node parent)
    {
        var index = OrdinaryRoot(parent).FormIndex;
        if (index is null)
        {
            return default;
        }

        List<string>? ids = null;
        foreach (var element in OrdinaryElements(node))
        {
            if (element.GetAttributeNodeNS(null, "id")?.Value is { Length: > 0 } id)
            {
                (ids ??= []).Add(id);
            }

            index.Remove(element);
        }

        return new FormRemoval(index, ids);
    }

    internal static void Removed(Node node, FormRemoval removal)
    {
        // The DOM removing steps observe the tree after unlinking. Keep ownership
        // when a form and its control leave together in the same ordinary tree.
        if (MayContainAssociated(node))
        {
            foreach (var element in ShadowIncludingAssociated(node))
            {
                var owner = element.FormAssociationState?.Owner;
                if (owner is not null && !ReferenceEquals(OrdinaryRoot(element), OrdinaryRoot(owner)))
                {
                    ResetOwner(element);
                }
            }
        }

        if (removal.Index is { } index && removal.Ids is { } ids)
        {
            foreach (var id in ids)
            {
                ResetReferences(index, id);
            }
        }
    }

    internal static void Inserted(Node node)
    {
        var root = OrdinaryRoot(node);
        if (!ReferenceEquals(root, node))
        {
            // An index once owned by a detached root cannot become an index of
            // its new parent's tree. No process-wide root table retains it.
            node.FormIndex = null;
        }

        var index = root.FormIndex;
        List<string>? ids = null;
        if (index is not null)
        {
            foreach (var element in OrdinaryElements(node))
            {
                index.Add(element);
                if (element.GetAttributeNodeNS(null, "id")?.Value is { Length: > 0 } id)
                {
                    (ids ??= []).Add(id);
                }
            }
        }

        if (MayContainAssociated(node))
        {
            foreach (var element in ShadowIncludingAssociated(node))
            {
                element.WasInserted = true;
                if (element.FormAssociationState?.ParserInserted != true)
                {
                    ResetOwner(element);
                }
            }
        }

        if (index is not null && ids is not null)
        {
            foreach (var id in ids)
            {
                ResetReferences(index, id);
            }
        }
    }

    private static Element? NearestAncestorForm(Node node)
    {
        for (var parent = node.ParentNode; parent is not null; parent = parent.ParentNode)
        {
            if (parent is Element { NamespaceUri: Namespaces.Html, LocalName: "form" } form)
            {
                return form;
            }
        }

        return null;
    }

    private static bool MayContainAssociated(Node node)
        => node.FirstChild is not null || node is Element element &&
            (HtmlFormState.IsFormAssociated(element) || element.AttachedShadowRoot is not null);

    private static void ResetReferences(HtmlFormIndex index, string? id)
    {
        if (string.IsNullOrEmpty(id))
        {
            return;
        }

        foreach (var control in index.Referencing(id))
        {
            ResetOwner(control);
        }
    }

    private static IEnumerable<Element> OrdinaryElements(Node node)
    {
        if (node is Element root)
        {
            yield return root;
        }

        foreach (var element in NodeTraversal.DescendantElements(node, default))
        {
            yield return element;
        }
    }

    private static IEnumerable<Element> ShadowIncludingAssociated(Node node)
    {
        var pending = new Stack<Node>();
        pending.Push(node);
        while (pending.TryPop(out var current))
        {
            if (current is Element element)
            {
                if (HtmlFormState.IsFormAssociated(element))
                {
                    yield return element;
                }

                if (element.AttachedShadowRoot is { } shadow)
                {
                    pending.Push(shadow);
                }
            }

            for (var child = current.LastChild; child is not null; child = child.PreviousSibling)
            {
                pending.Push(child);
            }
        }
    }
}

internal readonly record struct FormRemoval(HtmlFormIndex? Index, List<string>? Ids);
