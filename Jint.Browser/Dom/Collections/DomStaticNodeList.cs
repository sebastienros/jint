using Jint.HtmlParser;

namespace Jint.Browser.Dom.Collections;

/// <summary>DOM §4.2.6's static selector result, holding the native identities in tree order.</summary>
internal sealed class DomStaticNodeList : DomNodeList
{
    private readonly Node[] _nodes;

    /// <summary>Adopts <paramref name="matches"/>; the caller hands over a snapshot nothing else mutates.</summary>
    internal DomStaticNodeList(Node[] matches) => _nodes = matches;

    internal Node[] Nodes => _nodes;

    internal override int Length => _nodes.Length;

    internal override Node this[int index] => _nodes[index];
}
