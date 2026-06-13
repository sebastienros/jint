using BenchmarkDotNet.Attributes;

namespace Jint.Benchmark;

[MemoryDiagnoser]
public class ArrayFlatBenchmark
{
    private const int N = 100;

    private Engine _engine = null!;
    private Prepared<Script> _flatShallow;
    private Prepared<Script> _flatDeep;
    private Prepared<Script> _flatMap;

    [GlobalSetup]
    public void Setup()
    {
        _engine = new Engine();
        _engine.Execute("""
var nested = [];
for (var i = 0; i < 100; i++) {
    var inner = [];
    for (var j = 0; j < 10; j++) {
        inner.push(i * 10 + j);
    }
    nested.push(inner);
}
var deep = [1, [2, [3, [4, [5, [6, [7, [8, [9, [10]]]]]]]]]];
var flatArray = [];
for (var i = 0; i < 1000; i++) {
    flatArray.push(i);
}
""");

        _flatShallow = Engine.PrepareScript($"for (var n = 0; n < {N}; n++) {{ nested.flat(); }}");
        _flatDeep = Engine.PrepareScript($"for (var n = 0; n < {N}; n++) {{ deep.flat(Infinity); }}");
        _flatMap = Engine.PrepareScript($"for (var n = 0; n < {N}; n++) {{ flatArray.flatMap(function(x) {{ return [x, x]; }}); }}");
    }

    [Benchmark]
    public void FlatShallow() => _engine.Execute(_flatShallow);

    [Benchmark]
    public void FlatDeep() => _engine.Execute(_flatDeep);

    [Benchmark]
    public void FlatMap() => _engine.Execute(_flatMap);
}
