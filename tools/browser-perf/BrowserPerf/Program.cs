using System.Diagnostics;
using System.Net;
using System.Text;
using Jint.Browser;
using Jint.DevTools;
using Microsoft.Playwright;
using JintBrowser = Jint.Browser.Browser;
using JintOptions = Jint.Browser.BrowserOptions;

namespace BrowserPerf;

/// <summary>
/// Measures the same automation scenario three ways: Playwright driving Chromium, Playwright driving
/// Jint.Browser over CDP, and Jint.Browser's own in-process API.
/// </summary>
internal static class Program
{
    private static readonly Dictionary<string, List<double>> Samples = new(StringComparer.Ordinal);

    public static async Task<int> Main(string[] args)
    {
        var lanes = Arg(args, "--lanes", "chromium,jint-cdp,jint-direct").Split(',');
        var iterations = int.Parse(Arg(args, "--iterations", "20"));
        var warmup = int.Parse(Arg(args, "--warmup", "5"));
        var roundtrips = int.Parse(Arg(args, "--roundtrips", "200"));
        var scenarios = Arg(args, "--scenarios", "form,todo,roundtrip").Split(',');

        using var server = new StaticServer(SiteRoots());
        Console.WriteLine($"serving {server.Origin}");

        foreach (var lane in lanes)
        {
            Samples.Clear();
            Console.WriteLine();
            Console.WriteLine($"=== {lane} ===");
            var startup = Stopwatch.StartNew();
            await using ILane driver = lane switch
            {
                "chromium" => await PlaywrightLane.LaunchChromiumAsync(),
                "chromium-cdp" => await PlaywrightLane.ConnectChromiumAsync(),
                "jint-cdp" => await PlaywrightLane.ConnectJintAsync(),
                "jint-serve" => await PlaywrightLane.ConnectJintServeAsync(),
                "jint-direct" => new JintDirectLane(),
                _ => throw new ArgumentException(lane),
            };
            Console.WriteLine($"startup: {startup.Elapsed.TotalMilliseconds:F1} ms");

            foreach (var scenario in scenarios)
            {
                for (var i = 0; i < warmup + iterations; i++)
                {
                    var record = i >= warmup;
                    switch (scenario)
                    {
                        case "form": await driver.FormAsync(server.Origin, record); break;
                        case "todo": await driver.TodoAsync(server.Origin, record); break;
                        case "roundtrip": await driver.RoundTripAsync(server.Origin, record, i == warmup + iterations - 1 ? roundtrips : roundtrips / 10); break;
                        case "goto": await driver.GotoOnlyAsync(server.Origin, record); break;
                        case "clicks": await driver.ClickLoopAsync(server.Origin, record, roundtrips); break;
                        default: throw new ArgumentException(scenario);
                    }
                }
            }

            Report();
        }

        return 0;
    }

    internal static void Record(bool record, string name, double ms)
    {
        if (name.StartsWith("TOTAL", StringComparison.Ordinal) && Environment.GetEnvironmentVariable("PERF_TREND") == "1")
        {
            Console.WriteLine($"trend {name} {ms:F1} heap={GC.GetTotalMemory(false) / 1048576}MB gen2={GC.CollectionCount(2)}");
        }

        if (!record) return;
        if (!Samples.TryGetValue(name, out var list)) Samples[name] = list = new List<double>();
        list.Add(ms);
    }

    internal static async Task<T> Time<T>(bool record, string name, Func<Task<T>> work)
    {
        var sw = Stopwatch.StartNew();
        var result = await work();
        Record(record, name, sw.Elapsed.TotalMilliseconds);
        return result;
    }

    internal static async Task Time(bool record, string name, Func<Task> work)
    {
        var sw = Stopwatch.StartNew();
        await work();
        Record(record, name, sw.Elapsed.TotalMilliseconds);
    }

    private static void Report()
    {
        Console.WriteLine($"{"step",-28} {"n",5} {"median",10} {"mean",10} {"p90",10} {"min",10}");
        foreach (var (name, list) in Samples)
        {
            list.Sort();
            var median = list[list.Count / 2];
            var p90 = list[Math.Min(list.Count - 1, (int) (list.Count * 0.9))];
            Console.WriteLine($"{name,-28} {list.Count,5} {median,10:F2} {list.Average(),10:F2} {p90,10:F2} {list[0],10:F2}");
        }
    }

