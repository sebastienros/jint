using BenchmarkDotNet.Attributes;
using Jint.Native;

namespace Jint.Benchmark;

/// <summary>
/// <c>Array.prototype.toReversed</c> and <c>with</c> build a brand-new array by reading the source once per
/// index. A hole-free dense source admits a bulk copy instead, which is what these rows size.
/// <para>
/// Each method has a <c>Dense</c> row and a <c>Holey</c> control on an otherwise identical source: a single
/// hole makes the read a full <c>[[Get]]</c> that can reach the prototype chain, so the bulk copy declines
/// and the control row should not move at all. <c>ToSorted_Dense</c> is a second control - a sibling that
/// builds a new array the same way and is untouched here.
/// </para>
/// <para>
/// <c>Size</c> separates the two things the lane changes: at 8 elements the per-call overhead dominates and
/// a win means the dispatch shrank, at 1024 the per-element cost does and a win means the copy did. The
/// allocation is the same either way - both paths allocate exactly one backing array - so <c>Allocated</c>
/// is a control column here, not a result.
/// </para>
/// </summary>
[MemoryDiagnoser]
public class ArrayCopyingMethodsBenchmark
{
    private const int OperationsPerInvoke = 100;

    [Params(8, 1024)]
    public int Size { get; set; }

    private Engine _engine = null!;

    private Prepared<Script> _toReversedDense;
    private Prepared<Script> _toReversedHoley;
    private Prepared<Script> _withDense;
    private Prepared<Script> _withHoley;
    private Prepared<Script> _toSortedDense;

    [GlobalSetup]
    public void GlobalSetup()
    {
        _engine = new Engine();
        _engine.Execute($$"""
            var size = {{Size}};
            var dense = new Array(size);
            for (var i = 0; i < size; i++) dense[i] = i;
            var holey = new Array(size);
            for (var i = 0; i < size; i++) holey[i] = i;
            delete holey[size >> 1];
            """);

        _toReversedDense = Prepare("dense.toReversed()");
        _toReversedHoley = Prepare("holey.toReversed()");
        _withDense = Prepare("dense.with(0, 'X')");
        _withHoley = Prepare("holey.with(0, 'X')");
        _toSortedDense = Prepare("dense.toSorted()");

        _engine.Evaluate(_toReversedDense);
        _engine.Evaluate(_toReversedHoley);
        _engine.Evaluate(_withDense);
        _engine.Evaluate(_withHoley);
        _engine.Evaluate(_toSortedDense);
    }

    private static Prepared<Script> Prepare(string call)
        => Engine.PrepareScript("var r; for (var n = 0; n < " + OperationsPerInvoke + "; n++) { r = " + call + "; } r.length");

    [Benchmark(OperationsPerInvoke = OperationsPerInvoke)]
    public JsValue ToReversed_Dense() => _engine.Evaluate(_toReversedDense);

    /// <summary>Control: one hole sends the whole call down the per-element path.</summary>
    [Benchmark(OperationsPerInvoke = OperationsPerInvoke)]
    public JsValue ToReversed_Holey() => _engine.Evaluate(_toReversedHoley);

    [Benchmark(OperationsPerInvoke = OperationsPerInvoke)]
    public JsValue With_Dense() => _engine.Evaluate(_withDense);

    /// <summary>Control: one hole sends the whole call down the per-element path.</summary>
    [Benchmark(OperationsPerInvoke = OperationsPerInvoke)]
    public JsValue With_Holey() => _engine.Evaluate(_withHoley);

    /// <summary>Control: an untouched sibling that also builds a new array from the source.</summary>
    [Benchmark(OperationsPerInvoke = OperationsPerInvoke)]
    public JsValue ToSorted_Dense() => _engine.Evaluate(_toSortedDense);
}
