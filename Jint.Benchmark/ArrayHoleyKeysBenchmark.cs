using BenchmarkDotNet.Attributes;
using Jint.Native;

namespace Jint.Benchmark;

/// <summary>
/// Demonstrates that enumerating an array scales with its logical length rather than the number of
/// populated elements.
/// <para>
/// Ordinary <c>a[index] = value</c> assignment keeps the backing store dense for any index below the
/// dense-array threshold (10M): <c>SetIndexValue</c> bumps the length to <c>index + 1</c> via
/// <c>EnsureCorrectLength</c> before the "looks sparse" heuristic in <c>WriteArrayValueUnlikely</c>
/// runs, so that heuristic (<c>index &lt; denseHeadroom + 50</c>) never fires for plain assignment.
/// A holey array (few elements spread over a large length) therefore keeps a backing store
/// proportional to its max index, and <c>Object.keys</c> allocates a <c>List&lt;JsValue&gt;</c> of
/// that size and scans every slot, even though only <see cref="ElementCount"/> keys are produced.
/// </para>
/// <para>
/// Every case produces the SAME number of keys (<see cref="ElementCount"/>); only the gap between
/// indices — and hence the logical length — changes. Time and Allocated would ideally be flat, but
/// instead grow linearly with <see cref="Gap"/>.
/// </para>
/// </summary>
[MemoryDiagnoser]
public class ArrayHoleyKeysBenchmark
{
    private const int ElementCount = 500;

    /// <summary>
    /// Gap between populated indices; the logical length is <c>ElementCount * Gap</c>. <c>Gap == 1</c>
    /// is a packed array (the baseline); larger gaps are increasingly holey.
    /// </summary>
    [Params(1, 100, 1000)]
    public int Gap { get; set; }

    private Engine _engine = null!;
    private Prepared<Script> _keys;

    [GlobalSetup]
    public void Setup()
    {
        _engine = new Engine();
        _engine.Evaluate($"var arr = []; for (var i = 0; i < {ElementCount}; i++) arr[i * {Gap}] = i;");
        _keys = Engine.PrepareScript("Object.keys(arr);");
    }

    [Benchmark]
    public JsValue Keys() => _engine.Evaluate(_keys);
}
