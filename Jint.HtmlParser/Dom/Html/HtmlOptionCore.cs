namespace Jint.HtmlParser;

/// <summary>Intrinsic HTML option history, independent of the enhanced API view.</summary>
internal sealed class HtmlOptionCore
{
    internal HtmlOptionCore(Element element, CancellationToken token) : this(element, null, token) { }
    internal HtmlOptionCore(Element element, HtmlSelectWorkContext? context, CancellationToken token)
    {
        Element = element;
        var work = new HtmlSelectWork(element.OwnerDocument?.SelectWorkProbe, context, token);
        Selected = ReadDefault(element.Attributes, ref work);
    }
    internal HtmlOptionCore(Element element, bool selected) { Element = element; Selected = selected; }
    internal Element Element { get; }
    internal bool Selected { get; private set; }
    internal bool DirtySelectedness { get; private set; }
    internal Element? CachedNearestSelect { get; set; }
    internal static bool ReadDefault(IEnumerable<Attr> attributes, ref HtmlSelectWork work)
    {
        var selected = false;
        foreach (var attribute in attributes)
        {
            work.Step();
            if (attribute.NamespaceUri is null && attribute.LocalName == "selected") selected = true;
        }
        work.Check();
        return selected;
    }
    internal void SetSelected(bool value, CancellationToken token)
        => SetSelectedWithWork(value, (HtmlSelectWorkContext?) null, token);
    internal void SetSelectedWithWork(bool value, HtmlSelectWorkContext? context, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        if (CachedNearestSelect is { } select) select.GetSelectCoreWithWork(context, token).SetOptionSelectedWithWork(this, value, context, token);
        else Write(value, true);
    }
    internal void Write(bool selected, bool dirty, bool markDocument = true)
    {
        if (Selected == selected && DirtySelectedness == dirty) return;
        var changed = Selected != selected;
        Selected = selected;
        DirtySelectedness = dirty;
        if (CachedNearestSelect is { } select)
        {
            if (changed) select.ExistingSelectCore?.SelectionChanged(this);
            select.ExistingSelectState?.SelectionChanged(this);
        }
        if (markDocument) Element.OwnerDocument!.MarkMutation();
    }
    internal void CopyFrom(HtmlOptionCore source)
    {
        Selected = source.Selected;
        DirtySelectedness = source.DirtySelectedness;
    }
}
