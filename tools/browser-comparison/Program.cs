using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PuppeteerSharp;

namespace BrowserComparison;

internal static class Program
{
    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true, PropertyNameCaseInsensitive = true };

    internal static async Task<int> Main(string[] args)
    {
        using var stopping = new CancellationTokenSource(TimeSpan.FromSeconds(120));
        ConsoleCancelEventHandler cancel = (_, e) => { e.Cancel = true; stopping.Cancel(); };
        Console.CancelKeyPress += cancel;
        try
        {
            if (args is ["--export-workloads", var output])
            {
                if (Directory.Exists(output))
                {
                    throw new ArgumentException("Workload output directory must be new.");
                }
                Directory.CreateDirectory(output);
                foreach (var name in Workloads.Names)
                {
                    var definition = Workloads.Get(name);
                    await File.WriteAllTextAsync(Path.Combine(output, name + ".html"), definition.Html);
                    await File.WriteAllTextAsync(Path.Combine(output, name + ".js"), definition.Script);
                    await File.WriteAllTextAsync(Path.Combine(output, name + ".expected.txt"), definition.Expected);
                }
                return 0;
            }
            if (args is ["--identity", var identityConfig])
            {
                var identityOptions = JsonSerializer.Deserialize<AdapterOptions>(await File.ReadAllTextAsync(identityConfig), Json)!;
                identityOptions.Validate();
                Console.WriteLine(JsonSerializer.Serialize(await BinaryIdentity.ReadAsync(identityOptions), Json));
                return 0;
            }
            if (args is ["--idle-check"])
            {
                if (System.Environment.GetEnvironmentVariable("JINT_BENCH_SKIP_IDLE_CHECK") is "1" or "true")
                {
                    throw new InvalidOperationException("The comparison gate rejects idle-check overrides.");
                }
                var errors = Jint.Benchmark.MachineStateValidator.Blocking.Validate(null!).ToArray();
                Console.WriteLine(JsonSerializer.Serialize(new { Accepted = errors.Length == 0,
                    Errors = errors.Select(x => x.Message).ToArray() }, Json));
                return errors.Length == 0 ? 0 : 1;
            }
            if (args.Length != 3 || args[0] != "--config" || args[2] is not ("--smoke" or "--collect"))
            {
                Console.Error.WriteLine("Usage: BrowserComparison --config config.json --smoke|--collect");
                return 2;
            }

            var smoke = args[2] == "--smoke";
            var config = JsonSerializer.Deserialize<Configuration>(await File.ReadAllTextAsync(args[1]), Json)
                ?? throw new InvalidOperationException("Empty configuration.");
            config.Validate();
            var workloadDefinition = Workloads.Get(config.Workload);
            var fixture = System.Text.Encoding.UTF8.GetBytes(workloadDefinition.Html);
            var builder = WebApplication.CreateSlimBuilder();
            builder.Logging.ClearProviders();
            builder.WebHost.UseUrls("http://127.0.0.1:0");
            await using var server = builder.Build();
            server.MapGet("/data", () => Results.Json(new { values = Enumerable.Range(0, 100).ToArray() }));
            server.MapGet("/fixture", async context =>
            {
                context.Response.ContentType = "text/html; charset=utf-8";
                await context.Response.Body.WriteAsync(fixture);
            });
            await server.StartAsync();
            var address = server.Services.GetRequiredService<IServer>().Features
                .Get<IServerAddressesFeature>()!.Addresses.Single();
            var results = new List<RunResult>();
            // Alternate adapter order between rounds; every row still owns a new process/page.
            for (var round = 0; round < (smoke ? 1 : config.Rounds); round++)
            {
                var adapters = round % 2 == 0 ? config.Adapters : config.Adapters.Reverse();
                foreach (var adapter in adapters)
                {
                    results.Add(await RunAsync(adapter, round, address + "/fixture", smoke, config, stopping.Token));
                }
            }

            var report = new
            {
                SchemaVersion = 2,
                Mode = smoke ? "smoke-no-measurements" : "raw-diagnostics-not-publication-quality",
                RecordedAt = DateTimeOffset.UtcNow,
                Environment = new { RuntimeInformation.FrameworkDescription, RuntimeInformation.OSDescription,
                    Architecture = RuntimeInformation.ProcessArchitecture.ToString(), System.Environment.ProcessorCount },
                HarnessVersion = typeof(Program).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion,
                PuppeteerVersion = typeof(Puppeteer).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion,
                FixtureSha256 = Convert.ToHexString(SHA256.HashData(fixture)),
                Workload = config.Workload,
                config.Lane, config.WarmupCount, config.Iterations,
                AssertedResult = workloadDefinition.Expected,
                AutomationSha256 = Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(workloadDefinition.Script))),
                MemorySampleIntervalMilliseconds = ProcessMemorySampler.IntervalMilliseconds,
                Scope = "Optional cgroup v2 records include the entire owned process scope and retain exited-descendant CPU; charged memory is not RSS. Root diagnostics remain separate: CPU delta and OS peak working set for the explicitly named browser root PID only; excludes renderer/utility children, client and fixture server. OS peak is lifetime high-water mark, not a stage delta; sampled peak observes page work every 10 ms and can miss transients.",
                Results = results,
            };
            Console.WriteLine(JsonSerializer.Serialize(report, Json));
            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(exception);
            return 1;
        }
        finally
        {
            Console.CancelKeyPress -= cancel;
        }
    }

    private static async Task<RunResult> RunAsync(AdapterOptions options, int round, string url, bool smoke, Configuration config, CancellationToken cancellationToken)
    {
        var identity = options.IdentityFile is null ? await BinaryIdentity.ReadAsync(options)
            : JsonSerializer.Deserialize<BinaryIdentity>(await File.ReadAllTextAsync(options.IdentityFile), Json)!;
        var adapter = new BrowserAdapter(options);
        var run = new OwnedRun(options, adapter, options.JournalPath);
        var result = await run.ExecuteAsync(async browserAdapter =>
        {
            var scopeAfterLaunch = ScopeSnapshot.Read(options.AccountingDirectory);
            var browser = browserAdapter.Browser!;
            var version = await browser.GetVersionAsync().WaitAsync(TimeSpan.FromSeconds(30), cancellationToken);
            var agent = await browser.GetUserAgentAsync().WaitAsync(TimeSpan.FromSeconds(30), cancellationToken);
            var definition = Workloads.Get(config.Workload);
            async Task ExerciseAsync(IPage page)
            {
                await page.GoToAsync(url, new NavigationOptions { WaitUntil = [WaitUntilNavigation.Load], Timeout = 30000 }).WaitAsync(cancellationToken);
                var actual = await page.EvaluateExpressionAsync<string>(definition.Script).WaitAsync(TimeSpan.FromSeconds(30), cancellationToken);
                if (actual != definition.Expected)
                    throw new InvalidOperationException($"{options.Name}/{config.Workload}: expected {definition.Expected}, got {actual}");
            }
            var page = await browser.NewPageAsync().WaitAsync(TimeSpan.FromSeconds(30), cancellationToken);
            for (var warmup = 0; warmup < (config.Lane == "warm" ? config.WarmupCount : 0); warmup++) await ExerciseAsync(page);
            var scopeBefore = ScopeSnapshot.Read(options.AccountingDirectory);
            var before = ProcessSnapshot.Read(adapter.Process);
            await using var memory = new ProcessMemorySampler(adapter.Process);
            run.Stage("work");
            for (var iteration = 0; iteration < config.Iterations; iteration++) await ExerciseAsync(page);
            var scopeAfter = ScopeSnapshot.Read(options.AccountingDirectory);
            var after = ProcessSnapshot.Read(adapter.Process);
            await memory.DisposeAsync();
            run.Stage("teardown");
            await page.CloseAsync().WaitAsync(TimeSpan.FromSeconds(10));
            return new RunResult(options.Name, options.Kind, round, identity, version, agent,
                adapter.ProcessId, adapter.EffectiveArguments,
                new Dictionary<string, string?> { ["LIGHTPANDA_DISABLE_TELEMETRY"] = options.Kind == "lightpanda" ? "true" : null,
                    ["DOTNET_gcConcurrent"] = System.Environment.GetEnvironmentVariable("DOTNET_gcConcurrent"),
                    ["DOTNET_gcServer"] = System.Environment.GetEnvironmentVariable("DOTNET_gcServer") },
                options.Borrowed ? "external-process-not-restarted" : "new-process-per-row",
                smoke ? null : new Measurements(0, 0, 0, before, after, memory.PeakBytes, memory.Samples, memory.UnavailableReason,
                    before?.CpuMilliseconds is { } start && after?.CpuMilliseconds is { } end ? end - start : null,
                    scopeBefore, scopeAfter, null, [], false, 0, 0, scopeAfterLaunch));
        }, cancellationToken);
        return result with { Measurements = result.Measurements is null ? null : result.Measurements with
        {
            LaunchAndConnectMilliseconds = run.Memory.Duration("launch"), PageWorkMilliseconds = run.Memory.Duration("work"),
            TeardownMilliseconds = run.Memory.Duration("teardown"), PreparationMilliseconds = run.Memory.Duration("preparation"),
            LifecycleMilliseconds = run.Memory.ElapsedMilliseconds, ScopeAfterTeardown = run.FinalScope,
            ScopeMemorySamples = run.Memory.Snapshot(), ForcedTermination = adapter.ForcedTermination,
        } };
    }

}

