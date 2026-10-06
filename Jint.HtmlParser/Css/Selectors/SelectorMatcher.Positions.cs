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

    private static SelectorFilteredPositionKey FilteredKey(CompiledSelector.Predicate predicate,
        Node element, Node? scope, ref Work work)
    {
        var filter = predicate.Arguments!;
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
        return new(element.ParentNode ?? element, filter, usesScope ? scope : null,
            work.ShadowScope, work.Environment);
    }
}
