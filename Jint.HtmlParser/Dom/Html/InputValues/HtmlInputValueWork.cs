namespace Jint.HtmlParser;

// Per-invocation deterministic accounting; no document or engine is retained.
internal struct HtmlInputValueWork(Action<long>? checkpoint, CancellationToken token)
{
    private long _units;

    internal void Step()
    {
        _units++;
        checkpoint?.Invoke(_units);
        if ((_units & 255) == 0) Check();
    }

    internal void Check() => token.ThrowIfCancellationRequested();
}
