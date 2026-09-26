using System.Text;

namespace Jint.HtmlParser;

/// <summary>HTML §4.10.10 option selectedness, dirtiness and HTML-aware text.</summary>
internal sealed class HtmlOptionState
{
    internal HtmlOptionState(Element element, CancellationToken token = default) : this(element, null, token) { }
    internal HtmlOptionState(Element element, HtmlSelectWorkContext? context, CancellationToken token = default)
    {
        Element = element;
        RefreshMetadataWithWork(context, token);
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
        => RefreshMetadataWithWork((HtmlSelectWorkContext?) null, token);
    internal void RefreshMetadataWithWork(HtmlSelectWorkContext? context, CancellationToken token)
    {
        var work = new HtmlSelectWork(Element.OwnerDocument?.SelectWorkProbe, context, token);
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
        => GetFormWithWork((HtmlSelectWorkContext?) null, token);
    internal Element? GetFormWithWork(HtmlSelectWorkContext? context, CancellationToken token)
        => HtmlSelectAncestry.GetNearestSelectWithWork(Element, context, token) is { } select ? HtmlFormState.GetOwner(select) : null;
    internal bool IsDisabled(CancellationToken token)
        => IsDisabledWithWork((HtmlSelectWorkContext?) null, token);
    internal bool IsDisabledWithWork(HtmlSelectWorkContext? context, CancellationToken token) => HtmlDisabledness.IsOptionDisabledWithWork(Element, context, token);
    internal int GetIndex(CancellationToken token)
        => GetIndexWithWork((HtmlSelectWorkContext?) null, token);
    internal int GetIndexWithWork(HtmlSelectWorkContext? context, CancellationToken token)
    {
        var select = HtmlSelectAncestry.GetNearestSelectWithWork(Element, context, token);
        return select is null ? 0 : Math.Max(0, select.GetHtmlState()!.GetSelectStateWithWork(context, token)!.IndexOfWithWork(Element, context, token));
    }
    internal void SetDefaultSelected(bool value)
    {
        if (value) Element.SetAttribute("selected", "");
        else Element.RemoveAttributeNS(null, "selected");
    }
    internal void SetSelected(bool value, CancellationToken token)
        => SetSelectedWithWork(value, (HtmlSelectWorkContext?) null, token);
    internal void SetSelectedWithWork(bool value, HtmlSelectWorkContext? context, CancellationToken token)
    {
        _core.SetSelectedWithWork(value, context, token);
        HtmlSelectWork.Check(context, token);
    }
    internal void Write(bool selected, bool dirty, bool markDocument = true)
        => _core.Write(selected, dirty, markDocument);
    internal void CopyFrom(HtmlOptionState source) => _core.CopyFrom(source._core);
    internal string GetValue(CancellationToken token)
        => GetValueWithWork((HtmlSelectWorkContext?) null, token);
    internal string GetValueWithWork(HtmlSelectWorkContext? context, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        var result = _value ?? GetTextWithWork(context, token);
        HtmlSelectWork.Check(context, token);
        return result;
    }
    internal void SetValue(string value) { ArgumentNullException.ThrowIfNull(value); Element.SetAttribute("value", value); }
    internal string GetLabel(CancellationToken token)
        => GetLabelWithWork((HtmlSelectWorkContext?) null, token);
    internal string GetLabelWithWork(HtmlSelectWorkContext? context, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        var result = _label ?? GetTextWithWork(context, token);
        HtmlSelectWork.Check(context, token);
        return result;
    }
    internal string GetSemanticLabel(CancellationToken token)
        => GetSemanticLabelWithWork((HtmlSelectWorkContext?) null, token);
    internal string GetSemanticLabelWithWork(HtmlSelectWorkContext? context, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        return string.IsNullOrEmpty(_label) ? GetTextWithWork(context, token) : _label;
    }
    internal void SetLabel(string value) { ArgumentNullException.ThrowIfNull(value); Element.SetAttribute("label", value); }
    internal string GetText(CancellationToken token)
        => GetTextWithWork((HtmlSelectWorkContext?) null, token);
    internal string GetTextWithWork(HtmlSelectWorkContext? context, CancellationToken token)
    {
        var work = new HtmlSelectWork(Element.OwnerDocument?.SelectWorkProbe, context, token);
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
        var result = text.ToString();
        HtmlSelectWork.Check(context, token);
        return result;

        void Append(char character)
        {
            work.Step();
            if (character is ' ' or '\t' or '\n' or '\r' or '\f') { pendingSpace = text.Length != 0; return; }
            if (pendingSpace) { text.Append(' '); pendingSpace = false; }
            text.Append(character);
        }
    }
    internal void SetText(string value, CancellationToken token)
        => SetTextWithWork(value, (HtmlSelectWorkContext?) null, token);
    internal void SetTextWithWork(string value, HtmlSelectWorkContext? context, CancellationToken token)
    {
        ArgumentNullException.ThrowIfNull(value);
        token.ThrowIfCancellationRequested();
        HtmlSelectWork.Check(context, token);
        Element.ReplaceChildren(value.Length == 0 ? null : Element.OwnerDocument!.CreateTextNode(value));
        HtmlSelectWork.Check(context, token);
    }
    internal void ParserFinished(CancellationToken token)
        => ParserFinishedWithWork((HtmlSelectWorkContext?) null, token);
    internal void ParserFinishedWithWork(HtmlSelectWorkContext? context, CancellationToken token) => HtmlSelectedContent.MaybeCloneOptionWithWork(this, context, token);
    internal static Element Create(Document document, string text, string? value, bool defaultSelected, bool selected, CancellationToken token)
        => CreateWithWork(document, text, value, defaultSelected, selected, (HtmlSelectWorkContext?) null, token);
    internal static Element CreateWithWork(Document document, string text, string? value, bool defaultSelected, bool selected, HtmlSelectWorkContext? context, CancellationToken token)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(text);
        token.ThrowIfCancellationRequested();
        var option = document.CreateElementNS(Namespaces.Html, "option");
        var state = option.GetHtmlState()!.GetOptionStateWithWork(context, token)!;
        state.SetTextWithWork(text, context, token);
        if (value is not null) state.SetValue(value);
        state.SetDefaultSelected(defaultSelected);
        // Option's fourth argument overrides attribute initialization without dirtiness.
        state.Write(selected, false);
        return option;
    }

    internal void RefreshMetadata(Action<int>? checkpoint, CancellationToken token)
    {
        var context = HtmlSelectWorkContext.Create(checkpoint, token);
        context?.Check();
        RefreshMetadataWithWork(context, token);
        HtmlSelectWork.Check(context, token);
    }
    internal Element? GetForm(Action<int>? checkpoint, CancellationToken token)
    {
        var context = HtmlSelectWorkContext.Create(checkpoint, token);
        context?.Check();
        var result = GetFormWithWork(context, token);
        HtmlSelectWork.Check(context, token);
        return result;
    }
    internal bool IsDisabled(Action<int>? checkpoint, CancellationToken token)
    {
        var context = HtmlSelectWorkContext.Create(checkpoint, token);
        context?.Check();
        var result = IsDisabledWithWork(context, token);
        HtmlSelectWork.Check(context, token);
        return result;
    }
    internal int GetIndex(Action<int>? checkpoint, CancellationToken token)
    {
        var context = HtmlSelectWorkContext.Create(checkpoint, token);
        context?.Check();
        var result = GetIndexWithWork(context, token);
        HtmlSelectWork.Check(context, token);
        return result;
    }
    internal void SetSelected(bool value, Action<int>? checkpoint, CancellationToken token)
    {
        var context = HtmlSelectWorkContext.Create(checkpoint, token);
        context?.Check();
        SetSelectedWithWork(value, context, token);
        HtmlSelectWork.Check(context, token);
    }
    internal string GetValue(Action<int>? checkpoint, CancellationToken token)
    {
        var context = HtmlSelectWorkContext.Create(checkpoint, token);
        context?.Check();
        var result = GetValueWithWork(context, token);
        HtmlSelectWork.Check(context, token);
        return result;
    }
    internal string GetLabel(Action<int>? checkpoint, CancellationToken token)
    {
        var context = HtmlSelectWorkContext.Create(checkpoint, token);
        context?.Check();
        var result = GetLabelWithWork(context, token);
        HtmlSelectWork.Check(context, token);
        return result;
    }
    internal string GetSemanticLabel(Action<int>? checkpoint, CancellationToken token)
    {
        var context = HtmlSelectWorkContext.Create(checkpoint, token);
        context?.Check();
        var result = GetSemanticLabelWithWork(context, token);
        HtmlSelectWork.Check(context, token);
        return result;
    }
    internal string GetText(Action<int>? checkpoint, CancellationToken token)
    {
        var context = HtmlSelectWorkContext.Create(checkpoint, token);
        context?.Check();
        var result = GetTextWithWork(context, token);
        HtmlSelectWork.Check(context, token);
        return result;
    }
    internal void SetText(string value, Action<int>? checkpoint, CancellationToken token)
    {
        var context = HtmlSelectWorkContext.Create(checkpoint, token);
        context?.Check();
        SetTextWithWork(value, context, token);
        HtmlSelectWork.Check(context, token);
    }
    internal void ParserFinished(Action<int>? checkpoint, CancellationToken token)
    {
        var context = HtmlSelectWorkContext.Create(checkpoint, token);
        context?.Check();
        ParserFinishedWithWork(context, token);
        HtmlSelectWork.Check(context, token);
    }
    internal static Element Create(Document document, string text, string? value, bool defaultSelected, bool selected, Action<int>? checkpoint, CancellationToken token)
    {
        var context = HtmlSelectWorkContext.Create(checkpoint, token);
        context?.Check();
        var result = CreateWithWork(document, text, value, defaultSelected, selected, context, token);
        HtmlSelectWork.Check(context, token);
        return result;
    }
    internal void SetDefaultSelected(bool value, Action<int>? checkpoint, CancellationToken token)
    {
        // The operation context is transient even when only cancellation is supplied.
        var context = new HtmlSelectWorkContext(checkpoint ?? (static _ => { }), token);
        context.Check();
        if (!_core.DirtySelectedness && DefaultSelected != value)
            context.AttributeSelection = CachedNearestSelect is { } select
                ? select.GetSelectCoreWithWork(context, token).PrepareAttributeSelection(_core, value, context, token)
                : new HtmlOptionAttributeSelection(null, _core, value, null);
        Element.SetSelectAttribute("selected", value ? string.Empty : null, context, token);
    }
    internal void SetValue(string value, Action<int>? checkpoint, CancellationToken token)
    {
        ArgumentNullException.ThrowIfNull(value);
        Element.SetSelectAttribute("value", value, HtmlSelectWorkContext.Create(checkpoint, token), token);
    }
    internal void SetLabel(string value, Action<int>? checkpoint, CancellationToken token)
    {
        ArgumentNullException.ThrowIfNull(value);
        Element.SetSelectAttribute("label", value, HtmlSelectWorkContext.Create(checkpoint, token), token);
    }
}
