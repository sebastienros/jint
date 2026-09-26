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
        return element.LocalName switch
        {
            "option" => new HtmlSelectInitialization(HtmlOptionMetadata.Read(attributes, ref work), null),
            "select" => new HtmlSelectInitialization(null, HtmlSelectMetadata.Read(attributes, ref work)),
            _ => default
        };
    }
    internal static void Initialize(Element element, HtmlSelectInitialization initialization)
    {
        if (initialization.Option is { } option)
        {
            var state = element.GetHtmlState()!.InitializeOption(option);
            if (!state.DirtySelectedness) state.Write(state.DefaultSelected, false);
        }
        else if (initialization.Select is { } select) element.GetHtmlState()!.InitializeSelect(select);
    }
    internal static void AttributeChanged(Element element, string? ns, string name, string? oldValue, string? newValue)
    {
        if (ns is not null || element.NamespaceUri != Namespaces.Html) return;
        if (element.LocalName == "option" && name is "selected" or "value" or "label" or "id" or "name")
        {
            var state = element.GetHtmlState()!.Option!;
            state.AttributeChanged(name, newValue);
            if (name != "selected" || (oldValue is null) == (newValue is null) || state.DirtySelectedness) return;
            var select = state.CachedNearestSelect?.GetHtmlState()!.Select;
            select?.Prepare(default);
            state.Write(newValue is not null, false);
            if (state.Selected) select?.ExcludePeers(state);
            select?.SetSelectedness(default);
        }
        else if (name == "disabled" && element.LocalName is "option" or "optgroup")
            HtmlSelectAncestry.GetNearestSelect(element, default)?.GetHtmlState()!.Select!.InvalidateFallback();
        else if (element.LocalName == "select" && name is "multiple" or "size")
        {
            var state = element.GetHtmlState()!.Select!;
            state.AttributeChanged(name, newValue);
            state.SetSelectedness(default);
        }
    }
    internal static void Inserted(Node node, bool markDocument = true)
    {
        UpdateNearest(node, true, markDocument);
        if (markDocument) HtmlSelectedContent.TreeChanged(node, null, true);
    }
    internal static void Removed(Node node, Node oldParent)
    {
        UpdateNearest(node, false, true);
        HtmlSelectedContent.TreeChanged(node, oldParent, false);
    }
    private static void UpdateNearest(Node root, bool insertion, bool markDocument)
    {
        if (root.FirstChild is null &&
            root is not Element { NamespaceUri: Namespaces.Html, LocalName: "option" } &&
            root is not Element { AttachedShadowRoot: not null }) return;
        var work = new HtmlSelectWork(root.OwnerDocument?.SelectWorkProbe, default);
        var entrants = new List<(HtmlOptionState State, bool Selected, Element? Old, Element? Next)>();
        var affected = new HashSet<HtmlSelectState>();
        var pending = new Stack<(Node Node, HtmlSelectAncestry.Context Context)>();
        pending.Push((root, HtmlSelectAncestry.GetContext(root, ref work)));
        while (pending.TryPop(out var frame))
        {
            work.Step();
            var node = frame.Node;
            var context = frame.Context;
            if (!ReferenceEquals(node, root) && node.NextSibling is { } sibling) pending.Push((sibling, context));
            if (node is Element { NamespaceUri: Namespaces.Html, LocalName: "option" } element)
            {
                var state = element.GetHtmlState()!.Option!;
                var old = state.CachedNearestSelect;
                var next = context.Select;
                if (!ReferenceEquals(old, next))
                {
                    entrants.Add((state, state.Selected, old, next));
                    if (old is not null) affected.Add(old.GetHtmlState()!.Select!);
                    if (next is not null) affected.Add(next.GetHtmlState()!.Select!);
                }
            }
            if (node.FirstChild is { } child) pending.Push((child, context.ForChildren(node)));
            if (node is Element { AttachedShadowRoot: { } shadow }) pending.Push((shadow, default));
        }
        // Publish the new ancestry before building each affected inventory once.
        // All descendants of a wrapper are already linked at this mutation boundary.
        foreach (var entry in entrants) entry.State.CachedNearestSelect = entry.Next;
        foreach (var select in affected)
        {
            var updated = false;
            if (entrants.Count == 1)
            {
                var entry = entrants[0];
                if (ReferenceEquals(entry.Old, select.Element)) updated = select.RemoveOption(entry.State.Element);
                else if (insertion && ReferenceEquals(root, entry.State.Element)) updated = select.AppendOption(entry.State.Element);
            }
            if (!updated) select.InvalidateMembership();
            select.Prepare(default);
        }
        foreach (var entry in entrants)
        {
            work.Step();
            var option = entry.State;
            // WPT inserted-or-removed: replay actual selectedness for each entrant,
            // never reconstruct it from selected attributes (which dirtiness froze).
            if (insertion && entry.Selected) option.Write(true, option.DirtySelectedness, markDocument);
            var nextState = entry.Next?.GetHtmlState()!.Select;
            if (insertion && option.Selected) nextState?.ExcludePeers(option, markDocument);
            entry.Old?.GetHtmlState()!.Select!.SetSelectedness(default, markDocument);
            nextState?.SetSelectedness(default, markDocument);
        }
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
