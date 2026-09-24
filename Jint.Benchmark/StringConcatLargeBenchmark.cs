using BenchmarkDotNet.Attributes;
using Jint.Native;

namespace Jint.Benchmark;

/// <summary>
/// String building from LARGE pieces — the workload where a rope representation would beat the
/// current copy-into-builder strategy. Small-chunk appends (the common `s += c` loop) are the
/// existing ConcatenatedString sweet spot and serve as the baseline; the large-chunk lanes append
/// 4 KB pieces, concatenate two 64 KB strings, and scan the built result with charAt afterwards
/// (the read pattern a lazy/rope representation would have to pay for).
/// <para>
/// The Chain* lanes cover multi-operand <c>a + b + c</c> expressions, which are evaluated as one
/// flattened chain rather than a nested tree of pairwise additions. ChainSmallThree/ChainSmallSix
/// guard the short-string case (nothing meaningful to save, so they must not get slower),
/// ChainLargeThree shows the win when the skipped intermediate is large, and ChainNumericThree
/// guards the numeric fast lanes that must survive on chains that never become strings.
/// </para>
/// <para>
/// The Assign* lanes are the shape the rest of the class was missing, and the reason a rope evaluated
/// against it in July 2026 came out as a NO-GO: every accumulating lane above builds with
/// <c>s += chunk</c>, which is already amortised linear, and ConcatLargePair rebuilds a <em>fresh</em>
/// pair each iteration so its left operand never grows. Accumulating through the <c>+</c> operator —
/// <c>s = s + x</c>, <c>s = x + s</c>, <c>s = s + x + y</c> — is a different code path, and was
/// quadratic (<a href="https://github.com/sebastienros/jint/issues/3350">#3350</a>).
/// </para>
/// <para>
/// Two pairings carry the reading. AssignAppendSmallChunks against <b>AppendSmallChunks</b> is the same
/// 4,096 x 16 characters with one operator changed, so the difference between them is the whole cost of
/// the <c>+</c> path. AssignAppendThenScan against <b>AssignAppendSmallChunks</b> is that same build
/// plus a charAt scan of the result, so their difference is what reading the value back as text costs —
/// the lane a deferred representation cannot cheat, since the other three finish on <c>s.length</c>,
/// which such a value answers without ever producing the characters.
/// </para>
/// <para><b>Engine isolation.</b> Every row gets its own engine — built by <c>CreateEngine</c>, which
/// re-runs <see cref="SetupSource"/> so each engine owns its own <c>chunk16</c>/<c>chunk4k</c>/
/// <c>big64k</c> — and warmed with its own script and nothing else (see <see cref="IsolatedScript"/>).
/// It used to be one engine warmed with all eight row scripts, so each row was measured on an engine
/// carrying the other seven rows' globals (every one of them declares <c>f</c>, three also <c>g</c>)
/// and their handler-tree and call-site state, which makes a row's number depend on which siblings
/// exist and on what a change did to <em>them</em>. The rows still measure warm string building, and
/// engine construction and warm-up stay in <c>[GlobalSetup]</c>, outside the measurement. <b>Numbers
/// from this class are not comparable to any published before the harness changed.</b></para>
/// </summary>
[MemoryDiagnoser]
[HideColumns("Error", "Gen0", "Gen1", "Gen2")]
public class StringConcatLargeBenchmark
{
    private IsolatedScript _appendSmallChunks;
    private IsolatedScript _appendLargeChunks;
    private IsolatedScript _concatLargePair;
    private IsolatedScript _buildLargeThenScan;
    private IsolatedScript _chainSmallThree;
    private IsolatedScript _chainSmallSix;
    private IsolatedScript _chainLargeThree;
    private IsolatedScript _chainNumericThree;
    private IsolatedScript _assignAppendSmallChunks;
    private IsolatedScript _assignPrependSmallChunks;
    private IsolatedScript _assignChainThree;
    private IsolatedScript _assignAppendThenScan;

    private const string SetupSource = """
        var chunk16 = 'abcdefghijklmnop';
        var chunk4k = '';
        var big64k = '';
        (function () {
            var parts = [];
            for (var i = 0; i < 256; i++) { parts.push(chunk16); }
            chunk4k = parts.join('');
            parts = [];
            for (var i = 0; i < 16; i++) { parts.push(chunk4k); }
            big64k = parts.join('');
        })();
        """;

    /// <summary>Builds a fresh engine carrying the fixture every row needs, and nothing else.</summary>
    private static Engine CreateEngine()
    {
        var engine = new Engine();
        engine.Execute(SetupSource);
        return engine;
    }

