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
/// <para><b>Engine isolation.</b> Every row gets its own engine — built by <c>CreateEngine</c>, which
/// rebuilds this instance's <see cref="Size"/>-dependent <c>dense</c>/<c>holey</c> fixture — and warmed
/// with its own script and nothing else (see <see cref="IsolatedScript"/>). It used to be one shared
/// engine per <see cref="Size"/>, warmed with all five row scripts, so each dense row and its holey
/// control were measured on an engine already carrying the others' handler-tree and call-site state —
/// which is how a control row that must not move at all can move because a sibling changed. The rows
/// still measure warm dispatch, and engine construction, the fixture and the warm-up all stay in
/// <c>[GlobalSetup]</c>, outside the measurement. <b>Numbers from this class are not comparable to any
/// published before the harness changed.</b></para>
/// </summary>
[MemoryDiagnoser]
public class ArrayCopyingMethodsBenchmark
{
    private const int OperationsPerInvoke = 100;

    [Params(8, 1024)]
    public int Size { get; set; }

    private IsolatedScript _toReversedDense;
    private IsolatedScript _toReversedHoley;
    private IsolatedScript _withDense;
    private IsolatedScript _withHoley;
    private IsolatedScript _toSortedDense;

    /// <summary>Builds a fresh engine carrying the fixture every row needs, and nothing else.</summary>
    private Engine CreateEngine()
    {
        var engine = new Engine();
        engine.Execute($$"""
            var size = {{Size}};
            var dense = new Array(size);
            for (var i = 0; i < size; i++) dense[i] = i;
            var holey = new Array(size);
            for (var i = 0; i < size; i++) holey[i] = i;
            delete holey[size >> 1];
            """);
        return engine;
    }

    [GlobalSetup]
    public void GlobalSetup()
    {
        _toReversedDense = IsolatedScript.Warm(Prepare("dense.toReversed()"), CreateEngine);
        _toReversedHoley = IsolatedScript.Warm(Prepare("holey.toReversed()"), CreateEngine);
        _withDense = IsolatedScript.Warm(Prepare("dense.with(0, 'X')"), CreateEngine);
        _withHoley = IsolatedScript.Warm(Prepare("holey.with(0, 'X')"), CreateEngine);
        _toSortedDense = IsolatedScript.Warm(Prepare("dense.toSorted()"), CreateEngine);
    }

    private static Prepared<Script> Prepare(string call)
        => Engine.PrepareScript("var r; for (var n = 0; n < " + OperationsPerInvoke + "; n++) { r = " + call + "; } r.length");

    [Benchmark(OperationsPerInvoke = OperationsPerInvoke)]
    public JsValue ToReversed_Dense() => _toReversedDense.Run();

    /// <summary>Control: one hole sends the whole call down the per-element path.</summary>
    [Benchmark(OperationsPerInvoke = OperationsPerInvoke)]
    public JsValue ToReversed_Holey() => _toReversedHoley.Run();

    [Benchmark(OperationsPerInvoke = OperationsPerInvoke)]
    public JsValue With_Dense() => _withDense.Run();

    /// <summary>Control: one hole sends the whole call down the per-element path.</summary>
    [Benchmark(OperationsPerInvoke = OperationsPerInvoke)]
    public JsValue With_Holey() => _withHoley.Run();

    /// <summary>Control: an untouched sibling that also builds a new array from the source.</summary>
    [Benchmark(OperationsPerInvoke = OperationsPerInvoke)]
    public JsValue ToSorted_Dense() => _toSortedDense.Run();
}
