#nullable enable
using System.Diagnostics.Tracing;
using System.Globalization;

namespace Jint.Benchmark;

/// <summary>
/// Attributes managed allocations by CLR type using the runtime's GCAllocationTick events
/// (sampled ~every 100 KB per type), to find what allocates on a hot path when code reading and
/// GC.GetAllocatedBytesForCurrentThread (MemoryProbe) can't say *which type*. Used to chase the
/// per-call allocation floor seen in ClosureCallBenchmarks. Not a benchmark; temporary diagnostic.
///
/// Usage: --profile-alloc-types [scriptName|empty-closure|captured-var|campaignTarget] [iterations]
/// With a Scripts/ name it runs that engine-comparison script (fresh engine/iter, like the BDN bench);
/// "empty-closure"/"captured-var" run an inline isolated loop so the per-call floor is unmixed.
/// Campaign targets (see <see cref="CampaignTargets"/>) run the exact GATE-row sources the coverage
/// benchmark classes execute, in the same engine mode (reuse-engine warm vs fresh-engine per iter),
/// so census output and BDN rows attribute the identical workload.
/// </summary>
internal static class AllocTypeProbe
{
    /// <summary>Census names → the coverage-suite GATE-row workloads (same static sources the benchmarks run).</summary>
    private static readonly Dictionary<string, Func<Action>> CampaignTargets = new(StringComparer.Ordinal)
    {
        ["json-parse-records"] = static () => ReuseEngine(JsonJsBenchmark.ParseRecordsSource, static e => e.SetValue("recordsJson", JsonJsBenchmark.BuildRecordsJson())),
        ["json-parse-config"] = static () => ReuseEngine(JsonJsBenchmark.ParseConfigSource, static e => e.SetValue("configJson", JsonJsBenchmark.BuildConfigJson())),
        ["json-stringify-records"] = static () => ReuseEngine(
            JsonJsBenchmark.StringifyRecordsSource,
            static e =>
            {
                e.SetValue("recordsJson", JsonJsBenchmark.BuildRecordsJson());
                e.SetValue("configJson", JsonJsBenchmark.BuildConfigJson());
            },
            JsonJsBenchmark.SetupSource),
        ["object-spread-small"] = static () => ReuseEngine(ObjectSpreadBenchmark.SpreadSmallSource, setupSource: ObjectSpreadBenchmark.SetupSource),
        ["object-assign-fresh"] = static () => ReuseEngine(ObjectSpreadBenchmark.AssignFreshTargetSource, setupSource: ObjectSpreadBenchmark.SetupSource),
        ["rest-destructuring"] = static () => ReuseEngine(ObjectSpreadBenchmark.RestDestructuringSource, setupSource: ObjectSpreadBenchmark.SetupSource),
        ["reduce-sum"] = static () => ReuseEngine(ArrayCallbackBenchmark.ReduceSumSource, setupSource: ArrayCallbackBenchmark.SetupSource),
        ["map-filter-reduce"] = static () => ReuseEngine(ArrayCallbackBenchmark.MapFilterReduceChainSource, setupSource: ArrayCallbackBenchmark.SetupSource),
        ["await-loop"] = static () => ReuseEngine(AsyncAwaitBenchmark.AwaitResolvedLoopSource),
        ["then-chain"] = static () => ReuseEngine(AsyncAwaitBenchmark.ThenChain1000Source),
        ["promise-all"] = static () => ReuseEngine(AsyncAwaitBenchmark.PromiseAll100Source),
        ["try-nothrow"] = static () => ReuseEngine(ErrorHandlingBenchmark.TryNoThrowSource),
        ["throw-catch"] = static () => ReuseEngine(ErrorHandlingBenchmark.ThrowCatchLoopSource),
        ["error-stack"] = static () => ReuseEngine(ErrorHandlingBenchmark.ErrorStackAccessSource),
        ["map-get-hit"] = static () => ReuseEngine(MapSetLookupBenchmark.MapGetHitSource, setupSource: MapSetLookupBenchmark.SetupSource),
        ["memoize"] = static () => ReuseEngine(MapSetLookupBenchmark.MemoizePatternSource, setupSource: MapSetLookupBenchmark.SetupSource),
        ["ctor-window-8"] = static () => FreshEngine(ColdConstructorBenchmark.FunctionCtorX8Source),
        ["ctor-window-class-8"] = static () => FreshEngine(ColdConstructorBenchmark.ClassCtorX8Source),
        ["ctor-distinct-200"] = static () => FreshEngine(ColdConstructorBenchmark.BuildDistinctCtorsSource()),
        ["optional-chain-miss"] = static () => ReuseEngine(ModernOperatorsBenchmark.OptionalChainMissSource, setupSource: ModernOperatorsBenchmark.SetupSource),
        ["nullish-coalesce"] = static () => ReuseEngine(ModernOperatorsBenchmark.NullishCoalesceSource, setupSource: ModernOperatorsBenchmark.SetupSource),
        ["forin-hasown"] = static () => ReuseEngine(ForInGuardBenchmark.ForInHasOwnGuardSource, setupSource: ForInGuardBenchmark.SetupSource),
        ["typeof-switch"] = static () => ReuseEngine(ForInGuardBenchmark.TypeofSwitchMixedSource, setupSource: ForInGuardBenchmark.SetupSource),
        ["tagged-template"] = static () => ReuseEngine(TemplateLiteralBenchmark.TaggedTemplateSource),
        ["template-many"] = static () => ReuseEngine(TemplateLiteralBenchmark.ManyInterpolationsSource),
        ["parseint-loop"] = static () => ReuseEngine(NumberParseBenchmark.ParseIntLoopSource, setupSource: NumberParseBenchmark.SetupSource),
        ["tofixed-loop"] = static () => ReuseEngine(NumberParseBenchmark.ToFixedLoopSource, setupSource: NumberParseBenchmark.SetupSource),
        ["regexp-exec-loop"] = static () => ReuseEngine(RegExpExecLoopBenchmark.ExecWhileLoopSource, setupSource: RegExpExecLoopBenchmark.SetupSource),
    };

