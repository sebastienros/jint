using BenchmarkDotNet.Attributes;
using Jint.Native;

namespace Jint.Benchmark;

/// <summary>
/// Reuses one warmed engine and a resolved function per row; construction and parsing are excluded.
/// The control returns the same undefined value without entering the engine.
/// </summary>
[MemoryDiagnoser]
public class HostFunctionCallBenchmark
{
    private Engine _engine = null!;
    private JsValue _callable = null!;
    private readonly JsValue[] _arguments = System.Array.Empty<JsValue>();

    [GlobalSetup(Target = nameof(EmptyCall))]
    public void Setup()
    {
        _engine = new Engine();
        _callable = _engine.Evaluate("(function () {})");
        for (var i = 0; i < 1000; i++)
        {
            _engine.Call(_callable, JsValue.Undefined, _arguments);
        }
    }

    [Benchmark]
    public JsValue EmptyCall() => _engine.Call(_callable, JsValue.Undefined, _arguments);

    [Benchmark]
    public JsValue Control() => JsValue.Undefined;

    [GlobalCleanup(Target = nameof(EmptyCall))]
    public void Cleanup() => _engine.Dispose();
}
