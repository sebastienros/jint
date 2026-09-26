namespace Jint.HtmlParser;

/// <summary>HTML §4.10.7 select option inventory and selectedness-setting algorithm.</summary>
internal sealed class HtmlSelectState
{
    private List<Element>? _options;
    internal bool HasOptionInventory => _options is not null;
    internal bool UpdatingSelectedContent { get => _core.UpdatingSelectedContent; set => _core.UpdatingSelectedContent = value; }
    internal string FallbackButtonText { get; private set; } = string.Empty;
    internal void InvalidateSelectedContent() => _core.InvalidateSelectedContent();
    internal Element? GetEnabledSelectedContent(CancellationToken token) => _core.GetEnabledSelectedContent(token);
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
    internal HtmlSelectState(Element element, CancellationToken token = default) { Element = element; _core = element.GetSelectCore(token); }
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
    {
        token.ThrowIfCancellationRequested();
        return _core.DisplaySize;
    }
    internal void RefreshMetadata(CancellationToken token)
    {
        var work = new HtmlSelectWork(Element.OwnerDocument?.SelectWorkProbe, token);
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
    {
        token.ThrowIfCancellationRequested();
        if (_options is { } cached) return cached;
        var work = new HtmlSelectWork(Element.OwnerDocument?.SelectWorkProbe, token);
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
            if (option.GetOptionCore(token).Selected) selected.Add(option);
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
        => _core.SetOptionSelected(state.Element.GetOptionCore(token), selected, token);
    internal void ExcludePeers(HtmlOptionState chosen, bool markDocument = true)
        => _core.ExcludePeers(chosen.Element.GetOptionCore(), markDocument);
    internal void SetSelectedness(CancellationToken token, bool markDocument = true)
        => _core.SetSelectedness(token, markDocument);
    internal int IndexOf(Element option, CancellationToken token)
    {
        var options = Prepare(token);
        var work = new HtmlSelectWork(Element.OwnerDocument?.SelectWorkProbe, token);
        for (var i = 0; i < options.Count; i++) { work.Step(); if (ReferenceEquals(options[i], option)) { work.Check(); return i; } }
        work.Check();
        return -1;
    }
    internal int GetSelectedIndex(CancellationToken token)
    {
        var options = Prepare(token);
        var work = new HtmlSelectWork(Element.OwnerDocument?.SelectWorkProbe, token);
        for (var i = 0; i < options.Count; i++) { work.Step(); if (options[i].GetOptionCore(token).Selected) { work.Check(); return i; } }
        work.Check();
        return -1;
    }
    internal string GetValue(CancellationToken token)
    {
        var index = GetSelectedIndex(token);
        return index < 0 ? string.Empty : Prepare(token)[index].GetHtmlState()!.GetOptionState(token)!.GetValue(token);
    }
    internal void SetSelectedIndex(int index, CancellationToken token)
    {
        var options = Prepare(token);
        var chosen = (uint) index < (uint) options.Count ? options[index] : null;
        var update = HtmlSelectedContent.PrepareUpdate(this, chosen, token);
        var selected = PrepareSelected(token);
        token.ThrowIfCancellationRequested();
        SelectOnly(chosen, selected);
        HtmlSelectedContent.Apply(this, update);
    }
    internal void SetValue(string value, CancellationToken token)
    {
        ArgumentNullException.ThrowIfNull(value);
        var options = Prepare(token);
        var work = new HtmlSelectWork(Element.OwnerDocument?.SelectWorkProbe, token);
        Element? chosen = null;
        foreach (var option in options)
        {
            work.Step();
            if (HtmlSelectWork.StringEquals(option.GetHtmlState()!.GetOptionState(token)!.GetValue(token), value, ref work)) { chosen = option; break; }
        }
        work.Check();
        var update = HtmlSelectedContent.PrepareUpdate(this, chosen, token);
        var selected = PrepareSelected(token);
        token.ThrowIfCancellationRequested();
        SelectOnly(chosen, selected);
        HtmlSelectedContent.Apply(this, update);
    }
    private List<Element> PrepareSelected(CancellationToken token)
    {
        var result = new List<Element>(_selected.Count);
        var work = new HtmlSelectWork(Element.OwnerDocument?.SelectWorkProbe, token);
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
    {
        ArgumentNullException.ThrowIfNull(option);
        if (Element.GetHtmlState()!.GetDisabledState(token) != HtmlDisabledState.Enabled ||
            IndexOf(option, token) < 0 || HtmlDisabledness.IsOptionDisabled(option, token)) return false;
        if (!Multiple && !selected && GetDisplaySize(token) <= 1) return false;
        var state = option.GetOptionCore(token);
        var changed = state.Selected != selected || selected && !Multiple && _selected.Count > 1;
        var peers = selected && !Multiple ? PrepareSelected(token) : null;
        token.ThrowIfCancellationRequested();
        state.Write(selected, true);
        if (peers is not null)
            foreach (var peer in peers)
                if (!ReferenceEquals(peer, option))
                {
                    var other = peer.GetOptionCore(token);
                    other.Write(false, other.DirtySelectedness);
                }
        return changed;
    }
    // Browser queues notifications and dispatches input/change; native state owns
    // the user-validity, selectedcontent and fallback-button update at that turn.
    internal void CompleteUserSelection(CancellationToken token)
    {
        var index = GetSelectedIndex(token);
        var option = index < 0 ? null : Prepare(token)[index];
        var label = option?.GetHtmlState()!.GetOptionState(token)!.GetSemanticLabel(token) ?? string.Empty;
        var update = HtmlSelectedContent.PrepareUpdate(this, option, token);
        token.ThrowIfCancellationRequested();
        SetUserValidity(true);
        HtmlSelectedContent.Apply(this, update);
        if (FallbackButtonText != label) { FallbackButtonText = label; Element.OwnerDocument!.MarkMutation(); }
    }
    internal void Reset(CancellationToken token)
    {
        var options = Prepare(token);
        var defaults = new bool[options.Count];
        var work = new HtmlSelectWork(Element.OwnerDocument?.SelectWorkProbe, token);
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
}
