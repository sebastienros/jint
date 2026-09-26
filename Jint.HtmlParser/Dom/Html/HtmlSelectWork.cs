namespace Jint.HtmlParser;

internal sealed class HtmlSelectWorkProbe
{
    internal long Units { get; private set; }
    internal Action<long>? Checkpoint { get; set; }
    internal void Visit() { Units++; Checkpoint?.Invoke(Units); }
}

internal struct HtmlSelectWork(HtmlSelectWorkProbe? probe, CancellationToken token)
{
    private int _units;
    internal void Step() { probe?.Visit(); if ((++_units & 255) == 0) Check(); }
    internal void Check() => token.ThrowIfCancellationRequested();
    internal static bool StringEquals(string? left, string right, ref HtmlSelectWork work)
    {
        if (left is null || left.Length != right.Length) return false;
        for (var i = 0; i < left.Length; i++) { work.Step(); if (left[i] != right[i]) return false; }
        work.Check();
        return true;
    }
}
