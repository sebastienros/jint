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

        _engine = new Engine(static options => options.Strict());
        _engine.Execute("var gx = 0; var gy = 0; var gz = 0;");
        _engine.Evaluate(_globalVarLoop);
        _engine.Evaluate(_localVarLoop);
    }

    [Benchmark]
    public JsValue GlobalVarLoop() => _engine.Evaluate(_globalVarLoop);

    [Benchmark]
    public JsValue LocalVarLoop() => _engine.Evaluate(_localVarLoop);
}
