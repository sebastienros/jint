using BenchmarkDotNet.Attributes;

namespace Jint.Benchmark;

[MemoryDiagnoser]
public class RegExpSplitBenchmark
{
    private const int N = 100;

    private Engine _engine = null!;
    private Prepared<Script> _split;
    private Prepared<Script> _splitWithCaptures;
    private Prepared<Script> _splitUnicode;

    [GlobalSetup]
    public void Setup()
    {
        _engine = new Engine();
        _engine.Execute("""
var text = '';
for (var i = 0; i < 200; i++) {
    text += 'word' + i + '; ';
}
""");

        // .NET engine fast path
        _split = Engine.PrepareScript($"for (var n = 0; n < {N}; n++) {{ text.split(/;\\s/); }}");
        _splitWithCaptures = Engine.PrepareScript($"for (var n = 0; n < {N}; n++) {{ text.split(/(;)\\s/); }}");
        // unicode flag forces the generic exec loop
        _splitUnicode = Engine.PrepareScript($"for (var n = 0; n < {N}; n++) {{ text.split(/;\\s/u); }}");
    }

    [Benchmark]
    public void Split() => _engine.Execute(_split);

    [Benchmark]
    public void SplitWithCaptures() => _engine.Execute(_splitWithCaptures);

    [Benchmark]
    public void SplitUnicode() => _engine.Execute(_splitUnicode);
}
