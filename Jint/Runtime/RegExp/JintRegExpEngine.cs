using System.Diagnostics;
using System.Threading;
using Jint.Runtime.RegExp.GregsEx;
using Jint.Runtime.RegExp.GregsEx.Contracts;
using Jint.Runtime.RegExp.GregsEx.Contracts.EcmaScript;
using Jint.Runtime.RegExp.GregsEx.Execution.Backtracking;
using Jint.Runtime.RegExp.GregsEx.Flavors.EcmaScript.Parsing;
using Jint.Runtime.RegExp.GregsEx.Flavors.EcmaScript.Unicode;

namespace Jint.Runtime.RegExp;

/// <summary>
/// Jint's ECMAScript regular expression engine: a compiled, immutable pattern over the GregsEx
/// ECMAScript backtracking executor (see <c>GregsEx/README.md</c>).
/// </summary>
/// <remarks>
/// An instance is shareable across engines and threads — <see cref="Native.RegExp.RegExpParseCache"/>
/// hands one to every engine that compiles the same source. The executor's mutable storage lives in
/// per-thread scratch buffers instead, which is safe because no script runs while a match is in progress.
/// </remarks>
internal sealed class JintRegExpEngine
{
    /// <summary>An absolute deadline that never fires.</summary>
    internal const long NoDeadline = long.MaxValue;

    // JavaScript places no ceiling on these; GregsEx's defaults are a library's bounded policy. Compilation
    // stays bounded by the work and memory budgets, matching by the regex timeout and by MaxFrames below.
    private static readonly RegexCompileLimits CompileLimits = new(
        maxPatternUnits: int.MaxValue,
        maxNestingDepth: 4096,
        maxInstructions: int.MaxValue,
        maxClassRanges: int.MaxValue,
        maxCaptureGroups: int.MaxValue,
        maxCompileWork: long.MaxValue,
        maxPlanBytes: long.MaxValue,
        maxCompileMemoryBytes: long.MaxValue);

    // A backtracking frame is the executor's only input-sized storage. Start small and grow geometrically,
    // retrying the search from its start, so a regex that never backtracks deeply never pays for frames.
    private const int InitialFrames = 64;
    // About the depth of V8's backtrack stack; past it the match throws rather than exhausting memory.
    private const int MaxFrames = 1 << 22;

    [ThreadStatic] private static Scratch? t_scratch;
    [ThreadStatic] private static long t_recentTimestamp;
    [ThreadStatic] private static int t_recentTimestampUses;
    [ThreadStatic] private static int t_batchDepth;

    // How many searches may share one clock reading. A split or a global replace runs one search per match, and a
    // clock read per search was a third of the cost of a short one; see RecentTimestamp.
    private const int SearchesPerClockRead = 64;

    private readonly EcmaExecutor _executor;
    private readonly string?[] _groupNames;
    private readonly bool _sticky;
    // The frame count every search starts with and the partition sizes it needs, computed once per growth rather than
    // on every search, so a search can go straight to the executor's prevalidated entry.
    private sealed record FramePlan(int Frames, RegexWorkspaceCapacity Capacity);
    private FramePlan _plan;

    private JintRegExpEngine(EcmaExecutor executor, RegExpFlags flags)
    {
        _executor = executor;
        Flags = flags;
        _sticky = (flags & RegExpFlags.Sticky) != RegExpFlags.None;
        _plan = new FramePlan(InitialFrames, executor.GetRequiredCapacity(SearchOperation.Captures, InitialFrames));
        var program = executor.Program;
        CaptureCount = program.GroupCount;
        _groupNames = new string?[CaptureCount];
        var aliases = program.NameAliases;
        foreach (var name in program.Names)
        {
            HasGroupNames = true;
            for (var i = 0; i < name.Length; i++)
            {
                _groupNames[aliases[name.Offset + i]] = name.Name;
            }
        }
    }

    /// <summary>Compile a JavaScript regex pattern; throws <see cref="RegExpSyntaxException"/> for an early error.</summary>
    public static JintRegExpEngine Compile(string pattern, RegExpFlags flags, CancellationToken cancellationToken = default)
    {
        // The executor reads only i/m/s/u/v from the profile; g, y and d are RegExp object state that
        // RegExpBuiltinExec owns, so the compiled program is the same for every combination of them.
        var profile = new EcmaScriptProfile(ToProfileFlags(flags & ~(RegExpFlags.Global | RegExpFlags.Sticky | RegExpFlags.Indices)), UnicodeVersion.Unicode17_0);
        var budget = new CompileBudget(CompileLimits);
        if (!EcmaUnicodeData.TryCreate(UnicodeVersion.Unicode17_0, ref budget, out var unicode, out var failure))
        {
            throw new RegExpSyntaxException(DescribeFailure(failure.Code, pattern));
        }

        cancellationToken.ThrowIfCancellationRequested();
        if (!new EcmaCompiler().TryCompile(pattern.AsSpan(), profile, unicode!, ref budget, out var program, out var diagnostic))
        {
            throw new RegExpSyntaxException(DescribeFailure(diagnostic.Diagnostic.Code, pattern));
        }

        cancellationToken.ThrowIfCancellationRequested();
        if (!new EcmaExecutorFactory().TryCreate(program!, unicode!, ref budget, out var executor, out failure))
        {
            throw new RegExpSyntaxException(DescribeFailure(failure.Code, pattern));
        }

        return new JintRegExpEngine((EcmaExecutor) executor!, flags);
    }