    private static string Arg(string[] args, string name, string fallback)
    {
        var index = Array.IndexOf(args, name);
        return index >= 0 && index + 1 < args.Length ? args[index + 1] : fallback;
    }

    private static string[] SiteRoots()
    {
        var site = Path.Combine(AppContext.BaseDirectory, "site");
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Jint.slnx"))) dir = dir.Parent;
        var fixtures = dir is null ? null : Path.Combine(dir.FullName, "Jint.Tests.Browser", "Fixtures");
        return fixtures is null ? [site] : [site, fixtures];
    }
}

internal interface ILane : IAsyncDisposable
{
    Task FormAsync(string origin, bool record);

    Task TodoAsync(string origin, bool record);

    Task RoundTripAsync(string origin, bool record, int count);

    Task GotoOnlyAsync(string origin, bool record);

    Task ClickLoopAsync(string origin, bool record, int count);
}

internal sealed class PlaywrightLane : ILane
{
    private readonly IPlaywright _playwright;
    private readonly IBrowser _browser;
    private readonly Func<ValueTask>? _dispose;
    private readonly bool _reuseDefaultContext;

    private PlaywrightLane(IPlaywright playwright, IBrowser browser, Func<ValueTask>? dispose, bool reuseDefaultContext)
    {
        _playwright = playwright;
        _browser = browser;
        _dispose = dispose;
        _reuseDefaultContext = reuseDefaultContext;
    }

    internal static string ChromiumPath => Environment.GetEnvironmentVariable("PERF_CHROMIUM")
        ?? "/opt/pw-browsers/chromium_headless_shell-1243/chrome-headless-shell-linux64/chrome-headless-shell";

