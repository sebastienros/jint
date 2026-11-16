using BenchmarkDotNet.Attributes;
using Jint.Native;

namespace Jint.Benchmark;

/// <summary>Compares synchronous, synchronously completed async, and suspended async host entries.</summary>
/// <remarks>
/// Every row has its own warmed engine. Parsing, engine construction and correctness checks stay outside
/// measurement. The synchronous row is the control; the suspended row includes a thread-pool round trip.
/// </remarks>
[MemoryDiagnoser]
public class AsyncHostEntryBenchmark
{
    private Prepared<Script> _script;
    private Engine _sync = null!;
    private Engine _evaluate = null!;
    private Engine _invokeCompleted = null!;
    private Engine _invokeSuspended = null!;

    [GlobalSetup]
    public async Task Setup()
    {
        _script = Engine.PrepareScript("21 + 21");
        _sync = new Engine();
        _evaluate = new Engine();
        _invokeCompleted = new Engine().Execute("async function answer() { return 42; }");
        _invokeSuspended = new Engine()
            .SetValue("yieldAnswer", new Func<Task<int>>(YieldAnswer))
            .Execute("async function answer() { return await yieldAnswer(); }");

        Check(SyncEvaluate());
        Check(await EvaluateAsyncCompleted());
        Check(await InvokeAsyncCompleted());
        Check(await InvokeAsyncSuspended());
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _sync.Dispose();
        _evaluate.Dispose();
        _invokeCompleted.Dispose();
        _invokeSuspended.Dispose();
    }

    [Benchmark(Baseline = true)]
    public JsValue SyncEvaluate() => _sync.Evaluate(in _script);

    [Benchmark]
    public Task<JsValue> EvaluateAsyncCompleted() => _evaluate.EvaluateAsync(in _script);

    [Benchmark]
    public Task<JsValue> InvokeAsyncCompleted() => _invokeCompleted.InvokeAsync("answer");

    [Benchmark]
    public Task<JsValue> InvokeAsyncSuspended() => _invokeSuspended.InvokeAsync("answer");

    private static async Task<int> YieldAnswer()
    {
        await Task.Yield();
        return 42;
    }

    private static void Check(JsValue value)
    {
        if (value.AsNumber() != 42)
        {
            throw new InvalidOperationException("The async host entry did not return 42.");
        }
    }
}
