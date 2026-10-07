using System.Runtime.CompilerServices;
using Jint.HtmlParser;

namespace Jint.Browser.Dom.Collections;

/// <summary>The live NodeList returned by a labelable element's labels attribute.</summary>
internal sealed class DomLabelNodeList : DomNodeList
{
    // HTML #dom-lfe-labels associates one live list with each control. The value contains no engine;
    // each engine projects it through its own wrapper cache. The ephemeron does not root the control.
    private static readonly ConditionalWeakTable<Element, DomLabelNodeList> Lists = new();
    private readonly Element _control;

    private DomLabelNodeList(Element control) => _control = control;

    internal static DomLabelNodeList Of(Element control)
        => Lists.GetValue(control, static owner => new DomLabelNodeList(owner));

    internal override int Length => ReadLength(null, default);
    internal override int ReadLength(Action<int>? checkpoint, CancellationToken token)
        => HtmlLabelAssociation.CountLabels(_control, checkpoint, token);
    internal override Node? ReadItem(uint index, Action<int>? checkpoint, CancellationToken token)
        => HtmlLabelAssociation.LabelAt(_control, index, checkpoint, token);
    internal override Node this[int index]
        => ReadItem(unchecked((uint) index), null, default) ?? throw new ArgumentOutOfRangeException(nameof(index));
}