    internal static async Task<PlaywrightLane> LaunchChromiumAsync()
    {
        var playwright = await Playwright.CreateAsync();
        var browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
        {
            ExecutablePath = ChromiumPath,
            Headless = true,
        });
        return new PlaywrightLane(playwright, browser, null, false);
    }

    internal static async Task<PlaywrightLane> ConnectChromiumAsync()
    {
        var port = FreePort();
        var process = Process.Start(new ProcessStartInfo(ChromiumPath,
            $"--headless --remote-debugging-port={port} --no-first-run --no-sandbox --user-data-dir={Path.Combine(Path.GetTempPath(), "perf-chrome-" + port)} about:blank")
        { RedirectStandardError = true, RedirectStandardOutput = true })!;
        var playwright = await Playwright.CreateAsync();
        IBrowser? browser = null;
        for (var attempt = 0; browser is null; attempt++)
        {
            try { browser = await playwright.Chromium.ConnectOverCDPAsync($"http://127.0.0.1:{port}"); }
            catch when (attempt < 50) { await Task.Delay(100); }
        }

        return new PlaywrightLane(playwright, browser, () => { process.Kill(true); return ValueTask.CompletedTask; }, false);
    }

    internal static async Task<PlaywrightLane> ConnectJintServeAsync()
    {
        var exe = Environment.GetEnvironmentVariable("PERF_SERVE_EXE") ?? throw new InvalidOperationException("PERF_SERVE_EXE");
        var port = FreePort();
        var process = Process.Start(new ProcessStartInfo(exe, $"serve --port {port}")
        { RedirectStandardOutput = true, RedirectStandardError = true })!;
        while (await process.StandardOutput.ReadLineAsync() is { } line)
        {
            if (line.StartsWith("Jint browser listening", StringComparison.Ordinal)) break;
        }

        _ = process.StandardOutput.ReadToEndAsync();
        _ = process.StandardError.ReadToEndAsync();
        var playwright = await Playwright.CreateAsync();
        var browser = await playwright.Chromium.ConnectOverCDPAsync($"http://127.0.0.1:{port}");
        return new PlaywrightLane(playwright, browser, () => { process.Kill(true); return ValueTask.CompletedTask; }, false);
    }

    internal static async Task<PlaywrightLane> ConnectJintAsync()
    {
        var options = NewJintOptions();
        var pages = new JintBrowser(options);
        var protocol = new DevToolsServer();
        await protocol.AddBrowser(pages);
        await protocol.StartAsync();
        var playwright = await Playwright.CreateAsync();
        var browser = await playwright.Chromium.ConnectOverCDPAsync(protocol.BrowserHttpUrl);
        return new PlaywrightLane(playwright, browser, async () =>
        {
            await protocol.DisposeAsync();
            await pages.CloseAsync();
        }, Environment.GetEnvironmentVariable("PERF_REUSE_CONTEXT") == "1");
    }

    internal static JintOptions NewJintOptions()
    {
        var options = new JintOptions();
        if (Environment.GetEnvironmentVariable("PERF_RAF_MS") is { } raf)
        {
            options.AnimationFrameInterval = TimeSpan.FromMilliseconds(double.Parse(raf));
        }

        return options;
    }

    private static int FreePort()
    {
        var listener = new System.Net.Sockets.TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint) listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    private async Task<(IBrowserContext Context, IPage Page)> OpenAsync(bool record)
    {
        var context = await Program.Time(record, "newContext", () => _reuseDefaultContext && _browser.Contexts.Count > 0
            ? Task.FromResult(_browser.Contexts[0])
            : _browser.NewContextAsync());
        var page = await Program.Time(record, "newPage", () => context.NewPageAsync());
        return (context, page);
    }

    private async Task CloseAsync(bool record, IBrowserContext context, IPage page)
    {
        if (_reuseDefaultContext) await Program.Time(record, "close", () => page.CloseAsync());
        else await Program.Time(record, "close", () => context.CloseAsync());
    }

    public async Task GotoOnlyAsync(string origin, bool record)
    {
        var total = Stopwatch.StartNew();
        var (context, page) = await OpenAsync(record);
        await Program.Time(record, "goto(form)", () => page.GotoAsync(origin + "/form.html"));
        await CloseAsync(record, context, page);
        Program.Record(record, "TOTAL goto", total.Elapsed.TotalMilliseconds);
    }

    public async Task FormAsync(string origin, bool record)
    {
        var total = Stopwatch.StartNew();
        var (context, page) = await OpenAsync(record);
        await Program.Time(record, "goto(form)", () => page.GotoAsync(origin + "/form.html"));
        var title = await Program.Time(record, "title", () => page.TitleAsync());
        Check(title == "Perf form", title);
        await Program.Time(record, "fill", () => page.Locator("#name").FillAsync("Ada"));
        await Program.Time(record, "check", () => page.Locator("#agree").CheckAsync());
        await Program.Time(record, "selectOption", () => page.Locator("#color").SelectOptionAsync("green"));
        await Program.Time(record, "click(submit)", () => page.Locator("#save").ClickAsync());
        var saved = await Program.Time(record, "textContent", () => page.Locator("#out").TextContentAsync());
        Check(saved == "Saved Ada", saved);
        for (var i = 0; i < 5; i++)
        {
            await Program.Time(record, "click(inc)", () => page.Locator("#inc").ClickAsync());
        }

        var count = await Program.Time(record, "innerText", () => page.Locator("#count").InnerTextAsync());
        Check(count == "5", count);
        var items = await Program.Time(record, "count(li)", () => page.Locator("#list li").CountAsync());
        Check(items == 50, items.ToString());
        await CloseAsync(record, context, page);
        Program.Record(record, "TOTAL form", total.Elapsed.TotalMilliseconds);
    }

    public async Task TodoAsync(string origin, bool record)
    {
        var total = Stopwatch.StartNew();
        var (context, page) = await OpenAsync(record);
        await Program.Time(record, "goto(todo)", () => page.GotoAsync(origin + "/todomvc-react/index.html"));
        await Program.Time(record, "waitFor(.new-todo)", () => page.Locator(".new-todo").WaitForAsync());
        foreach (var todo in new[] { "buy milk", "write the fixture", "read the standard" })
        {
            await Program.Time(record, "todo.fill", () => page.Locator(".new-todo").FillAsync(todo));
            await Program.Time(record, "todo.press(Enter)", () => page.Locator(".new-todo").PressAsync("Enter"));
        }

        var count = await Program.Time(record, "todo.count", () => page.Locator(".todo-list li").CountAsync());
        Check(count == 3, count.ToString());
        await CloseAsync(record, context, page);
        Program.Record(record, "TOTAL todo", total.Elapsed.TotalMilliseconds);
    }

    public async Task RoundTripAsync(string origin, bool record, int count)
    {
        var (context, page) = await OpenAsync(false);
        await page.GotoAsync(origin + "/form.html");
        for (var i = 0; i < count; i++)
        {
            await Program.Time(record, "evaluate(1+1)", () => page.EvaluateAsync<int>("() => 1 + 1"));
        }

        for (var i = 0; i < count; i++)
        {
            await Program.Time(record, "locator.textContent", () => page.Locator("#heading").TextContentAsync());
        }

        await CloseAsync(false, context, page);
    }

    public async Task ClickLoopAsync(string origin, bool record, int count)
    {
        var (context, page) = await OpenAsync(false);
        await page.GotoAsync(origin + "/form.html");
        for (var i = 0; i < count; i++)
        {
            await Program.Time(record, "loop.click", () => page.Locator("#inc").ClickAsync());
        }

        for (var i = 0; i < count; i++)
        {
            await Program.Time(record, "loop.fill", () => page.Locator("#name").FillAsync("x" + i));
        }

        await CloseAsync(false, context, page);
    }

    private static void Check(bool condition, string? detail)
    {
        if (!condition) throw new InvalidOperationException("unexpected: " + detail);
    }

    public async ValueTask DisposeAsync()
    {
        await _browser.CloseAsync();
        _playwright.Dispose();
        if (_dispose is not null) await _dispose();
    }
}

