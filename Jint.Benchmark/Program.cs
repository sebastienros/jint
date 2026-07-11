using System.Reflection;
using BenchmarkDotNet.Columns;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Diagnosers;
using BenchmarkDotNet.Exporters;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Loggers;
using BenchmarkDotNet.Running;
using Jint;
using Jint.Benchmark;

if (args.Length > 0 && args[0] == "--profile-memory")
{
    return MemoryProbe.Run(args);
}

if (args.Length > 0 && args[0] == "--profile-cpu")
{
    return CpuProfileDriver.Run(args);
}

if (args.Length > 0 && args[0] == "--profile-time")
{
    return TimeProbe.Run(args);
}

if (args.Length > 0 && args[0] == "--profile-alloc-types")
{
    return AllocTypeProbe.Run(args);
}

if (args.Length > 0 && args[0] == "--smoke-comparison")
{
    // Runs one Scripts/<name>.js once per comparison engine and reports pass/fail + wall time.
    // Validates that a new comparison script is compatible with every engine before it joins the table.
    var name = args.Length > 1 ? args[1] : "json-parse";
    var path = Path.Combine(AppContext.BaseDirectory, "Scripts", name + ".js");
    if (!File.Exists(path))
    {
        Console.Error.WriteLine($"Script not found: {path}");
        return 2;
    }

    var source = File.ReadAllText(path);
    if (name.Contains("dromaeo", StringComparison.Ordinal))
    {
        source = """
            var startTest = function () { };
            var test = function (name, fn) { fn(); };
            var endTest = function () { };
            var prep = function (fn) { fn(); };
            """ + Environment.NewLine + source;
    }

    var failures = 0;
    RunSmoke("Jint", () => new Engine(static options => options.Strict()).Execute(source));
    RunSmoke("Jurassic", () => new Jurassic.ScriptEngine { ForceStrictMode = true }.Execute(source));
    RunSmoke("NiL.JS", () => new NiL.JS.Core.Context(strict: true).Eval(source));
    RunSmoke("YantraJS", () =>
    {
        var engine = new YantraJS.Core.JSContext();
        engine.Eval(source, null, engine);
    });
    return failures == 0 ? 0 : 1;

    void RunSmoke(string engineName, Action run)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            run();
            Console.WriteLine($"  {engineName,-10} OK    {sw.ElapsedMilliseconds,6} ms");
        }
        catch (Exception ex)
        {
            failures++;
            Console.WriteLine($"  {engineName,-10} FAIL  {sw.ElapsedMilliseconds,6} ms  {ex.GetType().Name}: {ex.Message.Split('\n')[0]}");
        }
    }
}

if (args.Length > 0 && args[0] is "--disasm" or "--disasm-hw")
{
    // JIT-asm inspection per lane (ShortRun for turnaround). INSPECTION ONLY: never quote these
    // numbers as benchmark gates — gating runs use the default job.
    Console.WriteLine("== DISASSEMBLY INSPECTION MODE (ShortRun) — asm output only, numbers are NOT gate-quality ==");
    var disasmConfig = ManualConfig.CreateEmpty()
        .AddColumnProvider(DefaultColumnProviders.Instance)
        .AddLogger(ConsoleLogger.Default)
        .AddExporter(MarkdownExporter.GitHub)
        .AddJob(Job.ShortRun)
        .AddDiagnoser(new DisassemblyDiagnoser(new DisassemblyDiagnoserConfig(
            maxDepth: 3,
            printSource: true,
            exportGithubMarkdown: true,
            exportCombinedDisassemblyReport: true)));
    if (args[0] == "--disasm-hw")
    {
        // ETW hardware counters need an elevated shell; optional by design.
        disasmConfig = disasmConfig.AddHardwareCounters(HardwareCounter.BranchMispredictions, HardwareCounter.BranchInstructions);
    }

    BenchmarkSwitcher
        .FromAssembly(typeof(ArrayBenchmark).GetTypeInfo().Assembly)
        .Run(args.Skip(1).ToArray(), disasmConfig);

    return 0;
}

BenchmarkSwitcher
    .FromAssembly(typeof(ArrayBenchmark).GetTypeInfo().Assembly)
    .Run(args);

return 0;
