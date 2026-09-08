using System.Diagnostics;
using System.Security.Cryptography;

namespace BrowserComparison;

internal sealed record BinaryIdentity(string? VersionLabel, string? Executable,
    string[] Arguments, IReadOnlyDictionary<string, string> FileSha256, string[] DependencyRoots, string DependencyCoverage)
{
    internal static async Task<BinaryIdentity> ReadAsync(AdapterOptions options)
    {
        var roots = new HashSet<string>(StringComparer.Ordinal);
        foreach (var entry in new[] { options.Executable }.Concat(options.Arguments ?? []).OfType<string>())
        {
            if (!File.Exists(entry)) continue;
            var file = new FileInfo(Path.GetFullPath(entry));
            var resolved = file.ResolveLinkTarget(true)?.FullName ?? file.FullName;
            var directory = Path.GetDirectoryName(resolved)!;
            // A Chromium macOS executable depends on sibling Frameworks, resources and helper apps.
            var app = directory.IndexOf(".app" + Path.DirectorySeparatorChar, StringComparison.Ordinal);
            roots.Add(app < 0 ? directory : directory[..(app + 4)]);
        }
        foreach (var root in options.DependencyRoots ?? []) roots.Add(Path.GetFullPath(root));
        // Native loaders and libraries can live outside a browser installation. Hash the complete
        // system library trees, not just ldd's startup subset (dlopen and child helpers matter too).
        if (OperatingSystem.IsLinux())
            foreach (var root in new[] { "/lib", "/lib64", "/usr/lib", "/usr/lib64" })
                if (Directory.Exists(root)) roots.Add(root);
        var hashes = await HashRootsAsync(roots);
        if (options.Executable is not null)
        {
            await using var executable = File.OpenRead(options.Executable);
            hashes[options.Executable] = Convert.ToHexString(await SHA256.HashDataAsync(executable));
        }
        return new BinaryIdentity(options.VersionLabel, options.Executable, options.Arguments ?? [], hashes,
            roots.Order(StringComparer.Ordinal).ToArray(), OperatingSystem.IsLinux()
                ? "installation-and-system-library-trees" : "installation-trees; OS shared runtime is diagnostic-only");
    }
    internal static async Task<Dictionary<string, string>> HashRootsAsync(IEnumerable<string> roots)
    {
        var hashes = new Dictionary<string, string>(StringComparer.Ordinal);
        var visited = new HashSet<string>(StringComparer.Ordinal);
        var pending = new Stack<string>(roots);
        while (pending.TryPop(out var path))
        {
            FileSystemInfo info = Directory.Exists(path) ? new DirectoryInfo(path) : new FileInfo(path);
            var actual = info.ResolveLinkTarget(true)?.FullName ?? info.FullName;
            if (!visited.Add(actual)) continue;
            if (Directory.Exists(actual))
            {
                foreach (var child in Directory.EnumerateFileSystemEntries(actual)) pending.Push(child);
            }
            else
            {
                await using var file = File.OpenRead(actual); // A missing/unreadable dependency invalidates provenance.
                hashes[actual] = Convert.ToHexString(await SHA256.HashDataAsync(file));
            }
        }
        return hashes;
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
