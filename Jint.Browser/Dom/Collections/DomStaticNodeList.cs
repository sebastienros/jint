using System.Collections;
using AngleSharp;
using AngleSharp.Dom;

namespace Jint.Browser.Dom.Collections;

/// <summary>
/// Adapts AngleSharp's <c>querySelectorAll</c> result to the static <c>NodeList</c> DOM requires.
/// </summary>
/// <remarks>
/// AngleSharp exposes the result as <c>IHtmlCollection&lt;IElement&gt;</c>, but it has only indexed
/// <c>NodeList</c> semantics. Adapting the target lets the ordinary generated <c>NodeList</c> accessor and
/// members serve it without exposing <c>HTMLCollection</c>'s named properties.
/// </remarks>
internal sealed class DomStaticNodeList : INodeList
{
    private readonly IHtmlCollection<IElement> _nodes;

    internal DomStaticNodeList(IHtmlCollection<IElement> nodes)
    {
        _nodes = nodes;
    }

    public int Length => _nodes.Length;

    public INode this[int index] => _nodes[index]!;

    public IEnumerator<INode> GetEnumerator() => _nodes.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    public void ToHtml(TextWriter writer, IMarkupFormatter formatter)
    {
        foreach (var node in _nodes)
        {
            node.ToHtml(writer, formatter);
        }
    }
}
