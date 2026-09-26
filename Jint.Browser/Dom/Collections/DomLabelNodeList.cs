using Jint.HtmlParser;

namespace Jint.Browser.Dom.Collections;

/// <summary>The live NodeList returned by a labelable element's labels attribute.</summary>
internal sealed class DomLabelNodeList(Element control) : DomNodeList
{
    internal override int Length => HtmlLabelAssociation.LabelsFor(control).Count;
    internal override Node this[int index] => HtmlLabelAssociation.LabelsFor(control)[index];
}
