using BenchmarkDotNet.Attributes;
using Jint.Native;

namespace Jint.Benchmark;

/// <summary>
/// Optional chaining, nullish coalescing and logical assignment over inputs mixed with an inline
/// LCG (~50% present / 50% short-circuiting) so the branch predictor cannot memorize the outcome —
/// the modern null-guard idioms that pervade current JS but appear in no other benchmark.
/// 100k iterations per op inside a function frame.
///
/// <para><b>Engine isolation.</b> Every row gets its own engine, built by <c>CreateEngine</c> (which
/// installs the shared <see cref="SetupSource"/> fixture every row needs) and warmed with its own script
/// and nothing else (see <see cref="IsolatedScript"/>). It used to be one shared engine warmed with all
/// five scripts, so each row was measured on an engine carrying its siblings' globals (every script
/// declares <c>f</c>, <c>s</c> and <c>i</c>) and their handler-tree and call-site state. The rows still
/// measure warm dispatch, and engine construction and warm-up stay in <c>[GlobalSetup]</c>, outside the
/// measurement. <b>Numbers from this class are not comparable to any published before the harness
/// changed.</b></para>
/// </summary>
[MemoryDiagnoser]
[HideColumns("Error", "Gen0", "Gen1", "Gen2")]
public class ModernOperatorsBenchmark
{
    private IsolatedScript _optionalChainHit;
    private IsolatedScript _optionalChainMiss;
    private IsolatedScript _nullishCoalesce;
    private IsolatedScript _nullishAssign;
    private IsolatedScript _logicalOrAssign;

    // Mixing is precomputed at setup into `order` (8,192 small-int indices) because a per-iteration
    // JS LCG boxes JsNumber transients (~120 B/iter) that would dominate what these rows measure.
    // All values stay inside the small-int cache and sub-selection reads stored array elements,
    // so the rows themselves are allocation-free apart from the construct under test.
    internal const string SetupSource = """
        var objs = [];
        var vals = [];
        var nullishInputs = [];
        var zeroOnes = [];
        var order = [];
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
                var npick = (seed >>> 12) & 3;
                nullishInputs.push(npick === 0 ? null : (npick === 1 ? undefined : (seed & 255)));
                zeroOnes.push((seed >>> 16) & 1);
            }
            for (var i = 0; i < 8192; i++) {
                seed = (seed * 1664525 + 1013904223) | 0;
                order.push((seed >>> 7) & 1023);
            }
        })();
        """;

    internal const string OptionalChainMissSource = """
        function f() {
            var s = 0;
            for (var i = 0; i < 100000; i++) {
                var o = objs[order[i & 8191]];
                s += (o?.a?.b?.c || 0);
            }
            return s;
        }
        f();
        """;

    internal const string NullishCoalesceSource = """
        function f() {
            var s = 0;
            for (var i = 0; i < 100000; i++) {
                s += (vals[order[i & 8191]] ?? 0);
            }
            return s;
        }
        f();
        """;

    /// <summary>Builds a fresh engine carrying the shared <see cref="SetupSource"/> fixture every row needs.</summary>
    private static Engine CreateEngine()
    {
        var engine = new Engine();
        engine.Execute(SetupSource);
        return engine;
    }

    [GlobalSetup]
    public void Setup()
    {
        // all links present: the chain always completes
        _optionalChainHit = IsolatedScript.Warm("""
            function f() {
                var s = 0;
                for (var i = 0; i < 100000; i++) { s += present?.a?.b?.c; }
                return s;
            }
            f();
            """, CreateEngine);

        _optionalChainMiss = IsolatedScript.Warm(OptionalChainMissSource, CreateEngine);
        _nullishCoalesce = IsolatedScript.Warm(NullishCoalesceSource, CreateEngine);

        // ??= on a local that is null/undefined/number in unpredictable rotation (~50% nullish)
        _nullishAssign = IsolatedScript.Warm("""
            function f() {
                var s = 0;
                for (var i = 0; i < 100000; i++) {
                    var x = nullishInputs[order[i & 8191]];
                    x ??= 7;
                    s += x;
                }
                return s;
            }
            f();
            """, CreateEngine);

        // ||= over a 50/50 truthy/falsy local
        _logicalOrAssign = IsolatedScript.Warm("""
            function f() {
                var s = 0;
                for (var i = 0; i < 100000; i++) {
                    var x = zeroOnes[order[i & 8191]];
                    x ||= 7;
                    s += x;
                }
                return s;
            }
            f();
            """, CreateEngine);
    }

    [Benchmark]
    public JsValue OptionalChainHit() => _optionalChainHit.Run();

    [Benchmark]
    public JsValue OptionalChainMiss() => _optionalChainMiss.Run();

    [Benchmark]
    public JsValue NullishCoalesce() => _nullishCoalesce.Run();

    [Benchmark]
    public JsValue NullishAssign() => _nullishAssign.Run();

    [Benchmark]
    public JsValue LogicalOrAssign() => _logicalOrAssign.Run();
}
