using BenchmarkDotNet.Attributes;
using Jint.Native;

namespace Jint.Benchmark;

/// <summary>
/// Isolates the allocation cost of creating functions that are never used as constructors and whose
/// <c>.prototype</c> is never read — the linq-js cold-start shape, where hundreds of helper functions
/// are declared but essentially none are <c>new</c>-ed. With a lazy <c>.prototype</c> each such function
/// skips its prototype object + two descriptor allocations. <see cref="ConstructEach"/> is the guard:
/// when the functions ARE used as constructors the prototype must still materialize (no allocation win,
/// no regression).
/// </summary>
[MemoryDiagnoser]
[HideColumns("Error", "Gen0", "Gen1", "Gen2")]
public class FunctionDeclarationAllocBenchmark
{
    private Engine _engine = null!;
    private Prepared<Script> _declareMany;
    private Prepared<Script> _constructEach;

    [GlobalSetup]
    public void Setup()
    {
        _declareMany = Engine.PrepareScript("""
            var arr = [];
            for (var i = 0; i < 500; i++) { arr.push(function (a, b) { return a + b; }); }
            arr.length;
            """, strict: true);

        _constructEach = Engine.PrepareScript("""
            var arr = [];
            for (var i = 0; i < 500; i++) { var F = function () { this.x = 1; }; arr.push(new F()); }
            arr.length;
            """, strict: true);

        _engine = new Engine(static options => options.Strict());
        _engine.Evaluate(_declareMany);
        _engine.Evaluate(_constructEach);
    }

    [Benchmark]
    public JsValue DeclareMany() => _engine.Evaluate(_declareMany);

    [Benchmark]
    public JsValue ConstructEach() => _engine.Evaluate(_constructEach);
}
