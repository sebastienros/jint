namespace Jint.HtmlParser;

// Per-invocation deterministic accounting; no document or engine is retained.
internal struct HtmlInputValueWork(Action<long>? checkpoint, CancellationToken token, long initialUnits = 0,
    bool cadencedCheckpoint = false)
{
    private long _units = initialUnits;
    internal long Units => _units;
    internal CancellationToken Token => token;

    internal void Step()
    {
        _units++;
        if (!cadencedCheckpoint || (_units & 255) == 0) checkpoint?.Invoke(_units);
        if ((_units & 255) == 0) Check();
    }

    internal void Check() => token.ThrowIfCancellationRequested();
}
