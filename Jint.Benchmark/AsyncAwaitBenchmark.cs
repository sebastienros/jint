using BenchmarkDotNet.Attributes;
using Jint.Native;

namespace Jint.Benchmark;

/// <summary>
/// Resolved-promise await chains and microtask-heavy shapes — the dominant real-world async
/// pattern. Top-level script evaluation drains the event loop until the continuation queue is
/// empty (including continuations enqueued by continuations), so every row completes fully within
/// one Evaluate; <see cref="AsyncFunctionExitBenchmark"/> keeps owning bare exit cost.
/// <see cref="SyncCallLoop"/> is the baseline: the same call count without suspension, so
/// per-await overhead = (AwaitResolvedLoop − SyncCallLoop) / 1000.
/// </summary>
[MemoryDiagnoser]
[HideColumns("Error", "Gen0", "Gen1", "Gen2")]
public class AsyncAwaitBenchmark
{
    private Engine _engine = null!;
    private Prepared<Script> _syncCallLoop;
    private Prepared<Script> _awaitResolvedLoop;
    private Prepared<Script> _awaitChainDepth50;
    private Prepared<Script> _promiseAll100;
    private Prepared<Script> _thenChain1000;
    private Prepared<Script> _microtaskFanout;

    internal const string AwaitResolvedLoopSource = """
        async function f() {
            var s = 0;
            for (var i = 0; i < 1000; i++) { s += await Promise.resolve(1); }
            return s;
        }
        f();
        """;

    internal const string ThenChain1000Source = """
        (function () {
            var p = Promise.resolve(0);
            for (var i = 0; i < 1000; i++) { p = p.then(function (x) { return x + 1; }); }
            return p;
        })();
        """;

    internal const string PromiseAll100Source = """
        (function () {
            function run() {
                var arr = [];
                for (var i = 0; i < 100; i++) { arr.push(Promise.resolve(i)); }
                return Promise.all(arr).then(function (r) { return r.length; });
            }
            var last;
            for (var j = 0; j < 10; j++) { last = run(); }
            return last;
        })();
        """;

    [GlobalSetup]
    public void Setup()
    {
        _engine = new Engine();

        // the sync floor for the same call count
        _syncCallLoop = Engine.PrepareScript("""
            function g(i) { return i + 1; }
            function f() {
                var s = 0;
                for (var i = 0; i < 1000; i++) { s = g(s); }
                return s;
            }
            f();
            """);

        _awaitResolvedLoop = Engine.PrepareScript(AwaitResolvedLoopSource);

        // 20 × a 50-deep await-recursion chain
        _awaitChainDepth50 = Engine.PrepareScript("""
            async function step(n) {
                if (n === 0) { return 0; }
                return (await step(n - 1)) + 1;
            }
            (function () {
                var last;
                for (var i = 0; i < 20; i++) { last = step(50); }
                return last;
            })();
            """);

        _promiseAll100 = Engine.PrepareScript(PromiseAll100Source);
        _thenChain1000 = Engine.PrepareScript(ThenChain1000Source);

        // 1,000 independent resolved promises, one .then each
        _microtaskFanout = Engine.PrepareScript("""
            (function () {
                var last;
                for (var i = 0; i < 1000; i++) { last = Promise.resolve(i).then(function (x) { return x + 1; }); }
                return last;
            })();
            """);

        _engine.Evaluate(_syncCallLoop);
        _engine.Evaluate(_awaitResolvedLoop);
        _engine.Evaluate(_awaitChainDepth50);
        _engine.Evaluate(_promiseAll100);
        _engine.Evaluate(_thenChain1000);
        _engine.Evaluate(_microtaskFanout);
    }

    [Benchmark(Baseline = true)]
    public JsValue SyncCallLoop() => _engine.Evaluate(_syncCallLoop);

    [Benchmark]
    public JsValue AwaitResolvedLoop() => _engine.Evaluate(_awaitResolvedLoop);

    [Benchmark]
    public JsValue AwaitChainDepth50() => _engine.Evaluate(_awaitChainDepth50);

    [Benchmark]
    public JsValue PromiseAll100() => _engine.Evaluate(_promiseAll100);

    [Benchmark]
    public JsValue ThenChain1000() => _engine.Evaluate(_thenChain1000);

    [Benchmark]
    public JsValue MicrotaskFanout() => _engine.Evaluate(_microtaskFanout);
}
