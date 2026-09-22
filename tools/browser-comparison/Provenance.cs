using System.Diagnostics;
using System.Security.Cryptography;
using System.Runtime.ExceptionServices;
using System.Threading.Channels;

namespace BrowserComparison;

internal sealed record BinaryIdentity(string? VersionLabel, string? Executable,
    string[] Arguments, IReadOnlyDictionary<string, string> FileSha256, string[] DependencyRoots, string DependencyCoverage,
    IReadOnlyDictionary<string, string> DependencyExclusions)
{
    internal const string PrivateKeyDirectory = "/etc/ssl/private";
    internal const string PrivateKeyExclusionReason = "private-key-directory; contents intentionally not read";

    internal static async Task<BinaryIdentity> ReadAsync(AdapterOptions options)
        => (await ReadBatchCoreAsync([options], union: false, includeSystemLibraries: true))[0];

    internal static Task<BinaryIdentity[]> ReadBatchAsync(AdapterOptions[] options)
        => ReadBatchCoreAsync(options, union: true, includeSystemLibraries: true);

    // Synthetic tests exercise installation unions without scanning the host's system libraries.
    internal static Task<BinaryIdentity[]> ReadInstallationBatchAsync(AdapterOptions[] options)
        => ReadBatchCoreAsync(options, union: true, includeSystemLibraries: false);

    private static async Task<BinaryIdentity[]> ReadBatchCoreAsync(AdapterOptions[] options, bool union, bool includeSystemLibraries)
    {
        if (options.Length == 0 || options.Select(x => x.Name).Distinct(StringComparer.Ordinal).Count() != options.Length)
            throw new ArgumentException("Identity batch requires nonempty, uniquely named adapters.");
        var roots = new HashSet<string>(StringComparer.Ordinal);
        foreach (var adapter in options)
        {
            adapter.Validate();
            foreach (var entry in new[] { adapter.Executable }.Concat(adapter.Arguments ?? []).OfType<string>())
            {
                if (OperatingSystem.IsLinux() && Path.IsPathFullyQualified(entry)) RejectExcludedRoot(entry, PrivateKeyDirectory);
                if (!File.Exists(entry))
                {
                    if (entry == adapter.Executable) throw new FileNotFoundException("Adapter executable is missing.", entry);
                    continue;
                }
                if (OperatingSystem.IsLinux()) RejectExcludedRoot(entry, PrivateKeyDirectory);
                var resolved = ResolvePath(entry, null);
                var directory = Path.GetDirectoryName(resolved)!;
                // A Chromium macOS executable depends on sibling Frameworks, resources and helper apps.
                var app = directory.IndexOf(".app" + Path.DirectorySeparatorChar, StringComparison.Ordinal);
                roots.Add(app < 0 ? directory : directory[..(app + 4)]);
            }
            foreach (var root in adapter.DependencyRoots ?? [])
            {
                if (OperatingSystem.IsLinux()) RejectExcludedRoot(root, PrivateKeyDirectory);
                roots.Add(ResolvePath(root, null));
            }
        }
        // Include dlopen candidates and child helpers, not just the startup loader dependency subset.
        if (includeSystemLibraries && OperatingSystem.IsLinux())
            foreach (var root in new[] { "/lib", "/lib64", "/usr/lib", "/usr/lib64" })
                if (Directory.Exists(root)) roots.Add(ResolvePath(root, null));
        var hashes = await HashRootsAsync(roots);
        foreach (var adapter in options)
        {
            if (adapter.Executable is not null)
            {
                // Retain the configured spelling as well as the canonical key without a second read.
                hashes[adapter.Executable] = hashes[ResolvePath(adapter.Executable, null)];
            }
        }
        var coverage = includeSystemLibraries && OperatingSystem.IsLinux()
            ? (union ? "adapter-and-harness-union-installation-and-system-library-trees-except-private-key-directory"
                     : "installation-and-system-library-trees-except-private-key-directory")
            : (union ? "installation-union; OS shared runtime is diagnostic-only"
                     : "installation-trees; OS shared runtime is diagnostic-only");
        var dependencyRoots = roots.Order(StringComparer.Ordinal).ToArray();
        IReadOnlyDictionary<string, string> exclusions = OperatingSystem.IsLinux()
            ? new Dictionary<string, string>(StringComparer.Ordinal) { [PrivateKeyDirectory] = PrivateKeyExclusionReason }
            : new Dictionary<string, string>(StringComparer.Ordinal);
        return options.Select(adapter => new BinaryIdentity(adapter.VersionLabel, adapter.Executable,
            adapter.Arguments ?? [], hashes, dependencyRoots, coverage, exclusions)).ToArray();
    }
    internal static Task<Dictionary<string, string>> HashRootsAsync(IEnumerable<string> roots)
        => HashRootsAsync(roots, OperatingSystem.IsLinux() ? PrivateKeyDirectory : null);

    // The boundary and hasher seam are internal for synthetic filesystem/concurrency tests only.
    internal static Task<Dictionary<string, string>> HashRootsAsync(IEnumerable<string> roots, string? excludedDirectory)
        => HashRootsAsync(roots, excludedDirectory, HashFile);

    internal static async Task<Dictionary<string, string>> HashRootsAsync(IEnumerable<string> roots,
        string? excludedDirectory, Func<string, (string Hash, long Bytes)> hashFile)
    {
        if (excludedDirectory is not null) excludedDirectory = ResolvePath(excludedDirectory, null);
        await using var progress = new DependencyHashProgress();
        using var stop = new CancellationTokenSource();
        var paths = Channel.CreateBounded<string>(new BoundedChannelOptions(32)
        {
            SingleWriter = true,
            FullMode = BoundedChannelFullMode.Wait
        });
        ExceptionDispatchInfo? failure = null;
        void Fail(Exception error)
        {
            Interlocked.CompareExchange(ref failure, ExceptionDispatchInfo.Capture(error), null);
            stop.Cancel();
        }

        // Only this producer resolves links, applies the boundary, and owns the visited set.
        // A bounded handoff limits resolved file paths awaiting a worker.
        async Task ProduceAsync()
        {
            try
            {
                var visited = new HashSet<string>(StringComparer.Ordinal);
                var pending = new Stack<string>();
                foreach (var root in roots)
                {
                    if (excludedDirectory is not null) RejectExcludedRoot(root, excludedDirectory);
                    pending.Push(ResolvePath(root, excludedDirectory));
                    while (pending.TryPop(out var path))
                    {
                        stop.Token.ThrowIfCancellationRequested();
                        progress.Begin("resolving-next-path", null);
                        // Canonical parents need no repeated ancestor traversal for ordinary files.
                        FileSystemInfo info = Directory.Exists(path) ? new DirectoryInfo(path) : new FileInfo(path);
                        var target = info.ResolveLinkTarget(true);
                        var actual = target is null ? info.FullName : ResolvePath(target.FullName, excludedDirectory);
                        if (excludedDirectory is not null && IsWithin(actual, excludedDirectory)) continue;
                        if (!visited.Add(actual)) continue;
                        if (Directory.Exists(actual))
                        {
                            progress.Begin("enumerating", actual);
                            foreach (var child in Directory.EnumerateFileSystemEntries(actual)) pending.Push(child);
                        }
                        else
                        {
                            progress.Begin("waiting-for-hash-worker", actual);
                            await paths.Writer.WriteAsync(actual, stop.Token);
                        }
                    }
                }
            }
            catch (Exception error)
            {
                Fail(error);
            }
            finally
            {
                paths.Writer.TryComplete();
            }
        }

        // Exactly four workers, outside measurement. Each file is freshly read in each snapshot;
        // there is no cached hash, metadata shortcut, or best-effort omission on failure.
        var workers = Enumerable.Range(0, 4).Select(_ => Task.Run(async () =>
        {
            var hashes = new Dictionary<string, string>(StringComparer.Ordinal);
            try
            {
                await foreach (var path in paths.Reader.ReadAllAsync(stop.Token))
                {
                    stop.Token.ThrowIfCancellationRequested();
                    progress.BeginHash(path);
                    try
                    {
                        var (hash, bytes) = hashFile(path);
                        hashes.Add(path, hash);
                        progress.Completed(bytes);
                    }
                    finally
                    {
                        progress.EndHash(path);
                    }
                }
            }
            catch (Exception error)
            {
                Fail(error);
            }
            return hashes;
        })).ToArray();
        await ProduceAsync();
        var results = await Task.WhenAll(workers);
        // Join every in-flight reader before surfacing the original failure, including producer
        // errors while the queue is full. A partial dictionary never becomes a manifest.
        failure?.Throw();
        var combined = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var (path, hash) in results.SelectMany(result => result).OrderBy(entry => entry.Key, StringComparer.Ordinal))
            combined.Add(path, hash);
        return combined;
    }

    private static (string Hash, long Bytes) HashFile(string path)
    {
        using var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read,
            bufferSize: 128 * 1024, FileOptions.SequentialScan);
        return (Convert.ToHexString(SHA256.HashData(file)), file.Length);
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
        private readonly HashSet<string> _activeHashes = new(StringComparer.Ordinal);
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

        internal void BeginHash(string path)
        {
            lock (_gate) _activeHashes.Add(path);
        }

        internal void EndHash(string path)
        {
            lock (_gate) _activeHashes.Remove(path);
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
                    activeHashPaths = _activeHashes.Order(StringComparer.Ordinal).ToArray(),
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
