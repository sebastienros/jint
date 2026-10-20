#nullable enable
using System.Threading;
namespace Jint.Runtime.RegExp.GregsEx;

/// <summary>Finite immutable compilation ceilings; pattern units are native pattern units.</summary>
internal sealed record RegexCompileLimits
{
    /// <summary>Maximum pattern units.</summary>
    public int MaxPatternUnits { get; }
    /// <summary>Maximum nested syntax groups/classes.</summary>
    public int MaxNestingDepth { get; }
    /// <summary>Maximum lowered instructions including group zero and acceptance.</summary>
    public int MaxInstructions { get; }
    /// <summary>Maximum total retained class intervals.</summary>
    public int MaxClassRanges { get; }
    /// <summary>Maximum groups including group zero.</summary>
    public int MaxCaptureGroups { get; }
    /// <summary>Maximum deterministic compile work units.</summary>
    public long MaxCompileWork { get; }
    /// <summary>Maximum logically retained plan bytes, including arrays and metadata.</summary>
    public long MaxPlanBytes { get; }
    /// <summary>Maximum live temporary and retained bytes while compiling.</summary>
    public long MaxCompileMemoryBytes { get; }

    /// <summary>Creates finite ceilings; every value must be positive.</summary>
    public RegexCompileLimits(int maxPatternUnits = 65_536, int maxNestingDepth = 256,
        int maxInstructions = 1_000_000, int maxClassRanges = 65_536, int maxCaptureGroups = 1_024,
        long maxCompileWork = 10_000_000, long maxPlanBytes = 67_108_864,
        long maxCompileMemoryBytes = 67_108_864)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxPatternUnits);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxNestingDepth);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxInstructions);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxClassRanges);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxCaptureGroups);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxCompileWork);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxPlanBytes);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxCompileMemoryBytes);
        MaxPatternUnits = maxPatternUnits;
        MaxNestingDepth = maxNestingDepth;
        MaxInstructions = maxInstructions;
        MaxClassRanges = maxClassRanges;
        MaxCaptureGroups = maxCaptureGroups;
        MaxCompileWork = maxCompileWork;
        MaxPlanBytes = maxPlanBytes;
        MaxCompileMemoryBytes = maxCompileMemoryBytes;
    }
}

/// <summary>Finite per-search work and prepared workspace memory ceilings.</summary>
internal sealed record RegexMatchLimits
{
    /// <summary>Maximum work units across validation, searching and capture writes.</summary>
    public long MaxWork { get; }
    /// <summary>Maximum logically owned workspace bytes.</summary>
    public long MaxWorkspaceBytes { get; }

    /// <summary>Creates positive finite ceilings.</summary>
    public RegexMatchLimits(long maxWork = 10_000_000, long maxWorkspaceBytes = 67_108_864)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxWork);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxWorkspaceBytes);
        MaxWork = maxWork;
        MaxWorkspaceBytes = maxWorkspaceBytes;
    }

    internal bool IsWithin(RegexMatchLimits ceiling) =>
        MaxWork <= ceiling.MaxWork && MaxWorkspaceBytes <= ceiling.MaxWorkspaceBytes;
}

/// <summary>Immutable compilation policy; default matching is interpreted and bounded.</summary>
internal sealed record RegexCompileOptions
{
    /// <summary>Requested matching storage representation.</summary>
    public RegexInputKind InputKind { get; }
    /// <summary>Requested backend preference.</summary>
    public RegexExecutionMode ExecutionMode { get; }
    /// <summary>Compilation ceilings.</summary>
    public RegexCompileLimits Limits { get; }
    /// <summary>Pattern safety ceilings for later operations.</summary>
    public RegexMatchLimits DefaultMatchLimits { get; }

    /// <summary>Creates validated options; profile admission is checked separately.</summary>
    public RegexCompileOptions(RegexInputKind inputKind = RegexInputKind.Utf16,
        RegexExecutionMode executionMode = RegexExecutionMode.Interpreted,
        RegexCompileLimits? limits = null, RegexMatchLimits? defaultMatchLimits = null)
    {
        if (!Enum.IsDefined(inputKind))
        {
            throw new ArgumentOutOfRangeException(nameof(inputKind));
        }

        if (!Enum.IsDefined(executionMode))
        {
            throw new ArgumentOutOfRangeException(nameof(executionMode));
        }

        InputKind = inputKind;
        ExecutionMode = executionMode;
        Limits = limits ?? new RegexCompileLimits();
        DefaultMatchLimits = defaultMatchLimits ?? new RegexMatchLimits();
    }
}

/// <summary>Per-operation cancellation, deadline and optional smaller safety ceilings.</summary>
internal readonly record struct RegexMatchOptions
{
    /// <summary>Cancellation for the entire operation.</summary>
    public CancellationToken CancellationToken { get; init; }
    /// <summary>Positive elapsed-time bound, or null for work-budget-only matching.</summary>
    public TimeSpan? Timeout { get; init; }
    /// <summary>Overrides must be no greater than the pattern's safety ceilings.</summary>
    public RegexMatchLimits? Limits { get; init; }

    internal RegexMatchLimits Resolve(RegexMatchLimits ceiling)
    {
        if (Timeout is { } timeout && timeout <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(Timeout));
        }

        if (Limits is { } limits && !limits.IsWithin(ceiling))
        {
            throw new ArgumentOutOfRangeException(nameof(Limits), "Overrides cannot increase the pattern ceiling.");
        }

        return Limits ?? ceiling;
    }
}

/// <summary>Prepared integer capacities; each lane reports required partitions before matching.</summary>
internal readonly record struct RegexWorkspaceCapacity
{
    /// <summary>Executor state storage in Int32 slots.</summary>
    public int StateSlots { get; }
    /// <summary>Capture/register storage in Int32 slots.</summary>
    public int CaptureSlots { get; }
    /// <summary>Auxiliary closure/deduplication storage in Int32 slots.</summary>
    public int ScratchSlots { get; }
    /// <summary>Conservative storage bytes: three array headers plus slot payloads.</summary>
    public long RequiredBytes => checked(96L + 4L * ((long)StateSlots + CaptureSlots + ScratchSlots));

    /// <summary>Creates nonnegative capacities with overflow-safe byte accounting.</summary>
    public RegexWorkspaceCapacity(int stateSlots, int captureSlots, int scratchSlots)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(stateSlots);
        ArgumentOutOfRangeException.ThrowIfNegative(captureSlots);
        ArgumentOutOfRangeException.ThrowIfNegative(scratchSlots);
        StateSlots = stateSlots;
        CaptureSlots = captureSlots;
        ScratchSlots = scratchSlots;
    }

    /// <summary>Whether every required partition fits.</summary>
    public bool Contains(RegexWorkspaceCapacity required) => StateSlots >= required.StateSlots &&
        CaptureSlots >= required.CaptureSlots && ScratchSlots >= required.ScratchSlots;
}

/// <summary>Workspace reservation policy. Growth is an explicit opt-in allocating policy.</summary>
internal sealed record RegexWorkspaceOptions
{
    /// <summary>Initial prepared reservation.</summary>
    public RegexWorkspaceCapacity InitialCapacity { get; }
    /// <summary>Whether insufficient reservations may allocate during an operation.</summary>
    public bool AllowGrowth { get; }
    /// <summary>Creates a workspace policy; default operations never silently grow.</summary>
    public RegexWorkspaceOptions(RegexWorkspaceCapacity initialCapacity = default, bool allowGrowth = false)
    {
        InitialCapacity = initialCapacity;
        AllowGrowth = allowGrowth;
    }
}
