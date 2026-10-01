using System.Runtime.CompilerServices;
namespace Jint.HtmlParser;

/// <summary>HTML §4.10.17 selectedcontent connection, cloning and primary-element rules.</summary>
internal static class HtmlSelectedContent
{
    internal readonly record struct Update(Element Target, DocumentFragment? Fragment)
    {
        internal void Apply() => Target.ReplaceChildren(Fragment);
    }
    internal static Update? PrepareUpdate(HtmlSelectState select, Element? option, CancellationToken token)
        => PrepareUpdateWithWork(select, option, (HtmlSelectWorkContext?) null, token);
    internal static Update? PrepareUpdateWithWork(HtmlSelectState select, Element? option, HtmlSelectWorkContext? context, CancellationToken token)
        => PrepareUpdateWithWork(select.Element.GetSelectCoreWithWork(context, token), option, context, token);
    private static Update? PrepareUpdateWithWork(HtmlSelectCore select, Element? option, HtmlSelectWorkContext? context, CancellationToken token)
    {
        var target = select.GetEnabledSelectedContentWithWork(context, token);
        if (target is null || select.UpdatingSelectedContent) return null;
        if (option is null) return new Update(target, null);
        var fragment = option.OwnerDocument!.CreateDocumentFragment();
        var work = new HtmlSelectWork(option.OwnerDocument.SelectWorkProbe, context, token);
        work.Check();
        for (var child = option.FirstChild; child is not null; child = child.NextSibling)
        {
            work.Step();
            var clone = NodeCloner.CloneWithWork(child, option.OwnerDocument, true, fallbackRegistry: null, context: context, cancellationToken: token);
            fragment.AppendClonedChild(clone, context, token);
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
        => UpdateCurrentWithWork(select, (HtmlSelectWorkContext?) null, token);
    internal static void UpdateCurrentWithWork(HtmlSelectState select, HtmlSelectWorkContext? context, CancellationToken token)
        => UpdateCurrentWithWork(select.Element.GetSelectCoreWithWork(context, token), context, token);
    private static void UpdateCurrentWithWork(HtmlSelectCore select, HtmlSelectWorkContext? context, CancellationToken token)
    {
        Element? option = null;
        foreach (var candidate in HtmlSelectCore.EnumerateWithWork(select.Element, context, token))
            if (candidate.GetOptionCoreWithWork(context, token).Selected) { option = candidate; break; }
        var prepared = PrepareUpdateWithWork(select, option, context, token);
        token.ThrowIfCancellationRequested();
        Apply(select, prepared);
    }
    internal static void MaybeCloneOption(HtmlOptionState option, CancellationToken token)
        => MaybeCloneOptionWithWork(option, (HtmlSelectWorkContext?) null, token);
    internal static void MaybeCloneOptionWithWork(HtmlOptionState option, HtmlSelectWorkContext? context, CancellationToken token)
        => MaybeCloneOptionWithWork(option.Element, context, token);
    internal static void MaybeCloneOption(Element element, CancellationToken token)
        => MaybeCloneOptionWithWork(element, (HtmlSelectWorkContext?) null, token);
    internal static void MaybeCloneOptionWithWork(Element element, HtmlSelectWorkContext? context, CancellationToken token)
    {
        var option = element.GetOptionCoreWithWork(context, token);
        if (!option.Selected || option.CachedNearestSelect is not { } owner) return;
        var select = owner.GetSelectCoreWithWork(context, token);
        var prepared = PrepareUpdateWithWork(select, option.Element, context, token);
        token.ThrowIfCancellationRequested();
        Apply(select, prepared);
    }
    // Every insertion and removal reaches this, so the common no-selectedcontent case must not pay
    // the walk's frame setup: the guard stays small enough to inline and the walk lives apart.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void TreeChanged(Node root, Node? oldParent, bool insertion, HtmlSelectWorkContext? context = null)
    {
        if ((root as Document ?? root.OwnerDocument) is { MayHaveSelectedContent: false }) return;
        TreeChangedCore(root, oldParent, insertion, context);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void TreeChangedCore(Node root, Node? oldParent, bool insertion, HtmlSelectWorkContext? context)
    {
        if (root.FirstChild is null &&
            root is not Element { NamespaceUri: Namespaces.Html, LocalName: "selectedcontent" } &&
            root is not Element { AttachedShadowRoot: not null }) return;
        // Capture before cloning can replace links inside this subtree.
        var work = new HtmlSelectWork(root.OwnerDocument?.SelectWorkProbe, context, context?.Token ?? default);
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
                var select = firstSelect.GetSelectCoreWithWork(context, context?.Token ?? default);
                if (select.Multiple || select.UpdatingSelectedContent) continue;
                UpdateCurrentWithWork(select, context, context?.Token ?? default);
                ClearNonPrimary(select, context);
            }
            else if (!insertion && !content.GetHtmlState()!.SelectedContentDisabled && nearest is null && oldSelect is not null)
                UpdateCurrentWithWork(oldSelect.GetSelectCoreWithWork(context, context?.Token ?? default), context, context?.Token ?? default);
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
    private static void ClearNonPrimary(HtmlSelectCore select, HtmlSelectWorkContext? context)
    {
        var candidates = new List<Element>();
        var work = new HtmlSelectWork(select.Element.OwnerDocument?.SelectWorkProbe, context, context?.Token ?? default);
        for (var node = select.Element.FirstChild; node is not null; node = HtmlSelectMutations.Next(node, select.Element, false, ref work))
        {
            work.Step();
            if (node is Element { NamespaceUri: Namespaces.Html, LocalName: "selectedcontent" } element) candidates.Add(element);
        }
        work.Check();
        select.UpdatingSelectedContent = true;
        try { for (var i = 1; i < candidates.Count; i++) candidates[i].ReplaceChildren(); }
        finally { select.UpdatingSelectedContent = false; }
    }
}
