namespace Jint.HtmlParser;

/// <summary>HTML §4.10.7 native selectedness transitions; no retained option inventory.</summary>
internal sealed class HtmlSelectCore
{
    private bool _selectedContentKnown;
    private Element? _selectedContent;
    internal bool UpdatingSelectedContent { get; set; }
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
    private HtmlSelectMetadata _metadata;
    private Element? _selected;
    private bool _known;
    private bool _fallbackExhausted;
    internal HtmlSelectCore(Element element, CancellationToken token)
    {
        Element = element;
        var work = new HtmlSelectWork(element.OwnerDocument?.SelectWorkProbe, token);
        _metadata = HtmlSelectMetadata.Read(element.Attributes, ref work);
    }
    internal Element Element { get; }
    internal bool Multiple => _metadata.Multiple;
    internal uint DisplaySize => _metadata.Size ?? (Multiple ? 4u : 1u);
    internal void ApplyMetadata(HtmlSelectMetadata metadata) => _metadata = metadata;
    internal void AttributeChanged(string name, string? value)
    {
        if (name == "multiple")
        {
            _metadata = _metadata with { Multiple = value is not null };
            _known = false;
            if (Multiple) _selected = null;
        }
        else
        {
            var work = new HtmlSelectWork(Element.OwnerDocument?.SelectWorkProbe, default);
            _metadata = _metadata with { Size = HtmlSelectState.ParseSize(value, ref work) };
        }
        _fallbackExhausted = false;
    }
    internal void SelectionChanged(HtmlOptionCore option)
    {
        if (Multiple) return;
        if (option.Selected)
        {
            if (_selected is null) _selected = option.Element;
            else if (!ReferenceEquals(_selected, option.Element)) _known = false;
        }
        else if (ReferenceEquals(_selected, option.Element)) { _selected = null; _fallbackExhausted = false; }
    }
    internal void MembershipChanged(bool insertion, bool append, Element? entrant, CancellationToken token)
    {
        if (_selected is not null && _selected.ExistingOptionCore?.CachedNearestSelect != Element)
        {
            _selected = null;
            _fallbackExhausted = false;
        }
        // Removing disabled candidates cannot make an exhausted fallback eligible.
        if (insertion && !append) _fallbackExhausted = false;
        else if (insertion && _fallbackExhausted && entrant is not null && !Multiple && DisplaySize == 1 &&
            !HtmlDisabledness.IsOptionDisabled(entrant, token)) _fallbackExhausted = false;
    }
    internal void InvalidateFallback() => _fallbackExhausted = false;
    internal static IEnumerable<Element> Enumerate(Element select, CancellationToken token)
    {
        var work = new HtmlSelectWork(select.OwnerDocument?.SelectWorkProbe, token);
        var pending = new Stack<(Node Node, int Groups)>();
        if (select.FirstChild is { } first) pending.Push((first, 0));
        while (pending.TryPop(out var entry))
        {
            work.Step();
            var node = entry.Node;
            if (node.NextSibling is { } sibling) pending.Push((sibling, entry.Groups));
            var groups = entry.Groups;
            var prune = false;
            if (node is Element { NamespaceUri: Namespaces.Html } html)
            {
                if (html.LocalName == "option") yield return html;
                prune = html.LocalName is "select" or "hr" or "option" or "datalist" ||
                    html.LocalName == "optgroup" && groups != 0;
                if (html.LocalName == "optgroup") groups++;
            }
            if (!prune && node.FirstChild is { } child) pending.Push((child, groups));
        }
        work.Check();
    }
    private void Normalize(bool markDocument, CancellationToken token)
    {
        if (Multiple || _known) return;
        var selected = new List<HtmlOptionCore>();
        foreach (var option in Enumerate(Element, token))
            if (option.GetOptionCore(token) is { Selected: true } core) selected.Add(core);
        token.ThrowIfCancellationRequested();
        for (var i = 0; i + 1 < selected.Count; i++) selected[i].Write(false, selected[i].DirtySelectedness, markDocument);
        _selected = selected.Count == 0 ? null : selected[^1].Element;
        _known = true;
    }
    internal void ExcludePeers(HtmlOptionCore chosen, bool markDocument = true)
    {
        if (Multiple) return;
        if (_selected is { } old && !ReferenceEquals(old, chosen.Element))
        {
            var core = old.GetOptionCore();
            core.Write(false, core.DirtySelectedness, markDocument);
        }
        _selected = chosen.Element;
        _known = true;
    }
    internal void PrepareTransition(CancellationToken token, bool markDocument = true) => Normalize(markDocument, token);
    internal void SetSelectedness(CancellationToken token, bool markDocument = true)
    {
        Normalize(markDocument, token);
        if (Multiple || _selected is not null || DisplaySize != 1 || _fallbackExhausted) return;
        Element? fallback = null;
        foreach (var option in Enumerate(Element, token))
            if (!HtmlDisabledness.IsOptionDisabled(option, token)) { fallback = option; break; }
        token.ThrowIfCancellationRequested();
        _fallbackExhausted = fallback is null;
        if (fallback is not null)
        {
            var core = fallback.GetOptionCore(token);
            core.Write(true, core.DirtySelectedness, markDocument);
            _selected = fallback;
        }
    }
    internal void SetOptionSelected(HtmlOptionCore option, bool selected, CancellationToken token)
    {
        Normalize(true, token);
        Element? fallback = null;
        if (!selected && !Multiple && (_selected is null || ReferenceEquals(_selected, option.Element)) && DisplaySize == 1)
            foreach (var candidate in Enumerate(Element, token))
                if (!HtmlDisabledness.IsOptionDisabled(candidate, token)) { fallback = candidate; break; }
        token.ThrowIfCancellationRequested();
        option.Write(selected, true);
        if (selected) ExcludePeers(option);
        if (fallback is not null)
        {
            var core = fallback.GetOptionCore(token);
            core.Write(true, core.DirtySelectedness);
            _selected = fallback;
        }
        _known = true;
    }
}
