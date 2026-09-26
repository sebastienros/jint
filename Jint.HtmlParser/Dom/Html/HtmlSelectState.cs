namespace Jint.HtmlParser;

/// <summary>HTML §4.10.7 select option inventory and selectedness-setting algorithm.</summary>
internal sealed class HtmlSelectState
{
    private List<Element>? _options;
    private int _fallbackScanIndex;
    private bool _selectedContentKnown;
    private Element? _selectedContent;
    internal bool UpdatingSelectedContent { get; set; }
    internal string FallbackButtonText { get; private set; } = string.Empty;
    internal void InvalidateSelectedContent() { _selectedContentKnown = false; _selectedContent = null; }
    internal Element? GetEnabledSelectedContent(CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        if (Multiple) return null;
        if (!_selectedContentKnown)
        {
            Element? result = null;
            var work = new HtmlSelectWork(Element.OwnerDocument?.SelectWorkProbe, token);
            for (var node = Element.FirstChild; node is not null; node = HtmlSelectMutations.Next(node, Element, false, ref work))
            {
                work.Step();
                if (node is Element { NamespaceUri: Namespaces.Html, LocalName: "selectedcontent" } content) { result = content; break; }
            }
            work.Check();
            _selectedContent = result;
            _selectedContentKnown = true;
        }
        return _selectedContent?.GetHtmlState()!.SelectedContentDisabled == false ? _selectedContent : null;
    }
    private readonly List<Element> _selected = [];
    private bool _multiple;
    private uint? _parsedSize;
    private void ClearSelected()
    {
        foreach (var option in _selected)
        {
            var state = option.GetHtmlState()!.Option!;
            if (!ReferenceEquals(state.SelectionIndexOwner, this)) continue;
            state.SelectionIndexOwner = null;
            state.SelectedPosition = -1;
        }
        _selected.Clear();
    }
    private void AddSelected(Element option)
    {
        var state = option.GetHtmlState()!.Option!;
        if (ReferenceEquals(state.SelectionIndexOwner, this)) return;
        state.SelectionIndexOwner?.RemoveSelected(option);
        state.SelectedPosition = _selected.Count;
        state.SelectionIndexOwner = this;
        _selected.Add(option);
    }
    private void RemoveSelected(Element option)
    {
        var state = option.GetHtmlState()!.Option!;
        if (!ReferenceEquals(state.SelectionIndexOwner, this)) return;
        var index = state.SelectedPosition;
        var last = _selected[^1];
        _selected[index] = last;
        last.GetHtmlState()!.Option!.SelectedPosition = index;
        _selected.RemoveAt(_selected.Count - 1);
        state.SelectionIndexOwner = null;
        state.SelectedPosition = -1;
    }
    private HtmlSelectOptions? _optionsView;
    private HtmlSelectOptions? _selectedView;
    internal HtmlSelectState(Element element, CancellationToken token = default) { Element = element; RefreshMetadata(token); }
    internal HtmlSelectState(Element element, HtmlSelectMetadata metadata) { Element = element; ApplyMetadata(metadata); }
    internal Element Element { get; }
    internal HtmlSelectOptions Options => _optionsView ??= new HtmlSelectOptions(this, false);
    internal HtmlSelectOptions SelectedOptions => _selectedView ??= new HtmlSelectOptions(this, true);
    internal bool Multiple => _multiple;
    internal string Type => Multiple ? "select-multiple" : "select-one";
    internal bool UserValidity { get; private set; }
    internal long MembershipRevision { get; private set; }
    internal long SelectionRevision { get; private set; }
    internal uint GetDisplaySize(CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        return _parsedSize ?? (Multiple ? 4u : 1u);
    }
    internal void RefreshMetadata(CancellationToken token)
    {
        var work = new HtmlSelectWork(Element.OwnerDocument?.SelectWorkProbe, token);
        work.Check();
        ApplyMetadata(HtmlSelectMetadata.Read(Element.Attributes, ref work));
    }
    internal void ApplyMetadata(HtmlSelectMetadata metadata) { _multiple = metadata.Multiple; _parsedSize = metadata.Size; }
    internal void AttributeChanged(string name, string? value)
    {
        if (name == "multiple") _multiple = value is not null;
        else if (name == "size")
        {
            var work = new HtmlSelectWork(Element.OwnerDocument?.SelectWorkProbe, default);
            _parsedSize = ParseSize(value, ref work);
        }
    }
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
            if (option.GetHtmlState()!.Option!.Selected) selected.Add(option);
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
        _fallbackScanIndex = 0;
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
        if (option.GetHtmlState()!.Option!.Selected) AddSelected(option);
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
    internal void InvalidateFallback() => _fallbackScanIndex = 0;
    internal void SelectionChanged(HtmlOptionState option)
    {
        if (option.Selected) AddSelected(option.Element);
        else { RemoveSelected(option.Element); _fallbackScanIndex = 0; }
        SelectionRevision++;
        _selectedView?.InvalidateSelection();
    }
    internal void SetOptionSelected(HtmlOptionState state, bool selected, CancellationToken token)
    {
        var options = Prepare(token);
        var peers = PrepareSelected(token);
        Element? fallback = null;
        var remaining = peers.Count - (state.Selected ? 1 : 0);
        var work = new HtmlSelectWork(Element.OwnerDocument?.SelectWorkProbe, token);
        if (!selected && !Multiple && remaining == 0 && GetDisplaySize(token) == 1)
            foreach (var option in options)
            {
                work.Step();
                if (!HtmlDisabledness.IsOptionDisabled(option, token)) { fallback = option; break; }
            }
        work.Check();
        state.Write(selected, true);
        if (selected && !Multiple)
            foreach (var peer in peers)
                if (!ReferenceEquals(peer, state.Element))
                {
                    var other = peer.GetHtmlState()!.Option!;
                    other.Write(false, other.DirtySelectedness);
                }
        if (fallback is not null)
        {
            var other = fallback.GetHtmlState()!.Option!;
            other.Write(true, other.DirtySelectedness);
        }
    }
    internal void ExcludePeers(HtmlOptionState chosen, bool markDocument = true)
    {
        if (Multiple) return;
        var work = new HtmlSelectWork(Element.OwnerDocument?.SelectWorkProbe, default);
        for (var i = _selected.Count - 1; i >= 0; i--)
        {
            work.Step();
            var other = _selected[i];
            if (ReferenceEquals(other, chosen.Element)) continue;
            var state = other.GetHtmlState()!.Option!;
            RemoveSelected(other);
            state.Write(false, state.DirtySelectedness, markDocument);
        }
    }
    internal void SetSelectedness(CancellationToken token, bool markDocument = true)
    {
        var options = Prepare(token);
        var work = new HtmlSelectWork(Element.OwnerDocument?.SelectWorkProbe, token);
        Element? chosen = null;
        if (!Multiple && _selected.Count == 0 && GetDisplaySize(token) == 1)
        {
            var scanned = _fallbackScanIndex;
            while (scanned < options.Count)
            {
                work.Step();
                var option = options[scanned++];
                if (!HtmlDisabledness.IsOptionDisabled(option, token)) { chosen = option; break; }
            }
            work.Check();
            _fallbackScanIndex = scanned;
        }
        else if (!Multiple && _selected.Count > 1)
        {
            foreach (var option in options) { work.Step(); if (option.GetHtmlState()!.Option!.Selected) chosen = option; }
        }
        work.Check();
        if (chosen is not null)
        {
            var state = chosen.GetHtmlState()!.Option!;
            state.Write(true, state.DirtySelectedness, markDocument);
            AddSelected(chosen);
            ExcludePeers(state, markDocument);
        }
    }
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
        for (var i = 0; i < options.Count; i++) { work.Step(); if (options[i].GetHtmlState()!.Option!.Selected) { work.Check(); return i; } }
        work.Check();
        return -1;
    }
    internal string GetValue(CancellationToken token)
    {
        var index = GetSelectedIndex(token);
        return index < 0 ? string.Empty : Prepare(token)[index].GetHtmlState()!.Option!.GetValue(token);
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
            if (HtmlSelectWork.StringEquals(option.GetHtmlState()!.Option!.GetValue(token), value, ref work)) { chosen = option; break; }
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
            var state = selected.GetHtmlState()!.Option!;
            state.Write(false, state.DirtySelectedness);
        }
        chosen?.GetHtmlState()!.Option!.Write(true, true);
    }
    internal bool ApplyUserSelection(Element option, bool selected, CancellationToken token)
    {
        ArgumentNullException.ThrowIfNull(option);
        if (Element.GetHtmlState()!.GetDisabledState(token) != HtmlDisabledState.Enabled ||
            IndexOf(option, token) < 0 || HtmlDisabledness.IsOptionDisabled(option, token)) return false;
        if (!Multiple && !selected && GetDisplaySize(token) <= 1) return false;
        var state = option.GetHtmlState()!.Option!;
        var changed = state.Selected != selected || selected && !Multiple && _selected.Count > 1;
        var peers = selected && !Multiple ? PrepareSelected(token) : null;
        token.ThrowIfCancellationRequested();
        state.Write(selected, true);
        if (peers is not null)
            foreach (var peer in peers)
                if (!ReferenceEquals(peer, option))
                {
                    var other = peer.GetHtmlState()!.Option!;
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
        var label = option?.GetHtmlState()!.Option!.GetSemanticLabel(token) ?? string.Empty;
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
        for (var i = 0; i < defaults.Length; i++) { work.Step(); defaults[i] = options[i].GetHtmlState()!.Option!.DefaultSelected; }
        work.Check();
        SetUserValidity(false);
        for (var i = 0; i < defaults.Length; i++) options[i].GetHtmlState()!.Option!.Write(defaults[i], false);
        SetSelectedness(CancellationToken.None);
    }
}
