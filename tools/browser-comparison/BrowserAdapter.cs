using System.Diagnostics;
using PuppeteerSharp;

namespace BrowserComparison;

/// <summary>Owns fresh Jint/Chromium processes; only borrows an external Lightpanda connection.</summary>
internal sealed class BrowserAdapter(AdapterOptions options) : IAsyncDisposable
{
    internal IBrowser? Browser { get; private set; }
    internal Process? Process { get; private set; }
    internal int? ProcessId { get; private set; }
    private bool _disposed;

    internal async Task StartAsync()
    {
        if (options.Kind == "chromium")
        {
            Browser = await Puppeteer.LaunchAsync(new LaunchOptions
            {
                ExecutablePath = options.Executable,
                Headless = true,
                DefaultViewport = null,
                Args = options.Arguments ?? [],
                Timeout = 30000,
            });
            Process = Browser.Process;
        }
        else
        {
            var endpoint = options.Endpoint;
            if (options.Kind == "jint")
            {
                var start = new ProcessStartInfo(options.Executable!)
                {
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                };
                foreach (var argument in options.Arguments ?? [])
                {
                    start.ArgumentList.Add(argument);
                }
                start.ArgumentList.Add("serve");
                start.ArgumentList.Add("--port");
                start.ArgumentList.Add("0");
                Process = System.Diagnostics.Process.Start(start)
                    ?? throw new InvalidOperationException("Jint did not start.");
                using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
                while (await Process.StandardOutput.ReadLineAsync(timeout.Token) is { } line)
                {
                    if (line.Trim().StartsWith("browser: ", StringComparison.Ordinal))
                    {
                        endpoint = line.Trim()["browser: ".Length..];
                        break;
                    }
                }
                if (endpoint is null)
                {
                    throw new InvalidOperationException("Jint exited without announcing a browser endpoint.");
                }
                // Drain subsequent diagnostics so a full pipe cannot stall the browser.
                _ = DrainAsync(Process.StandardOutput);
            }
            else if (options.ProcessId is { } id)
            {
                Process = System.Diagnostics.Process.GetProcessById(id);
            }
            Browser = await Puppeteer.ConnectAsync(new ConnectOptions
            {
                BrowserWSEndpoint = endpoint,
                DefaultViewport = null,
            }).WaitAsync(TimeSpan.FromSeconds(30));
        }
        ProcessId = Process?.Id;
    }

    private static async Task DrainAsync(StreamReader output)
    {
        try
        {
            while (await output.ReadLineAsync() is not null)
            {
            }
        }
        catch (ObjectDisposedException)
        {
            // Process disposal closes the pipe after termination.
        }
        catch (IOException)
        {
            // The owned process may terminate while its output is draining.
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
                if (options.Kind == "chromium")
                {
                    await Browser.CloseAsync().WaitAsync(TimeSpan.FromSeconds(10));
                }
                else
                {
                    Browser.Disconnect();
                }
            }
        }
        finally
        {
            if (Process is not null)
            {
                if (options.Kind != "lightpanda" && !Process.HasExited)
                {
                    Process.Kill(entireProcessTree: true);
                    await Process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(10));
                }
                Process.Dispose();
            }
        }
    }
}