internal sealed class JintDirectLane : ILane
{
    private readonly JintBrowser _browser = new(PlaywrightLane.NewJintOptions());

    private async Task<(Jint.Browser.BrowserContext Context, Jint.Browser.Page Page)> OpenAsync(bool record)
    {
        var context = await Program.Time(record, "newContext", () => _browser.NewContextAsync());
        var page = await Program.Time(record, "newPage", () => context.NewPageAsync());
        return (context, page);
    }

    public async Task GotoOnlyAsync(string origin, bool record)
    {
        var total = Stopwatch.StartNew();
        var (context, page) = await OpenAsync(record);
        await Program.Time(record, "goto(form)", () => page.NavigateAsync(origin + "/form.html"));
        await Program.Time(record, "close", () => context.CloseAsync());
        Program.Record(record, "TOTAL goto", total.Elapsed.TotalMilliseconds);
    }

    public async Task FormAsync(string origin, bool record)
    {
        var total = Stopwatch.StartNew();
        var (context, page) = await OpenAsync(record);
        await Program.Time(record, "goto(form)", () => page.NavigateAsync(origin + "/form.html"));
        var title = await Program.Time(record, "title", () => page.TitleAsync());
        Check(title == "Perf form", title);
        await Program.Time(record, "fill", () => page.FillAsync("#name", "Ada"));
        await Program.Time(record, "check", () => page.ClickAsync("#agree"));
        await Program.Time(record, "selectOption", () => page.SelectAsync("#color", "green"));
        await Program.Time(record, "click(submit)", () => page.ClickAsync("#save"));
        var saved = await Program.Time(record, "textContent", () => page.InnerTextAsync("#out"));
        Check(saved == "Saved Ada", saved);
        for (var i = 0; i < 5; i++)
        {
            await Program.Time(record, "click(inc)", () => page.ClickAsync("#inc"));
        }

        var count = await Program.Time(record, "innerText", () => page.InnerTextAsync("#count"));
        Check(count == "5", count);
        var items = await Program.Time(record, "count(li)", () => page.CountAsync("#list li"));
        Check(items == 50, items.ToString());
        await Program.Time(record, "close", () => context.CloseAsync());
        Program.Record(record, "TOTAL form", total.Elapsed.TotalMilliseconds);
    }

