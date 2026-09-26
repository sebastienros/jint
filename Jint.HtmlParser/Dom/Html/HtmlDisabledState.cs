namespace Jint.HtmlParser;

internal enum HtmlDisabledState
{
    Inapplicable,
    Enabled,
    Disabled
}

// One cadence covers every walk made by a disabledness or select-ancestry query.
internal struct HtmlDisabledWork(CancellationToken cancellationToken, Action<int>? checkpoint = null, int initialSteps = 0)
{
    private int _steps = initialSteps;
    internal int Steps => _steps;

    internal void Step()
    {
        if ((++_steps & 255) == 0)
        {
            checkpoint?.Invoke(_steps);
            cancellationToken.ThrowIfCancellationRequested();
        }
    }

    internal void Check() => cancellationToken.ThrowIfCancellationRequested();
}
