using BenchmarkDotNet.Attributes;
using Jint.Native;

namespace Jint.Benchmark;

/// <summary>
/// What a cancellable but never-cancelled token costs an <c>*Async</c> call on an engine with no constraints
/// — the shape of an ASP.NET host passing <c>HttpContext.RequestAborted</c>. Such a token arms the
/// interpreter's amortized check cadence (every 64 statements) for the duration of the call, which an
/// engine with nothing registered otherwise never runs.
///
/// <para><b>Rows.</b> <see cref="Evaluate"/> is the control: the synchronous entry takes no token, so no
/// change to the asynchronous entries can reach it, and any delta it shows is the cross-process floor.
/// <see cref="EvaluateAsyncWithoutToken"/> is the asynchronous entry with nothing to observe, and
/// <see cref="EvaluateAsyncWithLiveToken"/> is the subject: the same call with a token that can be
/// cancelled and never is.</para>
///
/// <para><b>Engine isolation.</b> Each op builds its own engine, exactly as <see cref="SunSpiderBenchmark"/>
/// does and for its reasons, so no row ever runs on an engine another row or another op has touched. The
/// script is parsed once, in <c>[GlobalSetup]</c>, and the prepared script is shared — a
/// <see cref="Prepared{T}"/> is engine-neutral — so parsing stays out of the measurement while engine
/// construction (roughly 0.1-0.3 ms against ops of a few to tens of milliseconds) is in every row
/// alike.</para>
///
/// <para>Written against the public API only, and only against members that predate the change it measures,
/// so the class can be copied into a baseline worktree unchanged.</para>
/// </summary>
[MemoryDiagnoser]
public class AsyncEntryTokenBenchmark
{
    private Prepared<Script> _script;
    private CancellationTokenSource _live = null!;

    [Params("3d-cube", "access-nbody", "bitops-bitwise-and", "crypto-md5")]
    public string FileName { get; set; } = null!;

    [GlobalSetup]
    public void Setup()
    {
        _script = Engine.PrepareScript(File.ReadAllText($"Scripts/{FileName}.js"));
        _live = new CancellationTokenSource();
    }

    [GlobalCleanup]
    public void Cleanup() => _live.Dispose();

    [Benchmark]
    public JsValue Evaluate() => CreateEngine().Evaluate(in _script);

    [Benchmark]
    public Task<JsValue> EvaluateAsyncWithoutToken() => CreateEngine().EvaluateAsync(in _script);

    [Benchmark]
    public Task<JsValue> EvaluateAsyncWithLiveToken() => CreateEngine().EvaluateAsync(in _script, _live.Token);

    private static Engine CreateEngine()
    {
        return new Engine()
            .SetValue("log", new Action<object>(Console.WriteLine))
            .SetValue("assert", new Action<bool>(_ => { }));
    }
}
