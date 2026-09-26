namespace Jint.HtmlParser;

/// <summary>HTML §4.10.17 selectedcontent connection, cloning and primary-element rules.</summary>
internal static class HtmlSelectedContent
{
    internal readonly record struct Update(Element Target, DocumentFragment? Fragment)
    {
        internal void Apply() => Target.ReplaceChildren(Fragment);
    }
    internal static Update? PrepareUpdate(HtmlSelectState select, Element? option, CancellationToken token)
        => PrepareUpdate(select.Element.GetSelectCore(token), option, token);
    private static Update? PrepareUpdate(HtmlSelectCore select, Element? option, CancellationToken token)
    {
        var target = select.GetEnabledSelectedContent(token);
        if (target is null || select.UpdatingSelectedContent) return null;
        if (option is null) return new Update(target, null);
        var fragment = option.OwnerDocument!.CreateDocumentFragment();
        var work = new HtmlSelectWork(option.OwnerDocument.SelectWorkProbe, token);
        work.Check();
        for (var child = option.FirstChild; child is not null; child = child.NextSibling)
        {
            work.Step();
            var clone = NodeCloner.Clone(child, option.OwnerDocument, true, cancellationToken: token);
            fragment.AppendClonedChild(clone, token);
        }
        work.Check();
        return new Update(target, fragment);
    }
    internal static void Apply(HtmlSelectState select, Update? update)
        => Apply(select.Element.GetSelectCore(), update);
    private static void Apply(HtmlSelectCore select, Update? update)
    {
        if (update is null) return;
        select.UpdatingSelectedContent = true;
        try { update.Value.Apply(); }
        finally { select.UpdatingSelectedContent = false; }
    }
    internal static void UpdateCurrent(HtmlSelectState select, CancellationToken token)
        => UpdateCurrent(select.Element.GetSelectCore(token), token);
    private static void UpdateCurrent(HtmlSelectCore select, CancellationToken token)
    {
        Element? option = null;
        foreach (var candidate in HtmlSelectCore.Enumerate(select.Element, token))
            if (candidate.GetOptionCore(token).Selected) { option = candidate; break; }
        var prepared = PrepareUpdate(select, option, token);
        token.ThrowIfCancellationRequested();
        Apply(select, prepared);
    }
    internal static void MaybeCloneOption(HtmlOptionState option, CancellationToken token)
        => MaybeCloneOption(option.Element, token);
    internal static void MaybeCloneOption(Element element, CancellationToken token)
    {
        var option = element.GetOptionCore(token);
        if (!option.Selected || option.CachedNearestSelect is not { } owner) return;
        var select = owner.GetSelectCore(token);
        var prepared = PrepareUpdate(select, option.Element, token);
        token.ThrowIfCancellationRequested();
        Apply(select, prepared);
    }
    internal static void TreeChanged(Node root, Node? oldParent, bool insertion)
    {
        if (root.FirstChild is null &&
            root is not Element { NamespaceUri: Namespaces.Html, LocalName: "selectedcontent" } &&
            root is not Element { AttachedShadowRoot: not null }) return;
        // Capture before cloning can replace links inside this subtree.
        var work = new HtmlSelectWork(root.OwnerDocument?.SelectWorkProbe, default);
        var selectedContents = new List<Element>();
        var pending = new Stack<Node>();
        pending.Push(root);
        while (pending.TryPop(out var node))
        {
            work.Step();
            if (node is Element element)
            {
                if (element is { NamespaceUri: Namespaces.Html, LocalName: "selectedcontent" }) selectedContents.Add(element);
                if (element.AttachedShadowRoot is { } shadow) pending.Push(shadow);
            }
            for (var child = node.LastChild; child is not null; child = child.PreviousSibling) { work.Step(); pending.Push(child); }
        }
        foreach (var content in selectedContents)
        {
            var nearest = InvalidateAncestors(content.ParentNode);
            var oldSelect = InvalidateAncestors(oldParent);
            if (insertion && ShadowTree.IsConnected(content, default))
            {
                var view = content.GetHtmlState()!;
                view.SelectedContentDisabled = false;
                Element? firstSelect = null;
                for (var ancestor = content.ParentNode; ancestor is not null; ancestor = ancestor.ParentNode)
                {
                    work.Step();
                    if (ancestor is not Element { NamespaceUri: Namespaces.Html } html) continue;
                    if (html.LocalName == "select")
                    {
                        if (firstSelect is null) { firstSelect = html; continue; }
                        view.SelectedContentDisabled = true;
                        break;
                    }
                    if (html.LocalName is "option" or "selectedcontent") { view.SelectedContentDisabled = true; break; }
                }
                if (view.SelectedContentDisabled || firstSelect is null) continue;
                var select = firstSelect.GetSelectCore();
                if (select.Multiple || select.UpdatingSelectedContent) continue;
                UpdateCurrent(select, default);
                ClearNonPrimary(select);
            }
            else if (!insertion && !content.GetHtmlState()!.SelectedContentDisabled && nearest is null && oldSelect is not null)
                UpdateCurrent(oldSelect.GetSelectCore(), default);
        }
    }
    private static Element? InvalidateAncestors(Node? node)
    {
        Element? nearest = null;
        for (; node is not null; node = node.ParentNode)
        {
            if (node is not Element { NamespaceUri: Namespaces.Html, LocalName: "select" } select) continue;
            nearest ??= select;
            select.ExistingSelectCore?.InvalidateSelectedContent();
        }
        return nearest;
    }
    private static void ClearNonPrimary(HtmlSelectCore select)
    {
        var candidates = new List<Element>();
        foreach (var element in NodeTraversal.DescendantElements(select.Element, default))
            if (element is { NamespaceUri: Namespaces.Html, LocalName: "selectedcontent" }) candidates.Add(element);
        select.UpdatingSelectedContent = true;
        try { for (var i = 1; i < candidates.Count; i++) candidates[i].ReplaceChildren(); }
        finally { select.UpdatingSelectedContent = false; }
    }
}
