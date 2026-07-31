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
///
/// <para><b>Engine isolation.</b> Every row gets its own default engine, warmed with its own script and
/// nothing else (see <see cref="IsolatedScript"/>). It used to be one engine warmed with all six row
/// scripts, which is worse here than almost anywhere else: the rows share global names (<c>f</c>, <c>i</c>,
/// <c>last</c>, <c>p</c>) and each one additionally leaves promise, microtask and event-loop state behind
/// for the next, on top of the usual handler-tree and call-site caches — so the baseline <em>and</em> the
/// row measured against it both depended on which siblings existed. The rows still measure warm async
/// dispatch, and engine construction and warm-up stay in <c>[GlobalSetup]</c>, outside the measurement.
/// <b>Numbers from this class are not comparable to any published before the harness changed.</b></para>
/// </summary>
[MemoryDiagnoser]
[HideColumns("Error", "Gen0", "Gen1", "Gen2")]
public class AsyncAwaitBenchmark
{
    private IsolatedScript _syncCallLoop;
    private IsolatedScript _awaitResolvedLoop;
    private IsolatedScript _awaitChainDepth50;
    private IsolatedScript _promiseAll100;
    private IsolatedScript _thenChain1000;
    private IsolatedScript _microtaskFanout;

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
        // the sync floor for the same call count
        _syncCallLoop = IsolatedScript.Warm(Engine.PrepareScript("""
            function g(i) { return i + 1; }
            function f() {
                var s = 0;
                for (var i = 0; i < 1000; i++) { s = g(s); }
                return s;
            }
            f();
            """));

        _awaitResolvedLoop = IsolatedScript.Warm(Engine.PrepareScript(AwaitResolvedLoopSource));

        // 20 × a 50-deep await-recursion chain
        _awaitChainDepth50 = IsolatedScript.Warm(Engine.PrepareScript("""
            async function step(n) {
                if (n === 0) { return 0; }
                return (await step(n - 1)) + 1;
            }
            (function () {
                var last;
                for (var i = 0; i < 20; i++) { last = step(50); }
                return last;
            })();
            """));

        _promiseAll100 = IsolatedScript.Warm(Engine.PrepareScript(PromiseAll100Source));
        _thenChain1000 = IsolatedScript.Warm(Engine.PrepareScript(ThenChain1000Source));

        // 1,000 independent resolved promises, one .then each
        _microtaskFanout = IsolatedScript.Warm(Engine.PrepareScript("""
            (function () {
                var last;
                for (var i = 0; i < 1000; i++) { last = Promise.resolve(i).then(function (x) { return x + 1; }); }
                return last;
            })();
            """));
    }

    [Benchmark(Baseline = true)]
    public JsValue SyncCallLoop() => _syncCallLoop.Run();

    [Benchmark]
    public JsValue AwaitResolvedLoop() => _awaitResolvedLoop.Run();

    [Benchmark]
    public JsValue AwaitChainDepth50() => _awaitChainDepth50.Run();

    [Benchmark]
    public JsValue PromiseAll100() => _promiseAll100.Run();

    [Benchmark]
    public JsValue ThenChain1000() => _thenChain1000.Run();

    [Benchmark]
    public JsValue MicrotaskFanout() => _microtaskFanout.Run();
}
