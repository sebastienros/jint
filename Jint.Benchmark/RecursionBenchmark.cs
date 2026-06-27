using BenchmarkDotNet.Attributes;

namespace Jint.Benchmark;

/// <summary>
/// Recursive call shapes, fresh engine per invocation. Direct-recursive functions otherwise allocate
/// a FunctionEnvironment + Binding[] on every call (the single-slot env pool cannot serve recursion);
/// the bounded recursive env pool lets simultaneously live frames reuse env + slots.
///   - Fib / Tak: wide branching recursion (many sibling calls at bounded depth) — the pool's best case.
///   - DeepSum: linear recursion deeper than the pool cap, repeated — the cap-bounded case.
/// </summary>
[MemoryDiagnoser]
[HideColumns("Error", "StdDev", "Median", "Gen0", "Gen1", "Gen2")]
public class RecursionBenchmark
{
    private readonly Prepared<Script> _fib;
    private readonly Prepared<Script> _tak;
    private readonly Prepared<Script> _deepSum;

    public RecursionBenchmark()
    {
        _fib = Engine.PrepareScript("function fib(n){ return n < 2 ? n : fib(n - 1) + fib(n - 2); } fib(30);");

        _tak = Engine.PrepareScript(
            "function tak(x, y, z){ return y < x ? tak(tak(x-1,y,z), tak(y-1,z,x), tak(z-1,x,y)) : z; } tak(18, 12, 6);");

        _deepSum = Engine.PrepareScript(
            "function sum(n){ return n === 0 ? 0 : n + sum(n - 1); } var t = 0; for (var i = 0; i < 2000; i++) { t = sum(400); } t;");
    }

    [Benchmark]
    public void Fib() => new Engine().Evaluate(_fib);

    [Benchmark]
    public void Tak() => new Engine().Evaluate(_tak);

    [Benchmark]
    public void DeepSum() => new Engine().Evaluate(_deepSum);
}
