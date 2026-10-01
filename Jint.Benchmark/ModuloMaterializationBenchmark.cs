using BenchmarkDotNet.Attributes;
using Jint.Native;

namespace Jint.Benchmark;

/// <summary>
/// Measures modulo operand materialization on a private warmed engine per row. Engine creation,
/// parsing and warmup are outside measurement. Each script runs 100,000 iterations to dominate
/// host entry overhead. PropertyRead is an untouched control with no modulo expression.
/// </summary>
[MemoryDiagnoser]
public class ModuloMaterializationBenchmark
{
    private IsolatedScript _arithmetic;
    private IsolatedScript _property;
    private IsolatedScript _callback;

    [GlobalSetup]
    public void Setup()
    {
        _arithmetic = IsolatedScript.Warm("(() => { let sum = 0; for (let i = 0; i < 100000; i++) sum += i % 97; return String(sum); })()");
        _property = IsolatedScript.Warm("(() => { const o = {n: 17}; let sum = 0; for (let i = 0; i < 100000; i++) sum += o.n; return sum; })()");
        _callback = IsolatedScript.Warm("(() => { function f(n) { return n % 97; } let sum = 0; for (let i = 0; i < 100000; i++) sum += f(i); return sum; })()");
    }

    [Benchmark]
    public JsValue Arithmetic() => _arithmetic.Run();

    [Benchmark]
    public JsValue PropertyRead() => _property.Run();

    [Benchmark]
    public JsValue Callback() => _callback.Run();
}
