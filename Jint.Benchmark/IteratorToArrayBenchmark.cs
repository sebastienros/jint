using BenchmarkDotNet.Attributes;

namespace Jint.Benchmark;

[MemoryDiagnoser]
public class IteratorToArrayBenchmark
{
    private const int N = 100;

    private Engine _engine = null!;
    private Prepared<Script> _arrayValuesToArray;
    private Prepared<Script> _setValuesToArray;
    private Prepared<Script> _helperChainToArray;

    [GlobalSetup]
    public void Setup()
    {
        _engine = new Engine();
        _engine.Execute("""
var numbers = [];
for (var i = 0; i < 1000; i++) {
    numbers.push(i);
}
var set = new Set(numbers);
""");

        _arrayValuesToArray = Engine.PrepareScript($"for (var n = 0; n < {N}; n++) {{ numbers.values().toArray(); }}");
        _setValuesToArray = Engine.PrepareScript($"for (var n = 0; n < {N}; n++) {{ set.values().toArray(); }}");
        _helperChainToArray = Engine.PrepareScript($"for (var n = 0; n < {N}; n++) {{ numbers.values().map(function(x) {{ return x * 2; }}).toArray(); }}");
    }

    [Benchmark]
    public void ArrayValuesToArray() => _engine.Execute(_arrayValuesToArray);

    [Benchmark]
    public void SetValuesToArray() => _engine.Execute(_setValuesToArray);

    [Benchmark]
    public void HelperChainToArray() => _engine.Execute(_helperChainToArray);
}
