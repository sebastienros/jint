using BenchmarkDotNet.Attributes;

namespace Jint.Benchmark;

/// <summary>
/// Isolates the prepared-script execution anomaly: a Prepared&lt;Script&gt; shared across
/// fresh engine instances has been consistently slower than re-parsing the source on
/// call-heavy workloads (stopwatch shape), despite saving the parse. The script here is
/// closure-call heavy with a negligible parse cost, so the delta is pure execution.
/// </summary>
[MemoryDiagnoser]
[HideColumns("Error", "Gen0", "Gen1", "Gen2")]
public class PreparedAnomalyBenchmarks
{
    private const string Source = """
        function Counter() {
            var c = 0;
            var on = false;
            this.start = function () { if (on) return; on = true; };
            this.stop = function () { if (!on) return; on = false; };
            this.bump = function () { c = c + 1; };
            this.value = function () { return c; };
        }
        var k = new Counter();
        k.start();
        for (var x = 0; x < 200; x++) {
            for (var y = 0; y < 250; y++) {
                var z = x ^ y;
                if (z % 2 == 0) k.start();
                else if (z % 3 == 0) k.stop();
                else if (z % 5 == 0) k.bump();
                var v = k.value;
                var r = k.bump;
            }
        }
        k.stop();
        """;

    private Prepared<Script> _prepared;

    [GlobalSetup]
    public void Setup()
    {
        _prepared = Engine.PrepareScript(Source, strict: true);
    }

    [Benchmark(Baseline = true)]
    public void SourcePerOp()
    {
        var engine = new Engine(static options => options.Strict());
        engine.Execute(Source);
    }

    [Benchmark]
    public void PreparedShared()
    {
        var engine = new Engine(static options => options.Strict());
        engine.Execute(_prepared);
    }

    // Diagnostic arm: prepared tree built fresh per op (never shared). Distinguishes
    // "shared across engines" from "prepared-tree shape" as the cause of the anomaly.
    [Benchmark]
    public void PreparedPerOp()
    {
        var prepared = Engine.PrepareScript(Source, strict: true);
        var engine = new Engine(static options => options.Strict());
        engine.Execute(prepared);
    }
}
