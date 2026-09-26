namespace Jint.HtmlParser;

internal enum HtmlCheckedChangeOrigin { Algorithm, UserInteraction }

/// <summary>Event-free HTML checkedness transitions; peer exclusion never dirties peers.</summary>
internal static class HtmlCheckednessAlgorithms
{
    internal static void Set(Element input, bool value, HtmlCheckedChangeOrigin origin,
        CancellationToken cancellationToken)
        => Set(input, value, origin, null, cancellationToken);
    internal static void Set(Element input, bool value, HtmlCheckedChangeOrigin origin,
        Action<int>? checkpoint, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(input);
        var work = new HtmlCheckedWork(input.OwnerDocument?.CheckedWorkProbe, checkpoint, cancellationToken);
        work.Check();
        var state = HtmlCheckableState.Get(input, ref work) ?? throw new ArgumentException("An HTML input is required.", nameof(input));
        SetCore(state, value, state.DirtyCheckedness ||
            origin == HtmlCheckedChangeOrigin.UserInteraction && state.Checked != value, ref work);
    }

    internal static void SetCore(HtmlInputCheckedState state, bool value, bool dirty,
        CancellationToken cancellationToken)
    {
        SetCore(state, value, dirty, null, cancellationToken);
    }
    internal static void SetCore(HtmlInputCheckedState state, bool value, bool dirty,
        Action<int>? checkpoint, CancellationToken cancellationToken)
    {
        var work = new HtmlCheckedWork(state.Element.OwnerDocument?.CheckedWorkProbe, checkpoint, cancellationToken);
        SetCore(state, value, dirty, ref work);
    }

    private static void SetCore(HtmlInputCheckedState state, bool value, bool dirty, ref HtmlCheckedWork work)
    {
        work.Check();
        HtmlInputCheckedState[] peers = [];
        if (value && state.Type == HtmlInputType.Radio)
        {
            HtmlRadioGroupIndex.Ensure(state, ref work);
            if (state.Group is { } group)
            {
                var staged = new List<HtmlInputCheckedState>(group.Checked.Count);
                foreach (var peer in group.Checked)
                {
                    work.Step();
                    if (!ReferenceEquals(peer, state)) staged.Add(peer);
                }
                peers = staged.ToArray();
                group.Checked.EnsureCapacity(group.Checked.Count + 1);
            }
        }
        work.Finish();
        // Everything which can observe cancellation or allocate precedes flag commit.
        foreach (var peer in peers) peer.Write(false, peer.DirtyCheckedness);
        state.Write(value, dirty);
    }

    internal static void ResetCheckedness(Element input, CancellationToken cancellationToken)
        => ResetCheckedness(input, null, cancellationToken);
    internal static void ResetCheckedness(Element input, Action<int>? checkpoint, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(input);
        var work = new HtmlCheckedWork(input.OwnerDocument?.CheckedWorkProbe, checkpoint, cancellationToken);
        work.Check();
        var state = HtmlCheckableState.Get(input, ref work) ?? throw new ArgumentException("An HTML input is required.", nameof(input));
        SetCore(state, state.DefaultChecked, dirty: false, ref work);
    }

    internal static void CopyCheckedness(Element source, Element copy, CancellationToken cancellationToken = default)
        => CopyCheckedness(source, copy, null, cancellationToken);
    internal static void CopyCheckedness(Element source, Element copy, Action<int>? checkpoint, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(copy);
        var work = new HtmlCheckedWork(source.OwnerDocument?.CheckedWorkProbe, checkpoint, cancellationToken);
        work.Check();
        var state = source.ExistingCheckedState;
        if (state is null) { work.Finish(); return; }
        var target = HtmlCheckableState.Get(copy, ref work)
            ?? throw new ArgumentException("An HTML input copy is required.", nameof(copy));
        work.Finish();
        target.CopyFrom(state);
    }
}
