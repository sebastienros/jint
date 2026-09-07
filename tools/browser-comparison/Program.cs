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
        try
        {
            if (args.Length != 3 || args[0] != "--config" || args[2] is not ("--smoke" or "--collect"))
            {
                Console.Error.WriteLine("Usage: BrowserComparison --config config.json --smoke|--collect");
                return 2;
            }

            var smoke = args[2] == "--smoke";
            var config = JsonSerializer.Deserialize<Configuration>(await File.ReadAllTextAsync(args[1]), Json)
                ?? throw new InvalidOperationException("Empty configuration.");
            config.Validate();
            var fixture = await File.ReadAllBytesAsync(Path.Combine(AppContext.BaseDirectory, "fixture.html"));
            var builder = WebApplication.CreateSlimBuilder();
            builder.Logging.ClearProviders();
            builder.WebHost.UseUrls("http://127.0.0.1:0");
            await using var server = builder.Build();
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
                    results.Add(await RunAsync(adapter, round, address + "/fixture", smoke));
                }
            }

            var report = new
            {
                SchemaVersion = 1,
                Mode = smoke ? "smoke-no-measurements" : "raw-diagnostics-not-publication-quality",
                RecordedAt = DateTimeOffset.UtcNow,
                Environment = new { RuntimeInformation.FrameworkDescription, RuntimeInformation.OSDescription,
                    Architecture = RuntimeInformation.ProcessArchitecture.ToString(), System.Environment.ProcessorCount },
                HarnessVersion = typeof(Program).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion,
                PuppeteerVersion = typeof(Puppeteer).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion,
                FixtureSha256 = Convert.ToHexString(SHA256.HashData(fixture)),
                AssertedResult = "100|4950|4950|Verified 100",
                MemorySampleIntervalMilliseconds = ProcessMemorySampler.IntervalMilliseconds,
                Scope = "CPU delta and OS peak working set for the explicitly named browser root PID only; excludes renderer/utility children, client and fixture server. OS peak is lifetime high-water mark, not a stage delta; sampled peak observes page work every 10 ms and can miss transients.",
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
    }

    private static async Task<RunResult> RunAsync(AdapterOptions options, int round, string url, bool smoke)
    {
        var identity = await BinaryIdentity.ReadAsync(options);
        await using var adapter = new BrowserAdapter(options);
        var launch = Stopwatch.StartNew();
        await adapter.StartAsync();
        launch.Stop();
        var browser = adapter.Browser!;
        var version = await browser.GetVersionAsync().WaitAsync(TimeSpan.FromSeconds(30));
        var agent = await browser.GetUserAgentAsync().WaitAsync(TimeSpan.FromSeconds(30));
        var before = ProcessSnapshot.Read(adapter.Process);
        await using var memory = new ProcessMemorySampler(adapter.Process);
        var workload = Stopwatch.StartNew();
        await using (var page = await browser.NewPageAsync().WaitAsync(TimeSpan.FromSeconds(30)))
        {
            await page.GoToAsync(url, new NavigationOptions { WaitUntil = [WaitUntilNavigation.Load], Timeout = 30000 });
            var actual = await page.EvaluateExpressionAsync<string>("""
                (() => {
                    const items = [...document.querySelectorAll('#items li')];
                    const total = items.reduce((sum, item) => sum + Number(item.dataset.value), 0);
                    document.querySelector('h1').textContent = 'Verified ' + items.length;
                    return [items.length, total, document.querySelector('#total').textContent,
                        document.querySelector('h1').textContent].join('|');
                })()
                """).WaitAsync(TimeSpan.FromSeconds(30));
            if (actual != "100|4950|4950|Verified 100")
            {
                throw new InvalidOperationException($"{options.Name}: workload assertion failed: {actual}");
            }
        }
        workload.Stop();
        await memory.DisposeAsync();
        var after = ProcessSnapshot.Read(adapter.Process);
        var teardown = Stopwatch.StartNew();
        await adapter.DisposeAsync();
        teardown.Stop();
        return new RunResult(options.Name, options.Kind, round, identity, version, agent,
            adapter.ProcessId, options.Kind == "lightpanda" ? "external-process-not-restarted" : "new-process-per-row",
            smoke ? null : new Measurements(launch.Elapsed.TotalMilliseconds, workload.Elapsed.TotalMilliseconds,
                teardown.Elapsed.TotalMilliseconds, before, after, memory.PeakBytes, memory.Samples, memory.UnavailableReason,
                before?.CpuMilliseconds is { } start && after?.CpuMilliseconds is { } end ? end - start : null));
    }
}

internal sealed record Configuration(int Rounds, AdapterOptions[] Adapters)
{
    internal void Validate()
    {
        if (Rounds is < 1 or > 100 || Adapters is not { Length: > 0 })
        {
            throw new ArgumentException("Require 1–100 rounds and at least one adapter.");
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
    string[]? Arguments = null, string? Endpoint = null, int? ProcessId = null, string? VersionLabel = null)
{
    internal void Validate()
    {
        if (string.IsNullOrWhiteSpace(Name) || Kind is not ("jint" or "chromium" or "lightpanda"))
        {
            throw new ArgumentException("Each adapter requires a name and kind jint, chromium or lightpanda.");
        }
        if (Kind == "lightpanda")
        {
            if (!Uri.TryCreate(Endpoint, UriKind.Absolute, out var endpoint) || endpoint.Scheme is not ("ws" or "wss") || !endpoint.IsLoopback
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
    string BrowserVersion, string UserAgent, int? ProcessId, string Lifecycle, Measurements? Measurements);
internal sealed record Measurements(double LaunchAndConnectMilliseconds, double PageWorkMilliseconds,
    double TeardownMilliseconds, ProcessSnapshot? BeforeWork, ProcessSnapshot? AfterWork, long? SampledPeakWorkingSetBytes, int MemorySamples,
    string? MemoryUnavailableReason, double? WorkCpuMilliseconds);
