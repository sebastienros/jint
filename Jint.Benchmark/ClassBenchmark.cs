using BenchmarkDotNet.Attributes;

namespace Jint.Benchmark;

[MemoryDiagnoser]
public class ClassBenchmark
{
    private Engine _engine;

    [IterationSetup]
    public void Setup()
    {
        _engine = new Engine();
        _engine.Execute("""
                        class A { x = 1; }; 
                        class B extends A { y = 2; };
                        class C extends B { z = 3; }; 
                        class D extends C { x2 = 1; };
                        class E extends D { x3 = 1; };
                        class F extends E { x4 = 1; }
                        """);
        _engine.Execute("const target = new F();");
    }

    [Benchmark]
    public void ConstructSimple()
    {
        var script = Engine.PrepareScript("new A();");
        for (var i = 0; i < 400_000; ++i)
        {
            _engine.Evaluate(script);
        }
    }

    [Benchmark]
    public void ConstructDeepInheritance()
    {
        var script = Engine.PrepareScript("new F();");
        for (var i = 0; i < 80_000; ++i)
        {
            _engine.Evaluate(script);
        }
    }

    [Benchmark]
    public void GetSet()
    {
        var script = Engine.PrepareScript("target.x4 = 42; target.x4;");
        for (var i = 0; i < 500_000; ++i)
        {
            _engine.Evaluate(script);
        }
    }
}
