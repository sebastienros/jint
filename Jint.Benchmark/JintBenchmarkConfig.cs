using BenchmarkDotNet.Columns;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Jobs;
using Perfolizer.Horology;

namespace Jint.Benchmark;

/// <summary>Which measurement environment a run uses. Selected by <c>JINT_BENCH_MODE</c>.</summary>
internal enum BenchmarkEnvironment
{
    /// <summary>BenchmarkDotNet's bare <c>DefaultJob</c>, as this suite ran before. Kept so the old
    /// environment stays measurable and comparable.</summary>
    Legacy,

    /// <summary>The default: pinned, fixed clock, deterministic GC, machine checked before starting.</summary>
    Stable,

    /// <summary>What a gating run uses — <see cref="Stable"/> plus the launches needed to see
    /// between-process variance at all. Roughly three times the wall-clock cost.</summary>
    Gate,
}

/// <summary>
/// The measurement environment every Jint benchmark runs under.
///
/// <para>Before this existed, all ~130 benchmark classes ran on BenchmarkDotNet's bare
/// <c>DefaultJob</c>. That is the root of most of what went wrong, because nearly everything needed
/// here was already in BenchmarkDotNet and simply unused: <c>LaunchCount</c>, <c>WithAffinity</c>,
/// <c>WithPowerPlan</c>, <c>WithGcConcurrent</c>, <c>WithEnvironmentVariable</c>,
/// <c>AnalyzeLaunchVariance</c>, and — the sharpest one — the <c>MValue</c> column and
/// <c>MultimodalDistributionAnalyzer</c>. BenchmarkDotNet was already computing the multimodality
/// statistic that flags a row as untrustworthy, and this project was discarding it.</para>
///
/// <para><b>The error term that matters.</b> <c>DefaultJob</c> sets <c>LaunchCount = 1</c>, so code
/// layout, heap placement, tiered-compilation outcome and the starting core are constants
/// <em>within</em> the measured process and variables <em>between</em> processes. None of them reach
/// the reported error. That is precisely the shape of an A/B pair that looks decisive and then will
/// not reproduce: the error bar is honest and is measuring the wrong quantity. More iterations
/// cannot fix it — they resample within one process. Only more launches can.</para>
///
/// <para><b>Tiered compilation and dynamic PGO stay at their production defaults, and nothing here
/// works around them.</b> That is a measured decision, not an omission. On a machine verified idle,
/// <c>TypeofStringGuard</c> over ten launches came back at StdDev 0.783 ms with MValue 2.000 —
/// unimodal — against 1.946 ms on a machine that was quietly busy. The bistable "slow mode" that
/// looked like a tiering artifact was mostly contention. Two mitigations were built and measured
/// against that baseline: running the workload to JIT quiescence (StdDev 1.224) and applying
/// <c>DOTNET_TC_QuickJitForLoops=0</c> + <c>DOTNET_TC_CallCountingDelayMs=0</c> (StdDev 1.275, and
/// ~13% slower). Neither helped, so neither shipped.</para>
///
/// <para><b>The fixed-clock power plan is opt-in for the same reason.</b> Measured one factor at a
/// time against <c>legacy</c>, blocking GC costs ~1% and pinning ~2-3%, while the fixed clock costs
/// <b>~47%</b> — and none of the three showed a variance reduction that survived the noise. A
/// control that expensive has to earn its place, and this one did not, so it applies only when
/// <c>JINT_BENCH_FIXED_CLOCK=1</c> is set explicitly. It remains useful for one thing the numbers do
/// support: making absolute figures comparable across sessions, since it removes boost as a free
/// variable.</para>
///
/// <para><b>What actually remains, then, is statistical.</b> No machine control reduced the
/// between-process offset, which is the term that breaks reproducibility. Only sampling it can:
/// <c>LaunchCount</c> in gate mode, and the interleaved paired comparison in
/// <c>measure-paired.ps1</c> for A/B work.</para>
/// </summary>
internal static class JintBenchmarkConfig
{
    private const string ModeVariable = "JINT_BENCH_MODE";
    private const string PowerPlanVariable = "JINT_BENCH_POWERPLAN";

    /// <summary>Set to <c>off</c> to skip processor pinning. Present so each control can be ablated alone.</summary>
    private const string AffinityVariable = "JINT_BENCH_AFFINITY";

    /// <summary>Set to <c>concurrent</c> to keep background GC. Present so each control can be ablated alone.</summary>
    private const string GcVariable = "JINT_BENCH_GC";

    /// <summary>
    /// Set to <c>1</c> to apply the fixed-clock plan named by <see cref="PowerPlanVariable"/>.
    /// Requiring both is deliberate: the plan GUID tends to live in a user-level environment variable
    /// forever, and applying it implicitly would silently cost ~47% on every run from then on.
    /// </summary>
    private const string FixedClockVariable = "JINT_BENCH_FIXED_CLOCK";

    /// <summary>
    /// Processes a gating run measures. Three is the smallest number that can show a between-process
    /// offset at all. It triples wall-clock cost, which is why it is not the default.
    /// </summary>
    private const int GateLaunchCount = 3;

