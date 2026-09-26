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
    private readonly Action<int>? _checkpoint;
    private int _units;
    internal HtmlCheckedWork(HtmlCheckedWorkProbe? probe, CancellationToken token)
        : this(probe, null, token) { }
    internal HtmlCheckedWork(HtmlCheckedWorkProbe? probe, Action<int>? checkpoint, CancellationToken token)
    { _token = token; _probe = probe; _checkpoint = checkpoint; _units = 0; }
    internal void Check() => _token.ThrowIfCancellationRequested();
    // Publication boundaries also check allocation work performed since the last source visit.
    internal void Finish()
    { _checkpoint?.Invoke(_units); Check(); }
    internal void Step()
    {
        _probe?.Visit();
        if ((++_units & 255) == 0)
        { _checkpoint?.Invoke(_units); Check(); }
    }
}
