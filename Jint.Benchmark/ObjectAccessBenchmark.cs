using BenchmarkDotNet.Attributes;

namespace Jint.Benchmark;

[MemoryDiagnoser]
public class ObjectAccessBenchmark
{
    private readonly Prepared<Script> _script;

    public ObjectAccessBenchmark()
    {
        const string Script = @"const summary = { res: 0; }; for (var i =0; i < 1_000_000; ++i){ summary.res = summary.res + 1; }";
        _script = Engine.PrepareScript(Script);
    }

    [Benchmark]
    public void UpdateObjectProperty()
    {
        var engine = new Engine();
        engine.Evaluate(_script);
    }
}
