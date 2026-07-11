using System.Text;
using BenchmarkDotNet.Attributes;

namespace Jint.Benchmark;

/// <summary>
/// The constructor allocation-site promote window on a FRESH engine per op — the short-lived /
/// per-request embedding shape. Constructors currently build dictionary-mode instances until the
/// 16th construction, so the x8 rows sit entirely inside that window while the x1000 rows show the
/// promoted steady state; <see cref="EngineOnly"/> is the baseline so every row reads as a delta
/// over bare engine construction. <see cref="DistinctCtors200"/> is the diverse-ctor guard: 200
/// different one-shot constructors, the shape that must never regress when the window shrinks.
/// </summary>
[MemoryDiagnoser]
public class ColdConstructorBenchmark
{
    private Prepared<Script> _functionCtorX8;
    private Prepared<Script> _functionCtorX64;
    private Prepared<Script> _functionCtorX1000;
    private Prepared<Script> _classCtorX8;
    private Prepared<Script> _classCtorX1000;
    private Prepared<Script> _distinctCtors200;

    internal const string FunctionCtorX8Source = """
        function Rec(i) { this.a = i; this.b = i + 1; this.c = i + 2; this.d = i + 3; this.e = i + 4; this.f = i + 5; }
        var arr = [];
        for (var i = 0; i < 8; i++) { arr.push(new Rec(i)); }
        arr.length;
        """;

    internal const string ClassCtorX8Source = """
        class Rec {
            constructor(i) { this.a = i; this.b = i + 1; this.c = i + 2; this.d = i + 3; this.e = i + 4; this.f = i + 5; }
        }
        var arr = [];
        for (var i = 0; i < 8; i++) { arr.push(new Rec(i)); }
        arr.length;
        """;

    /// <summary>200 distinct constructors, each constructed exactly once.</summary>
    internal static string BuildDistinctCtorsSource()
    {
        var sb = new StringBuilder(32 * 1024);
        sb.AppendLine("var arr = [];");
        for (var i = 0; i < 200; i++)
        {
            sb.Append("function C").Append(i).Append("(v) { this.a = v; this.b = v + 1; this.c = v + 2; this.d = v + 3; }")
                .Append(" arr.push(new C").Append(i).Append('(').Append(i).AppendLine("));");
        }

        sb.AppendLine("arr.length;");
        return sb.ToString();
    }

    private static string BuildCtorLoopSource(string declaration, int count) => $$"""
        {{declaration}}
        var arr = [];
        for (var i = 0; i < {{count}}; i++) { arr.push(new Rec(i)); }
        arr.length;
        """;

    [GlobalSetup]
    public void Setup()
    {
        const string FunctionDecl = "function Rec(i) { this.a = i; this.b = i + 1; this.c = i + 2; this.d = i + 3; this.e = i + 4; this.f = i + 5; }";
        const string ClassDecl = """
            class Rec {
                constructor(i) { this.a = i; this.b = i + 1; this.c = i + 2; this.d = i + 3; this.e = i + 4; this.f = i + 5; }
            }
            """;

        _functionCtorX8 = Engine.PrepareScript(FunctionCtorX8Source);
        _functionCtorX64 = Engine.PrepareScript(BuildCtorLoopSource(FunctionDecl, 64));
        _functionCtorX1000 = Engine.PrepareScript(BuildCtorLoopSource(FunctionDecl, 1000));
        _classCtorX8 = Engine.PrepareScript(ClassCtorX8Source);
        _classCtorX1000 = Engine.PrepareScript(BuildCtorLoopSource(ClassDecl, 1000));
        _distinctCtors200 = Engine.PrepareScript(BuildDistinctCtorsSource());
    }

    [Benchmark(Baseline = true)]
    public Engine EngineOnly() => new Engine();

    [Benchmark]
    public Engine FunctionCtor_x8()
    {
        var engine = new Engine();
        engine.Execute(_functionCtorX8);
        return engine;
    }

    [Benchmark]
    public Engine FunctionCtor_x64()
    {
        var engine = new Engine();
        engine.Execute(_functionCtorX64);
        return engine;
    }

    [Benchmark]
    public Engine FunctionCtor_x1000()
    {
        var engine = new Engine();
        engine.Execute(_functionCtorX1000);
        return engine;
    }

    [Benchmark]
    public Engine ClassCtor_x8()
    {
        var engine = new Engine();
        engine.Execute(_classCtorX8);
        return engine;
    }

    [Benchmark]
    public Engine ClassCtor_x1000()
    {
        var engine = new Engine();
        engine.Execute(_classCtorX1000);
        return engine;
    }

    [Benchmark]
    public Engine DistinctCtors200()
    {
        var engine = new Engine();
        engine.Execute(_distinctCtors200);
        return engine;
    }
}
