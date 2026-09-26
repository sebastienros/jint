namespace Jint.HtmlParser;

/// <summary>Stable live native views over HTML's list of options and selected options.</summary>
internal sealed class HtmlSelectOptions(HtmlSelectState select, bool selectedOnly)
{
    private IReadOnlyList<Element>? _selectedCache;
    private long _membership = -1;
    private long _selection = -1;
    internal void InvalidateSelection() { _selectedCache = null; _membership = -1; _selection = -1; }
    internal int Count => GetCount(default);
    internal int GetCount(CancellationToken token)
        => GetCountWithWork((HtmlSelectWorkContext?) null, token);
    internal int GetCountWithWork(HtmlSelectWorkContext? context, CancellationToken token) => CurrentWithWork(context, token).Count;
    private IReadOnlyList<Element> CurrentWithWork(HtmlSelectWorkContext? context, CancellationToken token)
    {
        var options = select.PrepareWithWork(context, token);
        if (!selectedOnly) return options;
        if (_selectedCache is not null && _membership == select.MembershipRevision && _selection == select.SelectionRevision)
            return _selectedCache;
        var result = new List<Element>();
        var work = new HtmlSelectWork(select.Element.OwnerDocument?.SelectWorkProbe, context, token);
        foreach (var option in options) { work.Step(); if (option.GetOptionCoreWithWork(context, token).Selected) result.Add(option); }
        work.Check();
        _selectedCache = result;
        _membership = select.MembershipRevision;
        _selection = select.SelectionRevision;
        return result;
    }
    internal Element? Item(uint index, CancellationToken token)
        => ItemWithWork(index, (HtmlSelectWorkContext?) null, token);
    internal Element? ItemWithWork(uint index, HtmlSelectWorkContext? context, CancellationToken token)
    {
        var options = CurrentWithWork(context, token);
        return index < (uint) options.Count ? options[(int) index] : null;
    }
    internal Element? NamedItem(string name, CancellationToken token)
        => NamedItemWithWork(name, (HtmlSelectWorkContext?) null, token);
    internal Element? NamedItemWithWork(string name, HtmlSelectWorkContext? context, CancellationToken token)
    {
        ArgumentNullException.ThrowIfNull(name);
        token.ThrowIfCancellationRequested();
        if (name.Length == 0) return null;
        var work = new HtmlSelectWork(select.Element.OwnerDocument?.SelectWorkProbe, context, token);
        foreach (var element in CurrentWithWork(context, token))
        {
            work.Step();
            if (element.GetHtmlState()!.GetOptionStateWithWork(context, token)!.MatchesName(name, ref work))
            { work.Check(); return element; }
        }
        work.Check();
        return null;
    }
    internal IReadOnlyList<Element> Snapshot(CancellationToken token)
        => SnapshotWithWork((HtmlSelectWorkContext?) null, token);
    internal IReadOnlyList<Element> SnapshotWithWork(HtmlSelectWorkContext? context, CancellationToken token)
    {
        var result = new List<Element>();
        var work = new HtmlSelectWork(select.Element.OwnerDocument?.SelectWorkProbe, context, token);
        foreach (var option in CurrentWithWork(context, token)) { work.Step(); result.Add(option); }
        work.Check();
        return result.AsReadOnly();
    }
    private void RequireOptions()
    {
        if (selectedOnly) throw new InvalidOperationException("Selected options is a read-only live collection.");
    }
    internal void SetLength(uint length, CancellationToken token)
        => SetLengthWithWork(length, (HtmlSelectWorkContext?) null, token);
    internal void SetLengthWithWork(uint length, HtmlSelectWorkContext? context, CancellationToken token)
    {
        RequireOptions();
        var options = select.PrepareWithWork(context, token);
        // Each DOM insertion/removal remains independently observable; cancel between steps.
        while ((uint) options.Count > length)
        {
            context?.Step();
            token.ThrowIfCancellationRequested();
            var last = options[^1];
            last.ParentNode!.RemoveChild(last);
            options = select.PrepareWithWork(context, token);
        }
        if (length > (uint) options.Count && length <= 100000)
            AppendBlankOptionsWithWork(length - (uint) options.Count, context, token);
    }
    private void AppendBlankOptionsWithWork(uint count, HtmlSelectWorkContext? context, CancellationToken token)
    {
        var fragment = select.Element.OwnerDocument!.CreateDocumentFragment();
        var work = new HtmlSelectWork(select.Element.OwnerDocument.SelectWorkProbe, context, token);
        for (uint i = 0; i < count; i++)
        {
            work.Step();
            fragment.AppendClonedChild(select.Element.OwnerDocument.CreateElementNS(Namespaces.Html, "option"), context, token);
        }
        work.Check();
        select.Element.AppendChild(fragment);
    }
    internal int GetSelectedIndex(CancellationToken token)
        => GetSelectedIndexWithWork((HtmlSelectWorkContext?) null, token);
    internal int GetSelectedIndexWithWork(HtmlSelectWorkContext? context, CancellationToken token) => select.GetSelectedIndexWithWork(context, token);
    internal void SetSelectedIndex(int value, CancellationToken token)
        => SetSelectedIndexWithWork(value, (HtmlSelectWorkContext?) null, token);
    internal void SetSelectedIndexWithWork(int value, HtmlSelectWorkContext? context, CancellationToken token) { RequireOptions(); select.SetSelectedIndexWithWork(value, context, token); }
    internal void SetIndexed(uint index, Element? option, CancellationToken token)
        => SetIndexedWithWork(index, option, (HtmlSelectWorkContext?) null, token);
    internal void SetIndexedWithWork(uint index, Element? option, HtmlSelectWorkContext? context, CancellationToken token)
    {
        RequireOptions();
        token.ThrowIfCancellationRequested();
        if (option is null) { RemoveWithWork(index > int.MaxValue ? -1 : (int) index, context, token); return; }
        if (option is not { NamespaceUri: Namespaces.Html, LocalName: "option" })
            throw new ArgumentException("An indexed value must be an HTML option.", nameof(option));
        var count = GetCountWithWork(context, token);
        if (index >= (uint) count)
        {
            AppendBlankOptionsWithWork(index - (uint) count, context, token);
            select.Element.AppendChild(option);
        }
        else
        {
            var existing = ItemWithWork(index, context, token)!;
            existing.ParentNode!.ReplaceChild(option, existing);
        }
    }
    internal void Remove(int index, CancellationToken token)
        => RemoveWithWork(index, (HtmlSelectWorkContext?) null, token);
    internal void RemoveWithWork(int index, HtmlSelectWorkContext? context, CancellationToken token)
    {
        RequireOptions();
        var option = index < 0 ? null : ItemWithWork((uint) index, context, token);
        option?.ParentNode!.RemoveChild(option);
    }
    internal void Add(Element element, int? before, CancellationToken token)
        => AddWithWork(element, before, (HtmlSelectWorkContext?) null, token);
    internal void AddWithWork(Element element, int? before, HtmlSelectWorkContext? context, CancellationToken token)
        => AddWithWork(element, before is >= 0 ? ItemWithWork((uint) before.Value, context, token) : null, context, token);
    internal void Add(Element element, Element? before, CancellationToken token)
        => AddWithWork(element, before, (HtmlSelectWorkContext?) null, token);
    internal void AddWithWork(Element element, Element? before, HtmlSelectWorkContext? context, CancellationToken token)
    {
        RequireOptions();
        ArgumentNullException.ThrowIfNull(element);
        if (element.NamespaceUri != Namespaces.Html || element.LocalName is not ("option" or "optgroup"))
            throw new ArgumentException("The added element must be an HTML option or optgroup.", nameof(element));
        token.ThrowIfCancellationRequested();
        var ancestorWork = new HtmlSelectWork(select.Element.OwnerDocument?.SelectWorkProbe, context, token);
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
            var work = new HtmlSelectWork(select.Element.OwnerDocument?.SelectWorkProbe, context, token);
            for (var parent = before.ParentNode; parent is not null; parent = parent.ParentNode)
            { work.Step(); if (ReferenceEquals(parent, select.Element)) { descendant = true; break; } }
            work.Check();
            if (!descendant) throw new DomException("NotFoundError", "The reference element is not a descendant of this select.");
        }
        if (ReferenceEquals(element, before)) return;
        (before?.ParentNode ?? select.Element).InsertBefore(element, before);
    }

    internal int GetCount(Action<int>? checkpoint, CancellationToken token)
    {
        var context = HtmlSelectWorkContext.Create(checkpoint, token);
        context?.Check();
        return GetCountWithWork(context, token);
    }
    internal Element? Item(uint index, Action<int>? checkpoint, CancellationToken token)
    {
        var context = HtmlSelectWorkContext.Create(checkpoint, token);
        context?.Check();
        return ItemWithWork(index, context, token);
    }
    internal Element? NamedItem(string name, Action<int>? checkpoint, CancellationToken token)
    {
        var context = HtmlSelectWorkContext.Create(checkpoint, token);
        context?.Check();
        return NamedItemWithWork(name, context, token);
    }
    internal IReadOnlyList<Element> Snapshot(Action<int>? checkpoint, CancellationToken token)
    {
        var context = HtmlSelectWorkContext.Create(checkpoint, token);
        context?.Check();
        return SnapshotWithWork(context, token);
    }
    internal void SetLength(uint length, Action<int>? checkpoint, CancellationToken token)
    {
        var context = HtmlSelectWorkContext.Create(checkpoint, token);
        context?.Check();
        SetLengthWithWork(length, context, token);
    }
    internal int GetSelectedIndex(Action<int>? checkpoint, CancellationToken token)
    {
        var context = HtmlSelectWorkContext.Create(checkpoint, token);
        context?.Check();
        return GetSelectedIndexWithWork(context, token);
    }
    internal void SetSelectedIndex(int value, Action<int>? checkpoint, CancellationToken token)
    {
        var context = HtmlSelectWorkContext.Create(checkpoint, token);
        context?.Check();
        SetSelectedIndexWithWork(value, context, token);
    }
    internal void SetIndexed(uint index, Element? option, Action<int>? checkpoint, CancellationToken token)
    {
        var context = HtmlSelectWorkContext.Create(checkpoint, token);
        context?.Check();
        SetIndexedWithWork(index, option, context, token);
    }
    internal void Remove(int index, Action<int>? checkpoint, CancellationToken token)
    {
        var context = HtmlSelectWorkContext.Create(checkpoint, token);
        context?.Check();
        RemoveWithWork(index, context, token);
    }
    internal void Add(Element element, int? before, Action<int>? checkpoint, CancellationToken token)
    {
        var context = HtmlSelectWorkContext.Create(checkpoint, token);
        context?.Check();
        AddWithWork(element, before, context, token);
    }
    internal void Add(Element element, Element? before, Action<int>? checkpoint, CancellationToken token)
    {
        var context = HtmlSelectWorkContext.Create(checkpoint, token);
        context?.Check();
        AddWithWork(element, before, context, token);
    }
}