    public async Task TodoAsync(string origin, bool record)
    {
        var total = Stopwatch.StartNew();
        var (context, page) = await OpenAsync(record);
        await Program.Time(record, "goto(todo)", () => page.NavigateAsync(origin + "/todomvc-react/index.html"));
        await Program.Time(record, "waitFor(.new-todo)", () => page.WaitForSelectorAsync(".new-todo", TimeSpan.FromSeconds(10)));
        foreach (var todo in new[] { "buy milk", "write the fixture", "read the standard" })
        {
            await Program.Time(record, "todo.fill", () => page.FillAsync(".new-todo", todo));
            await Program.Time(record, "todo.press(Enter)", () => page.PressAsync("Enter"));
        }

        var count = await Program.Time(record, "todo.count", () => page.CountAsync(".todo-list li"));
        Check(count == 3, count.ToString());
        await Program.Time(record, "close", () => context.CloseAsync());
        Program.Record(record, "TOTAL todo", total.Elapsed.TotalMilliseconds);
    }

    public async Task RoundTripAsync(string origin, bool record, int count)
    {
        var (context, page) = await OpenAsync(false);
        await page.NavigateAsync(origin + "/form.html");
        for (var i = 0; i < count; i++)
        {
            await Program.Time(record, "evaluate(1+1)", () => page.EvaluateAsync("1 + 1"));
        }

        for (var i = 0; i < count; i++)
        {
            await Program.Time(record, "locator.textContent", () => page.InnerTextAsync("#heading"));
        }

        await context.CloseAsync();
    }

    public async Task ClickLoopAsync(string origin, bool record, int count)
    {
        var (context, page) = await OpenAsync(false);
        await page.NavigateAsync(origin + "/form.html");
        for (var i = 0; i < count; i++)
        {
            await Program.Time(record, "loop.click", () => page.ClickAsync("#inc"));
        }

        for (var i = 0; i < count; i++)
        {
            await Program.Time(record, "loop.fill", () => page.FillAsync("#name", "x" + i));
        }

        await context.CloseAsync();
    }

    private static void Check(bool condition, string? detail)
    {
        if (!condition) throw new InvalidOperationException("unexpected: " + detail);
    }

    public async ValueTask DisposeAsync() => await _browser.CloseAsync();
}

/// <summary>A minimal static file server over loopback, so both browsers fetch the same bytes over a socket.</summary>
internal sealed class StaticServer : IDisposable
{
    private readonly HttpListener _listener = new();
    private readonly string[] _roots;

    internal StaticServer(string[] roots)
    {
        _roots = roots;
        var port = 0;
        for (var attempt = 0; ; attempt++)
        {
            port = Random.Shared.Next(20000, 60000);
            try
            {
                _listener.Prefixes.Clear();
                _listener.Prefixes.Add($"http://127.0.0.1:{port}/");
                _listener.Start();
                break;
            }
            catch (HttpListenerException) when (attempt < 20)
            {
            }
        }

        Origin = $"http://127.0.0.1:{port}";
        _ = Task.Run(LoopAsync);
    }

    internal string Origin { get; }

    private async Task LoopAsync()
    {
        while (_listener.IsListening)
        {
            HttpListenerContext context;
            try { context = await _listener.GetContextAsync(); }
            catch { return; }
            _ = Task.Run(() => Serve(context));
        }
    }

    private void Serve(HttpListenerContext context)
    {
        var path = Uri.UnescapeDataString(context.Request.Url!.AbsolutePath).TrimStart('/');
        if (path.Length == 0 || path.EndsWith('/')) path += "index.html";
        foreach (var root in _roots)
        {
            var file = Path.GetFullPath(Path.Combine(root, path));
            if (file.StartsWith(root, StringComparison.Ordinal) && File.Exists(file))
            {
                var bytes = File.ReadAllBytes(file);
                context.Response.ContentType = Path.GetExtension(file) switch
                {
                    ".html" => "text/html; charset=utf-8",
                    ".js" or ".mjs" => "text/javascript; charset=utf-8",
                    ".css" => "text/css; charset=utf-8",
                    ".json" => "application/json",
                    _ => "text/plain; charset=utf-8",
                };
                context.Response.ContentLength64 = bytes.Length;
                context.Response.OutputStream.Write(bytes);
                context.Response.Close();
                return;
            }
        }

        context.Response.StatusCode = 404;
        context.Response.OutputStream.Write(Encoding.UTF8.GetBytes("not found"));
        context.Response.Close();
    }

    public void Dispose() => _listener.Close();
}
