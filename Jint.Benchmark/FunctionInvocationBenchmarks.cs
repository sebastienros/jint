using BenchmarkDotNet.Attributes;
using Jint.Native;

namespace Jint.Benchmark;

/// <summary>
/// Source-gen sentinel for Function.prototype.{call,apply,bind}. Post-source-gen these methods take
/// `ICallable thisObject` directly with the generator emitting the cast + TypeError. Warm-path numbers
/// should match the pre-source-gen baseline; the cast emit replaces a manual `as ICallable` + null check.
/// </summary>
[ShortRunJob]
[MemoryDiagnoser]
public class FunctionInvocationBenchmarks
{
    private Engine _warm = null!;
    private Prepared<Script> _call;
    private Prepared<Script> _apply;
    private Prepared<Script> _bind;
    private Prepared<Script> _bindThenCall;

    [GlobalSetup]
    public void GlobalSetup()
    {
        // Each benchmark builds its own function on the engine to keep the call sites monomorphic.
        _call  = Engine.PrepareScript("(function(a, b) { return a + b; }).call(null, 1, 2)");
        _apply = Engine.PrepareScript("(function(a, b) { return a + b; }).apply(null, [1, 2])");
        _bind  = Engine.PrepareScript("(function(a, b) { return a + b; }).bind(null, 1)");
        _bindThenCall = Engine.PrepareScript("var f = (function(a, b) { return a + b; }).bind(null, 1); f(2)");

        _warm = new Engine();
        _warm.Evaluate(_call);
        _warm.Evaluate(_apply);
        _warm.Evaluate(_bind);
        _warm.Evaluate(_bindThenCall);
    }

    [Benchmark]
    public JsValue Warm_Call() => _warm.Evaluate(_call);

    [Benchmark]
    public JsValue Warm_Apply() => _warm.Evaluate(_apply);

    [Benchmark]
    public JsValue Warm_Bind() => _warm.Evaluate(_bind);

    [Benchmark]
    public JsValue Warm_BindThenCall() => _warm.Evaluate(_bindThenCall);
}
