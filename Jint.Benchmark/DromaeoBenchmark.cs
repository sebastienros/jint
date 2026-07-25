using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;

namespace Jint.Benchmark;

[MemoryDiagnoser]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByMethod)]
public class DromaeoBenchmark
{
    private static readonly Dictionary<string, string> _files = new()
    {
        { "dromaeo-3d-cube", null },
        { "dromaeo-core-eval", null },
        { "dromaeo-object-array", null },
        { "dromaeo-object-regexp", null },
        { "dromaeo-object-string", null },
        { "dromaeo-string-base64", null }
    };

    private readonly Dictionary<string, Prepared<Script>> _prepared = new();

    [GlobalSetup]
    public void Setup()
    {
        foreach (var fileName in _files.Keys.ToArray())
        {
            foreach (var suffix in new[] {"", "-modern"})
            {
                var name = fileName + suffix;
                var script = File.ReadAllText($"Scripts/{name}.js");
                _files[name] = script;
                _prepared[name] = Engine.PrepareScript(script, name);
            }
        }
    }

    // Deliberately NOT an [IterationSetup]. Each op needs a fresh engine (the modern scripts
    // declare top-level `let`, so the same script cannot run twice in one engine), but
    // [IterationSetup] forces InvocationCount=1 and UnrollFactor=1, which BenchmarkDotNet
    // documents as unsuitable for anything under ~100ms. With one op per iteration, BDN's
    // adaptive warmup judged convergence from single-op samples: tier-0 code is uniformly slow,
    // so eight flat 17.8ms warmup samples looked converged, warmup ended, and tiered
    // compilation then completed *inside* WorkloadActual — a measured ramp from 17.8ms down to
    // 3.4ms still descending at iteration 45. The reported Mean averaged that ramp, so identical
    // code produced 2.489ms in one run and 9.811ms in another.
    //
    // Building the engine inside the benchmark method instead lets BDN auto-scale
    // InvocationCount (the pilot targets ~500ms per iteration), so tiering finishes during the
    // pilot and both warmup and measurement run tier-1 code. This is what
    // EngineComparisonBenchmark already does, which is why its results are stable. Engine
    // construction now counts toward the measurement: ~0.1-0.3ms against a 5-70ms op, constant
    // across revisions, so A/B comparisons stay valid.
    private static Engine CreateEngine()
    {
        var engine = new Engine()
            .SetValue("log", new Action<object>(Console.WriteLine))
            .SetValue("assert", new Action<bool>(b => { }));

        engine.Execute("""

                       var startTest = function () { };
                       var test = function (name, fn) { fn(); };
                       var endTest = function () { };
                       var prep = function (fn) { fn(); };

                       """);

        return engine;
    }

    [Params(false, true, Priority = 50)]
    public bool Modern { get; set; }

    [Params(true, false, Priority = 100)]
    public bool Prepared { get; set; }

    [Benchmark]
    public void CoreEval()
    {
        Run("dromaeo-core-eval");
    }

    [Benchmark]
    public void Cube()
    {
        Run("dromaeo-3d-cube");
    }

    [Benchmark]
    public void ObjectArray()
    {
        Run("dromaeo-object-array");
    }

    [Benchmark]
    public void ObjectRegExp()
    {
        Run("dromaeo-object-regexp");
    }

    [Benchmark]
    public void ObjectString()
    {
        Run("dromaeo-object-string");
    }

    [Benchmark]
    public void StringBase64()
    {
        Run("dromaeo-string-base64");
    }

    private void Run(string fileName)
    {
        var finalName = Modern ? fileName + "-modern" : fileName;
        var engine = CreateEngine();

        if (Prepared)
        {
            engine.Execute(_prepared[finalName]);
        }
        else
        {
            engine.Execute(_files[finalName], finalName);
        }
    }
}
