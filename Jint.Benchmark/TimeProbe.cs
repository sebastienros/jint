#nullable enable
using System.Diagnostics;
using System.Globalization;

namespace Jint.Benchmark;

/// <summary>
/// Measures per-sub-test wall-clock time in dromaeo-style scripts that use the test(name, fn)
/// idiom — the timing companion to <see cref="MemoryProbe"/>. Reports the median per sub-test
/// across iterations plus the end-to-end remainder (engine construction, outer parse, prep()).
/// See Jint.Benchmark/Program.cs --profile-time. Not a benchmark; not registered with BDN.
/// Temporary diagnostic.
/// </summary>
internal static class TimeProbe
{
    public static int Run(string[] args)
    {
        // args[0] = "--profile-time", args[1] = script name (e.g. "dromaeo-core-eval"), args[2] = iterations
        var scriptName = args.Length > 1 ? args[1] : "dromaeo-core-eval";
        var iterations = args.Length > 2 ? int.Parse(args[2], CultureInfo.InvariantCulture) : 15;

        var path = Path.Combine(AppContext.BaseDirectory, "Scripts", scriptName + ".js");
        if (!File.Exists(path))
        {
            Console.Error.WriteLine($"Script not found: {path}");
            return 2;
        }
        var src = File.ReadAllText(path);

        var helpers = """
            var startTest = function () { };
            var endTest = function () { };
            var prep = function (fn) { fn(); };
            var test = function (name, fn) {
              var t0 = __probeNow();
              fn();
              var t1 = __probeNow();
              __probeRecord(name, t1 - t0);
            };
            """;
        var fullScript = helpers + Environment.NewLine + src;

        // Warm-up: untimed runs to settle JIT, statics, intrinsics.
        for (var i = 0; i < 3; i++)
        {
            Run(fullScript, recordSink: null);
        }

        Console.WriteLine($"# {scriptName} — per-test wall clock (median of {iterations} runs)");
        Console.WriteLine();

        // Each iteration is a fresh engine + fresh script execution to mirror BDN's per-op shape.
        var perIterResults = new List<List<(string Name, double Ms)>>(iterations);
        var endToEnd = new double[iterations];
        for (var i = 0; i < iterations; i++)
        {
            var iterResults = new List<(string Name, double Ms)>();
            var before = Stopwatch.GetTimestamp();
            Run(fullScript, iterResults);
            var after = Stopwatch.GetTimestamp();
            perIterResults.Add(iterResults);
            endToEnd[i] = ToMs(after - before);
        }

        var byName = perIterResults
            .SelectMany(list => list)
            .GroupBy(t => t.Name, StringComparer.Ordinal)
            .Select(g => (Name: g.Key, Median: Median(g.Select(x => x.Ms).ToArray())))
            .OrderByDescending(t => t.Median)
            .ToList();

        var endToEndMedian = Median(endToEnd);
        var subTestSum = byName.Sum(t => t.Median);
        Console.WriteLine($"  end-to-end per iteration: median {endToEndMedian:F3} ms; sum of sub-test medians {subTestSum:F3} ms; remainder (engine ctor + outer parse + prep) {endToEndMedian - subTestSum:F3} ms");
        Console.WriteLine();
        Console.WriteLine("  | Sub-test                                        |    Median ms |   % of total |");
        Console.WriteLine("  |-------------------------------------------------|-------------:|-------------:|");
        foreach (var (name, median) in byName)
        {
            var pct = endToEndMedian > 0 ? 100.0 * median / endToEndMedian : 0;
            Console.WriteLine($"  | {Pad(name, 47)} | {median,12:F3} | {pct,11:F2}% |");
        }
        Console.WriteLine();

        return 0;

        static void Run(string scriptText, List<(string Name, double Ms)>? recordSink)
        {
            var engine = new Engine(static options => options.Strict());
            engine.SetValue("__probeNow", new Func<double>(static () => Stopwatch.GetTimestamp()));
            engine.SetValue("__probeRecord", new Action<string, double>((name, ticks) =>
            {
                recordSink?.Add((name, ToMs((long) ticks)));
            }));
            engine.Execute(scriptText);
        }
    }

    private static double ToMs(long stopwatchTicks) => stopwatchTicks * 1000.0 / Stopwatch.Frequency;

    private static double Median(double[] values)
    {
        Array.Sort(values);
        return values.Length == 0 ? 0 : values[values.Length / 2];
    }

    private static string Pad(string s, int n)
        => s.Length >= n ? s.Substring(0, n) : s + new string(' ', n - s.Length);
}
