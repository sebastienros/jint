using System.Diagnostics;
using System.Text.RegularExpressions;
using PuppeteerSharp;

namespace BrowserComparison;

/// <summary>Owns each fresh browser process, or explicitly borrows a diagnostic-only endpoint.</summary>
internal sealed class BrowserAdapter(AdapterOptions options, Func<string, CancellationToken, Task<IBrowser?>>? connect = null, TimeSpan? connectionTimeout = null) : IAsyncDisposable
{
    internal IBrowser? Browser { get; private set; }
    internal Process? Process { get; private set; }
    internal int? ProcessId { get; private set; }
    internal bool ForcedTermination { get; private set; }
    internal string[] EffectiveArguments { get; private set; } = [];
    private bool _disposed;
    private string? _profile;
    private readonly List<Task> _drains = [];

    internal async Task StartAsync(CancellationToken cancellationToken = default)
    {
        var endpoint = options.Endpoint;
        if (!options.Borrowed)
        {
            var arguments = new List<string>(options.Arguments ?? []);
            if (options.Kind == "chromium")
            {
                _profile = Path.Combine(Path.GetTempPath(), "jint-comparison-" + Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(_profile);
                arguments.AddRange(["--headless=new", "--remote-debugging-port=0", "--user-data-dir=" + _profile,
                    "--no-first-run", "--no-default-browser-check", "--disable-background-networking",
                    "--disable-component-update", "--disable-sync", "about:blank"]);
            }
            else
            {
                arguments.AddRange(["serve", "--host", "127.0.0.1", "--port", "0"]);
                if (options.Kind == "jint")
                {
                    // Jint uses --bind, not Lightpanda's --host; default is already loopback.
                    arguments.RemoveRange(arguments.Count - 4, 2);
                }
            }
            EffectiveArguments = arguments.ToArray();
            var start = CreateStartInfo(options, arguments);
            Process = System.Diagnostics.Process.Start(start) ?? throw new InvalidOperationException("Browser did not start.");
            ProcessId = Process.Id;
            var ready = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
            _drains.Add(DrainAsync(Process.StandardOutput, ready));
            _drains.Add(DrainAsync(Process.StandardError, ready));
            var exited = Process.WaitForExitAsync();
            var winner = await Task.WhenAny(ready.Task, exited).WaitAsync(TimeSpan.FromSeconds(30), cancellationToken);
            if (winner == exited)
            {
                throw new InvalidOperationException($"{options.Name} exited before endpoint readiness (exit {Process.ExitCode}).");
            }
            endpoint = await ready.Task;
        }
        else if (options.ProcessId is { } id)
        {
            Process = System.Diagnostics.Process.GetProcessById(id);
            ProcessId = id;
        }
        if (connect is not null)
        {
            Browser = await connect(endpoint!, cancellationToken).WaitAsync(connectionTimeout ?? TimeSpan.FromSeconds(30), cancellationToken);
            return;
        }
        Browser = await Puppeteer.ConnectAsync(new ConnectOptions
        {
            BrowserWSEndpoint = endpoint,
            DefaultViewport = null,
        }).WaitAsync(TimeSpan.FromSeconds(30), cancellationToken);
    }

    internal static ProcessStartInfo CreateStartInfo(AdapterOptions options, IEnumerable<string> arguments)
    {
        var start = new ProcessStartInfo(options.AccountingDirectory is null ? options.Executable! : "/bin/sh")
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        if (options.AccountingDirectory is { } scope)
        {
            // Positional arguments are data. The browser exec happens only after kernel scope entry.
            start.ArgumentList.Add("-c");
            start.ArgumentList.Add("printf '%s\\n' \"$$\" > \"$1/cgroup.procs\" || exit; shift; exec \"$@\"");
            start.ArgumentList.Add("jint-accounted-browser");
            start.ArgumentList.Add(scope);
            start.ArgumentList.Add(options.Executable!);
        }
        foreach (var argument in arguments)
        {
            start.ArgumentList.Add(argument);
        }
        if (options.Kind == "lightpanda")
        {
            start.Environment["LIGHTPANDA_DISABLE_TELEMETRY"] = "true";
        }
        return start;
    }

    internal static string? ParseEndpoint(string line)
    {
        var match = Regex.Match(line, @"(?:browser: |DevTools listening on )(ws://[^\s]+)");
        if (match.Success)
        {
            return match.Groups[1].Value;
        }
        // Official Lightpanda nightly: $msg="server running" address=127.0.0.1:PORT.
        match = Regex.Match(line, @"address=127\.0\.0\.1:(\d+)");
        return line.Contains("server running", StringComparison.Ordinal) && match.Success
            ? "ws://127.0.0.1:" + match.Groups[1].Value : null;
    }

    private async Task DrainAsync(StreamReader output, TaskCompletionSource<string> ready)
    {
        try
        {
            while (await output.ReadLineAsync() is { } line)
            {
                Console.Error.WriteLine($"[{options.Name}] {line}");
                if (ParseEndpoint(line) is { } endpoint)
                {
                    ready.TrySetResult(endpoint);
                }
            }
        }
        catch (Exception exception) when (exception is ObjectDisposedException or IOException)
        {
            // Disposing the owned process closes diagnostic pipes.
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }
        _disposed = true;
        try
        {
            if (Browser is not null)
            {
                if (options.Borrowed)
                {
                    Browser.Disconnect();
                }
                else
                {
                    try
                    {
                        await Browser.CloseAsync().WaitAsync(TimeSpan.FromSeconds(10));
                    }
                    catch (Exception exception) when (exception is PuppeteerException or TimeoutException or IOException)
                    {
                        ForcedTermination = true;
                    }
                }
            }
        }
        finally
        {
            if (!options.Borrowed)
            {
                if (options.AccountingDirectory is { } scope)
                {
                    ForcedTermination |= File.ReadAllText(Path.Combine(scope, "cgroup.events")).Contains("populated 1", StringComparison.Ordinal);
                    await File.WriteAllTextAsync(Path.Combine(scope, "cgroup.kill"), "1");
                    using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
                    while (File.ReadAllText(Path.Combine(scope, "cgroup.events")).Contains("populated 1", StringComparison.Ordinal))
                    {
                        await Task.Delay(20, timeout.Token);
                    }
                }
                else if (Process is not null && !Process.HasExited)
                {
                    ForcedTermination = true;
                    Process.Kill(entireProcessTree: true);
                }
                if (Process is not null)
                {
                    await Process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(10));
                }
            }
            Process?.Dispose();
            await Task.WhenAll(_drains).WaitAsync(TimeSpan.FromSeconds(10));
            if (_profile is not null)
            {
                Directory.Delete(_profile, recursive: true);
            }
        }
    }
}
