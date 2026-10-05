using System.Runtime.CompilerServices;
using Jint.HtmlParser;
using Jint.HtmlParser.Css.Values;

namespace Jint.Browser.Styling;

internal static partial class NativeCssStyleSheets
{
    // https://drafts.csswg.org/cssom/#dom-document-stylesheets
    private sealed class OwnerOrders
    {
        internal OwnerOrder? Ordinary;
        internal OwnerOrder? WithShadow;
    }

    // A document cache must not retain a removed sheet's owner, its ancestors or a shadow tree.
    private sealed record OwnerOrder(ulong TreeStamp, ulong ResourceStamp, WeakReference<Element>[] Owners);

    private sealed class OwnerBranch(Node node)
    {
        internal readonly Node Node = node;
        internal Dictionary<Node, OwnerBranch>? Children;
        internal Element? Owner;
    }

    internal static List<Element> OrderedOwners(Document document, CssValueWork work)
    {
        var resources = ResourcesOf(document);
        var documentStamp = document.MutationStamp;
        var resourceStamp = resources.Version;
        var parentWork = work;
        work = CssValueWork.Guard(parentWork, () =>
        {
            parentWork.CheckCancellation();
            if (documentStamp == ulong.MaxValue || resourceStamp == ulong.MaxValue ||
                documentStamp != document.MutationStamp || resourceStamp != resources.Version)
                throw new InvalidOperationException(NativeCssQuery.Invalidated);
        });
        return OrderedOwners(document, document, resources, work, includeShadow: false);
    }

    private static readonly object OwnerAncestor = new();

    private static void RefreshOwnerAncestry(Document document, Resources resources, CssValueWork work)
    {
        if (resources.OrderAncestors is not null && resources.TreeVersion != ulong.MaxValue &&
            resources.Version != ulong.MaxValue && resources.AncestryTreeVersion == resources.TreeVersion &&
            resources.AncestryResourceVersion == resources.Version) return;
        var treeVersion = resources.TreeVersion;
        var resourceVersion = resources.Version;
        var ancestors = new ConditionalWeakTable<Node, object>();
        // Include detached and shadow owners too: their later attachment must invalidate order.
        // Keys and values retain no nodes. Shared ancestry is charged and captured only once.
        foreach (var pair in resources.AssociatedOwners)
        {
            work.Charge(1);
            if (!ReferenceEquals(pair.Key.OwnerDocument, document)) continue;
            for (Node? node = pair.Key; node is not null; node = node.ParentNode ?? (node as ShadowRoot)?.Host)
            {
                work.Charge(1);
                if (ancestors.TryGetValue(node, out _)) break;
                ancestors.Add(node, OwnerAncestor);
            }
        }
        work.CheckCancellation();
        resources.OrderAncestors = ancestors;
        resources.AncestryTreeVersion = treeVersion;
        resources.AncestryResourceVersion = resourceVersion;
    }

    private static List<Element> OrderedOwners(Node root, Document document, Resources resources,
        CssValueWork work, bool includeShadow)
    {
        var orders = resources.Orders.GetValue(root, static _ => new());
        var order = includeShadow ? orders.WithShadow : orders.Ordinary;
        RefreshOwnerAncestry(document, resources, work);
        var treeStamp = resources.TreeVersion;
        var resourceStamp = resources.Version;
        var result = new List<Element>();
        work.CheckCancellation();
        if (order is not null && treeStamp != ulong.MaxValue && resourceStamp != ulong.MaxValue &&
            order.TreeStamp == treeStamp && order.ResourceStamp == resourceStamp)
        {
            foreach (var weak in order.Owners)
            {
                work.Charge(1);
                if (weak.TryGetTarget(out var owner)) result.Add(owner);
            }
            work.CheckCancellation();
            return result;
        }

        // Build only the induced ancestry tree of known owners. Unrelated descendants are
        // never visited. At branching ancestors sibling links determine their relative order.
        // HTML shadow sheets follow the host's light descendants, matching collection traversal.
        var branches = new Dictionary<Node, OwnerBranch>();
        var top = new OwnerBranch(root);
        branches.Add(root, top);
        foreach (var pair in resources.AssociatedOwners)
        {
            work.Charge(1);
            var owner = pair.Key;
            if (!ReferenceEquals(owner.OwnerDocument, document)) continue;
            var path = new List<Node>();
            Node? current = owner;
            while (current is not null && !branches.ContainsKey(current))
            {
                work.Charge(1);
                path.Add(current);
                current = current.ParentNode ?? (includeShadow ? (current as ShadowRoot)?.Host : null);
            }
            if (current is null) continue;
            var parent = branches[current];
            for (var i = path.Count - 1; i >= 0; i--)
            {
                work.Charge(1);
                var branch = new OwnerBranch(path[i]);
                branches.Add(path[i], branch);
                (parent.Children ??= new()).Add(path[i], branch);
                parent = branch;
            }
            parent.Owner = owner;
        }
        var pending = new Stack<OwnerBranch>();
        pending.Push(top);
        while (pending.TryPop(out var branch))
        {
            work.Charge(1);
            if (branch.Owner is { } owner) result.Add(owner);
            if (branch.Children is not { } children) continue;
            if (children.Count == 1)
            {
                pending.Push(children.Values.First());
                continue;
            }
            if (includeShadow && branch.Node is Element { AttachedShadowRoot: { } shadow } &&
                children.TryGetValue(shadow, out var shadowBranch)) pending.Push(shadowBranch);
            for (var child = branch.Node.LastChild; child is not null; child = child.PreviousSibling)
            {
                work.Charge(1);
                if (children.TryGetValue(child, out var childBranch)) pending.Push(childBranch);
            }
        }
        work.CheckCancellation();
        var weakOwners = new WeakReference<Element>[result.Count];
        for (var i = 0; i < result.Count; i++)
        {
            work.Charge(1);
            weakOwners[i] = new(result[i]);
        }
        work.CheckCancellation();
        // Publish only after the caller's guarded work has proved the original DOM/resource witness.
        order = new(treeStamp, resourceStamp, weakOwners);
        if (includeShadow) orders.WithShadow = order;
        else orders.Ordinary = order;
        return result;
    }
}
