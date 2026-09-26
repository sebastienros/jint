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
        if (existing is null && !HtmlCheckableState.IsRadio(element)) return;
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
    // Type, email multiple, and range constraint changes can lose sanitation history,
    // so conservatively materialize those before publication, even without a reader.
    internal static void BeforeAttributeChanged(Element element, string? namespaceUri, string localName,
        string? newValue)
    {
        if (namespaceUri is not null || element is not { NamespaceUri: Namespaces.Html, LocalName: "input" } ||
            element.ExistingInputValueState is not null || localName is not ("type" or "multiple" or "min" or "max" or "step")) return;
        var oldType = element.ExistingCheckedState?.Type ?? HtmlInputTypes.Parse(element.GetAttributeNS(null, "type"));
        if (localName == "type" && oldType != HtmlInputTypes.Parse(newValue) ||
            localName == "multiple" && oldType == HtmlInputType.Email &&
            (element.GetAttributeNodeNS(null, "multiple") is null) != (newValue is null) ||
            oldType == HtmlInputType.Range && localName is "min" or "max" or "step")
            _ = element.GetHtmlState()!.InputValue;
    }

    internal static void AttributeChanged(Element element, string? namespaceUri, string localName,
        string? oldValue, string? newValue)
    {
        if (namespaceUri is not null || element is not { NamespaceUri: Namespaces.Html, LocalName: "input" }) return;
        var state = element.ExistingCheckedState;
        var valueState = element.ExistingInputValueState;
        valueState?.AttributeChanged(localName, oldValue, newValue);
        switch (localName)
        {
            case "checked" when state is not null:
                state.CheckedAttribute = newValue is null ? null : element.GetAttributeNodeNS(null, "checked");
                if ((oldValue is null) != (newValue is null) && !state.DirtyCheckedness)
                    HtmlCheckednessAlgorithms.SetCore(state, newValue is not null, false, default);
                break;
            case "name" when state is not null:
                state.Name = newValue;
                Rekey(state);
                Trigger(state);
                break;
            case "type":
                var nextType = HtmlInputTypes.Parse(newValue);
                // Radio entry needs group history; all other cold flags are still
                // derivable. Existing sidecars survive every type transition.
                if (state is null && nextType == HtmlInputType.Radio) state = HtmlCheckableState.Get(element)!;
                if (valueState is not null)
                {
                    valueState.TypeChanged(nextType, () => SignalType(state, nextType));
                }
                else if (HtmlInputTypes.Parse(oldValue) != nextType)
                {
                    SignalType(state, nextType);
                }
                break;
            case "required" when state is not null:
                HtmlRadioGroupIndex.RequiredChanged(state, newValue is not null);
                break;
        }
    }

    internal static void OwnerChanged(Element element)
    {
        if (!HtmlCheckableState.IsRadio(element)) return;
        var state = HtmlCheckableState.Get(element)!;
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
    private static void SignalType(HtmlInputCheckedState? state, HtmlInputType type)
    {
        if (state is null) return;
        state.Type = type;
        Rekey(state);
        Trigger(state);
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
