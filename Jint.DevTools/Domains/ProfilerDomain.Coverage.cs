using Acornima.Ast;
using Jint.DevTools.Protocol;
using Jint.DevTools.Protocol.Profiler;
using Jint.DevTools.Session;
using Jint.Runtime.Coverage;

namespace Jint.DevTools.Domains;

/// <summary>
/// Coverage: which of an engine's code ran, and how often.
/// </summary>
/// <remarks>
/// <para>
/// <b>The engine reports the covered set; the protocol wants counted ranges.</b> A construct that never ran
/// has no entry at all, so a report built from <c>GetCoverage</c> alone would say every function it mentions
/// was used and say nothing about the rest. The gap is closed from the abstract syntax tree the script
/// registry already holds: every function in the script gets a range, and the ones with no entry get
/// <c>count: 0</c>, which is what makes a front end's Coverage panel show them as unused.
/// </para>
/// <para>
/// <b>What is still approximate.</b> A statement inside a function that ran, but did not itself run, has no
/// range of its own and is covered by its function's count — so unused code is reported at function
/// granularity, not at block granularity, whatever <c>detailed</c> asks for. <c>isBlockCoverage</c> is
/// answered as what was asked for, because a client uses it to decide how to read the ranges, and the ranges
/// under <c>detailed</c> really are finer: every statement that ran carries its own count.
/// </para>
/// <para>
/// <b>Taking coverage resets it, and that costs a host.</b> The protocol says so — <c>startPreciseCoverage</c>
/// and <c>takePreciseCoverage</c> both reset execution counters, which is what makes successive takes
/// incremental rather than cumulative — and the counters are the engine's one set. A host reading
/// <c>Engine.Diagnostics.GetCoverage</c> for its own purposes therefore loses its numbers to an attached
/// client. That is the protocol's contract rather than a defect here, and it is the reason coverage is off
/// unless <c>UseDevTools</c> is asked for it.
/// </para>
/// </remarks>
internal sealed partial class ProfilerDomain
{
    private const string UnknownScriptId = "0";
    private const double MillisecondsPerSecond = 1000;

    private bool _preciseCoverage;
    private bool _detailedCoverage;

    /// <summary>
    /// Begins a coverage measurement, which for this engine means dropping the counts it already had.
    /// </summary>
    /// <remarks>
    /// <c>callCount</c> is accepted and not acted on: the engine counts entries and has no cheaper mode that
    /// only records whether something ran, so a client that asked for less is given more. <c>detailed</c>
    /// decides whether a function's own statements are reported inside it.
    /// <c>allowTriggeredUpdates</c> is ignored — nothing here pushes an update.
    /// </remarks>
    protected override ValueTask<StartPreciseCoverageResponse> StartPreciseCoverageAsync(StartPreciseCoverageRequest parameters, CommandContext context)
    {
        RequireCoverageEngine();

        _target.Engine.Diagnostics.ResetCoverage();
        _preciseCoverage = true;
        _detailedCoverage = parameters.Detailed == true;

        return new ValueTask<StartPreciseCoverageResponse>(new StartPreciseCoverageResponse { Timestamp = Timestamp() });
    }

    /// <summary>Answers what has run since the last take, and starts the next measurement.</summary>
    protected override ValueTask<TakePreciseCoverageResponse> TakePreciseCoverageAsync(EmptyParameters parameters, CommandContext context)
    {
        RequireCoverageEngine();

        if (!_preciseCoverage)
        {
            // V8's wording, which a client matches on to know it forgot the start.
            Throw.ServerError("Precise coverage has not been started.");
        }

        var result = Collect(_detailedCoverage);
        _target.Engine.Diagnostics.ResetCoverage();

        return new ValueTask<TakePreciseCoverageResponse>(new TakePreciseCoverageResponse
        {
            Result = result,
            Timestamp = Timestamp(),
        });
    }

    /// <summary>
    /// Ends the measurement without touching the counters, which are the engine's and outlive the client.
    /// </summary>
    /// <remarks>
    /// There is nothing to switch off: coverage collection is decided when the engine is constructed, so
    /// what stops is this attachment's measurement rather than the engine's counting.
    /// </remarks>
    protected override ValueTask<EmptyResult> StopPreciseCoverageAsync(EmptyParameters parameters, CommandContext context)
    {
        _preciseCoverage = false;
        _detailedCoverage = false;
        return new ValueTask<EmptyResult>(EmptyResult.Instance);
    }

