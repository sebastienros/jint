using BenchmarkDotNet.Attributes;

namespace Jint.Benchmark;

[MemoryDiagnoser]
public class StringBuilderBenchmark
{
    private const string script = @"
var x = 'some string';
";

    private Engine engine;

    [GlobalSetup]
    public void Setup()
    {
        engine = new Engine();
        engine.Execute(script);
    }

    [Benchmark]
    public void One()
    {
        engine.Execute("`hello ${x}`");
    }

    [Benchmark]
    public void Two()
    {
        engine.Execute("`hello ${x}, hello ${x}`");
    }

    [Benchmark]
    public void Three()
    {
        engine.Execute("`hello ${x}, hello ${x}, hello ${x}`");
    }
}