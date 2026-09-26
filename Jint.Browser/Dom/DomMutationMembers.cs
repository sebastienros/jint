using System.Runtime.CompilerServices;
using Jint.Browser.Dom.Collections;
using Jint.HtmlParser;
using Jint.Native;

namespace Jint.Browser.Dom;

internal static class DomMutationMembers
{
    private static readonly ConditionalWeakTable<IReadOnlyList<Node>, Snapshot> _snapshots = new();

    internal static JsValue Nodes(DomRealm realm, IReadOnlyList<Node> nodes)
        => realm.Wrap(_snapshots.GetValue(nodes, static list => new(list)), DomInterfaces.NodeList);

    // MutationRecord owns immutable snapshots; this view adds neither a copy nor live membership.
    private sealed class Snapshot(IReadOnlyList<Node> nodes) : DomNodeList
    {
        internal override int Length => nodes.Count;
        internal override Node this[int index] => nodes[index];
    }
}
