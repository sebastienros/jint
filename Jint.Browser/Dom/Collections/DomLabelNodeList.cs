using System.Collections;
using AngleSharp;
using AngleSharp.Dom;
using AngleSharp.Html.Dom;

namespace Jint.Browser.Dom.Collections;

/// <summary>The live <c>NodeList</c> returned by a labelable element's <c>labels</c> attribute.</summary>
internal sealed class DomLabelNodeList : INodeList
{
    private readonly IHtmlElement _control;

    internal DomLabelNodeList(IHtmlElement control) => _control = control;

    public int Length => HtmlLabelAssociation.LabelsFor(_control).Count;

    public INode this[int index]
    {
        get
        {
            var labels = HtmlLabelAssociation.LabelsFor(_control);
            return (uint) index < (uint) labels.Count ? labels[index] : null!;
        }
    }

    public IEnumerator<INode> GetEnumerator()
    {
        foreach (var label in HtmlLabelAssociation.LabelsFor(_control))
        {
            yield return label;
        }
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    public void ToHtml(TextWriter writer, IMarkupFormatter formatter)
    {
        foreach (var label in HtmlLabelAssociation.LabelsFor(_control))
        {
            label.ToHtml(writer, formatter);
        }
    }
}
