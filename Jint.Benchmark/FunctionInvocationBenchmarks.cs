using BenchmarkDotNet.Attributes;
using Jint.Native;

namespace Jint.Benchmark;

/// <summary>
/// Source-gen sentinel for Function.prototype.{call,apply,bind}. Post-source-gen these methods take
/// `ICallable thisObject` directly with the generator emitting the cast + TypeError. Warm-path numbers
/// should match the pre-source-gen baseline; the cast emit replaces a manual `as ICallable` + null check.
///
/// <para><b>Engine isolation.</b> Each row gets its own engine, warmed with its own script and nothing
/// else (see <see cref="IsolatedScript"/>). It used to be one engine warmed with all four scripts, so
/// each row was measured on an engine carrying the other three rows' handler-tree and call-site state
/// (plus <c>Warm_BindThenCall</c>'s <c>f</c> global), which makes a row's number depend on which siblings
/// exist and on what a change did to <em>them</em>. The rows still measure the warm path, and engine
/// construction and warm-up stay in <c>[GlobalSetup]</c>, outside the measurement. <b>Numbers from this
/// class are not comparable to any published before the harness changed.</b></para>
/// </summary>
[MemoryDiagnoser]
public class FunctionInvocationBenchmarks
{
    private IsolatedScript _call;
    private IsolatedScript _apply;
    private IsolatedScript _bind;
    private IsolatedScript _bindThenCall;

    [GlobalSetup]
    public void GlobalSetup()
    {
        // Each benchmark builds its own function on the engine to keep the call sites monomorphic.
        _call  = IsolatedScript.Warm("(function(a, b) { return a + b; }).call(null, 1, 2)");
        _apply = IsolatedScript.Warm("(function(a, b) { return a + b; }).apply(null, [1, 2])");
        _bind  = IsolatedScript.Warm("(function(a, b) { return a + b; }).bind(null, 1)");
        _bindThenCall = IsolatedScript.Warm("var f = (function(a, b) { return a + b; }).bind(null, 1); f(2)");
    }

    [Benchmark]
    public JsValue Warm_Call() => _call.Run();

    [Benchmark]
    public JsValue Warm_Apply() => _apply.Run();

    [Benchmark]
    public JsValue Warm_Bind() => _bind.Run();

    [Benchmark]
    public JsValue Warm_BindThenCall() => _bindThenCall.Run();
}
