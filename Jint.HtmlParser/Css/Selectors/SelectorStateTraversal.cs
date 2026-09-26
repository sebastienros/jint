namespace Jint.HtmlParser.Css.Selectors;

internal static class SelectorStateTraversal
{
    internal static Node OrdinaryRoot(Node node, ref SelectorMatchWork work)
    {
        work.Check();
        while (node.ParentNode is { } parent)
        {
            work.Step();
            node = parent;
        }
        work.Check();
        return node;
    }

    internal static bool Connected(Element seed, Document document, ref SelectorMatchWork work)
    {
        work.Observe(seed);
        Node current = seed;
        while (true)
        {
            work.Step();
            if (ReferenceEquals(current, document)) return true;
            if (current.ParentNode is { } parent) current = parent;
            else if (current is ShadowRoot shadow) current = shadow.Host;
            else return false;
        }
    }

    internal static void BuildFocus(in SelectorEnvironment environment, ref SelectorMatchWork work)
    {
        var result = new HashSet<Element>(ReferenceEqualityComparer.Instance);
        if (environment.FocusedElement is { } seed &&
            Connected(seed, environment.Document!, ref work))
        {
            work.Step();
            if (!IsHtml(seed, "iframe") && !IsHtml(seed, "frame")) result.Add(seed);
            for (var root = seed.TreeShadowRoot; root is not null; root = root.Host.TreeShadowRoot)
            {
                work.Step();
                result.Add(root.Host);
            }
        }
        work.Check();
        work.EnsureCell().Focus = result;
    }

    // CSS Shadow §2.4: https://drafts.csswg.org/css-shadow-1/#flat-tree
    // DOM fresh assignment: https://dom.spec.whatwg.org/#find-a-slot
    private static Node? FlatParent(Element element, ref SelectorMatchWork work)
    {
        work.Step();
        var parent = element.ParentNode;
        if (parent is Element { AttachedShadowRoot: not null })
            return SlotAssignment.FindSlot(element, false, work.EnsureCell());
        if (parent is ShadowRoot shadow) return shadow.Host;
        if (parent is Element slot && IsHtml(slot, "slot") && slot.TreeShadowRoot is not null &&
            slot.SlotState?.Assigned.Count is > 0) return null;
        return parent is Element or Document ? parent : null;
    }

    internal static void AddFlatAncestors(Element seed, Document document, HashSet<Element> result,
        ref SelectorMatchWork work)
    {
        var path = new List<Element>();
        Node? current = seed;
        while (current is Element element)
        {
            work.Step();
            path.Add(element);
            current = FlatParent(element, ref work);
        }
        work.Check();
        if (!ReferenceEquals(current, document)) return;
        foreach (var element in path)
        {
            work.Step();
            result.Add(element);
        }
    }

    internal static void BuildActive(in SelectorEnvironment environment, ref SelectorMatchWork work)
    {
        var result = new HashSet<Element>(ReferenceEqualityComparer.Instance);
        if (environment.PointerPressTarget is { } seed &&
            Connected(seed, environment.Document!, ref work))
        {
            work.Step();
            result.Add(seed);
            AddFlatAncestors(seed, environment.Document!, result, ref work);
            ResolveLabels(result, ref work);
        }
        work.Check();
        work.EnsureCell().ActiveElements = result;
    }

    private static bool IsHtml(Element element, string name)
        => element.NamespaceUri == Namespaces.Html && element.LocalName == name;

    private static string? Attribute(Element element, string name, ref SelectorMatchWork work)
    {
        foreach (var attribute in element.Attributes)
        {
            work.Step();
            // Charge the spelling comparison as well as the attribute visit.
            Charge(attribute.LocalName, ref work);
            if (attribute.NamespaceUri is not null || attribute.LocalName != name) continue;
            var value = attribute.Value;
            Charge(value, ref work);
            return value;
        }
        return null;
    }

    private static void Charge(string value, ref SelectorMatchWork work)
    {
        foreach (var unused in value) work.Step();
    }

    private static bool Labelable(Element element, ref SelectorMatchWork work)
    {
        work.Step();
        if (element.NamespaceUri != Namespaces.Html) return false;
        if (element.LocalName is "button" or "meter" or "output" or "progress" or "select" or "textarea") return true;
        if (element.LocalName != "input") return false;
        var type = Attribute(element, "type", ref work);
        return HtmlInputTypes.Info(HtmlInputTypes.Parse(type)).Keyword != "hidden";
    }

