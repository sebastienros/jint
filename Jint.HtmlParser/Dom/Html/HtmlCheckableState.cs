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
        => Get(element)?.Type == HtmlInputType.Radio;
    private static HtmlInputType? Type(Element element)
    { ArgumentNullException.ThrowIfNull(element); return Get(element)?.Type; }

    internal static HtmlRadioGroupFacts GetRadioGroupFacts(Element element, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(element);
        var work = new HtmlCheckedWork(element.OwnerDocument?.CheckedWorkProbe, cancellationToken);
        return Facts(element, ref work);
    }

    private static HtmlRadioGroupFacts Facts(Element element, ref HtmlCheckedWork work)
    {
        work.Check();
        var state = Get(element, ref work);
        if (state?.Type != HtmlInputType.Radio) return default;
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
        if (Get(first, ref work)?.Type != HtmlInputType.Radio || Get(second, ref work)?.Type != HtmlInputType.Radio) return false;
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
        => Type(element) is HtmlInputType.Checkbox or HtmlInputType.Radio && Get(element)!.Checked;
    internal static bool MatchesUnchecked(Element element, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(element);
        var work = new HtmlCheckedWork(element.OwnerDocument?.CheckedWorkProbe, cancellationToken);
        work.Check();
        var state = Get(element, ref work);
        var matches = state?.Type switch
        {
            HtmlInputType.Checkbox => !state.Checked && !state.Indeterminate,
            HtmlInputType.Radio => !state.Checked && Facts(element, ref work).CheckedCount > 0,
            _ => false
        };
        work.Check();
        return matches;
    }
    internal static bool MatchesIndeterminate(Element element, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(element);
        var work = new HtmlCheckedWork(element.OwnerDocument?.CheckedWorkProbe, cancellationToken);
        work.Check();
        var state = Get(element, ref work);
        var matches = state?.Type switch
        {
            HtmlInputType.Checkbox => state.Indeterminate,
            HtmlInputType.Radio => Facts(element, ref work).CheckedCount == 0,
            _ => false
        };
        work.Check();
        return matches;
    }
    internal static bool MatchesDefaultCheckable(Element element)
        => Type(element) is HtmlInputType.Checkbox or HtmlInputType.Radio && Get(element)!.DefaultChecked;
}
