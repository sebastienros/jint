#if NET8_0_OR_GREATER
#nullable enable

using System.Security.Cryptography;
using System.Text;

namespace Jint.Tests.Wpt;

/// <summary>
/// A controlled timestamp source for one reviewed, pure-timer corpus file. This supplies no timer queue:
/// the engine's TimerQueue still owns conversion, clamping, ordering, cancellation and checkpoints.
/// </summary>
internal sealed class WptTimerClock : TimeProvider
{
    internal const string File = "html/webappapis/timers/negative-setinterval.any.js";
    private const string SourceHash = "CC6BBA6AA3BFFC42321A44380937B4B7FB97AD494DD3A6DB919122372DDF1400";
    private const string PreludeHash = "FAC3C4A438171FEA135897419ACDDBEE061E6CD6B10170D5736397572D72EA44";

    private readonly TimeProvider _hostClock;
    private readonly long _started;
    private long _ticks;
    private int _advances;
    private int _pumps;

    private WptTimerClock(TimeProvider hostClock)
    {
        _hostClock = hostClock;
        _started = hostClock.GetTimestamp();
    }

    public override long TimestampFrequency => TimeSpan.TicksPerSecond;

    public override long GetTimestamp() => _ticks;

    private TimeSpan HostElapsed => _hostClock.GetElapsedTime(_started);

    internal string Evidence => $"controlled timer clock: pumps={_pumps}, advances={_advances}, "
        + $"timerElapsed={TimeSpan.FromTicks(_ticks):c}, hostElapsed={HostElapsed:c}";

    /// <summary>
    /// Admission is exact and fail-closed. The reviewed file has no META helpers, Date/performance reads,
    /// workers, Atomics deadlines or I/O. Performance shares this provider, so its absence is essential.
    /// Date and execution constraints retain separate clocks. The shim only registers a test and records done().
    /// Any source change requires that isolation argument to be reviewed again, not a clock fallback.
    /// </summary>
    internal static WptTimerClock? ForFile(
        string name, string source, string prelude, bool hasMetaScripts, TimeProvider? hostClock = null)
    {
        if (!string.Equals(name, File, StringComparison.Ordinal))
        {
            return null;
        }

        // Vendor bytes are LF-pinned by .gitattributes. The locally authored shim may be checked out CRLF;
        // normalize only its line endings, never the vendored subject's bytes.
        var sourceHash = Hash(source);
        var preludeHash = Hash(prelude.Replace("\r\n", "\n", StringComparison.Ordinal));
        if (hasMetaScripts || sourceHash != SourceHash || preludeHash != PreludeHash)
        {
            throw new InvalidOperationException($"Controlled timer clock admission refused for {name}: "
                + $"source SHA256={sourceHash}, shim SHA256={preludeHash}, META scripts={hasMetaScripts}. "
                + "Review clock/I/O isolation before updating the admission pins (#3937).");
        }

        return new WptTimerClock(hostClock ?? System);
    }

    internal string? DeadlineError(TimeSpan deadline)
        => HostElapsed >= deadline ? $"the controlled timer run did not complete within {deadline}" : null;

    internal void RecordPump() => _pumps++;

    internal void Advance(TimeSpan untilDue)
    {
        if (untilDue <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(untilDue), "Only a future due timer can advance time.");
        }

        _ticks = checked(_ticks + untilDue.Ticks);
        _advances++;
    }

    private static string Hash(string source) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(source)));
}
#endif
