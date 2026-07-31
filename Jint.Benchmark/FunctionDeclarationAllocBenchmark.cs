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
///
/// <para><b>Engine isolation.</b> Each row gets its own engine, warmed with its own script and nothing
/// else (see <see cref="IsolatedScript"/>). It used to be one engine warmed with both scripts, so each
/// row was measured on an engine already carrying the other row's <c>arr</c> global (both declare it, so
/// they collided outright) plus its handler-tree, call-site and function-prototype state — the very
/// state this class exists to size. The rows still measure the warm declaration path, and engine
/// construction and warm-up stay in <c>[GlobalSetup]</c>, outside the measurement. <b>Numbers from this
/// class are not comparable to any published before the harness changed.</b></para>
/// </summary>
[MemoryDiagnoser]
[HideColumns("Error", "Gen0", "Gen1", "Gen2")]
public class FunctionDeclarationAllocBenchmark
{
    private IsolatedScript _declareMany;
    private IsolatedScript _constructEach;

    private static Engine CreateEngine() => new(static options => options.Strict());

    [GlobalSetup]
    public void Setup()
    {
        _declareMany = IsolatedScript.Warm(Engine.PrepareScript("""
            var arr = [];
            for (var i = 0; i < 500; i++) { arr.push(function (a, b) { return a + b; }); }
            arr.length;
            """, strict: true), CreateEngine);

        _constructEach = IsolatedScript.Warm(Engine.PrepareScript("""
            var arr = [];
            for (var i = 0; i < 500; i++) { var F = function () { this.x = 1; }; arr.push(new F()); }
            arr.length;
            """, strict: true), CreateEngine);
    }

    [Benchmark]
    public JsValue DeclareMany() => _declareMany.Run();

    [Benchmark]
    public JsValue ConstructEach() => _constructEach.Run();
}
