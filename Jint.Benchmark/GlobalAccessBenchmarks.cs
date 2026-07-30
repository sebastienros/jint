using BenchmarkDotNet.Attributes;
using Jint.Native;

namespace Jint.Benchmark;

/// <summary>
/// Isolates top-level (global binding) variable access — the stopwatch.js shape where loop
/// counters and state live as global-object properties and every read/write pays a property
/// dictionary lookup. LocalVarLoop is the fixed-slot ceiling/guard for the same operations.
///
/// <para><b>Engine isolation.</b> Every row gets its own engine — built by <c>CreateEngine</c>, which
/// re-runs the global-declaration fixture so each engine owns its own <c>gx</c>/<c>gy</c>/… — and warmed
/// with its own script and nothing else (see <see cref="IsolatedScript"/>). It used to be one engine
/// warmed with all five scripts, which is the worst possible arrangement for a class about global
/// bindings: every row wrote the other rows' globals on the one global object they all shared, and each
/// row was additionally measured on the others' handler-tree, call-site and identifier-cache state. The
/// rows still measure warm access, and engine construction and warm-up stay in <c>[GlobalSetup]</c>,
/// outside the measurement. <b>Numbers from this class are not comparable to any published before the
/// harness changed.</b></para>
/// </summary>
[MemoryDiagnoser]
[HideColumns("Error", "Gen0", "Gen1", "Gen2")]
public class GlobalAccessBenchmarks
{
    private IsolatedScript _globalVarLoop;
    private IsolatedScript _localVarLoop;
    private IsolatedScript _globalUpdateLoop;
    private IsolatedScript _nestedGlobalReadLoop;
    private IsolatedScript _nestedGlobalWriteLoop;

    /// <summary>Builds a fresh engine carrying the fixture every row needs, and nothing else.</summary>
    private static Engine CreateEngine()
    {
        var engine = new Engine(static options => options.Strict());
        engine.Execute("var gx = 0; var gy = 0; var gz = 0; var gobj = null; var gsum = 0; var gval = 0;");
        return engine;
    }

    [GlobalSetup]
    public void Setup()
    {
        _globalVarLoop = IsolatedScript.Warm(Engine.PrepareScript("""
            gx = 0; gy = 0; gz = 0;
            for (var gi = 0; gi < 200000; gi++) {
                gz = gx ^ gy;
                gx = (gx + 1) & 1023;
                gy = (gy + (gz & 3)) & 2047;
            }
            gz;
            """, strict: true), CreateEngine);

        _localVarLoop = IsolatedScript.Warm(Engine.PrepareScript("""
            (function() {
                var x = 0, y = 0, z = 0;
                for (var i = 0; i < 200000; i++) {
                    z = x ^ y;
                    x = (x + 1) & 1023;
                    y = (y + (z & 3)) & 2047;
                }
                return z;
            })();
            """, strict: true), CreateEngine);

        // Update-expression heavy: gx++, gy++, and the gi++/go++ loop counters are all global
        // UpdateExpressions, which #2507 did not cache (it cached reads and simple assignments). The
        // counters stay in the small-integer cache (reset each outer iteration) so the measurement is
        // the binding-resolution cost, not JsNumber boxing — the stopwatch.js shape (x<1021, y<383).
        _globalUpdateLoop = IsolatedScript.Warm(Engine.PrepareScript("""
            gx = 0; gy = 0;
            for (var go = 0; go < 1000; go++) {
                for (var gi = 0; gi < 1000; gi++) { gx++; gy++; }
                gx = 0; gy = 0;
            }
            gx + gy;
            """, strict: true), CreateEngine);

        // Global reads from a NESTED lexical scope (let-header loop + const body): the validator
        // cannot take the hop-0 identity arm and re-walks the chain with shadow probes per read —
        // the stopwatch-modern shape where the most-referenced binding (`sw`) is a global reached
        // from two levels of loop scope.
        _nestedGlobalReadLoop = IsolatedScript.Warm(Engine.PrepareScript("""
            gobj = { n: 0 };
            gsum = 0;
            (function () {
                var t = 0;
                for (let r = 0; r < 20; r++) {
                    for (let i = 0; i < 10000; i++) {
                        const a = gobj;
                        const b = gobj;
                        t = (t + (a === b ? 1 : 0)) & 1023;
                    }
                }
                return t;
            })();
            """, strict: true), CreateEngine);

        _nestedGlobalWriteLoop = IsolatedScript.Warm(Engine.PrepareScript("""
            gval = 0;
            (function () {
                for (let r = 0; r < 20; r++) {
                    for (let i = 0; i < 10000; i++) {
                        gval = i & 1023;
                    }
                }
                return gval;
            })();
            """, strict: true), CreateEngine);
    }

    [Benchmark]
    public JsValue GlobalVarLoop() => _globalVarLoop.Run();

    [Benchmark]
    public JsValue LocalVarLoop() => _localVarLoop.Run();

    [Benchmark]
    public JsValue GlobalUpdateLoop() => _globalUpdateLoop.Run();

    [Benchmark]
    public JsValue NestedGlobalReadLoop() => _nestedGlobalReadLoop.Run();

    [Benchmark]
    public JsValue NestedGlobalWriteLoop() => _nestedGlobalWriteLoop.Run();
}
