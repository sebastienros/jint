using System.Diagnostics;
using System.Security.Cryptography;

namespace BrowserComparison;

internal sealed record BinaryIdentity(string? VersionLabel, string? Executable,
    string[] Arguments, IReadOnlyDictionary<string, string> FileSha256)
{
    internal static async Task<BinaryIdentity> ReadAsync(AdapterOptions options)
    {
        var hashes = new Dictionary<string, string>(StringComparer.Ordinal);
        var paths = new[] { options.Executable }.Concat(options.Arguments ?? []).Where(path => path is not null).Cast<string>().ToList();
        // A framework-dependent Jint executable is identified by all neighboring managed dependencies,
        // not just the dotnet host or entry assembly. Hash before starting any measured lifecycle.
        if (options.Kind == "jint")
        {
            foreach (var entry in paths.ToArray())
            {
                if (File.Exists(entry))
                {
                    paths.AddRange(Directory.EnumerateFiles(Path.GetDirectoryName(Path.GetFullPath(entry))!, "*.dll"));
                }
            }
        }
        foreach (var path in paths.Distinct(StringComparer.Ordinal))
        {
            if (path is not null && File.Exists(path))
            {
                await using var file = File.OpenRead(path);
                hashes[Path.GetFullPath(path)] = Convert.ToHexString(await SHA256.HashDataAsync(file));
            }
        }
        return new BinaryIdentity(options.VersionLabel, options.Executable, options.Arguments ?? [], hashes);
    }
}

/// <summary>
/// Operating-system counters for one named PID. PeakWorkingSet64 is a lifetime high-water mark;
/// subtracting two peaks would not produce a stage peak. Renderer children are deliberately not folded in.
/// </summary>
internal sealed record ProcessSnapshot(double? CpuMilliseconds, long? LifetimePeakWorkingSetBytes, string? UnavailableReason)
{
    internal static ProcessSnapshot? Read(Process? process)
    {
        if (process is null)
        {
            return null;
        }
        try
        {
            process.Refresh();
            var peak = process.PeakWorkingSet64;
            return new ProcessSnapshot(process.TotalProcessorTime.TotalMilliseconds, peak > 0 ? peak : null,
                peak > 0 ? null : "The OS does not expose a positive lifetime peak working set for this PID.");
        }
        catch (Exception exception) when (exception is InvalidOperationException or System.ComponentModel.Win32Exception or PlatformNotSupportedException)
        {
            return new ProcessSnapshot(null, null, exception.Message);
        }
    }
}

/// <summary>Observed PID working-set maximum during page work, not an OS high-water guarantee.</summary>
internal sealed class ProcessMemorySampler : IAsyncDisposable
{
    internal const int IntervalMilliseconds = 10;
    private readonly Process? _process;
    private readonly CancellationTokenSource _stop = new();
    private readonly Task _sampling;
    private bool _disposed;
    internal long? PeakBytes { get; private set; }
    internal int Samples { get; private set; }
    internal string? UnavailableReason { get; private set; }

    internal ProcessMemorySampler(Process? process)
    {
        _process = process;
        Sample();
        _sampling = ObserveAsync();
    }

    private void Sample()
    {
        if (_process is null)
        {
            return;
        }
        try
        {
            _process.Refresh();
            var bytes = _process.WorkingSet64;
            if (bytes > 0)
            {
                PeakBytes = Math.Max(PeakBytes ?? 0, bytes);
                Samples++;
            }
            else
            {
                UnavailableReason = "The OS returned no positive working-set value.";
            }
        }
        catch (Exception exception) when (exception is InvalidOperationException or System.ComponentModel.Win32Exception or PlatformNotSupportedException)
        {
            UnavailableReason = exception.Message;
        }
    }

    private async Task ObserveAsync()
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(IntervalMilliseconds));
        try
        {
            while (await timer.WaitForNextTickAsync(_stop.Token))
            {
                Sample();
            }
        }
        catch (OperationCanceledException) when (_stop.IsCancellationRequested)
        {
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }
        _disposed = true;
        await _stop.CancelAsync();
        await _sampling;
        Sample();
        _stop.Dispose();
    }
}
