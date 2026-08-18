using System.Diagnostics;
using BenchmarkDotNet.Validators;

namespace Jint.Benchmark;

/// <summary>
/// Refuses to start a measurement on a machine that is not quiet enough to measure on.
///
/// <para>BenchmarkDotNet has no notion of whether the host is fit to measure: it will produce a
/// perfectly formatted table from a run that shared the machine with a build, a sync client or
/// another benchmark, and nothing in the output says so. That failure is silent and it is not
/// hypothetical — it invalidated three separate runs while this harness was being built, and in the
/// worst case a whole arm came back uniformly wrong while looking *more* self-consistent than the
/// good one, because uniform interference reads as low variance.</para>
///
/// <para>So this samples background CPU before the first benchmark and fails the run outright when
/// it is too high, naming the processes responsible. A refusal costs a minute; a contaminated
/// twenty-minute arm costs far more, and costs more still if nobody notices and it reaches a PR.</para>
///
/// <para>The threshold is expressed against a <em>single core</em> rather than against the whole
/// machine deliberately. These benchmarks are single-threaded, so what hurts is one competing
/// runnable thread, and dividing by 32 logical processors would hide exactly that. An otherwise
/// idle desktop measured ~15% of one core here (shell, compositor, telemetry agents), so the
/// default leaves room for that and catches genuine contention.</para>
/// </summary>
internal sealed class MachineStateValidator : IValidator
{
    /// <summary>Background CPU, as a percentage of one core, above which the run is refused.</summary>
    private const double MaxBackgroundPercentOfOneCore = 40.0;

    private static readonly TimeSpan SampleWindow = TimeSpan.FromSeconds(2);

    /// <summary>
    /// How long to wait for a busy machine to go quiet before giving up on it.
    ///
    /// <para>Waiting rather than failing immediately is the difference between a check that helps and
    /// one that gets switched off. By far the most common reason the machine is busy at this moment is
    /// that <em>we</em> just made it busy: the build that produced this executable, or the previous
    /// benchmark in a sweep, is still winding down. That clears on its own in seconds, and refusing
    /// the run — or worse, measuring through it — would both be the wrong answer.</para>
    /// </summary>
    private static readonly TimeSpan SettleTimeout = TimeSpan.FromSeconds(45);

    /// <summary>
    /// BenchmarkDotNet invokes validators more than once per run. Sampling costs seconds, so the
    /// verdict is computed once and reused; the window is short enough that it cannot go stale within
    /// a single run's validation phase.
    /// </summary>
    private static (double Load, string Offenders)? _cached;

    /// <summary>Set <c>JINT_BENCH_SKIP_IDLE_CHECK=1</c> to measure anyway (results are then not gate-quality).</summary>
    private static bool Skipped =>
        Environment.GetEnvironmentVariable("JINT_BENCH_SKIP_IDLE_CHECK") is "1" or "true";

    /// <summary>
    /// Whether a busy machine aborts the run. Gating runs refuse, because their numbers reach a PR
    /// and a contaminated one is worse than no number at all. Development runs only warn: blocking
    /// someone's dev loop because a sync client woke up would be disproportionate, and they are not
    /// quoting those numbers anywhere.
    /// </summary>
    private readonly bool _fatal;

    private MachineStateValidator(bool fatal) => _fatal = fatal;

    /// <summary>Warns but lets the run proceed. For development runs.</summary>
    public static MachineStateValidator Advisory { get; } = new(fatal: false);

    /// <summary>Aborts the run. For gating runs.</summary>
    public static MachineStateValidator Blocking { get; } = new(fatal: true);

    public bool TreatsWarningsAsErrors => _fatal;