    /// <summary>Answers what has run, without resetting anything and without a measurement having started.</summary>
    /// <remarks>
    /// The engine's counters are exact rather than best-effort — nothing is lost to a collection — so this
    /// answers the same shape a take does. It is the read a client makes when it did not ask for a
    /// measurement, and the one that does not take a host's numbers away.
    /// </remarks>
    protected override ValueTask<GetBestEffortCoverageResponse> GetBestEffortCoverageAsync(EmptyParameters parameters, CommandContext context)
    {
        RequireCoverageEngine();
        return new ValueTask<GetBestEffortCoverageResponse>(new GetBestEffortCoverageResponse { Result = Collect(_detailedCoverage) });
    }

    /// <summary>Refuses an engine the host did not build with coverage switched on.</summary>
    private void RequireCoverageEngine()
    {
        if (!_target.Engine.Options.Coverage.Enabled)
        {
            Throw.ServerError(
                "The engine was not built with coverage enabled",
                "Options.Coverage.Enabled is read once, when the engine is constructed; build it with options.UseDevTools(devTools => devTools.Coverage = true)");
        }
    }

    private static double Timestamp() => EngineTarget.UnixMilliseconds() / MillisecondsPerSecond;

    /// <summary>Reads the engine's report and answers it in the protocol's shape, script by script.</summary>
    private ScriptCoverage[] Collect(bool detailed)
    {
        var report = _target.Engine.Diagnostics.GetCoverage();
        var scripts = new List<ScriptCoverage>(report.Sources.Count);

        foreach (var source in report.Sources)
        {
            // The engine names a source and the registry names a script, and the two meet on that name. A
            // source no script claims is still reported, against the unattributable identifier, because a
            // client can do something with ranges it cannot place and nothing with a source that vanished.
            var script = _target.Scripts?.At(source.Name, line: 1, column: 0);

            scripts.Add(new ScriptCoverage
            {
                ScriptId = script?.ScriptId ?? UnknownScriptId,
                Url = script?.Url ?? source.Name,
                Functions = Functions(source, script?.Program, detailed),
            });
        }

        return [.. scripts];
    }

    private static FunctionCoverage[] Functions(CoverageSource source, Program? program, bool detailed)
    {
        var counted = new Dictionary<int, long>();
        var statements = new List<CoverageEntry>();

        foreach (var entry in source.Entries)
        {
            if (entry.Kind == CoverageEntryKind.Function)
            {
                counted[entry.Start.Index] = entry.HitCount;
            }
            else
            {
                statements.Add(entry);
            }
        }

        var functions = new List<FunctionCoverage>();
        var claimed = new List<(int Start, int End)>();

        foreach (var declared in Declarations(program))
        {
            counted.Remove(declared.Start, out var hits);
            claimed.Add((declared.Start, declared.End));
            functions.Add(Coverage(declared.Name, declared.Start, declared.End, hits, statements, detailed));
        }

        // A function the abstract syntax tree did not offer: an evicted script, or one the registry never
        // saw. Its range is what the engine counted, and its name is one nothing can supply.
        foreach (var pair in counted)
        {
            functions.Add(Coverage("", pair.Key, EndOf(source, pair.Key), pair.Value, statements, detailed));
        }

        if (program is not null)
        {
            // V8 reports the script itself as the first, unnamed function, and the panel draws the file's
            // own top-level code from it. Registered means parsed and about to run, so it ran.
            functions.Insert(0, Coverage("", program.Range.Start, program.Range.End, hits: 1, TopLevel(statements, claimed), detailed));
        }

        return [.. functions];
    }