    /// <summary>One warm engine, row re-evaluated per iteration — the reuse-engine benchmark mode (non-strict, like the coverage classes).</summary>
    private static Action ReuseEngine(string rowSource, Action<Engine>? seed = null, string? setupSource = null)
    {
        var engine = new Engine();
        seed?.Invoke(engine);
        if (setupSource is not null)
        {
            engine.Execute(setupSource);
        }

        var prepared = Engine.PrepareScript(rowSource);
        return () => engine.Execute(prepared);
    }

    /// <summary>Fresh engine per iteration — the cold-start benchmark mode.</summary>
    private static Action FreshEngine(string source)
    {
        var prepared = Engine.PrepareScript(source);
        return () => new Engine().Execute(prepared);
    }

    private const string DromaeoHelpers = """
        var startTest = function () { };
        var test = function (name, fn) { fn(); };
        var endTest = function () { };
        var prep = function (fn) { fn(); };
        """;

    private sealed class AllocListener : EventListener
    {
        public readonly Dictionary<string, (long Bytes, long Count)> ByType = new(StringComparer.Ordinal);
        private volatile bool _enabled;

        public bool Enabled
        {
            get => _enabled;
            set => _enabled = value;
        }

        protected override void OnEventSourceCreated(EventSource eventSource)
        {
            if (eventSource.Name == "Microsoft-Windows-DotNETRuntime")
            {
                // keyword 0x1 = GC; Verbose so GCAllocationTick fires.
                EnableEvents(eventSource, EventLevel.Verbose, (EventKeywords) 0x1);
            }
        }

        protected override void OnEventWritten(EventWrittenEventArgs e)
        {
            if (!_enabled || e.EventName is not ("GCAllocationTick_V4" or "GCAllocationTick_V3" or "GCAllocationTick_V2"))
            {
                return;
            }

            string? typeName = null;
            long amount = 0;
            var names = e.PayloadNames;
            for (var i = 0; names is not null && i < names.Count; i++)
            {
                switch (names[i])
                {
                    case "TypeName":
                        typeName = e.Payload![i] as string;
                        break;
                    case "AllocationAmount64":
                        amount = Convert.ToInt64(e.Payload![i], CultureInfo.InvariantCulture);
                        break;
                    case "AllocationAmount" when amount == 0:
                        amount = Convert.ToInt64(e.Payload![i], CultureInfo.InvariantCulture);
                        break;
                }
            }

            if (typeName is null)
            {
                return;
            }

            ByType.TryGetValue(typeName, out var cur);
            ByType[typeName] = (cur.Bytes + amount, cur.Count + 1);
        }
    }

