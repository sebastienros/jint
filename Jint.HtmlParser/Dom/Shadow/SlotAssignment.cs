namespace Jint.HtmlParser;

// DOM Standard §4.2.2.3–4.2.2.4. The three facts here deliberately differ:
// manual intent, a slottable's stored assignment, and a fresh lookup.
internal static class SlotAssignment
{
    internal static Element? FindSlot(Node slottable, bool openOnly, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(slottable);
        var work = new QueryWork(cancellationToken);
        if (!IsSlottable(slottable) || slottable.ParentNode is not Element { AttachedShadowRoot: { } root })
        {
            work.Finish();
            return null;
        }

        if (openOnly && root.Mode != ShadowRootMode.Open)
        {
            work.Finish();
            return null;
        }

        var result = FindSlotInRoot(slottable, root, ref work);
        work.Finish();
        return result;
    }

    internal static Element? GetAssignedSlot(Node slottable)
    {
        ArgumentNullException.ThrowIfNull(slottable);
        return IsSlottable(slottable) ? slottable.StoredAssignedSlot : null;
    }

    internal static IReadOnlyList<Node> AssignedNodes(Element slot, bool flatten,
        CancellationToken cancellationToken)
    {
        RequireSlot(slot);
        var work = new QueryWork(cancellationToken);
        List<Node> result;
        if (flatten)
        {
            result = Flatten(slot, ref work);
        }
        else
        {
            var assigned = slot.SlotState?.Assigned;
            result = assigned is null ? [] : Copy(assigned, ref work);
        }

        work.Finish();
        return Array.AsReadOnly(result.ToArray());
    }

    internal static IReadOnlyList<Element> AssignedElements(Element slot, bool flatten,
        CancellationToken cancellationToken)
    {
        RequireSlot(slot);
        var work = new QueryWork(cancellationToken);
        var nodes = flatten ? Flatten(slot, ref work) : slot.SlotState?.Assigned;
        var result = new List<Element>();
        if (nodes is not null)
        {
            foreach (var node in nodes)
            {
                work.Step();
                if (node is Element element)
                {
                    result.Add(element);
                }
            }
        }

        work.Finish();
        return Array.AsReadOnly(result.ToArray());
    }

    // HTML §4.12.4: assign(...nodes). Validation is complete before changing intent.
    internal static void Assign(Element slot, ReadOnlySpan<Node> nodes)
    {
        RequireSlot(slot);
        var unique = new List<Node>(nodes.Length);
        var seen = new HashSet<Node>(ReferenceEqualityComparer.Instance);
        foreach (var node in nodes)
        {
            if (node is null || !IsSlottable(node))
            {
                throw new ArgumentException("Only Element, Text, and CDATA nodes are slottable.", nameof(nodes));
            }

            if (seen.Add(node))
            {
                unique.Add(node);
            }
        }

        var state = slot.SlotState ??= new SlotElementState();
        foreach (var weak in state.Manual)
        {
            if (weak.TryGetTarget(out var old) && ManualTarget(old) == slot)
            {
                old.ManualSlot = null;
            }
        }

        var manual = new List<WeakReference<Node>>(unique.Count);
        foreach (var node in unique)
        {
            if (ManualTarget(node) is { } previous && !ReferenceEquals(previous, slot))
            {
                previous.SlotState?.RemoveManual(node);
            }

            node.ManualSlot = new WeakReference<Element>(slot);
            manual.Add(new WeakReference<Node>(node));
        }

        state.Manual = manual;
        if (slot.TreeShadowRoot is { } root)
        {
            Reassign(root);
        }
    }

    internal static void AttributeChanged(Element element, string? namespaceUri, string localName)
    {
        if (namespaceUri is not null)
        {
            return;
        }

        if (localName == "slot" && element.ParentNode is Element { AttachedShadowRoot: { } hostRoot } &&
            hostRoot.SlotAssignment == SlotAssignmentMode.Named)
        {
            Reassign(hostRoot);
        }
        else if (localName == "name" && IsSlot(element) && element.TreeShadowRoot is { } root)
        {
            Rebuild(root);
        }
    }

