namespace Jint.HtmlParser;

// Per-invocation deterministic accounting; no document or engine is retained.
internal struct HtmlInputValueWork(Action<long>? checkpoint, CancellationToken token, long initialUnits = 0,
    bool cadencedCheckpoint = false)
{
    private long _units = initialUnits;
    private Action<int>? _nativeCheckpoint;
    internal long Units => _units;
    internal CancellationToken Token => token;
    internal Action<int>? NativeCheckpoint => _nativeCheckpoint;
    internal void ContinueFrom(long units) => _units = units;
    internal static HtmlInputValueWork ForNative(Action<int>? checkpoint, CancellationToken token, long initialUnits = 0)
        => new(null, token, initialUnits, cadencedCheckpoint: true) { _nativeCheckpoint = checkpoint };

    internal void Step()
    {
        _units++;
        if (!cadencedCheckpoint || (_units & 255) == 0) checkpoint?.Invoke(_units);
        if ((_units & 255) == 0) { _nativeCheckpoint?.Invoke(unchecked((int) _units)); Check(); }
    }

    internal void Check() => token.ThrowIfCancellationRequested();
    internal void Finish()
    {
        if ((_units & 255) != 0) _nativeCheckpoint?.Invoke(unchecked((int) _units));
        Check();
    }
    internal bool StringEquals(string left, string right)
    {
        Step();
        if (ReferenceEquals(left, right)) return true;
        if (left.Length != right.Length) return false;
        for (var i = 0; i < left.Length; i++) { Step(); if (left[i] != right[i]) return false; }
        return true;
    }
}
