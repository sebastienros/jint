using System.Text;

namespace Jint.HtmlParser;

/// <summary>HTML §4.10.10 option selectedness, dirtiness and HTML-aware text.</summary>
internal sealed class HtmlOptionState
{
    internal HtmlOptionState(Element element, CancellationToken token = default)
    {
        Element = element;
        RefreshMetadata(token);
        _core = element.InitializeOptionCore(DefaultSelected);
    }
    internal HtmlOptionState(Element element, HtmlOptionMetadata metadata)
    {
        Element = element;
        ApplyMetadata(metadata);
        _core = element.InitializeOptionCore(DefaultSelected);
    }
    internal Element Element { get; }
    private readonly HtmlOptionCore _core;
    internal bool Selected => _core.Selected;
    internal bool DirtySelectedness => _core.DirtySelectedness;
    internal bool DefaultSelected { get; private set; }
    private string? _value;
    private string? _label;
    private string? _id;
    private string? _name;
    internal HtmlSelectState? SelectionIndexOwner => CachedNearestSelect?.ExistingSelectState is { } state &&
        state.SelectedPosition(Element) >= 0 ? state : null;
    internal int SelectedPosition => SelectionIndexOwner?.SelectedPosition(Element) ?? -1;
    internal void RefreshMetadata(CancellationToken token)
    {
        var work = new HtmlSelectWork(Element.OwnerDocument?.SelectWorkProbe, token);
        work.Check();
        ApplyMetadata(HtmlOptionMetadata.Read(Element.Attributes, ref work));
    }
    internal void ApplyMetadata(HtmlOptionMetadata metadata)
    {
        DefaultSelected = metadata.DefaultSelected;
        _value = metadata.Value; _label = metadata.Label; _id = metadata.Id; _name = metadata.Name;
    }
    internal void AttributeChanged(string name, string? value)
    {
        switch (name)
        {
            case "selected": DefaultSelected = value is not null; break;
            case "value": _value = value; break;
            case "label": _label = value; break;
            case "id": _id = value; break;
            case "name": _name = value; break;
        }
    }
    internal bool MatchesName(string name, ref HtmlSelectWork work)
        => HtmlSelectWork.StringEquals(_id, name, ref work) || HtmlSelectWork.StringEquals(_name, name, ref work);
    internal Element? CachedNearestSelect { get => _core.CachedNearestSelect; set => _core.CachedNearestSelect = value; }
    internal Element? GetForm(CancellationToken token)
        => HtmlSelectAncestry.GetNearestSelect(Element, token) is { } select ? HtmlFormState.GetOwner(select) : null;
    internal bool IsDisabled(CancellationToken token) => HtmlDisabledness.IsOptionDisabled(Element, token);
    internal int GetIndex(CancellationToken token)
    {
        var select = HtmlSelectAncestry.GetNearestSelect(Element, token);
        return select is null ? 0 : Math.Max(0, select.GetHtmlState()!.GetSelectState(token)!.IndexOf(Element, token));
    }
    internal void SetDefaultSelected(bool value)
    {
        if (value) Element.SetAttribute("selected", "");
        else Element.RemoveAttributeNS(null, "selected");
    }
    internal void SetSelected(bool value, CancellationToken token)
    {
        _core.SetSelected(value, token);
    }
    internal void Write(bool selected, bool dirty, bool markDocument = true)
        => _core.Write(selected, dirty, markDocument);
    internal void CopyFrom(HtmlOptionState source) => _core.CopyFrom(source._core);
    internal string GetValue(CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        return _value ?? GetText(token);
    }
    internal void SetValue(string value) { ArgumentNullException.ThrowIfNull(value); Element.SetAttribute("value", value); }
    internal string GetLabel(CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        return _label ?? GetText(token);
    }
    internal string GetSemanticLabel(CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        return string.IsNullOrEmpty(_label) ? GetText(token) : _label;
    }
    internal void SetLabel(string value) { ArgumentNullException.ThrowIfNull(value); Element.SetAttribute("label", value); }
    internal string GetText(CancellationToken token)
    {
        var work = new HtmlSelectWork(Element.OwnerDocument?.SelectWorkProbe, token);
        work.Check();
        var text = new StringBuilder();
        var pendingSpace = false;
        for (var node = Element.FirstChild; node is not null;)
        {
            work.Step();
            var skip = node is Element e && (e.LocalName == "script" &&
                e.NamespaceUri is Namespaces.Html or Namespaces.Svg ||
                e.LocalName == "img" && e.NamespaceUri == Namespaces.Html);
            if (node is Text t)
            {
                for (var i = 0; i < t.DataLength; i++) Append(t.DataAt(i));
            }
            else if (node is CDataSection c)
            {
                foreach (var character in c.Data) Append(character);
            }
            node = HtmlSelectMutations.Next(node, Element, skip, ref work);
        }
        work.Check();
        return text.ToString();

        void Append(char character)
        {
            work.Step();
            if (character is ' ' or '\t' or '\n' or '\r' or '\f') { pendingSpace = text.Length != 0; return; }
            if (pendingSpace) { text.Append(' '); pendingSpace = false; }
            text.Append(character);
        }
    }
    internal void SetText(string value, CancellationToken token)
    {
        ArgumentNullException.ThrowIfNull(value);
        token.ThrowIfCancellationRequested();
        Element.ReplaceChildren(value.Length == 0 ? null : Element.OwnerDocument!.CreateTextNode(value));
    }
    internal void ParserFinished(CancellationToken token) => HtmlSelectedContent.MaybeCloneOption(this, token);
    internal static Element Create(Document document, string text, string? value, bool defaultSelected,
        bool selected, CancellationToken token)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(text);
        token.ThrowIfCancellationRequested();
        var option = document.CreateElementNS(Namespaces.Html, "option");
        var state = option.GetHtmlState()!.GetOptionState(token)!;
        state.SetText(text, token);
        if (value is not null) state.SetValue(value);
        state.SetDefaultSelected(defaultSelected);
        // Option's fourth argument overrides attribute initialization without dirtiness.
        state.Write(selected, false);
        return option;
    }
}
