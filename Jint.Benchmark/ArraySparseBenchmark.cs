using BenchmarkDotNet.Attributes;

namespace Jint.Benchmark;

[MemoryDiagnoser]
public class ArraySparseBenchmark
{
    private Engine _engine = null!;
    private Prepared<Script> _convertSmall;
    private Prepared<Script> _convertLarge;

    [GlobalSetup]
    public void Setup()
    {
        _engine = new Engine();

        // Each outer iteration creates a fresh dense array of N elements then triggers
        // dense->sparse conversion via a single far-away write.
        _convertSmall = Engine.PrepareScript(@"
for (var n = 0; n < 100; n++) {
    var a = [];
    for (var i = 0; i < 200; i++) a[i] = i;
    a[5000] = 1;
}");

        _convertLarge = Engine.PrepareScript(@"
for (var n = 0; n < 50; n++) {
    var a = [];
    for (var i = 0; i < 2000; i++) a[i] = i;
    a[50000] = 1;
}");
    }

    [Benchmark]
    public void ConvertSmallToSparse() => _engine.Execute(_convertSmall);

    [Benchmark]
    public void ConvertLargeToSparse() => _engine.Execute(_convertLarge);
}
