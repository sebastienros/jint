using Jint.HtmlParser;

namespace Jint.Browser.Dom.Collections;

/// <summary>The live NodeList returned by a labelable element's labels attribute.</summary>
internal sealed class DomLabelNodeList(Element control) : DomNodeList
{
    internal override int Length => ReadLength(null, default);
    internal override int ReadLength(Action<int>? checkpoint, CancellationToken token)
        => HtmlLabelAssociation.LabelsFor(control, checkpoint, token).Count;
    internal override Node? ReadItem(uint index, Action<int>? checkpoint, CancellationToken token)
    {
        var labels = HtmlLabelAssociation.LabelsFor(control, checkpoint, token);
        return index >= (uint) labels.Count ? null : labels[(int) index];
    }
    internal override Node this[int index] => HtmlLabelAssociation.LabelsFor(control)[index];
}
