using BenchmarkDotNet.Attributes;

namespace Jint.Benchmark;

[MemoryDiagnoser]
public class RegExpMatchBenchmark
{
    private const int N = 100;

    private Engine _engine = null!;
    private Prepared<Script> _matchGlobal;
    private Prepared<Script> _matchGlobalUnicode;

    [GlobalSetup]
    public void Setup()
    {
        _engine = new Engine();
        _engine.Execute("""
var text = '';
for (var i = 0; i < 100; i++) {
    text += 'word' + i + ' number ' + (i * 37) + '; ';
}
""");

        // non-sticky .NET engine path (already exact-sized, regression canary)
        _matchGlobal = Engine.PrepareScript($"for (var n = 0; n < {N}; n++) {{ text.match(/\\d+/g); }}");
        // unicode flag forces the MatchSlow exec loop
        _matchGlobalUnicode = Engine.PrepareScript($"for (var n = 0; n < {N}; n++) {{ text.match(/\\d+/gu); }}");
    }

    [Benchmark]
    public void MatchGlobal() => _engine.Execute(_matchGlobal);

    [Benchmark]
    public void MatchGlobalUnicode() => _engine.Execute(_matchGlobalUnicode);
}
