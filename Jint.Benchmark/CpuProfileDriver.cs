#nullable enable
using System.Globalization;

namespace Jint.Benchmark;

/// <summary>
/// Runs a single engine-comparison script in a tight loop so an external sampling profiler
/// (dotnet-trace collect -- ...) can attribute CPU time. Mirrors EngineComparisonBenchmark's
/// per-op shape: fresh engine per iteration, strict mode, dromaeo helper stubs.
/// Also prints wall-clock per iteration, usable as a quick source-vs-prepared A/B timer.
/// Not a benchmark; not registered with BDN. Temporary diagnostic.
/// </summary>
internal static class CpuProfileDriver
{
    private const string DromaeoHelpers = """
        var startTest = function () { };
        var test = function (name, fn) { fn(); };
        var endTest = function () { };
        var prep = function (fn) { fn(); };
        """;

    public static int Run(string[] args)
    {
        // args[0] = "--profile-cpu", args[1] = script name, args[2] = iterations, args[3] = "source" | "prepared"
        var scriptName = args.Length > 1 ? args[1] : "stopwatch";
        var iterations = args.Length > 2 ? int.Parse(args[2], CultureInfo.InvariantCulture) : 20;
        var variant = args.Length > 3 ? args[3] : "source";

        var path = Path.Combine(AppContext.BaseDirectory, "Scripts", scriptName + ".js");
        if (!File.Exists(path))
        {
            Console.Error.WriteLine($"Script not found: {path}");
            return 2;
        }

        var src = File.ReadAllText(path);
        if (scriptName.Contains("dromaeo", StringComparison.Ordinal))
        {
            src = DromaeoHelpers + Environment.NewLine + Environment.NewLine + src;
        }

        // Interop scripts expect the same host binding as EngineComparisonInteropBenchmark.
        var isInterop = scriptName.StartsWith("interop-", StringComparison.Ordinal);

        var prepared = Engine.PrepareScript(src, strict: true);
        var usePrepared = string.Equals(variant, "prepared", StringComparison.Ordinal);

        // Warm-up to settle JIT and statics before the profiler-relevant loop dominates samples.
        for (var i = 0; i < 2; i++)
        {
            RunOnce();
        }

        var sw = System.Diagnostics.Stopwatch.StartNew();
        for (var i = 0; i < iterations; i++)
        {
            RunOnce();
        }
        sw.Stop();

        Console.WriteLine($"{scriptName} [{variant}]: {iterations} iterations in {sw.Elapsed.TotalMilliseconds:F1} ms ({sw.Elapsed.TotalMilliseconds / iterations:F3} ms/iter)");
        return 0;

        void RunOnce()
        {
            var engine = new Engine(static options => options.Strict());
            if (isInterop)
            {
                engine.SetValue("host", new EngineComparisonInteropBenchmark.InteropHost());
            }
            if (usePrepared)
            {
                engine.Execute(prepared);
            }
            else
            {
                engine.Execute(src);
            }
        }
    }
}
