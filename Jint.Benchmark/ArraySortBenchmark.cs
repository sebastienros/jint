using BenchmarkDotNet.Attributes;

namespace Jint.Benchmark;

[MemoryDiagnoser]
public class ArraySortBenchmark
{
    private const string SetupScript = @"
function makeRandom(n) {
    var a = new Array(n);
    var seed = 0xCAFEBABE;
    for (var i = 0; i < n; i++) {
        seed = (seed * 1664525 + 1013904223) | 0;
        a[i] = (seed >>> 0) % 1000000;
    }
    return a;
}
function makeSorted(n) {
    var a = new Array(n);
    for (var i = 0; i < n; i++) a[i] = i;
    return a;
}
function makeReverse(n) {
    var a = new Array(n);
    for (var i = 0; i < n; i++) a[i] = n - i;
    return a;
}
var random100 = makeRandom(100);
var random1k = makeRandom(1000);
var random10k = makeRandom(10000);
var sorted1k = makeSorted(1000);
var reverse1k = makeReverse(1000);
";

    private Engine _engine = null!;
    private Prepared<Script> _sortRandom100;
    private Prepared<Script> _sortRandom1k;
    private Prepared<Script> _sortRandom10k;
    private Prepared<Script> _sortAlreadySorted1k;
    private Prepared<Script> _sortReverseSorted1k;
    private Prepared<Script> _sortWithComparer1k;

    [GlobalSetup]
    public void Setup()
    {
        _engine = new Engine();
        _engine.Execute(SetupScript);

        // each [Benchmark] performs a fresh copy then sorts it; copy cost is constant across runs
        _sortRandom100 = Engine.PrepareScript("for (var n = 0; n < 200; n++) { random100.slice().sort(); }");
        _sortRandom1k = Engine.PrepareScript("for (var n = 0; n < 50; n++) { random1k.slice().sort(); }");
        _sortRandom10k = Engine.PrepareScript("for (var n = 0; n < 5; n++) { random10k.slice().sort(); }");
        _sortAlreadySorted1k = Engine.PrepareScript("for (var n = 0; n < 50; n++) { sorted1k.slice().sort(); }");
        _sortReverseSorted1k = Engine.PrepareScript("for (var n = 0; n < 50; n++) { reverse1k.slice().sort(); }");
        _sortWithComparer1k = Engine.PrepareScript("for (var n = 0; n < 50; n++) { random1k.slice().sort(function(a,b){return a-b;}); }");
    }

    [Benchmark]
    public void SortRandom_100() => _engine.Execute(_sortRandom100);

    [Benchmark]
    public void SortRandom_1K() => _engine.Execute(_sortRandom1k);

    [Benchmark]
    public void SortRandom_10K() => _engine.Execute(_sortRandom10k);

    [Benchmark]
    public void SortAlreadySorted_1K() => _engine.Execute(_sortAlreadySorted1k);

    [Benchmark]
    public void SortReverseSorted_1K() => _engine.Execute(_sortReverseSorted1k);

    [Benchmark]
    public void SortWithComparer_1K() => _engine.Execute(_sortWithComparer1k);
}
