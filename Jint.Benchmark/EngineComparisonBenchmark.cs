using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Order;
using BenchmarkDotNet.Reports;
using Okojo.Parsing;
using Okojo.Runtime;
using OkojoCompiler = Okojo.Compiler.JsCompiler;

namespace Jint.Benchmark;

[Config(typeof(Config))]
[RankColumn]
[MemoryDiagnoser]
[Orderer(SummaryOrderPolicy.FastestToSlowest)]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByParams)]
[HideColumns("Error", "Gen0", "Gen1", "Gen2")]
[BenchmarkCategory("EngineComparison")]
public class EngineComparisonBenchmark
{
    // Widen the parameter column so full script names such as "dromaeo-object-string-modern"
    // are printed instead of BenchmarkDotNet's default 20-char truncation ("droma(...)odern [28]").
    public sealed class Config : ManualConfig
    {
        public Config() => WithSummaryStyle(SummaryStyle.Default.WithMaxParameterColumnWidth(40));
    }

    private static readonly Dictionary<string, Prepared<Script>> _parsedScripts = new();

    // Okojo has no engine-level strict switch (unlike the other engines' flags); the whole program is
    // made strict by a leading "use strict" directive, so it runs in global strict mode like the rest.
    private static readonly Dictionary<string, string> _okojoFiles = new();

    // Okojo's realm-independent prepared artifact is the parsed program (JsProgram). Bytecode
    // (JsScript) is compiled against a specific realm, so — mirroring the fresh-engine-per-iteration
    // rule — Okojo_Prepared reuses the parsed program and compiles+runs it against each run's own
    // realm. The gap to the Okojo lane is parsing cost, matching Jint_ParsedScript.
    private static readonly Dictionary<string, JsProgram> _okojoPrograms = new();

    private static readonly Dictionary<string, string> _files = new()
    {
        { "array-stress", null },
        { "evaluation", null },
        { "evaluation-modern", null },
        { "json-parse", null },
        { "json-parse-modern", null },
        { "linq-js", null },
        { "minimal", null },
        { "stopwatch", null },
        { "stopwatch-modern", null },
        { "dromaeo-3d-cube", null },
        { "dromaeo-3d-cube-modern", null },
        { "dromaeo-core-eval", null },
        { "dromaeo-core-eval-modern", null },
        { "dromaeo-object-array", null },
        { "dromaeo-object-array-modern", null },
        { "dromaeo-object-regexp", null },
        { "dromaeo-object-regexp-modern", null },
        { "dromaeo-object-string", null },
        { "dromaeo-object-string-modern", null },
        { "dromaeo-string-base64", null },
        { "dromaeo-string-base64-modern", null },
    };

    private static readonly string _dromaeoHelpers = @"
        var startTest = function () { };
        var test = function (name, fn) { fn(); };
        var endTest = function () { };
        var prep = function (fn) { fn(); };";

    [GlobalSetup]
    public void Setup()
    {
        foreach (var fileName in _files.Keys.ToList())
        {
            var script = File.ReadAllText($"Scripts/{fileName}.js");
            if (fileName.Contains("dromaeo"))
            {
                script = _dromaeoHelpers + Environment.NewLine + Environment.NewLine + script;
            }
            _files[fileName] = script;
            _parsedScripts[fileName] = Engine.PrepareScript(script, strict: true);

            var okojoSource = "\"use strict\";" + Environment.NewLine + script;
            _okojoFiles[fileName] = okojoSource;
            _okojoPrograms[fileName] = JavaScriptParser.ParseScript(okojoSource);
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
    public void Okojo()
    {
        using var runtime = JsRuntime.Create();
        runtime.Execute(_okojoFiles[FileName]);
    }

    [Benchmark]
    public void Okojo_Prepared()
    {
        using var runtime = JsRuntime.Create();
        var script = OkojoCompiler.Compile(runtime.MainRealm, _okojoPrograms[FileName]);
        runtime.Execute(script);
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
}
