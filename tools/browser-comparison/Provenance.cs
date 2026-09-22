using System.Diagnostics;
using System.Security.Cryptography;

namespace BrowserComparison;

internal sealed record BinaryIdentity(string? VersionLabel, string? Executable,
    string[] Arguments, IReadOnlyDictionary<string, string> FileSha256, string[] DependencyRoots, string DependencyCoverage,
    IReadOnlyDictionary<string, string> DependencyExclusions)
{
    internal const string PrivateKeyDirectory = "/etc/ssl/private";
    internal const string PrivateKeyExclusionReason = "private-key-directory; contents intentionally not read";

    internal static async Task<BinaryIdentity> ReadAsync(AdapterOptions options)
    {
        var roots = new HashSet<string>(StringComparer.Ordinal);
        foreach (var entry in new[] { options.Executable }.Concat(options.Arguments ?? []).OfType<string>())
        {
            if (OperatingSystem.IsLinux() && Path.IsPathFullyQualified(entry)) RejectExcludedRoot(entry, PrivateKeyDirectory);
            if (!File.Exists(entry)) continue;
            if (OperatingSystem.IsLinux()) RejectExcludedRoot(entry, PrivateKeyDirectory);
            var file = new FileInfo(Path.GetFullPath(entry));
            var resolved = file.ResolveLinkTarget(true)?.FullName ?? file.FullName;
            var directory = Path.GetDirectoryName(resolved)!;
            // A Chromium macOS executable depends on sibling Frameworks, resources and helper apps.
            var app = directory.IndexOf(".app" + Path.DirectorySeparatorChar, StringComparison.Ordinal);
            roots.Add(app < 0 ? directory : directory[..(app + 4)]);
        }
        foreach (var root in options.DependencyRoots ?? [])
        {
            if (OperatingSystem.IsLinux()) RejectExcludedRoot(root, PrivateKeyDirectory);
            roots.Add(Path.GetFullPath(root));
        }
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
                ? "installation-and-system-library-trees-except-private-key-directory" : "installation-trees; OS shared runtime is diagnostic-only",
            OperatingSystem.IsLinux()
                ? new Dictionary<string, string>(StringComparer.Ordinal) { [PrivateKeyDirectory] = PrivateKeyExclusionReason }
                : new Dictionary<string, string>(StringComparer.Ordinal));
    }
    internal static Task<Dictionary<string, string>> HashRootsAsync(IEnumerable<string> roots)
        => HashRootsAsync(roots, OperatingSystem.IsLinux() ? PrivateKeyDirectory : null);

    // The boundary parameter is internal for synthetic filesystem tests, never adapter configuration.
    internal static async Task<Dictionary<string, string>> HashRootsAsync(IEnumerable<string> roots, string? excludedDirectory)
    {
        if (excludedDirectory is not null) excludedDirectory = ResolvePath(excludedDirectory, null);
        await using var progress = new DependencyHashProgress();
        var hashes = new Dictionary<string, string>(StringComparer.Ordinal);
        var visited = new HashSet<string>(StringComparer.Ordinal);
        var pending = new Stack<string>();
        foreach (var root in roots)
        {
            if (excludedDirectory is not null) RejectExcludedRoot(root, excludedDirectory);
            pending.Push(ResolvePath(root, excludedDirectory));
        }
        while (pending.TryPop(out var path))
        {
            progress.Begin("resolving-next-path", null);
            // Children are enumerated from canonical parents. Only a link can leave that tree,
            // so ordinary files do not repeatedly resolve every ancestor in /usr/lib.
            FileSystemInfo info = Directory.Exists(path) ? new DirectoryInfo(path) : new FileInfo(path);
            var target = info.ResolveLinkTarget(true);
            var actual = target is null ? info.FullName : ResolvePath(target.FullName, excludedDirectory);
            // Check before enumeration/open: private keys are not runtime library dependencies.
            if (excludedDirectory is not null && IsWithin(actual, excludedDirectory)) continue;
            if (!visited.Add(actual)) continue;
            if (Directory.Exists(actual))
            {
                progress.Begin("enumerating", actual);
                foreach (var child in Directory.EnumerateFileSystemEntries(actual)) pending.Push(child);
            }
            else
            {
                progress.Begin("hashing", actual);
                await using var file = File.OpenRead(actual); // A missing/unreadable dependency invalidates provenance.
                hashes[actual] = Convert.ToHexString(await SHA256.HashDataAsync(file));
                progress.Completed(file.Length);
            }
        }
        return hashes;
    }

    // A timer reports even if directory enumeration or a single file read stops making progress.
    // This runs only during identity collection, outside accepted measurement windows.
    private sealed class DependencyHashProgress : IAsyncDisposable
    {
        private readonly object _gate = new();
        private readonly Stopwatch _elapsed = Stopwatch.StartNew();
        private readonly Timer _timer;
        private string _operation = "resolving-roots";
        private string? _path;
        private long _files;
        private long _bytes;

        internal DependencyHashProgress()
        {
            _timer = new Timer(_ => Report(), null, TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(30));
        }

        internal void Begin(string operation, string? path)
        {
            lock (_gate)
            {
                _operation = operation;
                _path = path; // Only canonical paths that passed the private-key boundary check.
            }
        }

        internal void Completed(long bytes)
        {
            lock (_gate)
            {
                _files++;
                _bytes += bytes;
            }
        }

        private void Report()
        {
            lock (_gate)
            {
                // JSON quoting keeps unusual filenames from becoming extra diagnostic lines.
                Console.Error.WriteLine("Dependency hashing progress: " + System.Text.Json.JsonSerializer.Serialize(new
                {
                    elapsedSeconds = _elapsed.Elapsed.TotalSeconds,
                    operation = _operation,
                    canonicalPath = _path,
                    completedFiles = _files,
                    completedFileBytes = _bytes
                }));
            }
        }

        public ValueTask DisposeAsync() => _timer.DisposeAsync();
    }

    internal static void RejectExcludedRoot(string path, string excludedDirectory)
    {
        excludedDirectory = ResolvePath(excludedDirectory, null);
        if (IsWithin(ResolvePath(path, excludedDirectory), excludedDirectory))
            throw new InvalidOperationException("An explicit dependency cannot enter the excluded private-key directory.");
    }

    private static bool IsWithin(string path, string directory)
        => path.Equals(directory, StringComparison.Ordinal)
           || path.StartsWith(directory + Path.DirectorySeparatorChar, StringComparison.Ordinal);

    internal static string ResolvePath(string path, string? excludedDirectory)
    {
        // Resolve parent links too: an explicitly configured file can live below a directory alias.
        var full = Path.GetFullPath(path);
        var current = Path.GetPathRoot(full)!;
        foreach (var component in full[current.Length..].Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries))
        {
            current = Path.Combine(current, component);
            FileSystemInfo info = Directory.Exists(current) ? new DirectoryInfo(current) : new FileInfo(current);
            current = info.LinkTarget is null ? info.FullName : info.ResolveLinkTarget(true)!.FullName;
            if (excludedDirectory is not null && IsWithin(current, excludedDirectory)) return current;
        }
        return current;
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