    public static int Run(string[] args)
    {
        var target = args.Length > 1 ? args[1] : "empty-closure";
        var iterations = args.Length > 2 ? int.Parse(args[2], CultureInfo.InvariantCulture) : 40;

        Action runOnce;
        if (target is "empty-closure" or "captured-var")
        {
            var body = target == "empty-closure"
                ? "b.nop(); b.nop(); b.nop();"
                : "b.enable(); b.toggle(); b.bump();";
            var src = $$"""
                (function() {
                    function Box() {
                        var on = false; var count = 0;
                        this.nop = function () { };
                        this.enable = function () { on = true; };
                        this.toggle = function () { on = !on; };
                        this.bump = function () { count = count + 1; return on; };
                    }
                    var b = new Box();
                    for (var i = 0; i < 100000; i++) { {{body}} }
                    return b;
                })();
                """;
            var prepared = Engine.PrepareScript(src, strict: true);
            var engine = new Engine(static o => o.Strict());
            runOnce = () => engine.Execute(prepared);
        }
        else if (CampaignTargets.TryGetValue(target, out var factory))
        {
            runOnce = factory();
        }
        else
        {
            var path = Path.Combine(AppContext.BaseDirectory, "Scripts", target + ".js");
            if (!File.Exists(path))
            {
                Console.Error.WriteLine($"Script not found: {path}");
                return 2;
            }
            var src = File.ReadAllText(path);
            if (target.Contains("dromaeo", StringComparison.Ordinal))
            {
                src = DromaeoHelpers + Environment.NewLine + Environment.NewLine + src;
            }
            var prepared = Engine.PrepareScript(src, strict: true);
            runOnce = () =>
            {
                var engine = new Engine(static o => o.Strict());
                engine.Execute(prepared);
            };
        }

        using var listener = new AllocListener();

        // Warm up (JIT, statics) with the listener disabled so startup allocation is excluded.
        for (var i = 0; i < 3; i++)
        {
            runOnce();
        }

        listener.Enabled = true;
        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < iterations; i++)
        {
            runOnce();
        }
        var after = GC.GetAllocatedBytesForCurrentThread();
        listener.Enabled = false;

        // Resolve compiler-generated short names (e.g. "<>c__DisplayClass12_0") to their full
        // declaring type so we can locate the source method. The runtime's GCAllocationTick reports
        // only the nested simple name; multiple declaring types can share it.
        var jintAssembly = typeof(Engine).Assembly;
        var allTypes = jintAssembly.GetTypes();
        var fullNames = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        foreach (var t in allTypes)
        {
            if (t.Name.Contains("c__DisplayClass", StringComparison.Ordinal) || t.Name.Contains("<>c", StringComparison.Ordinal))
            {
                if (!fullNames.TryGetValue(t.Name, out var list))
                {
                    fullNames[t.Name] = list = new List<string>();
                }
                list.Add(t.FullName ?? t.Name);
            }
        }

        var sampled = listener.ByType.Values.Sum(v => v.Bytes);
        Console.WriteLine($"# alloc-types: target={target}, iterations={iterations}");
        Console.WriteLine($"# end-to-end thread allocations: {Format(after - before)} total, {Format((after - before) / iterations)}/iter");
        Console.WriteLine($"# GCAllocationTick sampled total: {Format(sampled)} (sampled ~100KB/type; ranking, not exact)");
        Console.WriteLine();
        Console.WriteLine("  |  sampled bytes |    % |  ticks | type");
        Console.WriteLine("  |---------------:|-----:|-------:|-----");
        foreach (var (type, v) in listener.ByType.OrderByDescending(kv => kv.Value.Bytes))
        {
            var pct = sampled > 0 ? 100.0 * v.Bytes / sampled : 0;
            Console.WriteLine($"  | {Format(v.Bytes),14} | {pct,4:F1} | {v.Count,6} | {type}");
        }
        Console.WriteLine();

        Console.WriteLine("# compiler-generated type name resolution (Jint assembly):");
        foreach (var (type, _) in listener.ByType.OrderByDescending(kv => kv.Value.Bytes))
        {
            if (fullNames.TryGetValue(type, out var matches))
            {
                Console.WriteLine($"  {type} =>");
                foreach (var fn in matches)
                {
                    var t = allTypes.FirstOrDefault(x => x.FullName == fn);
                    var fields = t is null
                        ? ""
                        : string.Join(", ", t.GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).Select(f => $"{f.FieldType.Name} {f.Name}"));
                    Console.WriteLine($"      {fn}  {{ {fields} }}");
                }
            }
        }
        Console.WriteLine();
        return 0;
    }

    private static string Format(long bytes)
    {
        if (bytes < 1024)
        {
            return bytes.ToString("N0", CultureInfo.InvariantCulture) + " B";
        }
        if (bytes < 1024L * 1024)
        {
            return (bytes / 1024.0).ToString("N1", CultureInfo.InvariantCulture) + " KB";
        }
        return (bytes / 1024.0 / 1024.0).ToString("N2", CultureInfo.InvariantCulture) + " MB";
    }
}
