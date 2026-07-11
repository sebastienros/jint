using BenchmarkDotNet.Attributes;
using Jint.Native;

namespace Jint.Benchmark;

/// <summary>
/// Optional chaining, nullish coalescing and logical assignment over inputs mixed with an inline
/// LCG (~50% present / 50% short-circuiting) so the branch predictor cannot memorize the outcome —
/// the modern null-guard idioms that pervade current JS but appear in no other benchmark.
/// 100k iterations per op inside a function frame.
/// </summary>
[MemoryDiagnoser]
[HideColumns("Error", "Gen0", "Gen1", "Gen2")]
public class ModernOperatorsBenchmark
{
    private Engine _engine = null!;
    private Prepared<Script> _optionalChainHit;
    private Prepared<Script> _optionalChainMiss;
    private Prepared<Script> _nullishCoalesce;
    private Prepared<Script> _nullishAssign;
    private Prepared<Script> _logicalOrAssign;

    internal const string SetupSource = """
        var objs = [];
        var vals = [];
        var present = { a: { b: { c: 1 } } };
        (function () {
            var seed = 20260711;
            for (var i = 0; i < 1024; i++) {
                seed = (seed * 1664525 + 1013904223) | 0;
                var pick = (seed >>> 4) & 3;
                if (pick < 2) { objs.push({ a: { b: { c: 1 } } }); }
                else if (pick === 2) { objs.push({ a: null }); }
                else { objs.push({}); }
                var vpick = (seed >>> 8) & 3;
                if (vpick < 2) { vals.push(i & 255); }
                else if (vpick === 2) { vals.push(null); }
                else { vals.push(undefined); }
            }
        })();
        """;

    internal const string OptionalChainMissSource = """
        function f() {
            var seed = 20260711;
            var s = 0;
            for (var i = 0; i < 100000; i++) {
                seed = (seed * 1664525 + 1013904223) | 0;
                var o = objs[(seed >>> 4) & 1023];
                s += (o?.a?.b?.c || 0);
            }
            return s;
        }
        f();
        """;

    internal const string NullishCoalesceSource = """
        function f() {
            var seed = 20260711;
            var s = 0;
            for (var i = 0; i < 100000; i++) {
                seed = (seed * 1664525 + 1013904223) | 0;
                s += (vals[(seed >>> 6) & 1023] ?? 0);
            }
            return s;
        }
        f();
        """;

    [GlobalSetup]
    public void Setup()
    {
        _engine = new Engine();
        _engine.Execute(SetupSource);

        // all links present: the chain always completes
        _optionalChainHit = Engine.PrepareScript("""
            function f() {
                var s = 0;
                for (var i = 0; i < 100000; i++) { s += present?.a?.b?.c; }
                return s;
            }
            f();
            """);

        _optionalChainMiss = Engine.PrepareScript(OptionalChainMissSource);
        _nullishCoalesce = Engine.PrepareScript(NullishCoalesceSource);

        // ??= on a local that is null/undefined/number in unpredictable rotation (~50% nullish)
        _nullishAssign = Engine.PrepareScript("""
            function f() {
                var seed = 20260711;
                var s = 0;
                for (var i = 0; i < 100000; i++) {
                    seed = (seed * 1664525 + 1013904223) | 0;
                    var pick = seed & 3;
                    var x = pick === 0 ? null : (pick === 1 ? undefined : (seed & 255));
                    x ??= 7;
                    s += x;
                }
                return s;
            }
            f();
            """);

        // ||= over a 50/50 truthy/falsy local
        _logicalOrAssign = Engine.PrepareScript("""
            function f() {
                var seed = 20260711;
                var s = 0;
                for (var i = 0; i < 100000; i++) {
                    seed = (seed * 1664525 + 1013904223) | 0;
                    var x = (seed >>> 4) & 1;
                    x ||= 7;
                    s += x;
                }
                return s;
            }
            f();
            """);

        _engine.Evaluate(_optionalChainHit);
        _engine.Evaluate(_optionalChainMiss);
        _engine.Evaluate(_nullishCoalesce);
        _engine.Evaluate(_nullishAssign);
        _engine.Evaluate(_logicalOrAssign);
    }

    [Benchmark]
    public JsValue OptionalChainHit() => _engine.Evaluate(_optionalChainHit);

    [Benchmark]
    public JsValue OptionalChainMiss() => _engine.Evaluate(_optionalChainMiss);

    [Benchmark]
    public JsValue NullishCoalesce() => _engine.Evaluate(_nullishCoalesce);

    [Benchmark]
    public JsValue NullishAssign() => _engine.Evaluate(_nullishAssign);

    [Benchmark]
    public JsValue LogicalOrAssign() => _engine.Evaluate(_logicalOrAssign);
}
