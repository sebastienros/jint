namespace Jint.HtmlParser.Css.Selectors;

// Traversal-owned, never stored on a compiled selector or a DOM node.
internal sealed class SelectorSiblingPositions
{
    internal sealed class TypeCount { internal int Count; }
    internal readonly record struct Position(int ChildIndex, int TypeIndex, TypeCount Type);
    internal readonly Dictionary<Element, Position> Elements = new(ReferenceEqualityComparer.Instance);
    internal int Count;
}

internal readonly record struct SelectorFilteredPositionKey(Node Parent, CompiledSelector Filter,
    Node? Scope, ShadowRoot? ShadowScope, SelectorEnvironment Environment);

// Selectors §4.5: a :has() result for one anchor. The same inputs as a filtered position key, with the anchor
// rather than its parent, since the relative selectors are evaluated from the anchor.
internal readonly record struct SelectorRelativeMatchKey(Node Anchor, CompiledSelector Relative,
    Node? Scope, ShadowRoot? ShadowScope, SelectorEnvironment Environment);

internal sealed class SelectorFilteredPositions
{
    internal readonly Dictionary<Element, int> Elements = new(ReferenceEqualityComparer.Instance);
    internal int Count;
    internal int Index(Element element, bool fromEnd) => Elements.TryGetValue(element, out var index)
        ? fromEnd ? Count - index + 1 : index : 0;
}

internal static partial class SelectorMatcher
{
    // Selectors §14.3/14.4: index element siblings, with expanded-name type identity.
    // One guarded traversal builds all types together, so alternating types stay linear too.
    private static int SiblingIndex(Element element, bool ofType, bool fromEnd, ref Work work)
    {
        work.Step();
        if (element.ParentNode is not { } parent) return 1;
        work.Shared.Observe(parent);
        var cell = work.Shared.EnsureCell();
        cell.SiblingPositions ??= new(ReferenceEqualityComparer.Instance);
        if (!cell.SiblingPositions.TryGetValue(parent, out var positions))
        {
            // The first question a traversal asks about a parent's children is answered by counting them from the
            // element, as Chromium does outside its nth-index cache: el.matches(':nth-child(k) > …') asks once, and
            // indexing a list of 700 cards to answer it made each call linear in the list, with a table to throw
            // away. The second question indexes them all, so a querySelectorAll or a cascade over the list stays
            // linear.
            cell.CountedSiblingParents ??= new(ReferenceEqualityComparer.Instance);
            if (cell.CountedSiblingParents.Add(parent))
            {
                var counted = CountSiblings(element, ofType, fromEnd, ref work);
                work.Shared.VerifyRead();
                return counted;
            }

            positions = new SelectorSiblingPositions();
            var types = new Dictionary<(string? Namespace, string Name), SelectorSiblingPositions.TypeCount>();
            for (var node = parent.FirstChild; node is not null; node = node.NextSibling)
            {
                work.Step();
                if (node is not Element sibling) continue;
                // String hashing/equality is real work, even when hidden in the dictionary.
                foreach (var _ in sibling.LocalName) work.Step();
                if (sibling.NamespaceUri is { } ns) foreach (var _ in ns) work.Step();
                var key = (sibling.NamespaceUri, sibling.LocalName);
                if (!types.TryGetValue(key, out var type))
                    types.Add(key, type = new SelectorSiblingPositions.TypeCount());
                positions.Elements.Add(sibling, new(++positions.Count, ++type.Count, type));
            }
            work.Shared.VerifyRead();
            cell.SiblingPositions.Add(parent, positions);
        }
        var position = positions.Elements[element];
        var index = ofType ? position.TypeIndex : position.ChildIndex;
        return fromEnd ? (ofType ? position.Type.Count : positions.Count) - index + 1 : index;
    }

    // An element's 1-based index among its element siblings, or among those of its expanded name, counted from
    // the start or the end without indexing the others.
    private static int CountSiblings(Element element, bool ofType, bool fromEnd, ref Work work)
    {
        var index = 1;
        for (var node = fromEnd ? element.NextSibling : element.PreviousSibling; node is not null;
             node = fromEnd ? node.NextSibling : node.PreviousSibling)
        {
            work.Step();
            if (node is not Element sibling) continue;
            if (ofType)
            {
                // Comparing names is real work, as the index's hashing is.
                foreach (var _ in sibling.LocalName) work.Step();
                if (!string.Equals(sibling.LocalName, element.LocalName, StringComparison.Ordinal)
                    || !string.Equals(sibling.NamespaceUri, element.NamespaceUri, StringComparison.Ordinal)) continue;
            }
            index++;
        }
        return index;
    }

    private static SelectorFilteredPositionKey FilteredKey(CompiledSelector.Predicate predicate,
        Node element, Node? scope, ref Work work)
    {
        var filter = predicate.Arguments!;
        return new(element.ParentNode ?? element, filter, UsesScope(filter, ref work) ? scope : null,
            work.ShadowScope, work.Environment);
    }

    private static SelectorRelativeMatchKey RelativeMatchKey(CompiledSelector relative, Node anchor, Node? scope,
        ref Work work)
        => new(anchor, relative, UsesScope(relative, ref work) ? scope : null, work.ShadowScope, work.Environment);

    // Whether a selector reads the scoping root (:scope, or a & that may stand for it), memoized per selector.
    private static bool UsesScope(CompiledSelector filter, ref Work work)
    {
        var cell = work.Shared.EnsureCell();
        cell.ScopeDependencies ??= new();
        if (!cell.ScopeDependencies.TryGetValue(filter, out var usesScope))
        {
            var pending = new Stack<CompiledSelector>();
            pending.Push(filter);
            while (pending.TryPop(out var program))
            {
                foreach (var branch in program.Branches)
                {
                    foreach (var compound in branch.Compounds)
                    {
                        foreach (var part in compound.Predicates)
                        {
                            work.Step();
                            if (part.Kind == CompiledSelector.PredicateKind.Scope || part.IsNestingReference) usesScope = true;
                            if (part.Arguments is { } nested) pending.Push(nested);
                        }
                    }
                }
            }
            cell.ScopeDependencies.Add(filter, usesScope);
        }
        return usesScope;
    }
}
