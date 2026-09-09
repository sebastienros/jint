using AngleSharp.Dom;
using Jint.Native;
using Jint.Native.Object;

namespace Jint.Browser.Dom.Collections;

/// <summary>
/// The wrapper for a <see cref="DomStaticNodeList"/>: a <c>NodeList</c> whose membership cannot change, and
/// which therefore keeps the one element wrapper each of its indices answers.
/// </summary>
/// <remarks>
/// <para>
/// <b>What it caches, and why that is not a second identity.</b> The cached value <i>is</i> the canonical
/// wrapper — the one <c>DomRealm.WrapNode</c> answers, which is the one the realm's
/// <c>ConditionalWeakTable</c> holds for that node and the one every other route to the node returns. So
/// <c>list[0] === document.querySelector(…)</c> and <c>list[0] === otherList[0]</c> stay true by
/// construction: the array is a memo of a lookup, never a source of objects. What it removes is the lookup —
/// the weak-table probe (<c>FindEntry</c>) the profile in
/// <a href="https://github.com/sebastienros/jint/issues/4013">sebastienros/jint#4013</a> measured on the
/// element read of <c>for (j = 0; j &lt; list.length; j++) if (list[j] === el)</c>.
/// </para>
/// <para>
/// <b>Why it adds no retention.</b> The snapshot holds its nodes strongly, and a live node's wrapper is
/// already kept alive by the weak table keyed on it. The cache therefore names objects the wrapper's own
/// <c>DomTarget</c> was keeping alive anyway, and it dies with the wrapper — which dies when the page and the
/// script both let go of the list. Removing a cached node from the document changes nothing: the node is
/// still in this static list, which is precisely what "static" means, and it still has one wrapper.
/// </para>
/// <para>
/// <b>Why it is per wrapper rather than per snapshot.</b> An <see cref="ObjectInstance"/> belongs to one
/// engine and one realm, so a cache of them cannot live on the AngleSharp-side object the way the node array
/// does. This class is that per-engine half.
/// </para>
/// <para>
/// It also answers the other half of the read cost the same profile names: the target is a typed
/// <c>INode[]</c> field rather than <c>object</c> + a per-read interface cast, and there is no
/// <c>DomCollectionAccessor</c> virtual call, because this shape has exactly one interface behind it.
/// <see cref="DomCollectionAccessor"/>'s own <c>object</c> parameter is unchanged and must stay: one
/// process-shared singleton serves every engine and every CLR target type of its interface.
/// </para>
/// </remarks>
internal sealed class DomStaticNodeListObject : DomCollectionBase
{
    private readonly INode[] _nodes;

    /// <summary>
    /// One slot per index, filled on that index's first read. A <see langword="null"/> slot means "not read
    /// yet", never "no element": every index below <see cref="Length"/> has a node. The array itself is
    /// allocated on the first indexed read, so a match a page only takes the <c>length</c> of — or never
    /// touches at all — costs nothing for it.
    /// </summary>
    private ObjectInstance?[]? _elements;

    internal DomStaticNodeListObject(DomRealm realm, DomStaticNodeList snapshot)
        : base(realm, DomInterfaces.NodeList, snapshot)
    {
        _nodes = snapshot.Nodes;
    }

    /// <inheritdoc />
    public override uint Length => (uint) _nodes.Length;

    /// <inheritdoc />
    public override bool TryGetIndex(uint index, out JsValue value)
    {
        var nodes = _nodes;

        if (index >= (uint) nodes.Length)
        {
            value = JsValue.Undefined;
            return false;
        }

        var elements = _elements ??= new ObjectInstance?[nodes.Length];
        var wrapper = elements[index];

        if (wrapper is null)
        {
            // Not memoized before the wrap returns, so a projection the node budget refused leaves the slot
            // empty and the next read asks again -- and gets the same wrapper, because the realm's table took
            // it before the RangeError was raised.
            wrapper = DomRealm.WrapNode(nodes[index]);
            elements[index] = wrapper;
        }

        value = wrapper;
        return true;
    }

    /// <inheritdoc />
    protected override bool HasIndex(uint index) => index < (uint) _nodes.Length;
}
