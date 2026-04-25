using BenchmarkDotNet.Attributes;

namespace Jint.Benchmark;

[MemoryDiagnoser]
public class ArrayFindSearchBenchmark
{
    private const string SetupScript = @"
var data = new Array(1000);
for (var i = 0; i < 1000; i++) data[i] = i;
var notInData = -1;
var middleHit = 500;
var startHit = 0;
";

    private Engine _engine = null!;
    private Prepared<Script> _indexOfHitStart;
    private Prepared<Script> _indexOfHitMid;
    private Prepared<Script> _indexOfMiss;
    private Prepared<Script> _includesHitMid;
    private Prepared<Script> _includesMiss;

    [GlobalSetup]
    public void Setup()
    {
        _engine = new Engine();
        _engine.Execute(SetupScript);

        _indexOfHitStart = Engine.PrepareScript("for (var n = 0; n < 1000; n++) { data.indexOf(startHit); }");
        _indexOfHitMid = Engine.PrepareScript("for (var n = 0; n < 1000; n++) { data.indexOf(middleHit); }");
        _indexOfMiss = Engine.PrepareScript("for (var n = 0; n < 1000; n++) { data.indexOf(notInData); }");
        _includesHitMid = Engine.PrepareScript("for (var n = 0; n < 1000; n++) { data.includes(middleHit); }");
        _includesMiss = Engine.PrepareScript("for (var n = 0; n < 1000; n++) { data.includes(notInData); }");
    }

    [Benchmark]
    public void IndexOf_Hit_Start() => _engine.Execute(_indexOfHitStart);

    [Benchmark]
    public void IndexOf_Hit_Mid() => _engine.Execute(_indexOfHitMid);

    [Benchmark]
    public void IndexOf_Miss() => _engine.Execute(_indexOfMiss);

    [Benchmark]
    public void Includes_Hit_Mid() => _engine.Execute(_includesHitMid);

    [Benchmark]
    public void Includes_Miss() => _engine.Execute(_includesMiss);
}
