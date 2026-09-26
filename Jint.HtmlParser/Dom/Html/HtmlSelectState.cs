namespace Jint.HtmlParser;

/// <summary>HTML §4.10.7 select option inventory and selectedness-setting algorithm.</summary>
internal sealed class HtmlSelectState
{
    private List<Element>? _options;
    internal bool HasOptionInventory => _options is not null;
    internal bool UpdatingSelectedContent { get => _core.UpdatingSelectedContent; set => _core.UpdatingSelectedContent = value; }
    internal string FallbackButtonText { get; private set; } = string.Empty;
    internal void InvalidateSelectedContent() => _core.InvalidateSelectedContent();
    internal Element? GetEnabledSelectedContent(CancellationToken token)
        => GetEnabledSelectedContentWithWork((HtmlSelectWorkContext?) null, token);
    internal Element? GetEnabledSelectedContentWithWork(HtmlSelectWorkContext? context, CancellationToken token) => _core.GetEnabledSelectedContentWithWork(context, token);
    private readonly List<Element> _selected = [];
    private readonly HtmlSelectCore _core;
    private readonly Dictionary<Element, int> _selectedSlots = [];
    internal int SelectedPosition(Element option) => _selectedSlots.TryGetValue(option, out var index) ? index : -1;
    private void ClearSelected() { _selected.Clear(); _selectedSlots.Clear(); }
    private void AddSelected(Element option)
    {
        if (_selectedSlots.ContainsKey(option)) return;
        _selectedSlots.Add(option, _selected.Count);
        _selected.Add(option);
    }
    private void RemoveSelected(Element option)
    {
        if (!_selectedSlots.Remove(option, out var index)) return;
        var last = _selected[^1];
        _selected[index] = last;
        _selected.RemoveAt(_selected.Count - 1);
        if (!ReferenceEquals(last, option)) _selectedSlots[last] = index;
    }
    private HtmlSelectOptions? _optionsView;
    private HtmlSelectOptions? _selectedView;
    internal HtmlSelectState(Element element, CancellationToken token = default) : this(element, null, token) { }
    internal HtmlSelectState(Element element, HtmlSelectWorkContext? context, CancellationToken token = default) { Element = element; _core = element.GetSelectCoreWithWork(context, token); }
    internal HtmlSelectState(Element element, HtmlSelectMetadata metadata) { Element = element; _core = element.GetSelectCore(); ApplyMetadata(metadata); }
    internal Element Element { get; }
    internal HtmlSelectOptions Options => _optionsView ??= new HtmlSelectOptions(this, false);
    internal HtmlSelectOptions SelectedOptions => _selectedView ??= new HtmlSelectOptions(this, true);
    internal bool Multiple => _core.Multiple;
    internal string Type => Multiple ? "select-multiple" : "select-one";
    internal bool UserValidity { get; private set; }
    internal long MembershipRevision { get; private set; }
    internal long SelectionRevision { get; private set; }
    internal uint GetDisplaySize(CancellationToken token)
        => GetDisplaySizeWithWork((HtmlSelectWorkContext?) null, token);
    internal uint GetDisplaySizeWithWork(HtmlSelectWorkContext? context, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        return _core.DisplaySize;
    }
    internal void RefreshMetadata(CancellationToken token)
        => RefreshMetadataWithWork((HtmlSelectWorkContext?) null, token);
    internal void RefreshMetadataWithWork(HtmlSelectWorkContext? context, CancellationToken token)
    {
        var work = new HtmlSelectWork(Element.OwnerDocument?.SelectWorkProbe, context, token);
        work.Check();
        ApplyMetadata(HtmlSelectMetadata.Read(Element.Attributes, ref work));
    }
    internal void ApplyMetadata(HtmlSelectMetadata metadata) => _core.ApplyMetadata(metadata);
    internal void AttributeChanged(string name, string? value) => _core.AttributeChanged(name, value);
    internal static uint? ParseSize(string? raw, ref HtmlSelectWork work)
    {
        if (raw is null) return null;
        var i = 0;
        while (i < raw.Length && raw[i] is ' ' or '\t' or '\n' or '\r' or '\f') { work.Step(); i++; }
        if (i < raw.Length && raw[i] == '+') { work.Step(); i++; }
        if (i == raw.Length || raw[i] is < '0' or > '9') return null;
        uint result = 0;
        while (i < raw.Length && raw[i] is >= '0' and <= '9')
        {
            work.Step();
            var digit = (uint) (raw[i++] - '0');
            result = result > (uint.MaxValue - digit) / 10 ? uint.MaxValue : result * 10 + digit;
        }
        return result;
    }
    internal void SetUserValidity(bool value)
    {
        if (UserValidity == value) return;
        UserValidity = value;
        Element.OwnerDocument!.MarkMutation();
    }
    internal List<Element> Prepare(CancellationToken token)
        => PrepareWithWork((HtmlSelectWorkContext?) null, token);
    internal List<Element> PrepareWithWork(HtmlSelectWorkContext? context, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        if (_options is { } cached) return cached;
        var work = new HtmlSelectWork(Element.OwnerDocument?.SelectWorkProbe, context, token);
        var options = new List<Element>();
        // Carry optgroup depth on the traversal stack: no ancestry scan per candidate.
        var pending = new Stack<(Node Node, int Groups)>();
        if (Element.FirstChild is { } first) pending.Push((first, 0));
        while (pending.TryPop(out var entry))
        {
            var node = entry.Node;
            work.Step();
            if (node.NextSibling is { } sibling) pending.Push((sibling, entry.Groups));
            var groups = entry.Groups;
            var prune = false;
            if (node is Element { NamespaceUri: Namespaces.Html } html)
            {
                if (html.LocalName == "option") options.Add(html);
                prune = html.LocalName is "select" or "hr" or "option" or "datalist" ||
                    html.LocalName == "optgroup" && groups != 0;
                if (html.LocalName == "optgroup") groups++;
            }
            if (!prune && node.FirstChild is { } child) pending.Push((child, groups));
        }
        var selected = new List<Element>();
        foreach (var option in options)
        {
            work.Step();
            if (option.GetOptionCoreWithWork(context, token).Selected) selected.Add(option);
        }
        foreach (var option in _selected) work.Step();
        work.Check();
        _options = options;
        ClearSelected();
        foreach (var option in selected) AddSelected(option);
        return options;
    }
    internal void InvalidateMembership()
    {
        _options = null;
        ClearSelected();
        MembershipRevision++;
        SelectionRevision++;
        _selectedView?.InvalidateSelection();
    }
    internal bool AppendOption(Element option)
    {
        if (_options is null) return false;
        var work = new HtmlSelectWork(Element.OwnerDocument?.SelectWorkProbe, default);
        Node current = option;
        while (!ReferenceEquals(current, Element))
        {
            work.Step();
            if (current.NextSibling is not null || current.ParentNode is not { } parent) return false;
            current = parent;
        }
        _options.Add(option);
        if (option.GetOptionCore().Selected) AddSelected(option);
        MembershipRevision++;
        SelectionRevision++;
        _selectedView?.InvalidateSelection();
        return true;
    }
    internal bool RemoveOption(Element option)
    {
        if (_options is null || _options.Count == 0 || !ReferenceEquals(_options[^1], option)) return false;
        _options.RemoveAt(_options.Count - 1);
        RemoveSelected(option);
        MembershipRevision++;
        SelectionRevision++;
        _selectedView?.InvalidateSelection();
        return true;
    }
    internal void InvalidateFallback() => _core.InvalidateFallback();
    internal void SelectionChanged(HtmlOptionCore option)
    {
        if (_options is null) return;
        if (option.Selected) AddSelected(option.Element);
        else RemoveSelected(option.Element);
        SelectionRevision++;
        _selectedView?.InvalidateSelection();
    }
    internal void SetOptionSelected(HtmlOptionState state, bool selected, CancellationToken token)
        => SetOptionSelectedWithWork(state, selected, (HtmlSelectWorkContext?) null, token);
    internal void SetOptionSelectedWithWork(HtmlOptionState state, bool selected, HtmlSelectWorkContext? context, CancellationToken token)
        => _core.SetOptionSelectedWithWork(state.Element.GetOptionCoreWithWork(context, token), selected, context, token);
    internal void ExcludePeers(HtmlOptionState chosen, bool markDocument = true)
        => _core.ExcludePeers(chosen.Element.GetOptionCore(), markDocument);
    internal void SetSelectedness(CancellationToken token, bool markDocument = true)
        => SetSelectednessWithWork(markDocument, (HtmlSelectWorkContext?) null, token);
    internal void SetSelectednessWithWork(bool markDocument, HtmlSelectWorkContext? context, CancellationToken token)
        => _core.SetSelectednessWithWork(markDocument, context, token);
    internal int IndexOf(Element option, CancellationToken token)
        => IndexOfWithWork(option, (HtmlSelectWorkContext?) null, token);
    internal int IndexOfWithWork(Element option, HtmlSelectWorkContext? context, CancellationToken token)
    {
        var options = PrepareWithWork(context, token);
        var work = new HtmlSelectWork(Element.OwnerDocument?.SelectWorkProbe, context, token);
        for (var i = 0; i < options.Count; i++) { work.Step(); if (ReferenceEquals(options[i], option)) { work.Check(); return i; } }
        work.Check();
        return -1;
    }
    internal int GetSelectedIndex(CancellationToken token)
        => GetSelectedIndexWithWork((HtmlSelectWorkContext?) null, token);
    internal int GetSelectedIndexWithWork(HtmlSelectWorkContext? context, CancellationToken token)
    {
        var options = PrepareWithWork(context, token);
        var work = new HtmlSelectWork(Element.OwnerDocument?.SelectWorkProbe, context, token);
        for (var i = 0; i < options.Count; i++) { work.Step(); if (options[i].GetOptionCoreWithWork(context, token).Selected) { work.Check(); return i; } }
        work.Check();
        return -1;
    }
    internal string GetValue(CancellationToken token)
        => GetValueWithWork((HtmlSelectWorkContext?) null, token);
    internal string GetValueWithWork(HtmlSelectWorkContext? context, CancellationToken token)
    {
        var index = GetSelectedIndexWithWork(context, token);
        var result = index < 0 ? string.Empty : PrepareWithWork(context, token)[index].GetHtmlState()!.GetOptionStateWithWork(context, token)!.GetValueWithWork(context, token);
        HtmlSelectWork.Check(context, token);
        return result;
    }
    internal void SetSelectedIndex(int index, CancellationToken token)
        => SetSelectedIndexWithWork(index, (HtmlSelectWorkContext?) null, token);
    internal void SetSelectedIndexWithWork(int index, HtmlSelectWorkContext? context, CancellationToken token)
    {
        var options = PrepareWithWork(context, token);
        var chosen = (uint) index < (uint) options.Count ? options[index] : null;
        var update = HtmlSelectedContent.PrepareUpdateWithWork(this, chosen, context, token);
        var selected = PrepareSelectedWithWork(context, token);
        HtmlSelectWork.Check(context, token);
        SelectOnly(chosen, selected);
        HtmlSelectedContent.Apply(this, update);
        HtmlSelectWork.Check(context, token);
    }
    internal void SetValue(string value, CancellationToken token)
        => SetValueWithWork(value, (HtmlSelectWorkContext?) null, token);
    internal void SetValueWithWork(string value, HtmlSelectWorkContext? context, CancellationToken token)
    {
        ArgumentNullException.ThrowIfNull(value);
        var options = PrepareWithWork(context, token);
        var work = new HtmlSelectWork(Element.OwnerDocument?.SelectWorkProbe, context, token);
        Element? chosen = null;
        foreach (var option in options)
        {
            work.Step();
            if (HtmlSelectWork.StringEquals(option.GetHtmlState()!.GetOptionStateWithWork(context, token)!.GetValueWithWork(context, token), value, ref work)) { chosen = option; break; }
        }
        work.Check();
        var update = HtmlSelectedContent.PrepareUpdateWithWork(this, chosen, context, token);
        var selected = PrepareSelectedWithWork(context, token);
        HtmlSelectWork.Check(context, token);
        SelectOnly(chosen, selected);
        HtmlSelectedContent.Apply(this, update);
        HtmlSelectWork.Check(context, token);
    }
    private List<Element> PrepareSelectedWithWork(HtmlSelectWorkContext? context, CancellationToken token)
    {
        var result = new List<Element>(_selected.Count);
        var work = new HtmlSelectWork(Element.OwnerDocument?.SelectWorkProbe, context, token);
        foreach (var selected in _selected) { work.Step(); result.Add(selected); }
        work.Check();
        return result;
    }
    private static void SelectOnly(Element? chosen, List<Element> previous)
    {
        foreach (var selected in previous)
        {
            var state = selected.GetOptionCore();
            state.Write(false, state.DirtySelectedness);
        }
        chosen?.GetOptionCore().Write(true, true);
    }
    internal bool ApplyUserSelection(Element option, bool selected, CancellationToken token)
        => ApplyUserSelectionWithWork(option, selected, (HtmlSelectWorkContext?) null, token);
    internal bool ApplyUserSelectionWithWork(Element option, bool selected, HtmlSelectWorkContext? context, CancellationToken token)
    {
        ArgumentNullException.ThrowIfNull(option);
        if (Element.GetHtmlState()!.GetDisabledStateWithWork(context, token) != HtmlDisabledState.Enabled ||
            IndexOfWithWork(option, context, token) < 0 || HtmlDisabledness.IsOptionDisabledWithWork(option, context, token)) return false;
        if (!Multiple && !selected && GetDisplaySizeWithWork(context, token) <= 1) return false;
        var state = option.GetOptionCoreWithWork(context, token);
        var changed = state.Selected != selected || selected && !Multiple && _selected.Count > 1;
        var peers = selected && !Multiple ? PrepareSelectedWithWork(context, token) : null;
        token.ThrowIfCancellationRequested();
        state.Write(selected, true);
        if (peers is not null)
            foreach (var peer in peers)
                if (!ReferenceEquals(peer, option))
                {
                    var other = peer.GetOptionCoreWithWork(context, token);
                    other.Write(false, other.DirtySelectedness);
                }
        return changed;
    }
    // Browser queues notifications and dispatches input/change; native state owns
    // the user-validity, selectedcontent and fallback-button update at that turn.
    internal void CompleteUserSelection(CancellationToken token)
        => CompleteUserSelectionWithWork((HtmlSelectWorkContext?) null, token);
    internal void CompleteUserSelectionWithWork(HtmlSelectWorkContext? context, CancellationToken token)
    {
        var index = GetSelectedIndexWithWork(context, token);
        var option = index < 0 ? null : PrepareWithWork(context, token)[index];
        var label = option?.GetHtmlState()!.GetOptionStateWithWork(context, token)!.GetSemanticLabelWithWork(context, token) ?? string.Empty;
        var update = HtmlSelectedContent.PrepareUpdateWithWork(this, option, context, token);
        token.ThrowIfCancellationRequested();
        SetUserValidity(true);
        HtmlSelectedContent.Apply(this, update);
        HtmlSelectWork.Check(context, token);
        if (FallbackButtonText != label) { FallbackButtonText = label; Element.OwnerDocument!.MarkMutation(); }
    }
    internal void Reset(CancellationToken token)
        => ResetWithWork((HtmlSelectWorkContext?) null, token);
    internal void ResetWithWork(HtmlSelectWorkContext? context, CancellationToken token)
    {
        var options = PrepareWithWork(context, token);
        var defaults = new bool[options.Count];
        var work = new HtmlSelectWork(Element.OwnerDocument?.SelectWorkProbe, context, token);
        for (var i = 0; i < defaults.Length; i++)
        {
            work.Step();
            defaults[i] = options[i].ExistingOptionState?.DefaultSelected ?? HtmlOptionCore.ReadDefault(options[i].Attributes, ref work);
        }
        work.Check();
        SetUserValidity(false);
        for (var i = 0; i < defaults.Length; i++) options[i].GetOptionCore(CancellationToken.None).Write(defaults[i], false);
        SetSelectedness(CancellationToken.None);
    }