    [GlobalSetup]
    public void Setup()
    {
        // 4,096 x 16 chars -> 64 KB result; the established fast case
        _appendSmallChunks = IsolatedScript.Warm(Engine.PrepareScript("""
            function f() {
                var s = '';
                for (var i = 0; i < 4096; i++) { s += chunk16; }
                return s.length;
            }
            f();
            """), CreateEngine);

        // 256 x 4 KB -> 1 MB result; every append copies the whole chunk today
        _appendLargeChunks = IsolatedScript.Warm(Engine.PrepareScript("""
            function f() {
                var s = '';
                for (var i = 0; i < 256; i++) { s += chunk4k; }
                return s.length;
            }
            f();
            """), CreateEngine);

        // one-shot big + big, repeated; O(1) for a rope, O(n) copy today
        _concatLargePair = IsolatedScript.Warm(Engine.PrepareScript("""
            function f() {
                var total = 0;
                for (var i = 0; i < 64; i++) {
                    var t = big64k + big64k;
                    total += t.length;
                }
                return total;
            }
            f();
            """), CreateEngine);

        // build 256 KB from large chunks, then charAt-scan it — the pattern a lazy
        // representation must not regress
        _buildLargeThenScan = IsolatedScript.Warm(Engine.PrepareScript("""
            function f() {
                var s = '';
                for (var i = 0; i < 64; i++) { s += chunk4k; }
                var acc = 0;
                for (var i = 0; i < s.length; i += 997) { acc += s.charCodeAt(i); }
                return acc;
            }
            f();
            """), CreateEngine);

        // a + b + c over SMALL strings, no string literal among the operands. The dominant real-world
        // chain shape, and the one most exposed to per-evaluation overhead in the flattened path:
        // the saved intermediate is tiny here, so this lane is the guard against paying more than we
        // save on short chains.
        _chainSmallThree = IsolatedScript.Warm(Engine.PrepareScript("""
            function f(a, b, c) { return a + b + c; }
            function g() {
                var n = 0;
                for (var i = 0; i < 20000; i++) { n += f(chunk16, chunk16, chunk16).length; }
                return n;
            }
            g();
            """), CreateEngine);

        // six-operand chain: exercises the string[] path rather than the string.Concat overloads
        _chainSmallSix = IsolatedScript.Warm(Engine.PrepareScript("""
            function f(a, b, c, d, e, g) { return a + b + c + d + e + g; }
            function h() {
                var n = 0;
                for (var i = 0; i < 20000; i++) { n += f(chunk16, chunk16, chunk16, chunk16, chunk16, chunk16).length; }
                return n;
            }
            h();
            """), CreateEngine);

        // a + b + c over 64 KB operands: the shape where the skipped intermediate dominates
        _chainLargeThree = IsolatedScript.Warm(Engine.PrepareScript("""
            function f(a, b, c) { return a + b + c; }
            function g() {
                var n = 0;
                for (var i = 0; i < 64; i++) { n += f(chunk16, big64k, chunk16).length; }
                return n;
            }
            g();
            """), CreateEngine);

        // numeric 3-operand chain: must keep PlusBinaryExpression's unboxed numeric lanes
        _chainNumericThree = IsolatedScript.Warm(Engine.PrepareScript("""
            function f(a, b, c) { return a + b + c; }
            function g() {
                var n = 0;
                for (var i = 0; i < 100000; i++) { n += f(i, i + 1, i + 2); }
                return n;
            }
            g();
            """), CreateEngine);

        // the same 4,096 x 16 chars -> 64 KB as AppendSmallChunks, accumulated through `+` instead of
        // `+=`. Identical semantics, and until #3350 a different code path: this one copied the whole
        // accumulator on every iteration. Read it paired with AppendSmallChunks.
        _assignAppendSmallChunks = IsolatedScript.Warm(Engine.PrepareScript("""
            function f() {
                var s = '';
                for (var i = 0; i < 4096; i++) { s = s + chunk16; }
                return s.length;
            }
            f();
            """), CreateEngine);

        // prepending: the shape `+=` cannot express at all, so it has never had a fast path
        _assignPrependSmallChunks = IsolatedScript.Warm(Engine.PrepareScript("""
            function f() {
                var s = '';
                for (var i = 0; i < 4096; i++) { s = chunk16 + s; }
                return s.length;
            }
            f();
            """), CreateEngine);

        // accumulating through a three-operand chain, which is one flattened node rather than two
        // nested additions -- the same accumulator, reached by the other of the two '+' code paths
        _assignChainThree = IsolatedScript.Warm(Engine.PrepareScript("""
            function f() {
                var s = '';
                for (var i = 0; i < 2048; i++) { s = s + chunk16 + chunk16; }
                return s.length;
            }
            f();
            """), CreateEngine);

        // AssignAppendSmallChunks plus a charAt scan of the result, so the difference between the two is
        // what reading the built value back as text costs -- which for a representation that defers the
        // copy is where it has to pay for it, over a tree one node deep per iteration.
        _assignAppendThenScan = IsolatedScript.Warm(Engine.PrepareScript("""
            function f() {
                var s = '';
                for (var i = 0; i < 4096; i++) { s = s + chunk16; }
                var acc = 0;
                for (var i = 0; i < s.length; i += 997) { acc += s.charCodeAt(i); }
                return acc;
            }
            f();
            """), CreateEngine);
    }

    [Benchmark]
    public JsValue AppendSmallChunks() => _appendSmallChunks.Run();

    [Benchmark]
    public JsValue AppendLargeChunks() => _appendLargeChunks.Run();

    [Benchmark]
    public JsValue ConcatLargePair() => _concatLargePair.Run();

    [Benchmark]
    public JsValue BuildLargeThenScan() => _buildLargeThenScan.Run();

    [Benchmark]
    public JsValue ChainSmallThree() => _chainSmallThree.Run();

    [Benchmark]
    public JsValue ChainSmallSix() => _chainSmallSix.Run();

    [Benchmark]
    public JsValue ChainLargeThree() => _chainLargeThree.Run();

    [Benchmark]
    public JsValue ChainNumericThree() => _chainNumericThree.Run();

    [Benchmark]
    public JsValue AssignAppendSmallChunks() => _assignAppendSmallChunks.Run();

    [Benchmark]
    public JsValue AssignPrependSmallChunks() => _assignPrependSmallChunks.Run();

    [Benchmark]
    public JsValue AssignChainThree() => _assignChainThree.Run();

    [Benchmark]
    public JsValue AssignAppendThenScan() => _assignAppendThenScan.Run();
}
