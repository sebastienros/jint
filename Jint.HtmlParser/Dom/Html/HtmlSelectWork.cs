namespace Jint.HtmlParser;

internal sealed class HtmlSelectWorkProbe
{
    internal long Units { get; private set; }
    internal Action<long>? Checkpoint { get; set; }
    internal void Visit() { Units++; Checkpoint?.Invoke(Units); }
}

/// <summary>One invocation's work counter. Never retained by an element or document.</summary>
/// <remarks>Checkpoints run every 256 work units and at preparation boundaries;
/// boundary checks can repeat a count. The caller checks constraints without running script.</remarks>
internal sealed class HtmlSelectWorkContext(Action<int> checkpoint, CancellationToken token)
{
    private int _units;
    internal CancellationToken Token => token;
    internal HtmlOptionAttributeSelection? AttributeSelection { get; set; }
    internal static HtmlSelectWorkContext? Create(Action<int>? checkpoint, CancellationToken token)
        => checkpoint is null ? null : new HtmlSelectWorkContext(checkpoint, token);
    internal void Step()
    {
        if (_units != int.MaxValue) _units++;
        if ((_units & 255) == 0 || _units == int.MaxValue) Check();
    }
    // Each adapter receives one producer invocation's cumulative work counter.
    // Preparation boundaries can repeat a count without completing more work.
    internal Action<int> CreateCheckpointAdapter()
    {
        var previous = 0;
        return units =>
        {
            var delta = units > previous ? units - previous : 0;
            previous = units;
            var before = _units;
            if (delta > 0) _units = delta > int.MaxValue - _units ? int.MaxValue : _units + delta;
            if (delta == 0 || _units == int.MaxValue || (_units >> 8) != (before >> 8)) Check();
        };
    }
    internal void Check()
    {
        token.ThrowIfCancellationRequested();
        checkpoint(_units);
        token.ThrowIfCancellationRequested();
    }
}

internal struct HtmlSelectWork(HtmlSelectWorkProbe? probe, HtmlSelectWorkContext? context, CancellationToken token)
{
    internal HtmlSelectWork(HtmlSelectWorkProbe? probe, CancellationToken token) : this(probe, null, token) { }
    private int _units;
    internal void Step() { probe?.Visit(); context?.Step(); if ((++_units & 255) == 0) Check(); }
    internal void Check() => Check(context, token);
    internal static void Check(HtmlSelectWorkContext? context, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        context?.Check();
    }
    internal static bool StringEquals(string? left, string right, ref HtmlSelectWork work)
    {
        if (left is null || left.Length != right.Length) return false;
        for (var i = 0; i < left.Length; i++) { work.Step(); if (left[i] != right[i]) return false; }
        work.Check();
        return true;
    }
}