internal sealed record Configuration(int Rounds, AdapterOptions[] Adapters, string Workload = "mutate-query",
    string Lane = "cold", int WarmupCount = 5, int Iterations = 1)
{
    internal void Validate()
    {
        if (Rounds is < 1 or > 100 || Adapters is not { Length: > 0 })
        {
            throw new ArgumentException("Require 1–100 rounds and at least one adapter.");
        }
        _ = Workloads.Get(Workload);
        if (Lane is not ("cold" or "warm") || WarmupCount < 1 || Iterations < 1 || Iterations > 10000
            || (Lane == "cold" && Iterations != 1))
        {
            throw new ArgumentException("Require cold/one iteration or warm with positive warmup and iterations.");
        }
        foreach (var adapter in Adapters)
        {
            adapter.Validate();
        }
        if (Adapters.Select(x => x.Name).Distinct(StringComparer.Ordinal).Count() != Adapters.Length)
        {
            throw new ArgumentException("Adapter names must be unique.");
        }
    }
}

internal sealed record AdapterOptions(string Name, string Kind, string? Executable = null,
    string[]? Arguments = null, string? Endpoint = null, int? ProcessId = null, string? VersionLabel = null,
    string? AccountingDirectory = null, string[]? DependencyRoots = null, string? IdentityFile = null, string? JournalPath = null)
{
    internal bool Borrowed => Kind == "lightpanda" && Endpoint is not null;

    internal void Validate()
    {
        if (AccountingDirectory is not null && (!OperatingSystem.IsLinux() || !Path.IsPathFullyQualified(AccountingDirectory)
            || !Path.GetFileName(AccountingDirectory).StartsWith("jint-comparison-", StringComparison.Ordinal)
            || !Guid.TryParseExact(Path.GetFileName(AccountingDirectory)["jint-comparison-".Length..], "N", out _)
            || !File.Exists(Path.Combine(AccountingDirectory, "cgroup.procs"))
            || ScopeSnapshot.Read(AccountingDirectory)!.Populated))
        {
            throw new ArgumentException("Accounting requires an existing delegated Linux cgroup v2 scope.");
        }
        if (string.IsNullOrWhiteSpace(Name) || Kind is not ("jint" or "chromium" or "lightpanda"))
        {
            throw new ArgumentException("Each adapter requires a name and kind jint, chromium or lightpanda.");
        }
        if (Borrowed)
        {
            if (Executable is not null || AccountingDirectory is not null || !Uri.TryCreate(Endpoint, UriKind.Absolute, out var endpoint) || endpoint.Scheme is not ("ws" or "wss") || !endpoint.IsLoopback
                || string.IsNullOrWhiteSpace(VersionLabel) || ProcessId is <= 0)
            {
                throw new ArgumentException("Lightpanda requires a loopback WebSocket endpoint and exact version label; optional processId must identify a local process.");
            }
        }
        else if (Executable is null || !Path.IsPathFullyQualified(Executable) || !File.Exists(Executable))
        {
            throw new ArgumentException($"{Name}: executable must be an existing absolute path.");
        }
    }
}

internal sealed record RunResult(string Name, string Kind, int Round, BinaryIdentity Identity,
    string BrowserVersion, string UserAgent, int? ProcessId, string[] EffectiveArguments,
    Dictionary<string, string?> EffectiveEnvironment, string Lifecycle, Measurements? Measurements);
internal sealed record Measurements(double LaunchAndConnectMilliseconds, double PageWorkMilliseconds,
    double TeardownMilliseconds, ProcessSnapshot? BeforeWork, ProcessSnapshot? AfterWork, long? SampledPeakWorkingSetBytes, int MemorySamples,
    string? MemoryUnavailableReason, double? WorkCpuMilliseconds, ScopeSnapshot? ScopeBeforeWork,
    ScopeSnapshot? ScopeAfterWork, ScopeSnapshot? ScopeAfterTeardown, ScopeMemorySample[] ScopeMemorySamples, bool ForcedTermination, double PreparationMilliseconds, double LifecycleMilliseconds,
    ScopeSnapshot? ScopeAfterLaunch);
