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

    private Engine _engine;

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

    [IterationSetup]
    public void IterationSetup()
    {
        _engine = CreteEngine();
    }

    private static Engine CreteEngine()
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

        if (Prepared)
        {
            _engine.Execute(_prepared[finalName]);
        }
        else
        {
            _engine.Execute(_files[finalName], finalName);
        }
    }
}
