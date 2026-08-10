using BenchmarkDotNet.Attributes;
using Jint.Native;

namespace Jint.Benchmark;

/// <summary>
/// Strict-mode proper tail-call shapes compared with an equivalent loop. Each row owns one engine,
/// installs only its own workload in <see cref="Setup"/>, and warms that workload before measurement,
/// so engine construction and function declaration do not enter the measurement. At depth 1,000 the
/// operation is long enough to dominate the sub-microsecond Engine.Evaluate entry cost while remaining
/// representative of host code that repeatedly invokes an already-installed function.
/// </summary>
[MemoryDiagnoser]
[HideColumns("Error", "Gen0", "Gen1", "Gen2")]
public class TailCallBenchmark
{
    private Engine _directEngine = null!;
    private Engine _mutualEngine = null!;
    private Engine _loopEngine = null!;
    private Prepared<Script> _directCall;
    private Prepared<Script> _mutualCall;
    private Prepared<Script> _loopCall;

    [GlobalSetup]
    public void Setup()
    {
        _directEngine = CreateEngine("""
            function sum(n, total) {
                return n === 0 ? total : sum(n - 1, total + n);
            }
            """);
        _directCall = Engine.PrepareScript("sum(1000, 0);", strict: true);
        _directEngine.Evaluate(_directCall);

        _mutualEngine = CreateEngine("""
            function even(n) {
                return n === 0 || odd(n - 1);
            }
            function odd(n) {
                return n !== 0 && even(n - 1);
            }
            """);
        _mutualCall = Engine.PrepareScript("even(1000);", strict: true);
        _mutualEngine.Evaluate(_mutualCall);

        _loopEngine = CreateEngine("""
            function sumLoop(n) {
                let total = 0;
                while (n !== 0) {
                    total += n--;
                }
                return total;
            }
            """);
        _loopCall = Engine.PrepareScript("sumLoop(1000);", strict: true);
        _loopEngine.Evaluate(_loopCall);
    }

    [Benchmark]
    public JsValue DirectTailRecursion() => _directEngine.Evaluate(_directCall);

    [Benchmark]
    public JsValue MutualTailRecursion() => _mutualEngine.Evaluate(_mutualCall);

    [Benchmark(Baseline = true)]
    public JsValue IterativeLoop() => _loopEngine.Evaluate(_loopCall);

    private static Engine CreateEngine(string definitions)
    {
        var engine = new Engine(static options => options.Strict());
        engine.Execute(definitions);
        return engine;
    }
}
