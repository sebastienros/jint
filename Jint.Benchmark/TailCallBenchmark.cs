using BenchmarkDotNet.Attributes;
using Jint.Native;

namespace Jint.Benchmark;

/// <summary>
/// Strict-mode proper tail-call shapes compared with an equivalent loop. Each row owns one engine,
/// installs only its own workload in <see cref="Setup"/>, and warms that workload before measurement,
/// so engine construction and function declaration do not enter the measurement. At depth 500 the
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
        _directCall = Engine.PrepareScript("sum(500, 0);", strict: true);
        _directEngine.Evaluate(_directCall);

        _mutualEngine = CreateEngine("""
            function even(n) {
                return n === 0 || odd(n - 1);
            }
            function odd(n) {
                return n !== 0 && even(n - 1);
            }
            """);
        _mutualCall = Engine.PrepareScript("even(500);", strict: true);
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
        _loopCall = Engine.PrepareScript("sumLoop(500);", strict: true);
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

/// <summary>
/// A shallow strict tail delegation exercised repeatedly on one isolated, warmed engine. The
/// benchmark keeps engine construction and function declarations out of the measurement and
/// specifically guards the register-argument tail-request path used by everyday wrappers.
/// </summary>
[MemoryDiagnoser]
[HideColumns("Error", "Gen0", "Gen1", "Gen2")]
public class TailCallDelegationBenchmark
{
    private Engine _engine = null!;
    private Engine _limitedEngine = null!;
    private Prepared<Script> _call;

    [GlobalSetup]
    public void Setup()
    {
        _engine = CreateEngine(static options => options.Strict());
        _limitedEngine = CreateEngine(static options => options.Strict().LimitRecursion(100_000));
        _call = Engine.PrepareScript("""
            var value = 0;
            for (var i = 0; i < 10000; i++) {
                value = delegate(value);
            }
            value;
            """, strict: true);
        _engine.Evaluate(_call);
        _limitedEngine.Evaluate(_call);
    }

    private static Engine CreateEngine(Action<Options> configure)
    {
        var engine = new Engine(configure);
        engine.Execute("""
            function helper(value) {
                return (value + 1) & 1023;
            }
            function delegate(value) {
                return helper(value);
            }
            """);
        return engine;
    }

    [Benchmark(Baseline = true)]
    public JsValue StrictDelegation() => _engine.Evaluate(_call);

    [Benchmark]
    public JsValue StrictDelegationWithRecursionLimit() => _limitedEngine.Evaluate(_call);
}
