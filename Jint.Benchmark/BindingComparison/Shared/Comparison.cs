using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;
using Jint;
using Jint.Benchmark;

namespace BindingComparison;

internal static class Program
{
    public static int Main(string[] args)
    {
        if (args.SequenceEqual(new[] { "--verify" }))
        {
            var results = new List<object>();
            foreach (var workload in Enum.GetValues<Workload>())
            {
                // Two cold instances and repeated warm calls catch state accumulating between invocations.
                for (var instance = 0; instance < 2; instance++)
                {
                    var benchmark = new BindingBenchmark { Workload = workload };
                    benchmark.SetupCold();
                    results.Add(new { workload = workload.ToString(), instance, lane = "cold", checksum = benchmark.Cold() });
                    benchmark.SetupWarm();
                    try
                    {
                        for (var call = 0; call < 2; call++)
                            results.Add(new { workload = workload.ToString(), instance, lane = "warm", checksum = benchmark.Warm() });
                    }
                    finally { benchmark.Cleanup(); }
                }
            }
            Console.WriteLine(JsonSerializer.Serialize(new
            {
                arm = BindingSession.Arm,
                runtime = RuntimeInformation.FrameworkDescription,
                os = RuntimeInformation.OSDescription,
                architecture = RuntimeInformation.ProcessArchitecture.ToString(),
                assemblies = AppDomain.CurrentDomain.GetAssemblies()
                    .Where(a => new[] { "Jint", "AngleSharp", "AngleSharp.Js", "Acornima", "BenchmarkDotNet" }.Contains(a.GetName().Name))
                    .OrderBy(a => a.GetName().Name)
                    .Select(a => new { name = a.GetName().Name, version = a.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion,
                        sha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(a.Location))) }),
                workloadSha256 = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(
                    Workloads.Html + string.Join("\n", Enum.GetValues<Workload>().Select(Workloads.Script))))),
                results
            }));
            return 0;
        }
        var summaries = BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args, JintBenchmarkConfig.Create());
        return summaries.Any(s => s.HasCriticalValidationErrors || s.Reports.Any(r => !r.Success)) ? 1 : 0;
    }
}

public enum Workload { NodeRead, ElementReadWrite, DocumentLookup, InterpreterControl }

internal static class Workloads
{
    internal const int Iterations = 1000;
    internal const string Html = "<!doctype html><html><head></head><body><div id='target' data-value='abc'>text</div></body></html>";
    internal static string Script(Workload workload)
    {
        var body = workload switch
        {
            Workload.InterpreterControl => "sum += i & 7;",
            Workload.NodeRead => "sum += document.nodeType + element.nodeType + element.nodeName.length;",
            Workload.ElementReadWrite => "element.setAttribute('data-value', i % 2 === 0 ? 'a' : 'abcd'); sum += element.getAttribute('data-value').length + element.id.length;",
            Workload.DocumentLookup => "sum += document.getElementById('target') === element ? 1 : 0; sum += document.documentElement.nodeName.length;",
            _ => throw new ArgumentOutOfRangeException(nameof(workload))
        };
        var setup = workload == Workload.InterpreterControl ? "" : "var element = document.getElementById('target');";
        return $$"""
            (function () {
                {{setup}}
                var sum = 0;
                for (var i = 0; i < {{Iterations}}; i++) { {{body}} }
                return sum;
            })()
            """;
    }
    internal static void Check(Workload workload, double actual)
    {
        // Independent arithmetic from the fixed HTML, not computed by either binding arm.
        var expected = workload switch
        {
            Workload.NodeRead => Iterations * (9 + 1 + 3),
            Workload.ElementReadWrite => Iterations / 2 * (1 + 4 + 6 + 6),
            Workload.DocumentLookup => Iterations * (1 + 4),
            Workload.InterpreterControl => Iterations / 8 * 28,
            _ => throw new ArgumentOutOfRangeException(nameof(workload))
        };
        if (actual != expected) throw new InvalidOperationException($"{workload}: expected {expected}, got {actual}.");
    }
}

/// <summary>
/// Cold includes a new DOM, engine, bindings, first execution and disposal; it excludes script parsing.
/// Warm owns a separate engine per workload and excludes setup, parsing and disposal. Only that workload
/// warms it. These are public embedding APIs in both executables, with no InternalsVisibleTo access.
/// The cold lane is document-cold within a warmed process, not process startup or first-ever reflection.
/// </summary>
[MemoryDiagnoser]
public class BindingBenchmark
{
    private BindingSession? _warm;
    private Prepared<Acornima.Ast.Script> _script;
    [ParamsAllValues] public Workload Workload { get; set; }

    [GlobalSetup(Target = nameof(Cold))]
    public void SetupCold()
    {
        _script = Engine.PrepareScript(Workloads.Script(Workload));
        using var probe = BindingSession.Create();
        probe.Validate();
    }

    [GlobalSetup(Target = nameof(Warm))]
    public void SetupWarm()
    {
        SetupCold();
        _warm = BindingSession.Create();
        _warm.Validate();
        for (var i = 0; i < 10; i++) Workloads.Check(Workload, _warm.Engine.Evaluate(_script).AsNumber());
    }

    [Benchmark]
    public double Cold()
    {
        using var session = BindingSession.Create();
        var value = session.Engine.Evaluate(_script).AsNumber();
        Workloads.Check(Workload, value);
        return value;
    }

    [Benchmark]
    public double Warm()
    {
        var value = _warm!.Engine.Evaluate(_script).AsNumber();
        Workloads.Check(Workload, value);
        return value;
    }

    [GlobalCleanup(Target = nameof(Warm))]
    public void Cleanup() => _warm?.Dispose();
}
