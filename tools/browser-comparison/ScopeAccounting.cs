using System.Diagnostics;
using System.Globalization;

namespace BrowserComparison;

/// <summary>Kernel counters for an owned Linux cgroup v2, retaining CPU from exited descendants.</summary>
internal sealed record ScopeSnapshot(long CpuMicroseconds, long CurrentChargedBytes, long LifetimePeakChargedBytes,
    ScopeMemberObservation[] Members, bool Populated)
{
    internal static ScopeSnapshot? Read(string? directory, Func<string, string>? readDiagnostic = null)
    {
        if (directory is null)
        {
            return null;
        }
        var cpu = ReadFields(Path.Combine(directory, "cpu.stat"));
        var events = ReadFields(Path.Combine(directory, "cgroup.events"));
        return new ScopeSnapshot(cpu["usage_usec"], ReadCount(Path.Combine(directory, "memory.current")),
            ReadCount(Path.Combine(directory, "memory.peak")), ScopeMemberObservation.Read(directory, readDiagnostic ?? File.ReadAllText), events["populated"] != 0);
    }

    internal static Dictionary<string, long> ReadFields(string path) => File.ReadAllLines(path)
        .Select(line => line.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        .ToDictionary(parts => parts[0], parts => long.Parse(parts[1], CultureInfo.InvariantCulture), StringComparer.Ordinal);

    private static long ReadCount(string path) => long.Parse(File.ReadAllText(path).Trim(), CultureInfo.InvariantCulture);
}

/// <summary>Fallible, non-atomic PID diagnostics; never a substitute for kernel scope counters.</summary>
internal sealed record ScopeMemberObservation(string? ProcessId, string? KernelStartTime, string Status, string Phase, string? Error)
{
    internal static ScopeMemberObservation[] Read(string directory, Func<string, string> read)
    {
        var membership = Path.Combine(directory, "cgroup.procs");
        string[] pids;
        try
        {
            pids = Members(read(membership));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return [new(null, null, "membership-unavailable", "membership-initial", Describe(exception))];
        }
        return pids.Select(Observe).ToArray();

        ScopeMemberObservation Observe(string pid)
        {
            string? startTime = null;
            var phase = "stat-initial";
            try
            {
                startTime = StartTime(read("/proc/" + pid + "/stat"));
                if (startTime is null) return new(pid, null, "partial-identity", phase, "Missing or malformed kernel start-time field.");
                phase = "membership-after-stat";
                if (!Members(read(membership)).Contains(pid, StringComparer.Ordinal))
                    return new(pid, startTime, "membership-lost", phase, null);
                phase = "stat-recheck";
                var rechecked = StartTime(read("/proc/" + pid + "/stat"));
                if (rechecked is null) return new(pid, startTime, "partial-identity", phase, "Missing or malformed kernel start-time field.");
                if (rechecked != startTime) return new(pid, startTime, "identity-changed", phase, "PID was reused or its observed identity changed.");
                phase = "membership-final";
                if (!Members(read(membership)).Contains(pid, StringComparer.Ordinal))
                    return new(pid, startTime, "membership-lost", phase, null);
                return new(pid, startTime, "observed-non-atomic", phase, null);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                // An I/O error can mean a protected or unavailable procfs entry; it does not prove exit.
                return new(pid, startTime, "identity-unavailable", phase, Describe(exception));
            }
        }
    }

    private static string[] Members(string value) => value.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    private static string Describe(Exception exception) => exception.GetType().Name + ": " + exception.Message;
    private static string? StartTime(string stat)
    {
        var end = stat.LastIndexOf(')');
        if (end < 0 || end + 2 >= stat.Length) return null;
        var fields = stat[(end + 2)..].Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return fields.Length > 19 && ulong.TryParse(fields[19], NumberStyles.None, CultureInfo.InvariantCulture, out _)
            ? fields[19] : null; // Field 22, after the parenthesized command and fields 3..21.
    }
}

internal sealed record ScopeMemorySample(double ElapsedMilliseconds, long ChargedBytes, string Stage);

/// <summary>Simultaneous cgroup charged usage, not a sum of member lifetime RSS peaks.</summary>
internal sealed class ScopeMemorySampler : IAsyncDisposable
{
    private readonly string? _directory;
    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private readonly CancellationTokenSource _stop = new();
    private readonly Task _sampling;
    private bool _disposed;
    private readonly List<ScopeMemorySample> _samples = [];
    private readonly List<(string Stage, double At)> _boundaries = [];
    private readonly object _sync = new();
    private string _stage = "launch";
    internal double ElapsedMilliseconds => _clock.Elapsed.TotalMilliseconds;
    internal ScopeMemorySample[] Snapshot() { lock (_sync) { return _samples.ToArray(); } }
    internal void Stage(string stage)
    {
        lock (_sync)
        {
            if (_boundaries.Count > 0 && _stage == stage) return;
            Sample();
            _stage = stage;
            _boundaries.Add((stage, _clock.Elapsed.TotalMilliseconds));
            Sample();
        }
    }
    internal double Duration(string stage)
    {
        lock (_sync)
        {
            var index = _boundaries.FindIndex(x => x.Stage == stage);
            return index < 0 ? 0 : (index + 1 < _boundaries.Count ? _boundaries[index + 1].At : _clock.Elapsed.TotalMilliseconds) - _boundaries[index].At;
        }
    }

    internal ScopeMemorySampler(string? directory)
    {
        _directory = directory;
        Sample();
        _sampling = ObserveAsync();
    }

    private void Sample()
    {
        lock (_sync)
        {
            if (_directory is not null)
            {
                var value = long.Parse(File.ReadAllText(Path.Combine(_directory, "memory.current")).Trim(), CultureInfo.InvariantCulture);
                _samples.Add(new ScopeMemorySample(_clock.Elapsed.TotalMilliseconds, value, _stage));
            }
        }
    }

    private async Task ObserveAsync()
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(ProcessMemorySampler.IntervalMilliseconds));
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
        _clock.Stop();
        _stop.Dispose();
    }
}
