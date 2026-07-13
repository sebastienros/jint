using BenchmarkDotNet.Attributes;
using Jint.Native;

namespace Jint.Benchmark;

/// <summary>
/// String building from LARGE pieces — the workload where a rope representation would beat the
/// current copy-into-builder strategy. Small-chunk appends (the common `s += c` loop) are the
/// existing ConcatenatedString sweet spot and serve as the baseline; the large-chunk lanes append
/// 4 KB pieces, concatenate two 64 KB strings, and scan the built result with charAt afterwards
/// (the read pattern a lazy/rope representation would have to pay for).
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

        _engine.Evaluate(_appendSmallChunks);
        _engine.Evaluate(_appendLargeChunks);
        _engine.Evaluate(_concatLargePair);
        _engine.Evaluate(_buildLargeThenScan);
    }

    [Benchmark]
    public JsValue AppendSmallChunks() => _engine.Evaluate(_appendSmallChunks);

    [Benchmark]
    public JsValue AppendLargeChunks() => _engine.Evaluate(_appendLargeChunks);

    [Benchmark]
    public JsValue ConcatLargePair() => _engine.Evaluate(_concatLargePair);

    [Benchmark]
    public JsValue BuildLargeThenScan() => _engine.Evaluate(_buildLargeThenScan);
}
