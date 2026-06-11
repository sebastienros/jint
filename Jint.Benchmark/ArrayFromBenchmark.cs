using BenchmarkDotNet.Attributes;

namespace Jint.Benchmark;

[MemoryDiagnoser]
public class ArrayFromBenchmark
{
    private const int N = 100;

    private Engine _engine = null!;
    private Prepared<Script> _fromSet;
    private Prepared<Script> _fromArray;
    private Prepared<Script> _fromSetWithMapper;

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

        _fromSet = Engine.PrepareScript($"for (var n = 0; n < {N}; n++) {{ Array.from(set); }}");
        _fromArray = Engine.PrepareScript($"for (var n = 0; n < {N}; n++) {{ Array.from(numbers); }}");
        _fromSetWithMapper = Engine.PrepareScript($"for (var n = 0; n < {N}; n++) {{ Array.from(set, function(x) {{ return x * 2; }}); }}");
    }

    [Benchmark]
    public void FromSet() => _engine.Execute(_fromSet);

    [Benchmark]
    public void FromArray() => _engine.Execute(_fromArray);

    [Benchmark]
    public void FromSetWithMapper() => _engine.Execute(_fromSetWithMapper);
}