    /// <summary>One function's ranges: its own, and — when asked — every statement inside it that ran.</summary>
    private static FunctionCoverage Coverage(
        string name,
        int start,
        int end,
        long hits,
        List<CoverageEntry> statements,
        bool detailed)
    {
        var ranges = new List<CoverageRange>
        {
            new() { StartOffset = start, EndOffset = end, Count = Count(hits) },
        };

        if (detailed)
        {
            foreach (var statement in statements)
            {
                if (statement.Start.Index >= start && statement.End.Index <= end)
                {
                    ranges.Add(new CoverageRange
                    {
                        StartOffset = statement.Start.Index,
                        EndOffset = statement.End.Index,
                        Count = Count(statement.HitCount),
                    });
                }
            }
        }

        return new FunctionCoverage
        {
            FunctionName = name,
            Ranges = [.. ranges],
            IsBlockCoverage = detailed,
        };
    }

    /// <summary>The statements that belong to the script itself rather than to one of its functions.</summary>
    private static List<CoverageEntry> TopLevel(List<CoverageEntry> statements, List<(int Start, int End)> functions)
    {
        var top = new List<CoverageEntry>();

        foreach (var statement in statements)
        {
            var inside = false;
            foreach (var function in functions)
            {
                if (statement.Start.Index >= function.Start && statement.End.Index <= function.End)
                {
                    inside = true;
                    break;
                }
            }

            if (!inside)
            {
                top.Add(statement);
            }
        }

        return top;
    }

    /// <summary>
    /// Every function a program declares, by the range the engine counts it at: its body.
    /// </summary>
    /// <remarks>
    /// The engine's function counter sits on the body — a <c>FunctionBody</c>, or the expression that is a
    /// concise arrow body — so the body's own range is the key both sides agree on. A name the syntax does
    /// not carry is taken from what the function was being assigned to, which is the same inference the
    /// language itself makes for <c>Function.prototype.name</c>.
    /// </remarks>
    private static List<(string Name, int Start, int End)> Declarations(Program? program)
    {
        var declared = new List<(string Name, int Start, int End)>();
        if (program is not null)
        {
            Walk(program, hint: null, declared);
        }

        declared.Sort(static (a, b) => a.Start.CompareTo(b.Start));
        return declared;
    }

    /// <summary>Walks a program iteratively, because its depth is a script author's to choose.</summary>
    /// <remarks>
    /// A client's command may not put a host's thread at the mercy of how deeply somebody nested a block, so
    /// the walk carries its own stack rather than the call stack's.
    /// </remarks>
    private static void Walk(Node root, string? hint, List<(string Name, int Start, int End)> declared)
    {
        var pending = new Stack<(Node Node, string? Hint)>();
        pending.Push((root, hint));

        while (pending.Count > 0)
        {
            var (node, inherited) = pending.Pop();

            if (node is IFunction function)
            {
                var body = (Node) function.Body;
                declared.Add((function.Id?.Name ?? inherited ?? "", body.Range.Start, body.Range.End));
            }

            foreach (var child in node.ChildNodes)
            {
                if (child is not null)
                {
                    pending.Push((child, HintFor(node, child)));
                }
            }
        }
    }

    /// <summary>What an anonymous function immediately inside <paramref name="parent"/> would be called.</summary>
    private static string? HintFor(Node parent, Node child) => parent switch
    {
        VariableDeclarator { Id: Identifier name, Init: var init } when ReferenceEquals(init, child) => name.Name,
        AssignmentExpression { Left: Identifier name, Right: var right } when ReferenceEquals(right, child) => name.Name,
        Property { Key: Identifier key, Computed: false } => key.Name,
        MethodDefinition { Key: Identifier key, Computed: false } => key.Name,
        PropertyDefinition { Key: Identifier key, Computed: false } => key.Name,
        _ => null,
    };

    /// <summary>
    /// The end of a counted function whose declaration nothing could be matched to, taken from the entry
    /// itself.
    /// </summary>
    private static int EndOf(CoverageSource source, int start)
    {
        foreach (var entry in source.Entries)
        {
            if (entry.Kind == CoverageEntryKind.Function && entry.Start.Index == start)
            {
                return entry.End.Index;
            }
        }

        return start;
    }

    /// <summary>A count the protocol can carry, which is an <see cref="int"/> where the engine counts longer.</summary>
    private static int Count(long hits) => (int) Math.Clamp(hits, 0, int.MaxValue);
}
