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
    internal static HtmlInputCheckedState? Get(Element element, Action<int>? checkpoint, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(element);
        var work = new HtmlCheckedWork(element.OwnerDocument?.CheckedWorkProbe, checkpoint, cancellationToken);
        var wasWarm = element.ExistingCheckedState is not null;
        var result = Get(element, ref work);
        if (wasWarm || result is null) work.Finish();
        return result;
    }
    internal static HtmlInputCheckedState? Get(Element element, ref HtmlCheckedWork work)
    {
        return element is { NamespaceUri: Namespaces.Html, LocalName: "input" }
            ? element.GetHtmlState()!.GetCheckedState(ref work) : null;
    }
    internal static bool IsRadio(Element element)
        => IsRadio(element, null, default);
    internal static bool IsRadio(Element element, Action<int>? checkpoint, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(element);
        var work = new HtmlCheckedWork(element.OwnerDocument?.CheckedWorkProbe, checkpoint, cancellationToken);
        var result = IsRadioCore(element, ref work);
        work.Finish();
        return result;
    }
    private static bool IsRadioCore(Element element, ref HtmlCheckedWork work)
    {
        ArgumentNullException.ThrowIfNull(element);
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
        => GetRadioGroupFacts(element, null, cancellationToken);
    internal static HtmlRadioGroupFacts GetRadioGroupFacts(Element element, Action<int>? checkpoint, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(element);
        var work = new HtmlCheckedWork(element.OwnerDocument?.CheckedWorkProbe, checkpoint, cancellationToken);
        var result = GetRadioGroupFactsCore(element, ref work);
        work.Finish();
        return result;
    }
    private static HtmlRadioGroupFacts GetRadioGroupFactsCore(Element element, ref HtmlCheckedWork work)
    {
        ArgumentNullException.ThrowIfNull(element);
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
        => SameRadioGroup(first, second, null, cancellationToken);
    internal static bool SameRadioGroup(Element first, Element second, Action<int>? checkpoint, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(first);
        var work = new HtmlCheckedWork(first.OwnerDocument?.CheckedWorkProbe, checkpoint, cancellationToken);
        var result = SameRadioGroupCore(first, second, ref work);
        work.Finish();
        return result;
    }
    private static bool SameRadioGroupCore(Element first, Element second, ref HtmlCheckedWork work)
    {
        ArgumentNullException.ThrowIfNull(first);
        ArgumentNullException.ThrowIfNull(second);
        work.Check();
        if (Type(first, ref work) != HtmlInputType.Radio || Type(second, ref work) != HtmlInputType.Radio) return false;
        if (ReferenceEquals(first, second)) return true;
        Facts(first, ref work);
        Facts(second, ref work);
        return Get(first)!.Group is { } group && ReferenceEquals(group, Get(second)!.Group);
    }

    internal static IReadOnlyList<Element> SnapshotRadioGroup(Element element, CancellationToken cancellationToken)
        => SnapshotRadioGroup(element, null, cancellationToken);
    internal static IReadOnlyList<Element> SnapshotRadioGroup(Element element, Action<int>? checkpoint, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(element);
        var work = new HtmlCheckedWork(element.OwnerDocument?.CheckedWorkProbe, checkpoint, cancellationToken);
        var result = SnapshotRadioGroupCore(element, ref work);
        work.Finish();
        return result;
    }
    private static IReadOnlyList<Element> SnapshotRadioGroupCore(Element element, ref HtmlCheckedWork work)
    {
        ArgumentNullException.ThrowIfNull(element);
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
        => FirstCheckedRadio(element, null, cancellationToken);
    internal static Element? FirstCheckedRadio(Element element, Action<int>? checkpoint, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(element);
        var work = new HtmlCheckedWork(element.OwnerDocument?.CheckedWorkProbe, checkpoint, cancellationToken);
        var result = FirstCheckedRadioCore(element, ref work);
        work.Finish();
        return result;
    }
    private static Element? FirstCheckedRadioCore(Element element, ref HtmlCheckedWork work)
    {
        ArgumentNullException.ThrowIfNull(element);
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
        => MatchesChecked(element, null, default);
    internal static bool MatchesChecked(Element element, Action<int>? checkpoint, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(element);
        var work = new HtmlCheckedWork(element.OwnerDocument?.CheckedWorkProbe, checkpoint, cancellationToken);
        var result = MatchesCheckedCore(element, ref work);
        work.Finish();
        return result;
    }
    private static bool MatchesCheckedCore(Element element, ref HtmlCheckedWork work)
    {
        ArgumentNullException.ThrowIfNull(element);
        if (Type(element, ref work) is not (HtmlInputType.Checkbox or HtmlInputType.Radio)) return false;
        return element.ExistingCheckedState?.Checked ?? DefaultChecked(element, ref work);
    }
    internal static bool MatchesUnchecked(Element element, CancellationToken cancellationToken)
        => MatchesUnchecked(element, null, cancellationToken);
    internal static bool MatchesUnchecked(Element element, Action<int>? checkpoint, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(element);
        var work = new HtmlCheckedWork(element.OwnerDocument?.CheckedWorkProbe, checkpoint, cancellationToken);
        var result = MatchesUncheckedCore(element, ref work);
        work.Finish();
        return result;
    }
    private static bool MatchesUncheckedCore(Element element, ref HtmlCheckedWork work)
    {
        ArgumentNullException.ThrowIfNull(element);
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
        => MatchesIndeterminate(element, null, cancellationToken);
    internal static bool MatchesIndeterminate(Element element, Action<int>? checkpoint, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(element);
        var work = new HtmlCheckedWork(element.OwnerDocument?.CheckedWorkProbe, checkpoint, cancellationToken);
        var result = MatchesIndeterminateCore(element, ref work);
        work.Finish();
        return result;
    }
    private static bool MatchesIndeterminateCore(Element element, ref HtmlCheckedWork work)
    {
        ArgumentNullException.ThrowIfNull(element);
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
        => MatchesDefaultCheckable(element, null, default);
    internal static bool MatchesDefaultCheckable(Element element, Action<int>? checkpoint, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(element);
        var work = new HtmlCheckedWork(element.OwnerDocument?.CheckedWorkProbe, checkpoint, cancellationToken);
        var result = MatchesDefaultCheckableCore(element, ref work);
        work.Finish();
        return result;
    }
    private static bool MatchesDefaultCheckableCore(Element element, ref HtmlCheckedWork work)
    {
        ArgumentNullException.ThrowIfNull(element);
        return Type(element, ref work) is HtmlInputType.Checkbox or HtmlInputType.Radio && DefaultChecked(element, ref work);
    }
}