    /// <summary>
    /// Iteration time for a row whose operation is long enough that few of them fit in an iteration.
    /// Deliberately not applied globally: raising it four-fold helps only rows whose operation is a
    /// large fraction of an iteration, and most of this suite is microsecond-scale rows that converge
    /// in the minimum 15 iterations either way. A class with a genuinely long, allocation-heavy
    /// operation should opt in with its own <c>[Config]</c>.
    /// </summary>
    public static readonly TimeInterval LongOperationIterationTime = TimeInterval.FromMilliseconds(2000);

    public static IConfig Create()
    {
        var mode = ResolveMode();
        var config = ManualConfig.Create(DefaultConfig.Instance);

        // Surface multimodality on every row. A bimodal row's Mean is not a summary of anything, and
        // this is the column that says so. It costs nothing and BenchmarkDotNet already computes it.
        config = config.AddColumn(StatisticColumn.MValue, StatisticColumn.Median);

        if (mode == BenchmarkEnvironment.Legacy)
        {
            Announce(mode, affinity: null, powerPlan: null, launchCount: 1, concurrentGc: true);
            return config;
        }

        // Check the machine is fit to measure on. BenchmarkDotNet has no such notion; without this the
        // only symptom of contamination is numbers that quietly disagree with the next run. A gating
        // run refuses outright; a development run only warns, so nobody's dev loop is blocked by a
        // sync client waking up.
        config = config.AddValidator(mode == BenchmarkEnvironment.Gate
            ? MachineStateValidator.Blocking
            : MachineStateValidator.Advisory);

        var job = Job.Default;

        var affinityOff = string.Equals(
            Environment.GetEnvironmentVariable(AffinityVariable), "off", StringComparison.OrdinalIgnoreCase);
        var affinity = affinityOff ? null : BenchmarkTopology.ResolveAffinityMask();
        if (affinity is { } mask)
        {
            job = job.WithAffinity(mask);
        }

        // BenchmarkDotNet already forces a power plan for the duration of a run (High performance by
        // default), which would otherwise overwrite a fixed-clock plan the host had made active.
        // Handing it the plan's GUID makes it apply that one and restore the previous plan on exit.
        var powerPlan = ResolvePowerPlan();
        if (powerPlan is { } plan)
        {
            job = job.WithPowerPlan(plan);
        }

        // Background GC does collection work on its own threads, on whatever core is free and
        // asynchronously to the iteration boundary. Blocking workstation GC keeps it on the benchmark
        // thread, inside the affinity mask and inside the iteration being measured.
        var concurrentGc = string.Equals(
            Environment.GetEnvironmentVariable(GcVariable), "concurrent", StringComparison.OrdinalIgnoreCase);
        job = job.WithGcConcurrent(concurrentGc);

        var launchCount = 1;
        if (mode == BenchmarkEnvironment.Gate)
        {
            launchCount = GateLaunchCount;
            job = job.WithLaunchCount(GateLaunchCount).WithAnalyzeLaunchVariance(true);
        }

        Announce(mode, affinity, powerPlan, launchCount, concurrentGc);
        return config.AddJob(job);
    }

    private static BenchmarkEnvironment ResolveMode()
    {
        var raw = Environment.GetEnvironmentVariable(ModeVariable);
        if (string.IsNullOrWhiteSpace(raw))
        {
            return BenchmarkEnvironment.Stable;
        }

        if (Enum.TryParse<BenchmarkEnvironment>(raw.Trim(), ignoreCase: true, out var parsed))
        {
            return parsed;
        }

        Console.Error.WriteLine($"// {ModeVariable}='{raw}' is not legacy|stable|gate — using stable.");
        return BenchmarkEnvironment.Stable;
    }

    private static Guid? ResolvePowerPlan()
    {
        if (Environment.GetEnvironmentVariable(FixedClockVariable) is not ("1" or "true"))
        {
            return null;
        }

        var raw = Environment.GetEnvironmentVariable(PowerPlanVariable);
        if (string.IsNullOrWhiteSpace(raw))
        {
            Console.Error.WriteLine(
                $"// {FixedClockVariable} is set but {PowerPlanVariable} is not — leaving the power plan alone.");
            return null;
        }

        if (Guid.TryParse(raw.Trim(), out var guid))
        {
            return guid;
        }

        Console.Error.WriteLine($"// {PowerPlanVariable}='{raw}' is not a GUID — leaving the power plan alone.");
        return null;
    }

    private static void Announce(
        BenchmarkEnvironment mode, nint? affinity, Guid? powerPlan, int launchCount, bool concurrentGc)
    {
        Console.WriteLine($"// Jint measurement environment: {mode.ToString().ToLowerInvariant()}");
        Console.WriteLine($"//   affinity    : {(affinity is { } m ? BenchmarkTopology.Describe(m) : "unpinned (scheduler decides)")}");
        Console.WriteLine($"//   power plan  : {(powerPlan is { } p ? $"fixed clock {p}" : "BenchmarkDotNet default (High performance)")}");
        Console.WriteLine($"//   gc          : {(concurrentGc ? "concurrent" : "blocking")} workstation");
        Console.WriteLine($"//   launches    : {launchCount}");
        Console.WriteLine("//   tiering     : production defaults (tiered compilation + dynamic PGO)");

        if (mode != BenchmarkEnvironment.Gate)
        {
            Console.WriteLine($"//   note        : set {ModeVariable}=gate before quoting a number in a PR.");
        }

        Console.WriteLine();
    }
}