    // HTML labeled control: https://html.spec.whatwg.org/multipage/forms.html#labeled-control
    // One ordinary pass per requested root; only active labels/ID requests are retained.
    private static void ResolveLabels(HashSet<Element> active, ref SelectorMatchWork work)
    {
        var groups = new Dictionary<Node, List<Element>>(ReferenceEqualityComparer.Instance);
        var labelRoots = new Dictionary<Element, Node>(ReferenceEqualityComparer.Instance);
        foreach (var element in active)
        {
            work.Step();
            if (!IsHtml(element, "label")) continue;
            var root = LabelRoot(element, active, labelRoots, ref work);
            work.Step();
            if (!groups.TryGetValue(root, out var labels)) groups.Add(root, labels = []);
            work.Step();
            labels.Add(element);
        }
        foreach (var (root, labels) in groups)
        {
            work.Step();
            var comparer = new ChargedOrdinalComparer(work.EnsureCell());
            var ids = new Dictionary<string, Element?>(comparer);
            var implicitLabels = new HashSet<Element>(ReferenceEqualityComparer.Instance);
            foreach (var label in labels)
            {
                work.Step();
                var id = Attribute(label, "for", ref work);
                if (id is null) implicitLabels.Add(label);
                else if (id.Length != 0) ids.TryAdd(id, null);
            }
            var pending = new Stack<Element>();
            var controls = new List<Element>();
            Node? current = root;
            while (current is not null)
            {
                work.Step();
                if (current is Element element)
                {
                    var labelable = Labelable(element, ref work);
                    var id = ids.Count == 0 ? null : Attribute(element, "id", ref work);
                    if (id is not null && ids.TryGetValue(id, out var previous) && previous is null)
                    {
                        // Store first identity even if nonlabelable: it blocks duplicate IDs.
                        ids[id] = element;
                    }
                    if (labelable)
                    {
                        while (pending.TryPop(out _))
                        {
                            work.Step();
                            controls.Add(element);
                        }
                    }
                    work.Step();
                    if (implicitLabels.Contains(element)) pending.Push(element);
                }
                if (current.FirstChild is { } child)
                {
                    work.Step();
                    current = child;
                    continue;
                }
                while (true)
                {
                    work.Step();
                    if (current is Element leaving && pending.TryPeek(out var top) && ReferenceEquals(top, leaving))
                    {
                        work.Step();
                        pending.Pop();
                    }
                    if (ReferenceEquals(current, root)) { current = null; break; }
                    if (current.NextSibling is { } next) { current = next; break; }
                    current = current.ParentNode!;
                }
            }
            foreach (var control in ids.Values)
            {
                work.Step();
                if (control is not null && Labelable(control, ref work)) controls.Add(control);
            }
            foreach (var control in controls)
            {
                work.Step();
                active.Add(control);
            }
        }
    }

    // Memoize only requested labels. A walk stops at any previously resolved label;
    // intervening ordinary ancestry is traversed once within an active segment.
    private static Node LabelRoot(Element label, HashSet<Element> active,
        Dictionary<Element, Node> roots, ref SelectorMatchWork work)
    {
        work.Step();
        if (roots.TryGetValue(label, out var known)) return known;
        var pending = new List<Element>();
        Node current = label;
        Node root;
        while (true)
        {
            work.Step();
            if (current is Element element && IsHtml(element, "label"))
            {
                work.Step();
                if (roots.TryGetValue(element, out known)) { root = known; break; }
                work.Step();
                if (active.Contains(element))
                {
                    work.Step();
                    pending.Add(element);
                }
            }
            if (current.ParentNode is not { } parent) { root = current; break; }
            current = parent;
        }
        foreach (var element in pending)
        {
            work.Step();
            roots.Add(element, root);
        }
        work.Check();
        return root;
    }

    private sealed class ChargedOrdinalComparer(SelectorMatchWork.Cell work) : IEqualityComparer<string>
    {
        public bool Equals(string? x, string? y)
        {
            work.Step();
            if (x is null || y is null) return x is null && y is null;
            if (x.Length != y.Length) return false;
            for (var i = 0; i < x.Length; i++)
            {
                work.Step();
                if (x[i] != y[i]) return false;
            }
            return true;
        }
        public int GetHashCode(string value)
        {
            var hash = 2166136261u;
            foreach (var character in value)
            {
                work.Step();
                hash = unchecked((hash ^ character) * 16777619);
            }
            return unchecked((int) hash);
        }
    }
}