    /// <summary>Capture group count, including group 0 for the full match.</summary>
    public int CaptureCount { get; }

    /// <summary>Compiled flags.</summary>
    public RegExpFlags Flags { get; }

    /// <summary>Get the name of a capture group by its 1-based index.</summary>
    public string? GetGroupName(int index) => (uint) index < (uint) _groupNames.Length ? _groupNames[index] : null;

    /// <summary>Whether any capture group has a name.</summary>
    public bool HasGroupNames { get; private set; }

    /// <summary>
    /// Match from <paramref name="startIndex"/> (only there when the pattern is sticky), writing group <c>i</c>'s span into <paramref name="groups"/>[i] (group 0 is the
    /// whole match) instead of materializing strings. <paramref name="groups"/> needs <see cref="CaptureCount"/> entries.
    /// </summary>
    internal bool TryMatch(string input, int startIndex, long deadline, Span<RegexCapture> groups)
        => Search(input, startIndex, SearchOperation.Captures, groups, deadline, out _, _sticky);

    /// <summary>
    /// <see cref="TryMatch"/> as a forward search from <paramref name="startIndex"/> even when the pattern is sticky,
    /// for a caller that would otherwise retry a sticky match at every later position (the split matcher).
    /// </summary>
    internal bool TryFind(string input, int startIndex, long deadline, Span<RegexCapture> groups)
        => Search(input, startIndex, SearchOperation.Captures, groups, deadline, out _, sticky: false);

    /// <summary>
    /// Match like <see cref="TryMatch"/>, reporting only the whole match's range: no capture work at all.
    /// <paramref name="deadline"/> is an absolute <see cref="Stopwatch.GetTimestamp"/> value at/after which the match
    /// is abandoned with an <see cref="OperationCanceledException"/> (<see cref="NoDeadline"/> never fires).
    /// </summary>
    internal bool TryMatchRange(string input, int startIndex, long deadline, out int index, out int length)
    {
        var found = Search(input, startIndex, SearchOperation.Range, [], deadline, out var match, _sticky);
        index = match.Index;
        length = match.Length;
        return found;
    }

    /// <summary>Test if the regex matches the input string. See <see cref="TryMatchRange"/> for <paramref name="deadline"/>.</summary>
    public bool IsMatch(string input, int startIndex = 0, long deadline = NoDeadline)
        => Search(input, startIndex, SearchOperation.Boolean, [], deadline, out _, _sticky);

    /// <summary>
    /// Opens a scope in which consecutive searches may share a clock reading (see <see cref="RecentTimestamp"/>). Only
    /// a native loop that runs no script between its searches may open one: script could run for any length of time,
    /// which would leave the shared reading arbitrarily stale.
    /// </summary>
    internal static SearchBatch BeginBatch()
    {
        t_batchDepth++;
        t_recentTimestamp = 0;
        return default;
    }

    internal readonly ref struct SearchBatch
    {
        // An instance member because `using` binds Dispose by pattern; the state it resets is per thread.
#pragma warning disable CA1822
        public void Dispose()
        {
            t_batchDepth--;
            t_recentTimestamp = 0;
        }
#pragma warning restore CA1822
    }

    /// <summary>
    /// Inside a <see cref="BeginBatch"/> scope, a <see cref="Stopwatch.GetTimestamp"/> value at most
    /// <see cref="SearchesPerClockRead"/> short searches old, for arming a match deadline; outside one, the clock. Stale only ever means earlier, so a deadline built on it can fire early - by the time
    /// those few searches took, each too short to have read the clock itself - and never late: a search long enough
    /// to read the clock invalidates the reading.
    /// </summary>
    internal static long RecentTimestamp()
    {
        if (t_batchDepth == 0)
        {
            return Stopwatch.GetTimestamp();
        }

        if (t_recentTimestamp == 0 || ++t_recentTimestampUses >= SearchesPerClockRead)
        {
            t_recentTimestamp = Stopwatch.GetTimestamp();
            t_recentTimestampUses = 0;
        }

        return t_recentTimestamp;
    }