    internal static void AfterRemoval(Node oldParent, Node node)
    {
        var oldShadow = oldParent as ShadowRoot ?? oldParent.TreeShadowRoot;
        SetTreeShadowRoot(node, null);
        if (oldParent is Element { AttachedShadowRoot: { } hostRoot } && IsSlottable(node))
        {
            if (hostRoot.SlotState is not null)
            {
                Reassign(hostRoot);
            }
            else
            {
                node.StoredAssignedSlot = null;
            }
        }

        if (oldShadow is not null && oldParent is Element fallbackSlot && IsSlot(fallbackSlot) &&
            fallbackSlot.SlotState?.Assigned.Count is null or 0)
        {
            Signal(fallbackSlot);
        }

        if (oldShadow is { } root && ContainsSlot(node))
        {
            Rebuild(root);
            ClearDetachedSlots(node);
        }
    }

    internal static void AfterInsertion(Node parent, Node node, Node? referenceChild)
    {
        var shadow = parent as ShadowRoot ?? parent.TreeShadowRoot;
        SetTreeShadowRoot(node, shadow);
        if (parent is Element { AttachedShadowRoot: { } root } && IsSlottable(node))
        {
            if (root.SlotAssignment == SlotAssignmentMode.Named && referenceChild is null &&
                root.SlotState is { } state)
            {
                // The common append adds one identity. The owned assigned list is
                // never copied until a caller asks for a snapshot.
                var name = SlottableName(node);
                if (state.FirstByName.TryGetValue(name, out var slot))
                {
                    Signal(slot);
                    (slot.SlotState ??= new SlotElementState()).Assigned.Add(node);
                    node.StoredAssignedSlot = slot;
                }
            }
            else
            {
                Reassign(root);
            }
        }

        if (shadow is not null && parent is Element fallbackSlot && IsSlot(fallbackSlot) &&
            fallbackSlot.SlotState?.Assigned.Count is null or 0)
        {
            Signal(fallbackSlot);
        }

        if (shadow is not null && ContainsSlot(node))
        {
            Rebuild(shadow);
        }
    }

    private static void RequireSlot(Element? slot)
    {
        ArgumentNullException.ThrowIfNull(slot);
        if (!IsSlot(slot))
        {
            throw new ArgumentException("The receiver must be an HTML slot element.", nameof(slot));
        }
    }

    private static bool IsSlot(Element element)
        => element.NamespaceUri == Namespaces.Html && element.LocalName == "slot";

    private static bool IsSlottable(Node node) => node is Element or Text or CDataSection;

    private static string SlottableName(Node node)
        => node is Element element ? element.GetAttributeNS(null, "slot") ?? "" : "";

    private static string SlotName(Element slot) => slot.GetAttributeNS(null, "name") ?? "";

    private static Element? ManualTarget(Node node)
        => node.ManualSlot is { } weak && weak.TryGetTarget(out var slot) ? slot : null;

    private static Element? FindSlotInRoot(Node node, ShadowRoot root, ref QueryWork work)
    {
        work.Step();
        if (root.SlotAssignment == SlotAssignmentMode.Manual)
        {
            var manual = ManualTarget(node);
            if (manual is null)
            {
                return null;
            }

            work.Step();
            return ReferenceEquals(manual.TreeShadowRoot, root) ? manual : null;
        }

        var state = EnsureIndex(root);
        work.Step();
        return state.FirstByName.TryGetValue(SlottableName(node), out var slot) ? slot : null;
    }

    private static SlotTreeState EnsureIndex(ShadowRoot root)
        => root.SlotState ??= BuildIndex(root);

    private static SlotTreeState BuildIndex(ShadowRoot root)
    {
        var state = new SlotTreeState();
        var stack = new Stack<Node>();
        for (var child = root.LastChild; child is not null; child = child.PreviousSibling)
        {
            stack.Push(child);
        }

        while (stack.TryPop(out var node))
        {
            if (node is Element element && IsSlot(element))
            {
                state.Slots.Add(element);
                state.FirstByName.TryAdd(SlotName(element), element);
            }

            for (var child = node.LastChild; child is not null; child = child.PreviousSibling)
            {
                stack.Push(child);
            }
        }

        return state;
    }

