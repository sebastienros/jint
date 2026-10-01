using System.Runtime.CompilerServices;

namespace Jint.HtmlParser;

internal enum HtmlDisabledState
{
    Inapplicable,
    Enabled,
    Disabled
}

// One cadence covers every walk made by a disabledness or select-ancestry query.
internal struct HtmlDisabledWork(CancellationToken cancellationToken, Action<int>? checkpoint = null, int initialSteps = 0, HtmlSelectWorkContext? selectContext = null)
{
    private int _steps = initialSteps;
    internal int Steps => _steps;

    // Selector matching charges a step per character and per node, so the common case must inline
    // down to an increment and a mask; the select context and the cadence callback stay out of line.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void Step()
    {
        if (selectContext is not null) StepWithSelectContext();
        else if ((++_steps & 255) == 0) Poll();
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private void StepWithSelectContext()
    {
        selectContext!.Step();
        if ((++_steps & 255) == 0) Poll();
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private readonly void Poll()
    {
        checkpoint?.Invoke(_steps);
        cancellationToken.ThrowIfCancellationRequested();
    }

    internal void Check() => cancellationToken.ThrowIfCancellationRequested();
}
