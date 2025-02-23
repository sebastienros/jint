using BenchmarkDotNet.Attributes;

namespace Jint.Benchmark;

[MemoryDiagnoser]
public class FunctionBenchmark
{
    private readonly Engine _engine;

    public FunctionBenchmark()
    {
        _engine = new Engine();
        _engine.Execute("function objectPattern({ toMessage: t, code: e, reasonCode: s, syntaxPlugin: r }) {  return \"MissingPlugin\" === s || \"MissingOneOfPlugins\" === s; }");
    }

    [Benchmark]
    public bool ObjectPattern()
    {
        var b = true;
        for (var i = 0; i < 100; ++i)
        {
            b &= _engine.Evaluate("objectPattern({\"reasonCode\": \"MissingPlugin\"})").AsBoolean();
        }

        return b;
    }
}