    private static void Rebuild(ShadowRoot root)
    {
        var old = root.SlotState;
        var current = BuildIndex(root);
        root.SlotState = current;
        // DOM removal assigns the surviving tree before assigning the detached
        // subtree. This order is visible to the native signal collector.
        Reassign(root);
        if (old is not null)
        {
            var present = new HashSet<Element>(current.Slots, ReferenceEqualityComparer.Instance);
            foreach (var slot in old.Slots)
            {
                if (!present.Contains(slot))
                {
                    ClearAssigned(slot);
                }
            }
        }

    }

    private static void Reassign(ShadowRoot root)
    {
        var state = EnsureIndex(root);
        var next = new Dictionary<Element, List<Node>>(ReferenceEqualityComparer.Instance);
        foreach (var slot in state.Slots)
        {
            next.Add(slot, []);
        }

        if (root.SlotAssignment == SlotAssignmentMode.Named)
        {
            for (var child = root.Host.FirstChild; child is not null; child = child.NextSibling)
            {
                if (IsSlottable(child) && state.FirstByName.TryGetValue(SlottableName(child), out var slot))
                {
                    next[slot].Add(child);
                }
            }
        }
        else
        {
            foreach (var slot in state.Slots)
            {
                foreach (var weak in (slot.SlotState ??= new SlotElementState()).Manual)
                {
                    if (weak.TryGetTarget(out var node) && ReferenceEquals(node.ParentNode, root.Host) &&
                        ReferenceEquals(ManualTarget(node), slot))
                    {
                        next[slot].Add(node);
                    }
                }
            }
        }

        foreach (var slot in state.Slots)
        {
            var assigned = (slot.SlotState ??= new SlotElementState()).Assigned;
            foreach (var node in assigned)
            {
                if (ReferenceEquals(node.StoredAssignedSlot, slot))
                {
                    node.StoredAssignedSlot = null;
                }
            }
        }

        foreach (var slot in state.Slots)
        {
            var nodes = next[slot];
            if (!SameIdentityList(slot.SlotState!.Assigned, nodes))
            {
                Signal(slot);
            }

            (slot.SlotState ??= new SlotElementState()).Assigned = nodes;
            foreach (var node in nodes)
            {
                node.StoredAssignedSlot = slot;
            }
        }
    }

    private static void ClearAssigned(Element slot)
    {
        var assigned = slot.SlotState?.Assigned;
        if (assigned is null)
        {
            return;
        }

        if (assigned.Count != 0)
        {
            Signal(slot);
        }

        foreach (var node in assigned)
        {
            if (ReferenceEquals(node.StoredAssignedSlot, slot))
            {
                node.StoredAssignedSlot = null;
            }
        }

        assigned.Clear();
    }

    private static bool SameIdentityList(List<Node> first, List<Node> second)
    {
        if (first.Count != second.Count)
        {
            return false;
        }

        for (var i = 0; i < first.Count; i++)
        {
            if (!ReferenceEquals(first[i], second[i]))
            {
                return false;
            }
        }

        return true;
    }

    private static void Signal(Element slot) => slot.OwnerDocument?.SlotChangeSignal?.Invoke(slot);

    private static bool ContainsSlot(Node node)
    {
        if (node.FirstChild is null)
        {
            return node is Element element && IsSlot(element);
        }

        var stack = new Stack<Node>();
        stack.Push(node);
        while (stack.TryPop(out var current))
        {
            if (current is Element element && IsSlot(element))
            {
                return true;
            }

            for (var child = current.FirstChild; child is not null; child = child.NextSibling)
            {
                stack.Push(child);
            }
        }

        return false;
    }

    private static void ClearDetachedSlots(Node node)
    {
        var stack = new Stack<Node>();
        stack.Push(node);
        while (stack.TryPop(out var current))
        {
            if (current is Element element && IsSlot(element))
            {
                ClearAssigned(element);
            }

            for (var child = current.FirstChild; child is not null; child = child.NextSibling)
            {
                stack.Push(child);
            }
        }
    }

