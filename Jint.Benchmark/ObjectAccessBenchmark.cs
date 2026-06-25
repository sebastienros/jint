using BenchmarkDotNet.Attributes;

namespace Jint.Benchmark;

[MemoryDiagnoser]
public class ObjectAccessBenchmark
{
    private readonly Prepared<Script> _script;
    private readonly Prepared<Script> _writeOnlyScript;

    public ObjectAccessBenchmark()
    {
        // Read-then-write of the same property: exercises both the member-read inline cache and the
        // member-write inline cache on a stable object.
        const string Script = @"const summary = { res: 0 }; for (var i =0; i < 1_000_000; ++i){ summary.res = summary.res + 1; }";
        _script = Engine.PrepareScript(Script);

        // Pure write of an existing property (the LHS node is never read), so the write-side inline cache
        // can only warm up via write-miss population — then hit for the remaining iterations.
        const string WriteOnlyScript = @"const summary = { res: 0 }; for (var i =0; i < 1_000_000; ++i){ summary.res = i; }";
        _writeOnlyScript = Engine.PrepareScript(WriteOnlyScript);
    }

    [Benchmark]
    public void UpdateObjectProperty()
    {
        var engine = new Engine();
        engine.Evaluate(_script);
    }

    [Benchmark]
    public void WriteObjectProperty()
    {
        var engine = new Engine();
        engine.Evaluate(_writeOnlyScript);
    }
}
