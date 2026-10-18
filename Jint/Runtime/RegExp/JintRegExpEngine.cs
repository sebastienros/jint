using System.Diagnostics;
using System.Runtime.CompilerServices;
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

    // JavaScript places no ceiling on a pattern's size; GregsEx's defaults are a library's bounded policy. What
    // bounds compiling a script-supplied pattern is the regex timeout, as a work budget (CompileWorkPerMillisecond),
    // since GregsEx's compiler charges work but reads no clock; matching is bounded by the timeout itself and by the
    // scratch ceiling below.
    private static RegexCompileLimits CompileLimits(long maxCompileWork) => new(
        maxPatternUnits: int.MaxValue,
        maxNestingDepth: 4096,
        maxInstructions: int.MaxValue,
        maxClassRanges: int.MaxValue,
        maxCaptureGroups: int.MaxValue,
        maxCompileWork: maxCompileWork,
        maxPlanBytes: long.MaxValue,
        maxCompileMemoryBytes: long.MaxValue);

    // Compile work units per millisecond of the regex timeout: about what the compiler gets through in a millisecond
    // on current hardware (a 100K-unit literal charges 3.4M units in 53 ms; 1,000 i-flag classes 540M in 860 ms), so a
    // compile is abandoned after roughly the time the timeout allows a match. A floor keeps tiny timeouts from refusing
    // ordinary patterns.
    private const long CompileWorkPerMillisecond = 1_000_000;
    private const long MinimumCompileWork = 50_000_000;

    // A backtracking frame is the executor's only input-sized storage. Start small and grow geometrically,
    // retrying the search from its start, so a regex that never backtracks deeply never pays for frames. A frame
    // holds a copy of the task stack and the registers, so its size depends on the pattern (from a few dozen ints to
    // thousands): the bounds below are on the storage, not on the frame count.
    private const int InitialFrames = 64;
    private const int MaxFrames = 1 << 22;
    // The ceiling on one search's storage, the QuickJS port's 16M-int stack; past it the match throws a RangeError
    // instead of exhausting memory.
    private const long MaxScratchInts = 16L * 1024 * 1024;
    // The storage a thread keeps between searches, and the largest growth published to every user of a shared
    // matcher. A search needing more allocates for itself and drops it afterwards, so one large input does not make
    // every later search of the same pattern, on any thread or engine, allocate its storage.
    private const long RetainedScratchInts = 256L * 1024;

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
    // The frame count a search starts with and the partition sizes it needs, computed once per growth rather than on
    // every search, so a search can go straight to the executor's prevalidated entry.
    private sealed record FramePlan(int Frames, RegexWorkspaceCapacity Capacity)
    {
        public long Ints => (long) Capacity.StateSlots + Capacity.CaptureSlots + Capacity.ScratchSlots;
    }

    // The one mutable field: a sizing hint, never above RetainedScratchInts, shared by every search of this matcher.
    // A stale read only costs a growth retry, so it is published without ordering.
    private FramePlan _plan;
    // The most frames whose storage fits MaxScratchInts, so a capacity is never asked of the executor that its
    // checked arithmetic would overflow.
    private readonly int _maxFrames;

    private JintRegExpEngine(EcmaExecutor executor, RegExpFlags flags)
    {
        _executor = executor;
        Flags = flags;
        _sticky = (flags & RegExpFlags.Sticky) != RegExpFlags.None;
        // Storage is affine in the frame count: base + frames * perFrame.
        var one = new FramePlan(1, executor.GetRequiredCapacity(SearchOperation.Captures, 1)).Ints;
        var perFrame = new FramePlan(2, executor.GetRequiredCapacity(SearchOperation.Captures, 2)).Ints - one;
        _maxFrames = (int) Math.Max(1, Math.Min(MaxFrames, (MaxScratchInts - (one - perFrame)) / Math.Max(1, perFrame)));
        var initial = Math.Min(InitialFrames, _maxFrames);
        _plan = new FramePlan(initial, executor.GetRequiredCapacity(SearchOperation.Captures, initial));
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

    /// <summary>
    /// Compile a JavaScript regex pattern; throws <see cref="RegExpSyntaxException"/> for an early error, and
    /// <see cref="System.Text.RegularExpressions.RegexMatchTimeoutException"/> when compiling would take longer than <paramref name="timeout"/> allows
    /// (an infinite or non-positive timeout leaves compilation unbounded).
    /// </summary>
    public static JintRegExpEngine Compile(string pattern, RegExpFlags flags, TimeSpan? timeout = null)
    {
        // The executor reads only i/m/s/u/v from the profile; g, y and d are RegExp object state that
        // RegExpBuiltinExec owns, so the compiled program is the same for every combination of them.
        var profile = new EcmaScriptProfile(ToProfileFlags(flags & ~(RegExpFlags.Global | RegExpFlags.Sticky | RegExpFlags.Indices)), UnicodeVersion.Unicode17_0);
        var maxWork = timeout is { } t && t > TimeSpan.Zero && t != System.Threading.Timeout.InfiniteTimeSpan
            ? (long) Math.Min(long.MaxValue / 2, Math.Max(MinimumCompileWork, t.TotalMilliseconds * CompileWorkPerMillisecond))
            : long.MaxValue;
        var budget = new CompileBudget(CompileLimits(maxWork));
        if (!EcmaUnicodeData.TryCreate(UnicodeVersion.Unicode17_0, ref budget, out var unicode, out var failure))
        {
            throw Failure(failure.Code, pattern, timeout);
        }

        if (!new EcmaCompiler().TryCompile(pattern.AsSpan(), profile, unicode!, ref budget, out var program, out var diagnostic))
        {
            throw Failure(diagnostic.Diagnostic.Code, pattern, timeout);
        }

        if (!new EcmaExecutorFactory().TryCreate(program!, unicode!, ref budget, out var executor, out failure))
        {
            throw Failure(failure.Code, pattern, timeout);
        }

        return new JintRegExpEngine((EcmaExecutor) executor!, flags);
    }

    /// <summary>The frame count a search of this matcher starts with: the shared growth hint, never above what
    /// <c>RetainedScratchInts</c> holds.</summary>
    internal int PlannedFrames => _plan.Frames;

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

    /// <summary>
    /// Consecutive forward matches from <paramref name="startIndex"/>, each search starting where the previous match
    /// ended, written to <paramref name="matches"/>: one entry into the executor and one budget for the batch, where a
    /// split or global match otherwise paid both per match. Stops after an empty match (where to resume is the
    /// caller's rule) and when <paramref name="matches"/> is full; returns how many it wrote, 0 for no match.
    /// </summary>
    internal int FindRanges(string input, int startIndex, long deadline, Span<RegexMatchRange> matches)
    {
        if ((uint) startIndex > (uint) input.Length || matches.IsEmpty)
        {
            return 0;
        }

        var plan = _plan;
        var scratch = t_scratch;
        if (scratch is null || !scratch.Fits(plan.Capacity))
        {
            scratch = Scratch.ForThread(plan.Capacity);
        }

        var state = new RunState(scratch.Buffers, long.MaxValue, deadline);
        var span = input.AsSpan();
        var count = 0;
        var position = startIndex;
        while (count < matches.Length && position <= span.Length)
        {
            var status = _executor.SearchPrevalidated(span, position, sticky: false, SearchOperation.Range, plan.Frames, ref state, [], out var match);
            if (status == RegexSearchStatus.Match)
            {
                matches[count++] = match;
                if (match.Length == 0)
                {
                    break;
                }

                position = match.Index + match.Length;
                continue;
            }

            if (status != RegexSearchStatus.NoMatch && count == 0)
            {
                // Growth, a timeout or a resource failure: the single search handles each.
                ForgetTimestampAfterLongSearch(state.Budget.WorkConsumed);
                if (!Search(input, position, SearchOperation.Range, [], deadline, out match, sticky: false))
                {
                    return 0;
                }

                matches[0] = match;
                return 1;
            }

            // No further match, or a failure the next call reaches on its own.
            break;
        }

        ForgetTimestampAfterLongSearch(state.Budget.WorkConsumed);
        return count;
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

        // A reference read is atomic, and a stale plan only means one more growth retry.
        var plan = _plan;
        var scratch = t_scratch;
        if (scratch is null || !scratch.Fits(plan.Capacity))
        {
            scratch = Scratch.ForThread(plan.Capacity);
        }

        // The admitted frame count is the planned one, the start index is checked above and every caller passes
        // CaptureCount groups: what Search would validate is already known, and the prevalidated entry reads the
        // partitions only up to the planned frame count, so they may be longer than planned.
        var state = new RunState(scratch.Buffers, long.MaxValue, deadline);
        var status = _executor.SearchPrevalidated(input.AsSpan(), startIndex, sticky, operation, plan.Frames, ref state, groups, out match);
        if (status == RegexSearchStatus.Match)
        {
            ForgetTimestampAfterLongSearch(state.Budget.WorkConsumed);
            return true;
        }

        if (status == RegexSearchStatus.NoMatch)
        {
            ForgetTimestampAfterLongSearch(state.Budget.WorkConsumed);
            return false;
        }

        return SearchSlow(input, startIndex, operation, groups, deadline, out match, sticky, status, state.Failure.Code, plan);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void ForgetTimestampAfterLongSearch(long workConsumed)
    {
        if (workConsumed >= RunBudget.ClockStride)
        {
            // Long enough to have read the clock: the cached reading is no longer recent.
            t_recentTimestamp = 0;
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private bool SearchSlow(string input, int startIndex, SearchOperation operation, Span<RegexCapture> groups, long deadline,
        out RegexMatchRange match, bool sticky, RegexSearchStatus status, RegexExecutionFailureCode failure, FramePlan plan)
    {
        while (true)
        {
            t_recentTimestamp = 0;
            if (status is RegexSearchStatus.TimedOut or RegexSearchStatus.Cancelled)
            {
                throw new OperationCanceledException();
            }

            if (failure != RegexExecutionFailureCode.WorkspaceCapacityExceeded || plan.Frames >= _maxFrames)
            {
                throw new RegExpResourceException(failure == RegexExecutionFailureCode.WorkspaceCapacityExceeded
                    ? "Maximum regular expression backtracking depth exceeded"
                    : "Regular expression failed: " + failure);
            }

            var frames = (int) Math.Min(_maxFrames, (long) plan.Frames * 4);
            plan = new FramePlan(frames, _executor.GetRequiredCapacity(SearchOperation.Captures, frames));

            WorkspaceBuffers buffers;
            if (plan.Ints <= RetainedScratchInts)
            {
                // Small enough for every search of this pattern to keep: later searches start here.
                _plan = plan;
                buffers = Scratch.ForThread(plan.Capacity).Buffers;
            }
            else
            {
                // This search's alone, collected once it returns.
                buffers = new WorkspaceBuffers(new int[plan.Capacity.StateSlots], new int[plan.Capacity.CaptureSlots], new int[plan.Capacity.ScratchSlots]);
            }

            var state = new RunState(buffers, long.MaxValue, deadline);
            status = _executor.SearchPrevalidated(input.AsSpan(), startIndex, sticky, operation, plan.Frames, ref state, groups, out match);
            if (status == RegexSearchStatus.Match)
            {
                return true;
            }

            if (status == RegexSearchStatus.NoMatch)
            {
                return false;
            }

            failure = state.Failure.Code;
        }
    }

    /// <summary>One thread's executor partitions, grown on demand and reused by every matcher on that thread.</summary>
    private sealed class Scratch
    {
        private int[] _states = [];
        private int[] _captures = [];
        private int[] _scratch = [];

        public WorkspaceBuffers Buffers => new(_states, _captures, _scratch);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Fits(RegexWorkspaceCapacity capacity)
            => _states.Length >= capacity.StateSlots && _captures.Length >= capacity.CaptureSlots && _scratch.Length >= capacity.ScratchSlots;

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static Scratch ForThread(RegexWorkspaceCapacity capacity)
        {
            var scratch = t_scratch ??= new Scratch();
            Grow(ref scratch._states, capacity.StateSlots);
            Grow(ref scratch._captures, capacity.CaptureSlots);
            Grow(ref scratch._scratch, capacity.ScratchSlots);
            return scratch;
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

    private static Exception Failure(RegexDiagnosticCode code, string pattern, TimeSpan? timeout)
        => code == RegexDiagnosticCode.CompileWorkLimitExceeded
            ? new System.Text.RegularExpressions.RegexMatchTimeoutException(pattern, pattern, timeout ?? System.Threading.Timeout.InfiniteTimeSpan)
            : new RegExpSyntaxException(DescribeFailure(code, pattern));

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
