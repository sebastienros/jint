namespace Jint.HtmlParser;

/// <summary>Stored form ownership and synchronous HTML insertion/removal hooks.</summary>
internal static class HtmlFormAssociation
{
    internal static Node OrdinaryRoot(Node node)
        => OrdinaryRoot(node, null, default);

    internal static Node OrdinaryRoot(Node node, Action<int>? checkpoint,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(node);
        cancellationToken.ThrowIfCancellationRequested();
        var steps = 1;
        while (node.ParentNode is { } parent)
        {
            node = parent;
            steps++;
            if ((steps & 255) == 0)
            {
                checkpoint?.Invoke(steps);
                cancellationToken.ThrowIfCancellationRequested();
            }
        }

        if (node.FormWorkProbe is { } probe)
        {
            for (var i = 0; i < steps; i++)
            {
                probe.Visit();
            }
        }

        cancellationToken.ThrowIfCancellationRequested();
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

        StoreOwner(element, state, null);
        if (hasExplicitForm && ShadowTree.IsConnected(element, default))
        {
            var id = formAttribute!.Value;
            if (id.Length != 0 && HtmlFormIndex.GetOrCreate(OrdinaryRoot(element)).FirstWithId(id) is
                { NamespaceUri: Namespaces.Html, LocalName: "form" } form)
            {
                StoreOwner(element, state, form);
            }

            return;
        }

        StoreOwner(element, state, nearest);
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
        StoreOwner(element, state, form);
        state.ParserInserted = true;
    }

    internal static void AttributeChanged(Element element, string? namespaceUri, string localName,
        string? oldValue, string? newValue)
    {
        if (namespaceUri is not null)
        {
            return;
        }

        if (localName == "form" && HtmlFormState.IsListed(element))
        {
            OrdinaryRoot(element).FormIndex?.ChangeReference(element, oldValue, newValue);
            ResetOwner(element);
        }
        else if (localName == "id" && oldValue != newValue)
        {
            if (element.OwnerDocument?.HasFormIndex != true)
            {
                return;
            }

            var index = OrdinaryRoot(element).FormIndex;
            if (index is null)
            {
                return;
            }

            index.ChangeId(element, oldValue, newValue);
            ResetReferences(index);
        }
    }

    internal static FormRemoval BeforeRemoval(Node node, Node parent)
    {
        HtmlDisabledness.DirectChildChanged(node, parent);
        if (MayContainAssociated(node))
        {
            var removalRoot = OrdinaryRoot(node);
            foreach (var element in ShadowIncludingAssociated(node, null))
                HtmlInputStateChanges.BeforeRemoval(element, removalRoot);
        }
        if (!MayAffectForms(node) || node.OwnerDocument?.HasFormIndex != true)
        {
            return new FormRemoval(null, false, node.OwnerDocument?.FormWorkProbe);
        }

        var root = OrdinaryRoot(parent);
        var index = root.FormIndex;
        if (index is null)
        {
            return new FormRemoval(null, false, root.FormWorkProbe);
        }

        var hasIds = false;
        foreach (var element in OrdinaryElements(node, root.FormWorkProbe))
        {
            if (element.GetAttributeNodeNS(null, "id") is not null)
            {
                hasIds = true;
            }

            index.Remove(element);
        }

        return new FormRemoval(index, hasIds, root.FormWorkProbe);
    }

    internal static void Removed(Node node, FormRemoval removal)
    {
        // The DOM removing steps observe the tree after unlinking. Keep ownership
        // when a form and its control leave together in the same ordinary tree.
        if (MayContainAssociated(node))
        {
            foreach (var element in ShadowIncludingAssociated(node, removal.Probe))
            {
                var owner = element.FormAssociationState?.Owner;
                if (owner is not null && !ReferenceEquals(OrdinaryRoot(element), OrdinaryRoot(owner)))
                {
                    ResetOwner(element);
                }
            }
        }

        if (removal.Index is { } index && removal.HasIds)
        {
            ResetReferences(index);
        }
    }

    internal static void Inserted(Node node)
    {
        HtmlDisabledness.DirectChildChanged(node, node.ParentNode);
        if (!MayContainAssociated(node) &&
            (node is not Element leaf || leaf.GetAttributeNodeNS(null, "id") is null ||
             node.OwnerDocument?.HasFormIndex != true))
        {
            return;
        }

        var root = OrdinaryRoot(node);
        if (!ReferenceEquals(root, node))
        {
            // An index once owned by a detached root cannot become an index of
            // its new parent's tree. No process-wide root table retains it.
            node.FormIndex = null;
            node.RadioIndex?.Retire();
        }

        var index = root.FormIndex;
        var hasIds = false;
        if (index is not null)
        {
            foreach (var element in OrdinaryElements(node, root.FormWorkProbe))
            {
                index.Add(element);
                if (element.GetAttributeNodeNS(null, "id") is not null)
                {
                    hasIds = true;
                }
            }
        }

        if (MayContainAssociated(node))
        {
            foreach (var element in ShadowIncludingAssociated(node, root.FormWorkProbe))
            {
                element.WasInserted = true;
                if (element.FormAssociationState?.ParserInserted != true)
                {
                    ResetOwner(element);
                }
                HtmlInputStateChanges.Inserted(element);
            }
        }

        if (index is not null && hasIds)
        {
            ResetReferences(index);
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

    private static bool MayAffectForms(Node node)
        => MayContainAssociated(node) || node is Element element &&
            element.GetAttributeNodeNS(null, "id") is not null;

    private static void StoreOwner(Element element, HtmlFormAssociationState state, Element? owner)
    {
        if (ReferenceEquals(state.Owner, owner)) return;
        var oldOwner = state.Owner;
        state.Owner = owner;
        element.OwnerDocument?.CheckedWorkProbe?.OwnerStore?.Invoke(element, oldOwner, owner);
        HtmlInputStateChanges.OwnerChanged(element);
    }

    private static void ResetReferences(HtmlFormIndex index)
    {
        foreach (var control in index.ResetCandidates())
        {
            index.Probe?.ResetCandidate();
            ResetOwner(control);
        }
    }

    private static IEnumerable<Element> OrdinaryElements(Node node, HtmlFormWorkProbe? probe)
    {
        if (node is Element root)
        {
            probe?.Visit();
            yield return root;
        }

        foreach (var element in NodeTraversal.DescendantElements(node, default))
        {
            probe?.Visit();
            yield return element;
        }
    }

    private static IEnumerable<Element> ShadowIncludingAssociated(Node node, HtmlFormWorkProbe? probe)
    {
        var pending = new Stack<Node>();
        pending.Push(node);
        while (pending.TryPop(out var current))
        {
            probe?.Visit();
            if (current is Element element)
            {
                if (HtmlFormState.IsFormAssociated(element))
                {
                    yield return element;
                }
            }

            for (var child = current.LastChild; child is not null; child = child.PreviousSibling)
            {
                pending.Push(child);
            }
            if (current is Element { AttachedShadowRoot: { } shadow }) pending.Push(shadow);
        }
    }
}

internal readonly record struct FormRemoval(HtmlFormIndex? Index, bool HasIds,
    HtmlFormWorkProbe? Probe);
