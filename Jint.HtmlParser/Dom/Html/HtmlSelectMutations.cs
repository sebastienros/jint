using System.Runtime.CompilerServices;
namespace Jint.HtmlParser;

/// <summary>HTML option insertion/removing steps and attribute selectedness transitions.</summary>
internal static class HtmlSelectMutations
{
    internal static HtmlSelectInitialization PrepareInitialization(Element element, IEnumerable<Attr> attributes,
        CancellationToken token)
    {
        if (element.NamespaceUri != Namespaces.Html) return default;
        var work = new HtmlSelectWork(element.OwnerDocument?.SelectWorkProbe, token);
        work.Check();
        if (element.LocalName == "option")
        {
            if (element.ExistingOptionState is not null)
            {
                var metadata = HtmlOptionMetadata.Read(attributes, ref work);
                return new HtmlSelectInitialization(metadata.DefaultSelected, metadata, null);
            }
            return new HtmlSelectInitialization(HtmlOptionCore.ReadDefault(attributes, ref work), null, null);
        }
        if (element.LocalName == "select" && element.ExistingSelectCore is not null)
            return new HtmlSelectInitialization(null, null, HtmlSelectMetadata.Read(attributes, ref work));
        return default;
    }
    internal static void Initialize(Element element, HtmlSelectInitialization initialization)
    {
        if (initialization.Selected is { } selected)
        {
            var core = element.InitializeOptionCore(selected);
            if (!core.DirtySelectedness) core.Write(selected, false);
            if (initialization.Option is { } option) element.ExistingOptionState!.ApplyMetadata(option);
        }
        if (initialization.Select is { } select) element.ExistingSelectCore!.ApplyMetadata(select);
    }
    internal static void AttributeChanged(Element element, string? ns, string name, string? oldValue, string? newValue, HtmlSelectWorkContext? context = null)
    {
        if (ns is not null || element.NamespaceUri != Namespaces.Html) return;
        if (element.LocalName == "option" && name is "selected" or "value" or "label" or "id" or "name")
        {
            element.ExistingOptionState?.AttributeChanged(name, newValue);
            if (name != "selected") return;
            if (context?.AttributeSelection is { } prepared && ReferenceEquals(prepared.Option.Element, element))
            {
                prepared.Apply();
                return;
            }
            var state = element.InitializeOptionCore(oldValue is not null);
            if ((oldValue is null) == (newValue is null) || state.DirtySelectedness) return;
            var select = state.CachedNearestSelect?.GetSelectCoreWithWork(context, context?.Token ?? default);
            select?.PrepareTransitionWithWork(true, context, context?.Token ?? default);
            state.Write(newValue is not null, false);
            if (state.Selected) select?.ExcludePeers(state);
            select?.SetSelectednessWithWork(true, context, context?.Token ?? default);
        }
        else if (name == "disabled" && element.LocalName is "option" or "optgroup")
            HtmlSelectAncestry.GetNearestSelect(element, default)?.ExistingSelectCore?.InvalidateFallback();
        else if (element.LocalName == "select" && name is "multiple" or "size")
        {
            var state = element.GetSelectCoreWithWork(context, context?.Token ?? default);
            state.AttributeChanged(name, newValue, context);
            state.SetSelectednessWithWork(true, context, context?.Token ?? default);
        }
    }
    internal static void Inserted(Node node, bool markDocument = true, CancellationToken cancellationToken = default)
        => InsertedWithWork(node, markDocument, (HtmlSelectWorkContext?) null, cancellationToken);
    internal static void InsertedWithWork(Node node, bool markDocument, HtmlSelectWorkContext? context, CancellationToken cancellationToken = default)
    {
        UpdateNearestWithWork(node, true, markDocument, context, cancellationToken);
        if (markDocument) HtmlSelectedContent.TreeChanged(node, null, true, context);
    }
    internal static void Removed(Node node, Node oldParent, HtmlSelectWorkContext? context = null)
    {
        UpdateNearestWithWork(node, false, true, context, context?.Token ?? default);
        HtmlSelectedContent.TreeChanged(node, oldParent, false, context);
    }
    // A parsed leaf reaches this on every insertion: keep the guard inlinable, the walk's frame apart.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void UpdateNearestWithWork(Node root, bool insertion, bool markDocument, HtmlSelectWorkContext? context, CancellationToken token)
    {
        if (root.FirstChild is null &&
            root is not Element { NamespaceUri: Namespaces.Html, LocalName: "option" } &&
            root is not Element { AttachedShadowRoot: not null }) return;
        UpdateNearestCore(root, insertion, markDocument, context, token);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void UpdateNearestCore(Node root, bool insertion, bool markDocument, HtmlSelectWorkContext? context, CancellationToken token)
    {
        var work = new HtmlSelectWork(root.OwnerDocument?.SelectWorkProbe, context, token);
        var entrants = new List<(HtmlOptionCore State, bool Selected, Element? Old, Element? Next)>();
        var affected = new HashSet<HtmlSelectCore>();
        var pending = new Stack<(Node Node, HtmlSelectAncestry.Context Context)>();
        pending.Push((root, HtmlSelectAncestry.GetContext(root, ref work)));
        while (pending.TryPop(out var frame))
        {
            work.Step();
            var node = frame.Node;
            var ancestry = frame.Context;
            if (!ReferenceEquals(node, root) && node.NextSibling is { } sibling) pending.Push((sibling, ancestry));
            if (node is Element { NamespaceUri: Namespaces.Html, LocalName: "option" } element)
            {
                var state = element.GetOptionCoreWithWork(context, token);
                var old = state.CachedNearestSelect;
                var next = ancestry.Select;
                if (!ReferenceEquals(old, next))
                {
                    entrants.Add((state, state.Selected, old, next));
                    if (old is not null) affected.Add(old.GetSelectCoreWithWork(context, token));
                    if (next is not null) affected.Add(next.GetSelectCoreWithWork(context, token));
                }
            }
            if (node.FirstChild is { } child) pending.Push((child, ancestry.ForChildren(node)));
            if (node is Element { AttachedShadowRoot: { } shadow }) pending.Push((shadow, default));
        }
        // Publish ancestry before preparing native transitions or updating existing caches.
        // All descendants of a wrapper are already linked at this mutation boundary.
        foreach (var entry in entrants) entry.State.CachedNearestSelect = entry.Next;
        foreach (var select in affected)
        {
            var append = false;
            Element? entrant = null;
            var view = select.Element.ExistingSelectState;
            if (entrants.Count == 1)
            {
                var entry = entrants[0];
                if (ReferenceEquals(entry.Old, select.Element))
                {
                    if (view?.RemoveOption(entry.State.Element) != true) view?.InvalidateMembership();
                }
                else
                {
                    entrant = entry.State.Element;
                    append = insertion && ReferenceEquals(root, entrant) && IsTail(entrant, select.Element, ref work);
                    if (!append || view?.AppendOption(entrant) != true) view?.InvalidateMembership();
                }
            }
            else view?.InvalidateMembership();
            select.MembershipChangedWithWork(insertion, append, entrant, context, token);
            select.PrepareTransitionWithWork(markDocument, context, token);
        }
        foreach (var entry in entrants)
        {
            work.Step();
            var option = entry.State;
            // WPT inserted-or-removed: replay actual selectedness for each entrant,
            // never reconstruct it from selected attributes (which dirtiness froze).
            if (insertion && entry.Selected) option.Write(true, option.DirtySelectedness, markDocument);
            var nextState = entry.Next?.GetSelectCoreWithWork(context, token);
            if (insertion && option.Selected) nextState?.ExcludePeers(option, markDocument);
            entry.Old?.GetSelectCoreWithWork(context, token).SetSelectednessWithWork(markDocument, context, token);
            nextState?.SetSelectednessWithWork(markDocument, context, token);
        }
    }

    private static bool IsTail(Node option, Element select, ref HtmlSelectWork work)
    {
        for (var node = option; !ReferenceEquals(node, select); node = node.ParentNode!)
        {
            work.Step();
            if (node.NextSibling is not null || node.ParentNode is null) return false;
        }
        return true;
    }

    internal static Node? Next(Node node, Node root, bool skip)
    {
        var work = new HtmlSelectWork(null, default);
        return Next(node, root, skip, ref work);
    }
    internal static Node? Next(Node node, Node root, bool skip, ref HtmlSelectWork work)
    {
        if (!skip && node.FirstChild is { } child) return child;
        while (!ReferenceEquals(node, root))
        {
            work.Step();
            if (node.NextSibling is { } sibling) return sibling;
            node = node.ParentNode!;
        }
        return null;
    }
}