    private bool Search(string input, int startIndex, SearchOperation operation, Span<RegexCapture> groups, long deadline, out RegexMatchRange match, bool sticky)
    {
        if ((uint) startIndex > (uint) input.Length)
        {
            match = default;
            return false;
        }

        var scratch = t_scratch ??= new Scratch();
        var plan = Volatile.Read(ref _plan);
        while (true)
        {
            // The partitions are rented at exactly the planned capacity, so the admitted frame count is the planned
            // one, the start index is checked above and every caller passes CaptureCount groups: what Search would
            // validate is already known.
            var buffers = scratch.Rent(plan.Capacity);
            var state = new RunState(buffers, long.MaxValue, deadline);

            var status = _executor.SearchPrevalidated(input.AsSpan(), startIndex, sticky, operation, plan.Frames, ref state, groups, out match);
            if (state.Budget.WorkConsumed >= RunBudget.ClockStride)
            {
                // Long enough to have read the clock: the cached reading is no longer recent.
                t_recentTimestamp = 0;
            }

            switch (status)
            {
                case RegexSearchStatus.Match:
                    return true;
                case RegexSearchStatus.NoMatch:
                    return false;
                case RegexSearchStatus.TimedOut:
                case RegexSearchStatus.Cancelled:
                    throw new OperationCanceledException();
            }

            if (state.Failure.Code != RegexExecutionFailureCode.WorkspaceCapacityExceeded || plan.Frames >= MaxFrames)
            {
                throw new RegExpResourceException(state.Failure.Code.ToString());
            }

            var frames = Math.Min(MaxFrames, plan.Frames * 4);
            plan = new FramePlan(frames, _executor.GetRequiredCapacity(SearchOperation.Captures, frames));
            Volatile.Write(ref _plan, plan);
        }
    }

    /// <summary>One thread's executor partitions, grown on demand and reused by every matcher on that thread.</summary>
    private sealed class Scratch
    {
        private int[] _states = [];
        private int[] _captures = [];
        private int[] _scratch = [];

        public WorkspaceBuffers Rent(RegexWorkspaceCapacity capacity)
        {
            Grow(ref _states, capacity.StateSlots);
            Grow(ref _captures, capacity.CaptureSlots);
            Grow(ref _scratch, capacity.ScratchSlots);
            return new WorkspaceBuffers(
                _states.AsSpan(0, capacity.StateSlots),
                _captures.AsSpan(0, capacity.CaptureSlots),
                _scratch.AsSpan(0, capacity.ScratchSlots));
        }

        private static void Grow(ref int[] array, int length)
        {
            if (array.Length < length)
            {
                array = new int[Math.Max(length, array.Length * 2)];
            }
        }
    }

    private static EcmaScriptFlags ToProfileFlags(RegExpFlags flags)
    {
        var result = EcmaScriptFlags.None;
        if ((flags & RegExpFlags.Global) != RegExpFlags.None) result |= EcmaScriptFlags.Global;
        if ((flags & RegExpFlags.IgnoreCase) != RegExpFlags.None) result |= EcmaScriptFlags.IgnoreCase;
        if ((flags & RegExpFlags.Multiline) != RegExpFlags.None) result |= EcmaScriptFlags.Multiline;
        if ((flags & RegExpFlags.DotAll) != RegExpFlags.None) result |= EcmaScriptFlags.DotAll;
        if ((flags & RegExpFlags.Unicode) != RegExpFlags.None) result |= EcmaScriptFlags.Unicode;
        if ((flags & RegExpFlags.Sticky) != RegExpFlags.None) result |= EcmaScriptFlags.Sticky;
        if ((flags & RegExpFlags.Indices) != RegExpFlags.None) result |= EcmaScriptFlags.HasIndices;
        if ((flags & RegExpFlags.UnicodeSets) != RegExpFlags.None) result |= EcmaScriptFlags.UnicodeSets;
        return result;
    }

    private static string DescribeFailure(RegexDiagnosticCode code, string pattern) => code switch
    {
        RegexDiagnosticCode.InvalidSyntax => $"Invalid regular expression: /{pattern}/: Invalid syntax",
        _ => $"Invalid regular expression: /{pattern}/: {code}",
    };
}

/// <summary>Flags of a compiled JavaScript regular expression.</summary>
[Flags]
internal enum RegExpFlags : ushort
{
    None = 0,
    Global = 1 << 0,
    IgnoreCase = 1 << 1,
    Multiline = 1 << 2,
    DotAll = 1 << 3,
    Unicode = 1 << 4,
    Sticky = 1 << 5,
    Indices = 1 << 6,
    NamedGroups = 1 << 7,
    UnicodeSets = 1 << 8,
}

/// <summary>Thrown when a match exhausts the executor's backtracking storage ceiling.</summary>
internal sealed class RegExpResourceException : Exception
{
    public RegExpResourceException(string message) : base(message)
    {
    }
}
