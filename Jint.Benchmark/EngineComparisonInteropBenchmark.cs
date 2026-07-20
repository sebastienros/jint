using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Order;
using Microsoft.ClearScript.V8;
using Microsoft.ClearScript.V8.FastProxy;
using YantraClrProxy = YantraJS.Core.Clr.ClrProxy;
using YantraContext = YantraJS.Core.JSContext;
using YantraString = YantraJS.Core.JSString;
using YantraValue = YantraJS.Core.JSValue;

namespace Jint.Benchmark;

/// <summary>
/// Compares the cost of script → host ("interop") traffic across the engines: the same scripts
/// drive a loop of host method calls, host property reads/writes, strings crossing the boundary
/// and host array traversal.
/// </summary>
/// <remarks>
/// <para>
/// Okojo is absent because its public API cannot enable CLR access (0.1.2-preview.1 exposes no
/// equivalent of the AllowClrAccess option its own error message refers to).
/// </para>
/// <para>
/// The host members are lowercase on purpose: YantraJS camel-cases CLR member names while the
/// other engines surface them verbatim, and already-lowercase names are the fixed point of both
/// conventions — letting every engine run byte-identical scripts.
/// </para>
/// <para>
/// Each script validates its aggregate and throws on a mismatch, so an engine that silently
/// mis-marshals (e.g. returns undefined) fails the benchmark instead of posting a fantasy time.
/// </para>
/// </remarks>
[Config(typeof(EngineComparisonBenchmark.Config))]
[RankColumn]
[MemoryDiagnoser]
[Orderer(SummaryOrderPolicy.FastestToSlowest)]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByParams)]
[HideColumns("Error", "Gen0", "Gen1", "Gen2")]
[BenchmarkCategory("EngineComparison", "EngineComparisonInterop")]
public class EngineComparisonInteropBenchmark
{
    public class InteropHost
    {
        public int value { get; set; }
        public int[] numbers { get; } = Enumerable.Range(0, 100).ToArray();
        public int count => numbers.Length;
        public int add(int a, int b) => a + b;
        public string concat(string a, string b) => a + b;
    }

    // ClearScript's recommended low-overhead interop path (the FastProxy API introduced in 7.5):
    // members are registered explicitly and marshal without allocation for fundamental types.
    public sealed class FastInteropHost : V8FastHostObject<FastInteropHost>
    {
        private int _value;
        private readonly int[] _numbers = Enumerable.Range(0, 100).ToArray();

        static FastInteropHost()
        {
            Configure(static configuration =>
            {
                configuration.AddPropertyAccessors("value",
                    static (FastInteropHost self, in V8FastResult result) => result.Set(self._value),
                    static (FastInteropHost self, in V8FastArg value) => self._value = value.GetInt32("value"));
                configuration.AddPropertyGetter("count",
                    static (FastInteropHost self, in V8FastResult result) => result.Set(self._numbers.Length));
                configuration.AddPropertyGetter("numbers",
                    static (FastInteropHost self, in V8FastResult result) => result.Set(self._numbers));
                configuration.AddMethodGetter("add", 2,
                    static (FastInteropHost self, in V8FastArgs args, in V8FastResult result) =>
                        result.Set(args.GetInt32(0, "a") + args.GetInt32(1, "b")));
                configuration.AddMethodGetter("concat", 2,
                    static (FastInteropHost self, in V8FastArgs args, in V8FastResult result) =>
                        result.Set(args.GetString(0, "a") + args.GetString(1, "b")));
            });
        }
    }

    private static readonly Dictionary<string, string> _files = new()
    {
        { "interop-method-calls", null },
        { "interop-property-access", null },
        { "interop-string-passing", null },
        { "interop-collection-traversal", null },
    };

    private static readonly Dictionary<string, string> _strictFiles = new();

    [GlobalSetup]
    public void Setup()
    {
        foreach (var fileName in _files.Keys.ToList())
        {
            var script = File.ReadAllText($"Scripts/{fileName}.js");
            _files[fileName] = script;
            _strictFiles[fileName] = "\"use strict\";" + Environment.NewLine + script;
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
        engine.SetValue("host", new InteropHost());
        engine.Execute(_files[FileName]);
    }

    [Benchmark]
    public void NilJS()
    {
        var context = new NiL.JS.Core.Context(strict: true);
        context.DefineVariable("host").Assign(context.GlobalContext.ProxyValue(new InteropHost()));
        context.Eval(_files[FileName]);
    }

    [Benchmark]
    public void YantraJS()
    {
        var context = new YantraContext();
        context[(YantraValue)new YantraString("host")] = YantraClrProxy.Marshal(new InteropHost());
        context.Eval(_files[FileName], null, context);
    }

    [Benchmark]
    public void ClearScript()
    {
        using var engine = new V8ScriptEngine();
        engine.AddHostObject("host", new InteropHost());
        engine.Execute(_strictFiles[FileName]);
    }

    [Benchmark]
    public void ClearScript_FastProxy()
    {
        using var engine = new V8ScriptEngine();
        engine.AddHostObject("host", new FastInteropHost());
        engine.Execute(_strictFiles[FileName]);
    }
}
