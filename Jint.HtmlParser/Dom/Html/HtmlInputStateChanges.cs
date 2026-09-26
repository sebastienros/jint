namespace Jint.HtmlParser;

/// <summary>One native input mutation route, extended by later value-state families.</summary>
internal static class HtmlInputStateChanges
{
    internal static HtmlInputValueState? PrepareInitialization(Element element, IReadOnlyList<Attr> attributes,
        CancellationToken cancellationToken)
        => element.ExistingInputValueState is not null
            ? new HtmlInputValueState(element, attributes, cancellationToken) : null;

    internal static void Initialize(Element element, HtmlInputValueState? prepared = null)
    {
        if (element.ExistingInputValueState is { } value)
        {
            if (prepared is not null) value.InitializeFrom(prepared);
            else value.InitializeMetadata(default);
        }
        var existing = element.ExistingCheckedState;
        if (HtmlCheckableState.Get(element) is not { } state) return;
        // A fresh component's constructor already saw the complete batch. An
        // existing read-only view must refresh once after publication too.
        if (existing is not null)
        {
            var work = new HtmlCheckedWork(element.OwnerDocument?.CheckedWorkProbe, default);
            state.RefreshMetadata(ref work);
        }
        if (state.DefaultChecked) HtmlCheckednessAlgorithms.SetCore(state, true, false, default);
    }

    // Initial/default reflection is reconstructible from the current attributes.
    // A real type transition or email multiple toggle can lose sanitation history,
    // so conservatively materialize those before publication, even without a reader.
    internal static void BeforeAttributeChanged(Element element, string? namespaceUri, string localName,
        string? newValue)
    {
        if (namespaceUri is not null || element is not { NamespaceUri: Namespaces.Html, LocalName: "input" } ||
            element.ExistingInputValueState is not null || localName is not ("type" or "multiple")) return;
        var oldType = element.ExistingCheckedState?.Type ?? HtmlInputTypes.Parse(element.GetAttributeNS(null, "type"));
        if (localName == "type" && oldType != HtmlInputTypes.Parse(newValue) ||
            localName == "multiple" && oldType == HtmlInputType.Email &&
            (element.GetAttributeNodeNS(null, "multiple") is null) != (newValue is null))
            _ = element.GetHtmlState()!.InputValue;
    }

    internal static void AttributeChanged(Element element, string? namespaceUri, string localName,
        string? oldValue, string? newValue)
    {
        if (namespaceUri is not null || element is not { NamespaceUri: Namespaces.Html, LocalName: "input" }) return;
        var state = HtmlCheckableState.Get(element)!;
        var valueState = element.ExistingInputValueState;
        valueState?.AttributeChanged(localName, oldValue, newValue);
        switch (localName)
        {
            case "checked":
                state.CheckedAttribute = newValue is null ? null : element.GetAttributeNodeNS(null, "checked");
                if ((oldValue is null) != (newValue is null) && !state.DirtyCheckedness)
                    HtmlCheckednessAlgorithms.SetCore(state, newValue is not null, false, default);
                break;
            case "name":
                state.Name = newValue;
                Rekey(state);
                Trigger(state);
                break;
            case "type":
                var nextType = HtmlInputTypes.Parse(newValue);
                if (valueState is not null)
                {
                    valueState.TypeChanged(nextType, () =>
                    {
                        state.Type = nextType;
                        Rekey(state);
                        Trigger(state);
                    });
                }
                else if (HtmlInputTypes.Parse(oldValue) != nextType)
                {
                    state.Type = nextType;
                    Rekey(state);
                    Trigger(state);
                }
                break;
            case "required":
                HtmlRadioGroupIndex.RequiredChanged(state, newValue is not null);
                break;
        }
    }

    internal static void OwnerChanged(Element element)
    {
        if (HtmlCheckableState.Get(element) is not { } state) return;
        Rekey(state);
        Trigger(state);
    }
    internal static void BeforeRemoval(Element element, Node removalRoot)
    {
        var state = element.ExistingCheckedState;
        // A host disconnect does not change any shadow descendant's ordinary root.
        if (state?.Index is { } index && ReferenceEquals(index.Root, removalRoot)) index.Remove(state);
    }
    internal static void Inserted(Element element)
    {
        if (!HtmlCheckableState.IsRadio(element)) return;
        var state = HtmlCheckableState.Get(element)!;
        Rekey(state);
        if (ShadowTree.IsConnected(element, default)) Trigger(state);
    }
    private static void Trigger(HtmlInputCheckedState state)
    {
        if (state.Checked && state.Type == HtmlInputType.Radio)
            HtmlCheckednessAlgorithms.SetCore(state, true, state.DirtyCheckedness, default);
    }
    private static void Rekey(HtmlInputCheckedState state)
    {
        var old = state.Index;
        old?.Remove(state);
        if (!HtmlCheckableState.IsRadio(state.Element)) return;
        var work = new HtmlCheckedWork(state.Element.OwnerDocument?.CheckedWorkProbe, default);
        Node root = state.Element;
        while (root.ParentNode is { } parent) { work.Step(); root = parent; }
        if (root.RadioIndex is { } index)
        {
            index.Add(state, ref work);
        }
    }
}