    public IEnumerable<ValidationError> Validate(ValidationParameters validationParameters)
    {
        if (Skipped)
        {
            Console.WriteLine("// idle check skipped via JINT_BENCH_SKIP_IDLE_CHECK — results are not gate-quality.");
            yield break;
        }

        if (Debugger.IsAttached)
        {
            yield return new ValidationError(
                isCritical: _fatal,
                "A debugger is attached. It disarms the interpreter's tight-loop lane and invalidates every number.",
                benchmarkCase: null!);
            yield break;
        }

        var (load, offenders) = WaitForQuietMachine();
        Console.WriteLine($"// machine check: background CPU {load:F1}% of one core");

        if (load > MaxBackgroundPercentOfOneCore)
        {
            var remedy = _fatal
                ? "Close them and re-run, or set JINT_BENCH_SKIP_IDLE_CHECK=1 to measure anyway and accept that the numbers are not gate-quality."
                : "Proceeding, because this is not a gating run — but do not quote these numbers.";

            yield return new ValidationError(
                isCritical: _fatal,
                $"Machine is not idle: {load:F1}% of one core is busy (limit {MaxBackgroundPercentOfOneCore:F0}%). " +
                $"Top consumers: {offenders}. {remedy}",
                benchmarkCase: null!);
        }
    }

    /// <summary>
    /// Samples until the machine is quiet or <see cref="SettleTimeout"/> expires, returning the last
    /// reading either way. The verdict is cached for the rest of the validation phase.
    /// </summary>
    private static (double Load, string Offenders) WaitForQuietMachine()
    {
        if (_cached is { } hit)
        {
            return hit;
        }

        var deadline = Stopwatch.StartNew();
        var reading = SampleBackgroundLoad();
        var announced = false;

        while (reading.Load > MaxBackgroundPercentOfOneCore && deadline.Elapsed < SettleTimeout)
        {
            if (!announced)
            {
                Console.WriteLine(
                    $"// machine busy ({reading.Load:F0}% of one core: {reading.Offenders}) — " +
                    $"waiting up to {SettleTimeout.TotalSeconds:F0}s for it to settle");
                announced = true;
            }

            reading = SampleBackgroundLoad();
        }

        _cached = reading;
        return reading;
    }

    /// <summary>
    /// Returns background CPU as a percentage of one core, plus the processes responsible. Our own
    /// process is excluded: the host is about to hand off to a child and its own cost is not
    /// contention.
    /// </summary>
    private static (double Load, string Offenders) SampleBackgroundLoad()
    {
        var self = Environment.ProcessId;
        var first = Snapshot(self);
        var clock = Stopwatch.StartNew();
        Thread.Sleep(SampleWindow);
        clock.Stop();
        var second = Snapshot(self);

        var deltas = new List<(string Name, double Percent)>();
        foreach (var (id, (name, cpu)) in second)
        {
            if (!first.TryGetValue(id, out var before))
            {
                continue;
            }

            var percent = 100.0 * (cpu - before.Cpu).TotalSeconds / clock.Elapsed.TotalSeconds;
            if (percent > 0.5)
            {
                deltas.Add((name, percent));
            }
        }

        var total = deltas.Sum(d => d.Percent);
        var offenders = string.Join(", ", deltas
            .OrderByDescending(d => d.Percent)
            .Take(4)
            .Select(d => $"{d.Name} {d.Percent:F0}%"));

        return (total, offenders.Length == 0 ? "none" : offenders);
    }

    private static Dictionary<int, (string Name, TimeSpan Cpu)> Snapshot(int self)
    {
        var map = new Dictionary<int, (string, TimeSpan)>();
        foreach (var process in Process.GetProcesses())
        {
            try
            {
                // Protected and exiting processes throw on either access; they are not what we are
                // looking for, so skipping them is the correct answer rather than a lost sample.
                if (process.Id != self)
                {
                    map[process.Id] = (process.ProcessName, process.TotalProcessorTime);
                }
            }
            catch (Exception)
            {
                // ignored by design — see above
            }
            finally
            {
                process.Dispose();
            }
        }

        return map;
    }
}
