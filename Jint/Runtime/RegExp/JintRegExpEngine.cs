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

    [ThreadStatic] private static int[]? t_states;
    [ThreadStatic] private static int[]? t_captures;
    [ThreadStatic] private static int[]? t_scratch;
    [ThreadStatic] private static RegexCapture[]? t_groups;

    private readonly EcmaExecutor _executor;
    private readonly string?[] _groupNames;
    private readonly bool _sticky;
    private int _framesHint = InitialFrames;

    private JintRegExpEngine(EcmaExecutor executor, RegExpFlags flags)
    {
        _executor = executor;
        Flags = flags;
        _sticky = (flags & RegExpFlags.Sticky) != RegExpFlags.None;
        var program = executor.Program;
        CaptureCount = program.GroupCount;
        _groupNames = new string?[CaptureCount];
        var aliases = program.NameAliases;
        foreach (var name in program.Names)
        {
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

    /// <summary>
    /// Execute the regex against the input string starting at <paramref name="startIndex"/>, matching only
    /// there when the pattern is sticky. <paramref name="deadline"/> is an absolute <see cref="Stopwatch.GetTimestamp"/>
    /// value at/after which the match is abandoned with an <see cref="OperationCanceledException"/>.
    /// </summary>
    public RegExpMatchResult Execute(string input, int startIndex, long deadline = NoDeadline)
    {
        var groups = t_groups;
        if (groups is null || groups.Length < CaptureCount)
        {
            t_groups = groups = new RegexCapture[Math.Max(CaptureCount, 16)];
        }

        if (!Search(input, startIndex, SearchOperation.Captures, groups, deadline, out var match))
        {
            return RegExpMatchResult.NoMatch;
        }

        return BuildResult(input, match, groups);
    }

    /// <summary>Test if the regex matches the input string. See <see cref="Execute"/> for <paramref name="deadline"/>.</summary>
    public bool IsMatch(string input, int startIndex = 0, long deadline = NoDeadline)
        => Search(input, startIndex, SearchOperation.Boolean, [], deadline, out _);

    private bool Search(string input, int startIndex, SearchOperation operation, Span<RegexCapture> groups, long deadline, out RegexMatchRange match)
    {
        if ((uint) startIndex > (uint) input.Length)
        {
            match = default;
            return false;
        }

        var frames = Volatile.Read(ref _framesHint);
        while (true)
        {
            var capacity = _executor.GetRequiredCapacity(operation, frames);
            var states = Rent(ref t_states, capacity.StateSlots);
            var captures = Rent(ref t_captures, capacity.CaptureSlots);
            var scratch = Rent(ref t_scratch, capacity.ScratchSlots);
            var buffers = new WorkspaceBuffers(states.AsSpan(0, capacity.StateSlots), captures.AsSpan(0, capacity.CaptureSlots), scratch.AsSpan(0, capacity.ScratchSlots));
            var state = new RunState(buffers, long.MaxValue, default, ToTimeout(deadline));

            var status = _executor.Search(input.AsSpan(), startIndex, _sticky, EcmaStartPolicy.BuiltinLastIndex, operation, ref state, groups, out match);
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

            if (state.Failure.Code != RegexExecutionFailureCode.WorkspaceCapacityExceeded || frames >= MaxFrames)
            {
                throw new RegExpResourceException(state.Failure.Code.ToString());
            }

            frames = Math.Min(MaxFrames, frames * 4);
            Volatile.Write(ref _framesHint, frames);
        }
    }

    private static int[] Rent(ref int[]? slot, int length)
    {
        var array = slot;
        if (array is null || array.Length < length)
        {
            slot = array = new int[Math.Max(length, (array?.Length ?? 0) * 2)];
        }

        return array;
    }

    private static TimeSpan? ToTimeout(long deadline)
    {
        if (deadline == NoDeadline)
        {
            return null;
        }

        var remaining = deadline - Stopwatch.GetTimestamp();
        return remaining <= 0 ? TimeSpan.Zero : TimeSpan.FromSeconds(remaining / (double) Stopwatch.Frequency);
    }

    private RegExpMatchResult BuildResult(string input, RegexMatchRange match, RegexCapture[] captures)
    {
        var matchValue = input.Substring(match.Index, match.Length);
        var groups = new RegExpGroupResult[CaptureCount];
        groups[0] = new RegExpGroupResult(Success: true, Index: match.Index, Length: match.Length, Value: matchValue, Name: null);
        for (var i = 1; i < groups.Length; i++)
        {
            var capture = captures[i];
            groups[i] = capture.Participated
                ? new RegExpGroupResult(Success: true, Index: capture.Index, Length: capture.Length, Value: input.Substring(capture.Index, capture.Length), Name: _groupNames[i])
                : new RegExpGroupResult(Success: false, Index: -1, Length: 0, Value: string.Empty, Name: _groupNames[i]);
        }

        return new RegExpMatchResult(Success: true, Index: match.Index, Length: match.Length, Value: matchValue, Groups: groups);
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
