namespace Jint.HtmlParser;

/// <summary>Stable live native views over HTML's list of options and selected options.</summary>
internal sealed class HtmlSelectOptions(HtmlSelectState select, bool selectedOnly)
{
    private IReadOnlyList<Element>? _selectedCache;
    private long _membership = -1;
    private long _selection = -1;
    internal void InvalidateSelection() { _selectedCache = null; _membership = -1; _selection = -1; }
    internal int Count => GetCount(default);
    internal int GetCount(CancellationToken token) => Current(token).Count;
    private IReadOnlyList<Element> Current(CancellationToken token)
    {
        var options = select.Prepare(token);
        if (!selectedOnly) return options;
        if (_selectedCache is not null && _membership == select.MembershipRevision && _selection == select.SelectionRevision)
            return _selectedCache;
        var result = new List<Element>();
        var work = new HtmlSelectWork(select.Element.OwnerDocument?.SelectWorkProbe, token);
        foreach (var option in options) { work.Step(); if (option.GetHtmlState()!.Option!.Selected) result.Add(option); }
        work.Check();
        _selectedCache = result;
        _membership = select.MembershipRevision;
        _selection = select.SelectionRevision;
        return result;
    }
    internal Element? Item(uint index, CancellationToken token)
    {
        var options = Current(token);
        return index < (uint) options.Count ? options[(int) index] : null;
    }
    internal Element? NamedItem(string name, CancellationToken token)
    {
        ArgumentNullException.ThrowIfNull(name);
        token.ThrowIfCancellationRequested();
        if (name.Length == 0) return null;
        var work = new HtmlSelectWork(select.Element.OwnerDocument?.SelectWorkProbe, token);
        foreach (var element in Current(token))
        {
            work.Step();
            if (element.GetHtmlState()!.Option!.MatchesName(name, ref work))
            { work.Check(); return element; }
        }
        work.Check();
        return null;
    }
    internal IReadOnlyList<Element> Snapshot(CancellationToken token)
    {
        var result = new List<Element>();
        var work = new HtmlSelectWork(select.Element.OwnerDocument?.SelectWorkProbe, token);
        foreach (var option in Current(token)) { work.Step(); result.Add(option); }
        work.Check();
        return result.AsReadOnly();
    }
    private void RequireOptions()
    {
        if (selectedOnly) throw new InvalidOperationException("Selected options is a read-only live collection.");
    }
    internal void SetLength(uint length, CancellationToken token)
    {
        RequireOptions();
        var options = select.Prepare(token);
        // Each DOM insertion/removal remains independently observable; cancel between steps.
        while ((uint) options.Count > length)
        {
            token.ThrowIfCancellationRequested();
            var last = options[^1];
            last.ParentNode!.RemoveChild(last);
            options = select.Prepare(token);
        }
        if (length > (uint) options.Count && length <= 100000)
            AppendBlankOptions(length - (uint) options.Count, token);
    }
    private void AppendBlankOptions(uint count, CancellationToken token)
    {
        var fragment = select.Element.OwnerDocument!.CreateDocumentFragment();
        var work = new HtmlSelectWork(select.Element.OwnerDocument.SelectWorkProbe, token);
        for (uint i = 0; i < count; i++)
        {
            work.Step();
            fragment.AppendClonedChild(select.Element.OwnerDocument.CreateElementNS(Namespaces.Html, "option"));
        }
        work.Check();
        select.Element.AppendChild(fragment);
    }
    internal int GetSelectedIndex(CancellationToken token) => select.GetSelectedIndex(token);
    internal void SetSelectedIndex(int value, CancellationToken token) { RequireOptions(); select.SetSelectedIndex(value, token); }
    internal void SetIndexed(uint index, Element? option, CancellationToken token)
    {
        RequireOptions();
        token.ThrowIfCancellationRequested();
        if (option is null) { Remove(index > int.MaxValue ? -1 : (int) index, token); return; }
        if (option is not { NamespaceUri: Namespaces.Html, LocalName: "option" })
            throw new ArgumentException("An indexed value must be an HTML option.", nameof(option));
        var count = GetCount(token);
        if (index >= (uint) count)
        {
            AppendBlankOptions(index - (uint) count, token);
            select.Element.AppendChild(option);
        }
        else
        {
            var existing = Item(index, token)!;
            existing.ParentNode!.ReplaceChild(option, existing);
        }
    }
    internal void Remove(int index, CancellationToken token)
    {
        RequireOptions();
        var option = index < 0 ? null : Item((uint) index, token);
        option?.ParentNode!.RemoveChild(option);
    }
    internal void Add(Element element, int? before, CancellationToken token)
        => Add(element, before is >= 0 ? Item((uint) before.Value, token) : null, token);
    internal void Add(Element element, Element? before, CancellationToken token)
    {
        RequireOptions();
        ArgumentNullException.ThrowIfNull(element);
        if (element.NamespaceUri != Namespaces.Html || element.LocalName is not ("option" or "optgroup"))
            throw new ArgumentException("The added element must be an HTML option or optgroup.", nameof(element));
        token.ThrowIfCancellationRequested();
        var ancestorWork = new HtmlSelectWork(select.Element.OwnerDocument?.SelectWorkProbe, token);
        for (var ancestor = select.Element.ParentNode; ancestor is not null; ancestor = ancestor.ParentNode)
        {
            ancestorWork.Step();
            if (ReferenceEquals(ancestor, element))
                throw new DomException("HierarchyRequestError", "An ancestor cannot be inserted into its select.");
        }
        ancestorWork.Check();
        if (before is not null)
        {
            var descendant = false;
            var work = new HtmlSelectWork(select.Element.OwnerDocument?.SelectWorkProbe, token);
            for (var parent = before.ParentNode; parent is not null; parent = parent.ParentNode)
            { work.Step(); if (ReferenceEquals(parent, select.Element)) { descendant = true; break; } }
            work.Check();
            if (!descendant) throw new DomException("NotFoundError", "The reference element is not a descendant of this select.");
        }
        if (ReferenceEquals(element, before)) return;
        (before?.ParentNode ?? select.Element).InsertBefore(element, before);
    }
}
