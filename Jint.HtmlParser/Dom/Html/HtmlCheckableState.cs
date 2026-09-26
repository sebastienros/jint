namespace Jint.HtmlParser;

internal readonly record struct HtmlRadioGroupFacts(bool Applies, int MemberCount, int CheckedCount, int RequiredCount);

internal static class HtmlCheckableState
{
    internal static HtmlInputCheckedState? Get(Element element)
    {
        ArgumentNullException.ThrowIfNull(element);
        return element is { NamespaceUri: Namespaces.Html, LocalName: "input" }
            ? element.GetHtmlState()!.CheckedState : null;
    }
    internal static HtmlInputCheckedState? Get(Element element, ref HtmlCheckedWork work)
    {
        return element is { NamespaceUri: Namespaces.Html, LocalName: "input" }
            ? element.GetHtmlState()!.GetCheckedState(ref work) : null;
    }
    internal static bool IsRadio(Element element)
    {
        ArgumentNullException.ThrowIfNull(element);
        var work = new HtmlCheckedWork(element.OwnerDocument?.CheckedWorkProbe, default);
        return Type(element, ref work) == HtmlInputType.Radio;
    }
    internal static HtmlInputType? Type(Element element, ref HtmlCheckedWork work)
    {
        ArgumentNullException.ThrowIfNull(element);
        work.Check();
        if (element is not { NamespaceUri: Namespaces.Html, LocalName: "input" }) return null;
        if (element.ExistingCheckedState is { } state) return state.Type;
        for (uint i = 0; element.GetAttributeAt(i) is { } attribute; i++)
        {
            work.Step();
            if (attribute.NamespaceUri is null && attribute.LocalName == "type")
            { work.Check(); return HtmlInputTypes.Parse(attribute.Value); }
        }
        work.Check();
        return HtmlInputType.Text;
    }
    private static bool DefaultChecked(Element element, ref HtmlCheckedWork work)
    {
        if (element.ExistingCheckedState is { } state) return state.DefaultChecked;
        for (uint i = 0; element.GetAttributeAt(i) is { } attribute; i++)
        {
            work.Step();
            if (attribute.NamespaceUri is null && attribute.LocalName == "checked")
            { work.Check(); return true; }
        }
        work.Check();
        return false;
    }

    internal static HtmlRadioGroupFacts GetRadioGroupFacts(Element element, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(element);
        var work = new HtmlCheckedWork(element.OwnerDocument?.CheckedWorkProbe, cancellationToken);
        return Facts(element, ref work);
    }

    private static HtmlRadioGroupFacts Facts(Element element, ref HtmlCheckedWork work)
    {
        work.Check();
        if (Type(element, ref work) != HtmlInputType.Radio) return default;
        var state = Get(element, ref work)!;
        HtmlRadioGroupIndex.Ensure(state, ref work);
        return state.Group is { } group
            ? new(true, group.MemberCount, group.Checked.Count, group.RequiredCount)
            : new(true, 1, state.Checked ? 1 : 0,
                state.RegisteredRequired ? 1 : 0);
    }

    internal static bool SameRadioGroup(Element first, Element second, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(first);
        ArgumentNullException.ThrowIfNull(second);
        var work = new HtmlCheckedWork(first.OwnerDocument?.CheckedWorkProbe, cancellationToken);
        work.Check();
        if (Type(first, ref work) != HtmlInputType.Radio || Type(second, ref work) != HtmlInputType.Radio) return false;
        if (ReferenceEquals(first, second)) return true;
        Facts(first, ref work);
        Facts(second, ref work);
        return Get(first)!.Group is { } group && ReferenceEquals(group, Get(second)!.Group);
    }

    internal static IReadOnlyList<Element> SnapshotRadioGroup(Element element, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(element);
        var work = new HtmlCheckedWork(element.OwnerDocument?.CheckedWorkProbe, cancellationToken);
        var facts = Facts(element, ref work);
        return Snapshot(element, facts, ref work);
    }

    private static IReadOnlyList<Element> Snapshot(Element element, HtmlRadioGroupFacts facts, ref HtmlCheckedWork work)
    {
        if (!facts.Applies) return Array.Empty<Element>();
        var state = Get(element)!;
        if (state.Group is null) return Array.AsReadOnly(new[] { element });
        var result = new List<Element>(facts.MemberCount);
        var root = state.Index!.Root;
        for (Node? current = root; current is not null; current = HtmlRadioGroupIndex.Next(current, root, ref work))
        {
            work.Step();
            if (current is Element candidate && candidate.ExistingCheckedState?.Group == state.Group) result.Add(candidate);
        }
        work.Check();
        return result.AsReadOnly();
    }

    internal static Element? FirstCheckedRadio(Element element, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(element);
        var work = new HtmlCheckedWork(element.OwnerDocument?.CheckedWorkProbe, cancellationToken);
        var facts = Facts(element, ref work);
        if (!facts.Applies || facts.CheckedCount == 0) return null;
        var state = Get(element)!;
        if (state.Group is null) return element;
        if (facts.CheckedCount == 1) return state.Group.Checked[0].Element;
        var root = state.Index!.Root;
        for (Node? current = root; current is not null; current = HtmlRadioGroupIndex.Next(current, root, ref work))
        {
            work.Step();
            if (current is Element { ExistingCheckedState: { Checked: true } peer } candidate && peer.Group == state.Group)
            { work.Check(); return candidate; }
        }
        work.Check();
        return null;
    }

    internal static bool MatchesChecked(Element element)
    {
        ArgumentNullException.ThrowIfNull(element);
        var work = new HtmlCheckedWork(element.OwnerDocument?.CheckedWorkProbe, default);
        if (Type(element, ref work) is not (HtmlInputType.Checkbox or HtmlInputType.Radio)) return false;
        return element.ExistingCheckedState?.Checked ?? DefaultChecked(element, ref work);
    }
    internal static bool MatchesUnchecked(Element element, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(element);
        var work = new HtmlCheckedWork(element.OwnerDocument?.CheckedWorkProbe, cancellationToken);
        var type = Type(element, ref work);
        var state = element.ExistingCheckedState;
        var matches = type switch
        {
            HtmlInputType.Checkbox => state is not null ? !state.Checked && !state.Indeterminate
                : !DefaultChecked(element, ref work),
            HtmlInputType.Radio => !Get(element, ref work)!.Checked && Facts(element, ref work).CheckedCount > 0,
            _ => false
        };
        work.Check();
        return matches;
    }
    internal static bool MatchesIndeterminate(Element element, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(element);
        var work = new HtmlCheckedWork(element.OwnerDocument?.CheckedWorkProbe, cancellationToken);
        var matches = Type(element, ref work) switch
        {
            HtmlInputType.Checkbox => element.ExistingCheckedState?.Indeterminate ?? false,
            HtmlInputType.Radio => Facts(element, ref work).CheckedCount == 0,
            _ => false
        };
        work.Check();
        return matches;
    }
    internal static bool MatchesDefaultCheckable(Element element)
    {
        ArgumentNullException.ThrowIfNull(element);
        var work = new HtmlCheckedWork(element.OwnerDocument?.CheckedWorkProbe, default);
        return Type(element, ref work) is HtmlInputType.Checkbox or HtmlInputType.Radio && DefaultChecked(element, ref work);
    }
}
