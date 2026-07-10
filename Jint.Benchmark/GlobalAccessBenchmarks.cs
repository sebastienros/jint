using BenchmarkDotNet.Attributes;
using Jint.Native;

namespace Jint.Benchmark;

/// <summary>
/// Isolates top-level (global binding) variable access — the stopwatch.js shape where loop
/// counters and state live as global-object properties and every read/write pays a property
/// dictionary lookup. LocalVarLoop is the fixed-slot ceiling/guard for the same operations.
/// </summary>
[MemoryDiagnoser]
[HideColumns("Error", "Gen0", "Gen1", "Gen2")]
public class GlobalAccessBenchmarks
{
    private Engine _engine = null!;
    private Prepared<Script> _globalVarLoop;
    private Prepared<Script> _localVarLoop;
    private Prepared<Script> _globalUpdateLoop;
    private Prepared<Script> _nestedGlobalReadLoop;
    private Prepared<Script> _nestedGlobalWriteLoop;

    [GlobalSetup]
    public void Setup()
    {
        _globalVarLoop = Engine.PrepareScript("""
            gx = 0; gy = 0; gz = 0;
            for (var gi = 0; gi < 200000; gi++) {
                gz = gx ^ gy;
                gx = (gx + 1) & 1023;
                gy = (gy + (gz & 3)) & 2047;
            }
            gz;
            """, strict: true);

        _localVarLoop = Engine.PrepareScript("""
            (function() {
                var x = 0, y = 0, z = 0;
                for (var i = 0; i < 200000; i++) {
                    z = x ^ y;
                    x = (x + 1) & 1023;
                    y = (y + (z & 3)) & 2047;
                }
                return z;
            })();
            """, strict: true);

        // Update-expression heavy: gx++, gy++, and the gi++/go++ loop counters are all global
        // UpdateExpressions, which #2507 did not cache (it cached reads and simple assignments). The
        // counters stay in the small-integer cache (reset each outer iteration) so the measurement is
        // the binding-resolution cost, not JsNumber boxing — the stopwatch.js shape (x<1021, y<383).
        _globalUpdateLoop = Engine.PrepareScript("""
            gx = 0; gy = 0;
            for (var go = 0; go < 1000; go++) {
                for (var gi = 0; gi < 1000; gi++) { gx++; gy++; }
                gx = 0; gy = 0;
            }
            gx + gy;
            """, strict: true);

        // Global reads from a NESTED lexical scope (let-header loop + const body): the validator
        // cannot take the hop-0 identity arm and re-walks the chain with shadow probes per read —
        // the stopwatch-modern shape where the most-referenced binding (`sw`) is a global reached
        // from two levels of loop scope.
        _nestedGlobalReadLoop = Engine.PrepareScript("""
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
            """, strict: true);

        _nestedGlobalWriteLoop = Engine.PrepareScript("""
            gval = 0;
            (function () {
                for (let r = 0; r < 20; r++) {
                    for (let i = 0; i < 10000; i++) {
                        gval = i & 1023;
                    }
                }
                return gval;
            })();
            """, strict: true);

        _engine = new Engine(static options => options.Strict());
        _engine.Execute("var gx = 0; var gy = 0; var gz = 0; var gobj = null; var gsum = 0; var gval = 0;");
        _engine.Evaluate(_globalVarLoop);
        _engine.Evaluate(_localVarLoop);
        _engine.Evaluate(_globalUpdateLoop);
        _engine.Evaluate(_nestedGlobalReadLoop);
        _engine.Evaluate(_nestedGlobalWriteLoop);
    }

    [Benchmark]
    public JsValue GlobalVarLoop() => _engine.Evaluate(_globalVarLoop);

    [Benchmark]
    public JsValue LocalVarLoop() => _engine.Evaluate(_localVarLoop);

    [Benchmark]
    public JsValue GlobalUpdateLoop() => _engine.Evaluate(_globalUpdateLoop);

    [Benchmark]
    public JsValue NestedGlobalReadLoop() => _engine.Evaluate(_nestedGlobalReadLoop);

    [Benchmark]
    public JsValue NestedGlobalWriteLoop() => _engine.Evaluate(_nestedGlobalWriteLoop);
}