    internal Element? GetEnabledSelectedContent(Action<int>? checkpoint, CancellationToken token)
    {
        var context = HtmlSelectWorkContext.Create(checkpoint, token);
        context?.Check();
        var result = GetEnabledSelectedContentWithWork(context, token);
        HtmlSelectWork.Check(context, token);
        return result;
    }
    internal uint GetDisplaySize(Action<int>? checkpoint, CancellationToken token)
    {
        var context = HtmlSelectWorkContext.Create(checkpoint, token);
        context?.Check();
        var result = GetDisplaySizeWithWork(context, token);
        HtmlSelectWork.Check(context, token);
        return result;
    }
    internal void RefreshMetadata(Action<int>? checkpoint, CancellationToken token)
    {
        var context = HtmlSelectWorkContext.Create(checkpoint, token);
        context?.Check();
        RefreshMetadataWithWork(context, token);
        HtmlSelectWork.Check(context, token);
    }
    internal List<Element> Prepare(Action<int>? checkpoint, CancellationToken token)
    {
        var context = HtmlSelectWorkContext.Create(checkpoint, token);
        context?.Check();
        var result = PrepareWithWork(context, token);
        HtmlSelectWork.Check(context, token);
        return result;
    }
    internal void SetOptionSelected(HtmlOptionState state, bool selected, Action<int>? checkpoint, CancellationToken token)
    {
        var context = HtmlSelectWorkContext.Create(checkpoint, token);
        context?.Check();
        SetOptionSelectedWithWork(state, selected, context, token);
        HtmlSelectWork.Check(context, token);
    }
    internal int IndexOf(Element option, Action<int>? checkpoint, CancellationToken token)
    {
        var context = HtmlSelectWorkContext.Create(checkpoint, token);
        context?.Check();
        var result = IndexOfWithWork(option, context, token);
        HtmlSelectWork.Check(context, token);
        return result;
    }
    internal int GetSelectedIndex(Action<int>? checkpoint, CancellationToken token)
    {
        var context = HtmlSelectWorkContext.Create(checkpoint, token);
        context?.Check();
        var result = GetSelectedIndexWithWork(context, token);
        HtmlSelectWork.Check(context, token);
        return result;
    }
    internal string GetValue(Action<int>? checkpoint, CancellationToken token)
    {
        var context = HtmlSelectWorkContext.Create(checkpoint, token);
        context?.Check();
        var result = GetValueWithWork(context, token);
        HtmlSelectWork.Check(context, token);
        return result;
    }
    internal void SetSelectedIndex(int index, Action<int>? checkpoint, CancellationToken token)
    {
        var context = HtmlSelectWorkContext.Create(checkpoint, token);
        context?.Check();
        SetSelectedIndexWithWork(index, context, token);
        HtmlSelectWork.Check(context, token);
    }
    internal void SetValue(string value, Action<int>? checkpoint, CancellationToken token)
    {
        var context = HtmlSelectWorkContext.Create(checkpoint, token);
        context?.Check();
        SetValueWithWork(value, context, token);
        HtmlSelectWork.Check(context, token);
    }
    internal bool ApplyUserSelection(Element option, bool selected, Action<int>? checkpoint, CancellationToken token)
    {
        var context = HtmlSelectWorkContext.Create(checkpoint, token);
        context?.Check();
        var result = ApplyUserSelectionWithWork(option, selected, context, token);
        HtmlSelectWork.Check(context, token);
        return result;
    }
    internal void CompleteUserSelection(Action<int>? checkpoint, CancellationToken token)
    {
        var context = HtmlSelectWorkContext.Create(checkpoint, token);
        context?.Check();
        CompleteUserSelectionWithWork(context, token);
        HtmlSelectWork.Check(context, token);
    }
    internal void Reset(Action<int>? checkpoint, CancellationToken token)
    {
        var context = HtmlSelectWorkContext.Create(checkpoint, token);
        context?.Check();
        ResetWithWork(context, token);
        HtmlSelectWork.Check(context, token);
    }
}
