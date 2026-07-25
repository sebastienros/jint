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
/// </summary>
[MemoryDiagnoser]
[HideColumns("Error", "Gen0", "Gen1", "Gen2")]
public class StringConcatLargeBenchmark
{
    private Engine _engine = null!;
    private Prepared<Script> _appendSmallChunks;
    private Prepared<Script> _appendLargeChunks;
    private Prepared<Script> _concatLargePair;
    private Prepared<Script> _buildLargeThenScan;
    private Prepared<Script> _chainSmallThree;
    private Prepared<Script> _chainSmallSix;
    private Prepared<Script> _chainLargeThree;
    private Prepared<Script> _chainNumericThree;

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

    [GlobalSetup]
    public void Setup()
    {
        _engine = new Engine();
        _engine.Execute(SetupSource);

        // 4,096 x 16 chars -> 64 KB result; the established fast case
        _appendSmallChunks = Engine.PrepareScript("""
            function f() {
                var s = '';
                for (var i = 0; i < 4096; i++) { s += chunk16; }
                return s.length;
            }
            f();
            """);

        // 256 x 4 KB -> 1 MB result; every append copies the whole chunk today
        _appendLargeChunks = Engine.PrepareScript("""
            function f() {
                var s = '';
                for (var i = 0; i < 256; i++) { s += chunk4k; }
                return s.length;
            }
            f();
            """);

        // one-shot big + big, repeated; O(1) for a rope, O(n) copy today
        _concatLargePair = Engine.PrepareScript("""
            function f() {
                var total = 0;
                for (var i = 0; i < 64; i++) {
                    var t = big64k + big64k;
                    total += t.length;
                }
                return total;
            }
            f();
            """);

        // build 256 KB from large chunks, then charAt-scan it — the pattern a lazy
        // representation must not regress
        _buildLargeThenScan = Engine.PrepareScript("""
            function f() {
                var s = '';
                for (var i = 0; i < 64; i++) { s += chunk4k; }
                var acc = 0;
                for (var i = 0; i < s.length; i += 997) { acc += s.charCodeAt(i); }
                return acc;
            }
            f();
            """);

        // a + b + c over SMALL strings, no string literal among the operands. The dominant real-world
        // chain shape, and the one most exposed to per-evaluation overhead in the flattened path:
        // the saved intermediate is tiny here, so this lane is the guard against paying more than we
        // save on short chains.
        _chainSmallThree = Engine.PrepareScript("""
            function f(a, b, c) { return a + b + c; }
            function g() {
                var n = 0;
                for (var i = 0; i < 20000; i++) { n += f(chunk16, chunk16, chunk16).length; }
                return n;
            }
            g();
            """);

        // six-operand chain: exercises the string[] path rather than the string.Concat overloads
        _chainSmallSix = Engine.PrepareScript("""
            function f(a, b, c, d, e, g) { return a + b + c + d + e + g; }
            function h() {
                var n = 0;
                for (var i = 0; i < 20000; i++) { n += f(chunk16, chunk16, chunk16, chunk16, chunk16, chunk16).length; }
                return n;
            }
            h();
            """);

        // a + b + c over 64 KB operands: the shape where the skipped intermediate dominates
        _chainLargeThree = Engine.PrepareScript("""
            function f(a, b, c) { return a + b + c; }
            function g() {
                var n = 0;
                for (var i = 0; i < 64; i++) { n += f(chunk16, big64k, chunk16).length; }
                return n;
            }
            g();
            """);

        // numeric 3-operand chain: must keep PlusBinaryExpression's unboxed numeric lanes
        _chainNumericThree = Engine.PrepareScript("""
            function f(a, b, c) { return a + b + c; }
            function g() {
                var n = 0;
                for (var i = 0; i < 100000; i++) { n += f(i, i + 1, i + 2); }
                return n;
            }
            g();
            """);

        _engine.Evaluate(_appendSmallChunks);
        _engine.Evaluate(_appendLargeChunks);
        _engine.Evaluate(_concatLargePair);
        _engine.Evaluate(_buildLargeThenScan);
        _engine.Evaluate(_chainSmallThree);
        _engine.Evaluate(_chainSmallSix);
        _engine.Evaluate(_chainLargeThree);
        _engine.Evaluate(_chainNumericThree);
    }

    [Benchmark]
    public JsValue AppendSmallChunks() => _engine.Evaluate(_appendSmallChunks);

    [Benchmark]
    public JsValue AppendLargeChunks() => _engine.Evaluate(_appendLargeChunks);

    [Benchmark]
    public JsValue ConcatLargePair() => _engine.Evaluate(_concatLargePair);

    [Benchmark]
    public JsValue BuildLargeThenScan() => _engine.Evaluate(_buildLargeThenScan);

    [Benchmark]
    public JsValue ChainSmallThree() => _engine.Evaluate(_chainSmallThree);

    [Benchmark]
    public JsValue ChainSmallSix() => _engine.Evaluate(_chainSmallSix);

    [Benchmark]
    public JsValue ChainLargeThree() => _engine.Evaluate(_chainLargeThree);

    [Benchmark]
    public JsValue ChainNumericThree() => _engine.Evaluate(_chainNumericThree);
}
