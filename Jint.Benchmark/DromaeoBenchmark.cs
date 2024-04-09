using BenchmarkDotNet.Attributes;

namespace Jint.Benchmark;

[MemoryDiagnoser]
public class DromaeoBenchmark
{
    private static readonly Dictionary<string, string> _files = new()
    {
        {"dromaeo-3d-cube", null},
        {"dromaeo-core-eval", null},
        {"dromaeo-object-array", null},
        {"dromaeo-object-regexp", null},
        {"dromaeo-object-string", null},
        {"dromaeo-string-base64", null}
    };

    private readonly Dictionary<string, Prepared<Script>> _prepared = new();

    private Engine engine;

    [GlobalSetup]
    public void Setup()
    {
        foreach (var fileName in _files.Keys)
        {
            var script = File.ReadAllText($"Scripts/{fileName}.js");
            _files[fileName] = script;
            _prepared[fileName] = Engine.PrepareScript(script);
        }

        engine = new Engine()
            .SetValue("log", new Action<object>(Console.WriteLine))
            .SetValue("assert", new Action<bool>(b => { }));

        engine.Execute(@"
var startTest = function () { };
var test = function (name, fn) { fn(); };
var endTest = function () { };
var prep = function (fn) { fn(); };
");
    }

    [ParamsSource(nameof(FileNames))]
    public string FileName { get; set; }

    [Params(true, false)]
    public bool Prepared { get; set; }

    public IEnumerable<string> FileNames()
    {
        foreach (var entry in _files)
        {
            yield return entry.Key;
        }
    }

    [Benchmark]
    public void Run()
    {
        if (Prepared)
        {
            engine.Execute(_prepared[FileName]);
        }
        else
        {
            engine.Execute(_files[FileName]);
        }
    }
}
