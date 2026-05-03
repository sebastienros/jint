#nullable enable
using System.Globalization;

namespace Jint.Benchmark;

/// <summary>
/// Measures per-sub-test allocations in dromaeo-style scripts that use the test(name, fn) idiom.
/// Replaces BDN for targeted memory profiling — see Jint.Benchmark/Program.cs --profile-memory.
/// Not a benchmark; not registered with BDN. Temporary diagnostic.
/// </summary>
internal static class MemoryProbe
{
    public static int Run(string[] args)
    {
        // args[0] = "--profile-memory", args[1] = script name (e.g. "dromaeo-object-array")
        var scriptName = args.Length > 1 ? args[1] : "dromaeo-object-array";
        var iterations = args.Length > 2 ? int.Parse(args[2], CultureInfo.InvariantCulture) : 5;

        var path = Path.Combine(AppContext.BaseDirectory, "Scripts", scriptName + ".js");
        if (!File.Exists(path))
        {
            Console.Error.WriteLine($"Script not found: {path}");
            return 2;
        }
        var src = File.ReadAllText(path);

        // Replace the dromaeo helpers with versions that bracket each test() with allocation snapshots.
        // We collect (name, bytes) into an in-process buffer the host reads after the run.
        var results = new List<(string Name, long Bytes)>();

        var helpers = """
            var __probe = {};
            var __probeNames = [];
            var startTest = function () { };
            var endTest = function () { };
            var prep = function (fn) { fn(); };
            var test = function (name, fn) {
              var b0 = __probeBegin();
              fn();
              var b1 = __probeEnd();
              __probeRecord(name, b1 - b0);
            };
            """;
        var fullScript = helpers + Environment.NewLine + src;

        // Warm-up: one untimed run to settle JIT, statics, intrinsics.
        Run(fullScript, recordSink: null);

        // Force a clean baseline.
        for (var i = 0; i < 3; i++)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
        }

        Console.WriteLine($"# {scriptName} — per-test allocation (median of {iterations} runs)");
        Console.WriteLine();

        // Each iteration is a fresh engine + fresh script execution to mirror BDN's setup.
        var perIterResults = new List<List<(string Name, long Bytes)>>(iterations);
        long totalSum = 0;
        for (var i = 0; i < iterations; i++)
        {
            var iterResults = new List<(string Name, long Bytes)>();
            var beforeTotal = GC.GetAllocatedBytesForCurrentThread();
            Run(fullScript, iterResults);
            var afterTotal = GC.GetAllocatedBytesForCurrentThread();
            perIterResults.Add(iterResults);
            totalSum += afterTotal - beforeTotal;
        }

        // Aggregate by sub-test name across iterations using the median (stable against transient noise).
        var byName = perIterResults
            .SelectMany(list => list)
            .GroupBy(t => t.Name, StringComparer.Ordinal)
            .Select(g => (Name: g.Key, Median: Median(g.Select(x => x.Bytes).ToArray())))
            .OrderByDescending(t => t.Median)
            .ToList();

        var totalMedian = byName.Sum(t => t.Median);
        Console.WriteLine($"  per-iteration end-to-end allocations: avg {Format(totalSum / iterations)}, sum-of-test-medians {Format(totalMedian)}");
        Console.WriteLine();
        Console.WriteLine("  | Sub-test                                        | Median bytes |   % of total |");
        Console.WriteLine("  |-------------------------------------------------|-------------:|-------------:|");
        foreach (var (name, median) in byName)
        {
            var pct = totalMedian > 0 ? 100.0 * median / totalMedian : 0;
            Console.WriteLine($"  | {Pad(name, 47)} | {Format(median),12} | {pct,11:F2}% |");
        }
        Console.WriteLine();

        return 0;

        void Run(string scriptText, List<(string Name, long Bytes)>? recordSink)
        {
            var engine = new Engine(static options => options.Strict());
            long openSnapshot = 0;
            engine.SetValue("__probeBegin", new Func<long>(() =>
            {
                openSnapshot = GC.GetAllocatedBytesForCurrentThread();
                return openSnapshot;
            }));
            engine.SetValue("__probeEnd", new Func<long>(GC.GetAllocatedBytesForCurrentThread));
            engine.SetValue("__probeRecord", new Action<string, double>((name, bytes) =>
            {
                if (recordSink is not null)
                {
                    recordSink.Add((name, (long) bytes));
                }
            }));
            engine.Execute(scriptText);
        }
    }

    private static long Median(long[] values)
    {
        Array.Sort(values);
        return values.Length == 0 ? 0 : values[values.Length / 2];
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

    private static string Pad(string s, int n)
        => s.Length >= n ? s.Substring(0, n) : s + new string(' ', n - s.Length);
}
