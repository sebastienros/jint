using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Order;
using Esprima;
using Esprima.Ast;

namespace Jint.Benchmark;

[RankColumn]
[MemoryDiagnoser]
[Orderer(SummaryOrderPolicy.FastestToSlowest)]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByParams)]
[HideColumns("Error", "Gen0", "Gen1", "Gen2")]
[BenchmarkCategory("EngineComparison")]
public class EngineComparisonBenchmark
{
    private static readonly Dictionary<string, Script> _parsedScripts = new();

    private static readonly Dictionary<string, string> _files = new()
    {
        { "array-stress", null },
        { "evaluation", null },
        { "linq-js", null },
        { "minimal", null },
        { "stopwatch", null },
        { "dromaeo-3d-cube", null },
        { "dromaeo-core-eval", null },
        { "dromaeo-object-array", null },
        { "dromaeo-object-regexp", null },
        { "dromaeo-object-string", null },
        { "dromaeo-string-base64", null },
    };

    private static readonly string _dromaeoHelpers = @"
        var startTest = function () { };
        var test = function (name, fn) { fn(); };
        var endTest = function () { };
        var prep = function (fn) { fn(); };";

    [GlobalSetup]
    public void Setup()
    {
        var javaScriptParser = new JavaScriptParser();
        foreach (var fileName in _files.Keys.ToList())
        {
            var script = File.ReadAllText($"Scripts/{fileName}.js");
            if (fileName.Contains("dromaeo"))
            {
                script = _dromaeoHelpers + Environment.NewLine + Environment.NewLine + script;
            }
            _files[fileName] = script;
            _parsedScripts[fileName] = javaScriptParser.ParseScript(script, strict: true);
        }
    }

    [ParamsSource(nameof(FileNames))]
    public string FileName { get; set; }

    public IEnumerable<string> FileNames()
    {
        return _files.Select(entry => entry.Key);
    }

    [Benchmark]
    public void Jint()
    {
        var engine = new Engine(static options => options.Strict());
        engine.Execute(_files[FileName]);
    }

    [Benchmark]
    public void Jint_ParsedScript()
    {
        var engine = new Engine(static options => options.Strict());
        engine.Execute(_parsedScripts[FileName]);
    }

    [Benchmark]
    public void Jurassic()
    {
        var engine = new Jurassic.ScriptEngine { ForceStrictMode = true };
        engine.Execute(_files[FileName]);
    }

    [Benchmark]
    public void NilJS()
    {
        var engine = new NiL.JS.Core.Context(strict: true);
        engine.Eval(_files[FileName]);
    }

    [Benchmark]
    public void YantraJS()
    {
        var engine = new YantraJS.Core.JSContext();
        // By default YantraJS is strict mode only, in strict mode
        // we need to pass `this` explicitly in global context
        // if script is expecting global context as `this`
        engine.Eval(_files[FileName], null, engine);
    }

    [Benchmark]
    public void ClearScript()
    {
       var engine = new Microsoft.ClearScript.V8.V8ScriptEngine();
       engine.Evaluate(_files[FileName]);
    }
}