    private static List<Node> Flatten(Element slot, ref QueryWork work)
    {
        var result = new List<Node>();
        var stack = new Stack<FlattenFrame>();
        stack.Push(new FlattenFrame(FreshSlottables(slot, ref work)));
        while (stack.TryPop(out var frame))
        {
            work.Step();
            if (frame.Index == frame.Nodes.Count)
            {
                continue;
            }

            var node = frame.Nodes[frame.Index++];
            stack.Push(frame);
            if (node is Element nested && IsSlot(nested) && nested.TreeShadowRoot is not null)
            {
                stack.Push(new FlattenFrame(FreshSlottables(nested, ref work)));
            }
            else
            {
                result.Add(node);
            }
        }

        return result;
    }

    private static List<Node> FreshSlottables(Element slot, ref QueryWork work)
    {
        var result = new List<Node>();
        if (slot.TreeShadowRoot is not { } root)
        {
            return result;
        }

        if (root.SlotAssignment == SlotAssignmentMode.Manual)
        {
            foreach (var weak in slot.SlotState?.Manual ?? [])
            {
                work.Step();
                if (weak.TryGetTarget(out var node) && ReferenceEquals(node.ParentNode, root.Host) &&
                    ReferenceEquals(ManualTarget(node), slot))
                {
                    result.Add(node);
                }
            }
        }
        else
        {
            var first = EnsureIndex(root).FirstByName;
            for (var child = root.Host.FirstChild; child is not null; child = child.NextSibling)
            {
                work.Step();
                if (IsSlottable(child) && first.TryGetValue(SlottableName(child), out var found) &&
                    ReferenceEquals(found, slot))
                {
                    result.Add(child);
                }
            }
        }

        if (result.Count == 0)
        {
            for (var child = slot.FirstChild; child is not null; child = child.NextSibling)
            {
                work.Step();
                if (IsSlottable(child))
                {
                    result.Add(child);
                }
            }
        }

        return result;
    }

    private static List<Node> Copy(List<Node> source, ref QueryWork work)
    {
        var copy = new List<Node>(source.Count);
        foreach (var node in source)
        {
            work.Step();
            copy.Add(node);
        }

        return copy;
    }

    private static void SetTreeShadowRoot(Node node, ShadowRoot? root)
    {
        if (node.FirstChild is null)
        {
            node.TreeShadowRoot = root;
            return;
        }

        if (root is null && node.TreeShadowRoot is null)
        {
            return;
        }

        var stack = new Stack<Node>();
        stack.Push(node);
        while (stack.TryPop(out var current))
        {
            current.TreeShadowRoot = root;
            for (var child = current.FirstChild; child is not null; child = child.NextSibling)
            {
                stack.Push(child);
            }
        }
    }

    private struct FlattenFrame(List<Node> nodes)
    {
        internal List<Node> Nodes = nodes;
        internal int Index;
    }

    private struct QueryWork
    {
        private readonly CancellationToken _cancellationToken;
        private int _steps;

        internal QueryWork(CancellationToken cancellationToken)
        {
            _cancellationToken = cancellationToken;
            _cancellationToken.ThrowIfCancellationRequested();
        }

        internal void Step()
        {
            if ((++_steps & 255) == 0)
            {
                _cancellationToken.ThrowIfCancellationRequested();
            }
        }

        internal void Finish() => _cancellationToken.ThrowIfCancellationRequested();
    }
}

internal sealed class SlotTreeState
{
    internal List<Element> Slots { get; } = [];
    internal Dictionary<string, Element> FirstByName { get; } = new(StringComparer.Ordinal);
}

internal sealed class SlotElementState
{
    internal List<Node> Assigned = [];
    internal List<WeakReference<Node>> Manual = [];

    internal void RemoveManual(Node node)
    {
        for (var i = Manual.Count - 1; i >= 0; i--)
        {
            if (!Manual[i].TryGetTarget(out var target) || ReferenceEquals(target, node))
            {
                Manual.RemoveAt(i);
            }
        }
    }
}
