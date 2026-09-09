using System.Collections;
using AngleSharp;
using AngleSharp.Dom;

namespace Jint.Browser.Dom.Collections;

/// <summary>
/// The <b>static</b> <c>NodeList</c> that
/// <a href="https://dom.spec.whatwg.org/#dom-parentnode-queryselectorall">DOM §4.2.6's
/// <c>querySelectorAll</c></a> returns: "the static result of running scope-match a selectors string".
/// </summary>
/// <remarks>
/// <para>
/// It is the mirror image of <see cref="DomLiveHtmlCollection"/>, which exists because AngleSharp answers a
/// snapshot where DOM wants a live collection. Here the standard wants the snapshot, and the reason the
/// binding owns one anyway is that <b>nothing about an <see cref="INodeList"/> says whether it is live</b>:
/// <c>childNodes</c>, <c>labels</c> and a selector match all arrive at the same wrapper through the same
/// generated accessor, and a wrapper that cached anything per index would be wrong for two of the three. So
/// staticness is made a property of a <i>type</i> rather than a claim about an instance — this class holds
/// the matched nodes in an array nothing else can reach, and that type is what
/// <see cref="DomCollectionObject.TryGetIndex"/> tests before it keeps one element wrapper per index.
/// </para>
/// <para>
/// Note that <i>this</i> is the type staticness lives in, and the wrapper on the JavaScript side stays the
/// one every other <c>NodeList</c> has. A second <c>ArrayLikeObject</c> beside it would have put a second
/// candidate on the interpreter's array-like read lane, which devirtualizes from a class profile holding one
/// guess, and the live <c>NodeList</c>s would then have been paying for this one's cache;
/// <see cref="DomCollectionObject"/> records the measurement that says so.
/// </para>
/// <para>
/// The array is a copy of the collection AngleSharp produced rather than a reference to it. That costs one
/// array of references per <c>querySelectorAll</c> — beside the <c>List&lt;IElement&gt;</c> the match already
/// built — and buys the whole safety argument: an <see cref="INodeList"/> handed in from elsewhere can never
/// become the thing behind a cache merely because it happened to be a snapshot on the day it was written.
/// </para>
/// <para>
/// It holds the nodes strongly, exactly as AngleSharp's own snapshot does, so it adds no retention over the
/// collection it copies — and the wrapper's per-index cache adds none over <i>this</i>, because a node this
/// array names is a node whose wrapper the realm's <see cref="System.Runtime.CompilerServices.ConditionalWeakTable{TKey,TValue}"/>
/// is already keeping alive.
/// </para>
/// </remarks>
internal sealed class DomStaticNodeList : INodeList
{
    private readonly INode[] _nodes;

    internal DomStaticNodeList(IHtmlCollection<IElement> matches)
    {
        var count = matches.Length;
        if (count == 0)
        {
            _nodes = [];
            return;
        }

        var nodes = new INode[count];
        for (var i = 0; i < count; i++)
        {
            nodes[i] = matches[i];
        }

        _nodes = nodes;
    }

    /// <summary>The matched nodes, in tree order. Never mutated after the constructor returns.</summary>
    internal INode[] Nodes => _nodes;

    /// <inheritdoc />
    public int Length => _nodes.Length;

    /// <summary>
    /// The CLR indexer, which every caller in this package guards before reaching. It is deliberately a bare
    /// array index: an out-of-range read is a defect here rather than something a page can ask for, because
    /// both doors — <c>NodeList.item</c> and <see cref="DomCollectionObject.TryGetIndex"/> — answer WebIDL's
    /// out-of-range value themselves.
    /// </summary>
    public INode this[int index] => _nodes[index];

    /// <inheritdoc />
    public IEnumerator<INode> GetEnumerator() => ((IEnumerable<INode>) _nodes).GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => _nodes.GetEnumerator();

    /// <inheritdoc />
    public void ToHtml(TextWriter writer, IMarkupFormatter formatter)
    {
        foreach (var node in _nodes)
        {
            node.ToHtml(writer, formatter);
        }
    }
}
