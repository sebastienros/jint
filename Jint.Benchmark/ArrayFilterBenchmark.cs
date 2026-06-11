using BenchmarkDotNet.Attributes;

namespace Jint.Benchmark;

[MemoryDiagnoser]
public class ArrayFilterBenchmark
{
    private Engine _engine = null!;
    private Prepared<Script> _filter;

    [Params(100, 10_000)]
    public int Size { get; set; }

    /// <summary>
    /// Share of source elements that pass the predicate: 0 exercises the empty-result path,
    /// 50 the growth path, 100 the full-copy worst case.
    /// </summary>
    [Params(0, 50, 100)]
    public int SelectivityPercent { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        _engine = new Engine();
        _engine.Execute($$"""
var testArray = [];
for (var i = 0; i < {{Size}}; i++) {
    testArray.push(i);
}
var threshold = {{Size}} * {{SelectivityPercent}} / 100;
""");
        _filter = Engine.PrepareScript("testArray.filter(function(x) { return x < threshold; });");
    }

    [Benchmark]
    public void Filter() => _engine.Execute(_filter);
}
