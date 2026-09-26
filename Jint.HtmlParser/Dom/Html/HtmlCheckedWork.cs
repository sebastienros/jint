namespace Jint.HtmlParser;

internal sealed class HtmlCheckedWorkProbe
{
    internal long Units { get; private set; }
    internal int Builds { get; private set; }
    internal Action<Element, Element?, Element?>? OwnerStore { get; set; }
    internal Action<long>? Checkpoint { get; set; }
    internal void Visit() { Units++; Checkpoint?.Invoke(Units); }
    internal void Built() => Builds++;
}

internal struct HtmlCheckedWork
{
    private readonly CancellationToken _token;
    private readonly HtmlCheckedWorkProbe? _probe;
    private int _units;
    internal HtmlCheckedWork(HtmlCheckedWorkProbe? probe, CancellationToken token)
    { _token = token; _probe = probe; _units = 0; }
    internal void Check() => _token.ThrowIfCancellationRequested();
    internal void Step()
    {
        _probe?.Visit();
        if ((++_units & 255) == 0) Check();
    }
}
