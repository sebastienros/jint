using BenchmarkDotNet.Attributes;
using Esprima.Ast;

namespace Jint.Benchmark;

[MemoryDiagnoser]
public class ObjectAccessBenchmark
{
    private readonly Script _script;

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
