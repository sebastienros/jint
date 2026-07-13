using BenchmarkDotNet.Attributes;
using Jint.Native;

namespace Jint.Benchmark;

/// <summary>
/// Per-statement constraint check overhead: a registered timeout (the most common embedder
/// safety net) used to force a virtual Check() call before every executed statement and
/// disarm the tight for-body lane. The amortized-constraint partition reduces that to a
/// countdown decrement per statement and keeps the lane armed, so the TimeoutEnabled=true
/// row should track the unconstrained row closely.
/// </summary>
[MemoryDiagnoser]
public class ConstrainedExecutionBenchmark
{
    private Engine _engine = null!;
    private Prepared<Script> _functionLocalLoop;

    [Params(false, true)]
    public bool TimeoutEnabled { get; set; }

    [GlobalSetup]
    public void GlobalSetup()
    {
        _functionLocalLoop = Engine.PrepareScript("""
            function f() {
                var s = 0;
                for (var i = 0; i < 1000000; i++) {
                    s += 2;
                }
                return s;
            }
            f();
            """);

        _engine = TimeoutEnabled
            ? new Engine(options => options.TimeoutInterval(TimeSpan.FromSeconds(30)))
            : new Engine();
        _engine.Evaluate(_functionLocalLoop);
    }

    [Benchmark]
    public JsValue FunctionLocalLoop() => _engine.Evaluate(_functionLocalLoop);
}
