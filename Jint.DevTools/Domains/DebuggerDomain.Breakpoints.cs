using System.Globalization;
using System.Text.RegularExpressions;
using Jint.DevTools.Protocol;
using Jint.DevTools.Protocol.Debugger;
using Jint.DevTools.Session;
using Jint.Runtime.Debugger;
using EngineBreakLocation = Jint.Runtime.Debugger.BreakLocation;
using ProtocolBreakLocation = Jint.DevTools.Protocol.Debugger.BreakLocation;
using ProtocolLocation = Jint.DevTools.Protocol.Debugger.Location;

namespace Jint.DevTools.Domains;

/// <summary>
/// Where a client asks the engine to stop, and where the engine says it can.
/// </summary>
/// <remarks>
/// <para>
/// <b>A breakpoint is snapped before it is set.</b> A client asks for a line, and the engine only ever stops
/// at the positions its step lane visits — the start of a statement, a loop's test, a function's return
/// point. <c>DebugHandler.FindStepLocation</c> answers the first such position at or after what was asked
/// for, which is why a breakpoint on column zero of an indented line is reached and one on a blank line
/// moves down to the next statement. The <c>locations</c> a client is answered with are where the engine
/// will actually stop, not where the client asked.
/// </para>
/// <para>
/// <b>A request outlives the scripts it matched.</b> <c>setBreakpointByUrl</c> is kept, so a script parsed
/// afterwards under a matching name gets the breakpoint before its first statement runs and the client hears
/// <c>breakpointResolved</c>. That is what makes setting a breakpoint before starting a run work at all.
/// </para>
/// <para>
/// <b>One engine breakpoint per position.</b> <c>BreakPointCollection</c> keys on source, line and column
/// and a second breakpoint at one position replaces the first, so two protocol breakpoints that snap to the
/// same place collapse into one; the second identifier is answered and removing it removes the position.
/// Chrome keeps both. It is a divergence and not a defect: the engine's collection is what the pause reads.
/// </para>
/// </remarks>
internal sealed partial class DebuggerDomain
{
    /// <summary>
    /// V8's own breakpoint-identifier shape, which is <c>&lt;type&gt;:&lt;line&gt;:&lt;column&gt;:&lt;selector&gt;</c>.
    /// </summary>
    /// <remarks>
    /// Copied rather than invented so that identical requests answer identical identifiers, which is what
    /// makes "this breakpoint already exists" a thing a client can be told rather than a duplicate it never
    /// finds out about.
    /// </remarks>
    private const int ByUrl = 1;

    /// <inheritdoc cref="ByUrl"/>
    private const int ByScriptHash = 2;

    /// <inheritdoc cref="ByUrl"/>
    private const int ByScriptId = 3;

    /// <inheritdoc cref="ByUrl"/>
    private const int DebugCommand = 4;

    /// <inheritdoc cref="ByUrl"/>
    private const int Instrumentation = 7;

    /// <summary>How long a client's <c>urlRegex</c> may run against one script name before it is abandoned.</summary>
    /// <remarks>
    /// A client's pattern is a client's pattern: it is not this package's job to decide it is well written,
    /// and a catastrophically backtracking one would otherwise hold the engine thread for as long as it
    /// liked. A pattern that times out matches nothing, and the breakpoint stays pending.
    /// </remarks>
    private static readonly TimeSpan RegexTimeout = TimeSpan.FromMilliseconds(250);

    private readonly object _breakpointGate = new();
    private readonly Dictionary<string, BreakpointRequest> _breakpoints = new(StringComparer.Ordinal);

    private int _oneShot;

    /// <inheritdoc/>
    protected override ValueTask<SetBreakpointByUrlResponse> SetBreakpointByUrlAsync(SetBreakpointByUrlRequest parameters, CommandContext context)
    {
        RequireDebuggerEngine();

        var selector = Selector(parameters);
        var id = Identifier(selector.Kind, parameters.LineNumber, parameters.ColumnNumber ?? 0, selector.Text);

        var request = new BreakpointRequest(id, selector, parameters.LineNumber, parameters.ColumnNumber, parameters.Condition);
        Remember(request);

        var locations = new List<ProtocolLocation>();
        foreach (var script in _target.Scripts!.Snapshot())
        {
            if (Place(request, script) is { } placed)
            {
                locations.Add(placed);
            }
        }

        return new ValueTask<SetBreakpointByUrlResponse>(new SetBreakpointByUrlResponse
        {
            BreakpointId = id,
            Locations = [.. locations],
        });
    }

    /// <inheritdoc/>
    protected override ValueTask<SetBreakpointResponse> SetBreakpointAsync(SetBreakpointRequest parameters, CommandContext context)
    {
        var script = RequireScript(parameters.Location.ScriptId);
        var id = Identifier(ByScriptId, parameters.Location.LineNumber, parameters.Location.ColumnNumber ?? 0, script.ScriptId);

        var request = new BreakpointRequest(
            id,
            new BreakpointSelector(ByScriptId, script.ScriptId, null),
            parameters.Location.LineNumber,
            parameters.Location.ColumnNumber,
            parameters.Condition);

        Remember(request);

        var placed = Place(request, script);
        if (placed is null)
        {
            Forget(id);

            // V8's wording for a location no code follows: everything after the line asked for is either
            // blank or something the step lane never visits.
            Throw.ServerError("Could not resolve breakpoint");
        }

        return new ValueTask<SetBreakpointResponse>(new SetBreakpointResponse
        {
            BreakpointId = id,
            ActualLocation = placed,
        });
    }

    /// <inheritdoc/>
    protected override ValueTask<EmptyResult> RemoveBreakpointAsync(RemoveBreakpointRequest parameters, CommandContext context)
    {
        // A breakpoint nothing knows about is not an error: a client removing what it already removed, or
        // what a disable took with it, is tidying up.
        Forget(parameters.BreakpointId);
        return new ValueTask<EmptyResult>(EmptyResult.Instance);
    }

    /// <inheritdoc/>
    protected override ValueTask<EmptyResult> SetBreakpointsActiveAsync(SetBreakpointsActiveRequest parameters, CommandContext context)
    {
        RequireDebuggerEngine();

        // The engine's own switch, which makes every breakpoint fail to match without forgetting any of
        // them. It is the engine's rather than this session's, which is the same thing here: one session
        // debugs one engine.
        _target.Engine.Debugger.BreakPoints.Active = parameters.Active;
        return new ValueTask<EmptyResult>(EmptyResult.Instance);
    }

    /// <summary>
    /// Answers every position in a range the engine will stop at, which is what a front end shades a line
    /// with.
    /// </summary>
    /// <remarks>
    /// The walk is purely syntactic, so it answers for a script that has never run. <c>restrictToFunction</c>
    /// is accepted and ignored: the range a client sends for it is already the function's own, so the answer
    /// is the same set either way.
    /// </remarks>
    protected override ValueTask<GetPossibleBreakpointsResponse> GetPossibleBreakpointsAsync(GetPossibleBreakpointsRequest parameters, CommandContext context)
    {
        var script = RequireScript(parameters.Start.ScriptId);

        if (parameters.End is { } end && !string.Equals(end.ScriptId, script.ScriptId, StringComparison.Ordinal))
        {
            // V8's wording. The two ends of a range name one script, or the range means nothing.
            Throw.ServerError("Locations should contain the same scriptId");
        }

        // The protocol counts lines from zero and the engine from one; the end is exclusive on both sides,
        // and an absent end means the rest of the script.
        var startLine = parameters.Start.LineNumber + 1;
        var startColumn = parameters.Start.ColumnNumber ?? 0;
        var endLine = int.MaxValue;
        var endColumn = int.MaxValue;
        if (parameters.End is { } bound)
        {
            endLine = bound.LineNumber + 1;
            endColumn = bound.ColumnNumber ?? 0;
        }

        var found = DebugHandler.GetStepLocations(script.Program, startLine, startColumn, endLine, endColumn);
        var locations = new ProtocolBreakLocation[found.Count];

        for (var i = 0; i < found.Count; i++)
        {
            var location = found[i];
            locations[i] = new ProtocolBreakLocation
            {
                ScriptId = script.ScriptId,
                LineNumber = Math.Max(0, location.Line - 1),
                ColumnNumber = location.Column,
                Type = LocationType(location.Kind),
            };
        }

        return new ValueTask<GetPossibleBreakpointsResponse>(new GetPossibleBreakpointsResponse { Locations = locations });
    }

    /// <summary>
    /// Resumes, and stops once at a position of the client's choosing.
    /// </summary>
    /// <remarks>
    /// A breakpoint that removes itself when it is hit, which is what the command means: the client is
    /// running to a line, not setting a breakpoint there. <c>targetCallFrames</c> is accepted and ignored —
    /// the engine's breakpoints match a position rather than a frame, so <c>current</c> and <c>any</c> are
    /// the same thing here, and a client that asked for <c>current</c> may stop in a recursive call it did
    /// not mean.
    /// </remarks>
    protected override ValueTask<EmptyResult> ContinueToLocationAsync(ContinueToLocationRequest parameters, CommandContext context)
    {
        RequirePaused();

        var script = RequireScript(parameters.Location.ScriptId);
        var id = Identifier(DebugCommand, parameters.Location.LineNumber, parameters.Location.ColumnNumber ?? 0, Interlocked.Increment(ref _oneShot).ToString(CultureInfo.InvariantCulture));

        var request = new BreakpointRequest(
            id,
            new BreakpointSelector(DebugCommand, script.ScriptId, null),
            parameters.Location.LineNumber,
            parameters.Location.ColumnNumber,
            condition: null)
        {
            IsOneShot = true,
        };

        Remember(request);

        if (Place(request, script) is null)
        {
            Forget(id);
            Throw.ServerError("Could not resolve breakpoint");
        }

        RequestResume(StepMode.None);
        return new ValueTask<EmptyResult>(EmptyResult.Instance);
    }

    /// <summary>
    /// Answers an identifier for an instrumentation point this target never reaches.
    /// </summary>
    /// <remarks>
    /// The two the protocol defines — <c>beforeScriptExecution</c> and
    /// <c>beforeScriptWithSourceMapExecution</c> — are a page's, and a front end sends one while connecting.
    /// An identifier is answered so that the client's own bookkeeping holds and <c>removeBreakpoint</c> takes
    /// it away again; nothing ever pauses on it.
    /// </remarks>
    protected override ValueTask<SetInstrumentationBreakpointResponse> SetInstrumentationBreakpointAsync(SetInstrumentationBreakpointRequest parameters, CommandContext context)
    {
        return new ValueTask<SetInstrumentationBreakpointResponse>(new SetInstrumentationBreakpointResponse
        {
            BreakpointId = Identifier(Instrumentation, 0, 0, parameters.Instrumentation),
        });
    }

    /// <summary>Places a pending request on one script, answering where the engine will actually stop.</summary>
    private ProtocolLocation? Place(BreakpointRequest request, RegisteredScript script)
    {
        if (!Matches(request.Selector, script))
        {
            return null;
        }

        var snapped = DebugHandler.FindStepLocation(script.Program, request.LineNumber + 1, request.ColumnNumber ?? 0);
        if (snapped is not { } location)
        {
            return null;
        }

        var breakLocation = location.ToBreakLocation();

        // The engine is written to under the same lock the request table is, so that a detach arriving from
        // a transport thread cannot remove the placed positions in between and leave this one behind on an
        // engine nobody is debugging any more. The engine's own collection lock is the only one taken under
        // this one, never the other way round, and it is never held while script runs.
        lock (_breakpointGate)
        {
            if (!_breakpoints.TryGetValue(request.Id, out var live) || !ReferenceEquals(live, request))
            {
                // Removed while this was being resolved.
                return null;
            }

            if (!live.Placed.Contains(breakLocation))
            {
                live.Placed.Add(breakLocation);
                _target.Engine.Debugger.BreakPoints.Set(new DevToolsBreakPoint(request.Id, breakLocation, request.Condition, request.IsOneShot));
            }
        }

        return At(script, location.Line, location.Column);
    }

    /// <summary>Places every pending request that matches a script the engine has just parsed.</summary>
    private void ResolvePending(RegisteredScript script)
    {
        BreakpointRequest[] pending;
        lock (_breakpointGate)
        {
            pending = [.. _breakpoints.Values];
        }

        foreach (var request in pending)
        {
            if (Place(request, script) is { } location)
            {
                EmitDetached(DebuggerEvents.BreakpointResolved(new BreakpointResolvedEvent
                {
                    BreakpointId = request.Id,
                    Location = location,
                }));
            }
        }
    }

    private void Remember(BreakpointRequest request)
    {
        lock (_breakpointGate)
        {
            if (_breakpoints.ContainsKey(request.Id))
            {
                // V8's wording, and the reason the identifier is derived from the request rather than
                // counted: a client that sets the same breakpoint twice is told, instead of holding two
                // identifiers for one place and finding that removing either removes both.
                Throw.ServerError("Breakpoint at specified location already exists.");
            }

            _breakpoints.Add(request.Id, request);
        }
    }

    private void Forget(string breakpointId)
    {
        lock (_breakpointGate)
        {
            if (_breakpoints.Remove(breakpointId, out var removed))
            {
                RemovePlaced(removed);
            }
        }
    }

    private void RemoveAllBreakpoints()
    {
        lock (_breakpointGate)
        {
            foreach (var request in _breakpoints.Values)
            {
                RemovePlaced(request);
            }

            _breakpoints.Clear();
        }
    }

    /// <summary>Takes one request's positions off the engine, leaving anything another request placed.</summary>
    /// <remarks>Called under <c>_breakpointGate</c>, which is what keeps it and <see cref="Place"/> apart.</remarks>
    private void RemovePlaced(BreakpointRequest request)
    {
        if (request.Placed.Count == 0 || _target.Scripts is null)
        {
            // Nothing was placed, or the engine never had a debugger to place it on. Reaching for
            // Engine.Debugger in either case would claim engine ownership from a transport thread for the
            // sake of a collection that is empty.
            request.Placed.Clear();
            return;
        }

        var breakpoints = _target.Engine.Debugger.BreakPoints;
        foreach (var location in request.Placed)
        {
            // Removal is by position, and a later request may have replaced what this one set there: the
            // engine's collection keeps one breakpoint per position. Only ours is taken away.
            if (breakpoints.Contains(location) && IsOurs(breakpoints, location, request.Id))
            {
                breakpoints.RemoveAt(location);
            }
        }

        request.Placed.Clear();
    }

    private static bool IsOurs(BreakPointCollection breakpoints, EngineBreakLocation location, string breakpointId)
    {
        foreach (var candidate in breakpoints)
        {
            if (candidate is DevToolsBreakPoint ours &&
                string.Equals(ours.BreakpointId, breakpointId, StringComparison.Ordinal) &&
                ours.Location == location)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Whether one script is what a request asked to break in.</summary>
    private static bool Matches(BreakpointSelector selector, RegisteredScript script) => selector.Kind switch
    {
        ByScriptId or DebugCommand => string.Equals(selector.Text, script.ScriptId, StringComparison.Ordinal),
        ByScriptHash => string.Equals(selector.Text, script.Hash, StringComparison.Ordinal),
        // A client sends back the URL it read off scriptParsed; a host driving the protocol itself has only
        // ever seen the source name it passed. ScriptUrl.Same accepts either.
        _ => selector.Pattern is { } pattern ? IsMatch(pattern, script.Url) : ScriptUrl.Same(selector.Text, script.Url),
    };

    private static bool IsMatch(Regex pattern, string url)
    {
        try
        {
            return pattern.IsMatch(url);
        }
        catch (RegexMatchTimeoutException)
        {
            // A pattern that cannot be evaluated matches nothing rather than failing the parse of every
            // script that follows it.
            return false;
        }
    }

    /// <summary>Reads the one of <c>url</c>, <c>urlRegex</c> and <c>scriptHash</c> a client sent.</summary>
    private static BreakpointSelector Selector(SetBreakpointByUrlRequest parameters)
    {
        if (parameters.Url is { } url)
        {
            return new BreakpointSelector(ByUrl, url, null);
        }

        if (parameters.UrlRegex is { } expression)
        {
            Regex pattern;
            try
            {
                // RegexOptions.None deliberately: the pattern is the client's own and compiling it would
                // spend more on a breakpoint than the matching ever costs.
                pattern = new Regex(expression, RegexOptions.None, RegexTimeout);
            }
            catch (ArgumentException exception)
            {
                return Throw.InvalidParams<BreakpointSelector>("Invalid parameters", "urlRegex: " + exception.Message);
            }

            return new BreakpointSelector(ByUrl, expression, pattern);
        }

        if (parameters.ScriptHash is { } hash)
        {
            return new BreakpointSelector(ByScriptHash, hash, null);
        }

        // V8's wording, and what a client matches on to know it sent an incomplete request.
        return Throw.ServerError<BreakpointSelector>("Either url or urlRegex must be specified.");
    }

    private static string Identifier(int kind, int line, int column, string selector) => string.Create(
        CultureInfo.InvariantCulture,
        $"{kind}:{line}:{column}:{selector}");

    /// <summary>Which of the protocol's break-location types a step location is, if any.</summary>
    /// <remarks>
    /// The protocol also has <c>call</c>, and nothing here ever answers it: the engine's step lane pauses on
    /// statements and never on the calls inside one, so naming a call position would name a place the
    /// debugger never stops.
    /// </remarks>
    private static string? LocationType(StepLocationKind kind) => kind switch
    {
        StepLocationKind.Return => BreakLocationTypeValues.Return,
        StepLocationKind.DebuggerStatement => BreakLocationTypeValues.DebuggerStatement,
        _ => null,
    };
}

/// <summary>
/// A breakpoint as the client asked for it, kept so that a script parsed later can still receive it.
/// </summary>
/// <remarks>
/// The positions it has already been placed at are on it rather than in a second table, because removing the
/// breakpoint and removing those positions are one operation and a table that could disagree with the engine
/// is a breakpoint nothing takes away.
/// </remarks>
internal sealed class BreakpointRequest
{
    internal BreakpointRequest(string id, BreakpointSelector selector, int lineNumber, int? columnNumber, string? condition)
    {
        Id = id;
        Selector = selector;
        LineNumber = lineNumber;
        ColumnNumber = columnNumber;
        Condition = condition;
    }

    /// <summary>Gets the identifier the client holds the breakpoint by.</summary>
    internal string Id { get; }

    /// <summary>Gets what decides which scripts the breakpoint belongs in.</summary>
    internal BreakpointSelector Selector { get; }

    /// <summary>Gets the 0-based line the client asked for.</summary>
    internal int LineNumber { get; }

    /// <summary>Gets the 0-based column the client asked for, or <see langword="null"/> for the whole line.</summary>
    internal int? ColumnNumber { get; }

    /// <summary>Gets the expression that has to be true for the engine to stop, or <see langword="null"/>.</summary>
    internal string? Condition { get; }

    /// <summary>Gets or sets whether hitting the breakpoint takes it away, which <c>continueToLocation</c> sets.</summary>
    internal bool IsOneShot { get; init; }

    /// <summary>Gets the engine positions this request has been placed at.</summary>
    internal List<EngineBreakLocation> Placed { get; } = [];
}

/// <summary>
/// Which scripts a breakpoint request belongs in: an exact name, a pattern over names, a digest, or one
/// script's identifier.
/// </summary>
/// <param name="Kind">V8's breakpoint-type number, which is also the first field of the identifier.</param>
/// <param name="Text">What the client sent, and what the identifier carries.</param>
/// <param name="Pattern">The compiled pattern for a <c>urlRegex</c> request, or <see langword="null"/>.</param>
[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct BreakpointSelector(int Kind, string Text, Regex? Pattern);

/// <summary>
/// A breakpoint the engine holds, carrying the identifier the protocol knows it by.
/// </summary>
/// <remarks>
/// <c>BreakPoint</c> is unsealed for exactly this: the engine matches and evaluates it like any other, and
/// the pause reads the identifier back off whatever matched so that <c>hitBreakpoints</c> names what the
/// client set.
/// </remarks>
internal sealed class DevToolsBreakPoint : BreakPoint
{
    internal DevToolsBreakPoint(string breakpointId, EngineBreakLocation location, string? condition, bool isOneShot)
        : base(location.Source, location.Line, location.Column, condition)
    {
        BreakpointId = breakpointId;
        IsOneShot = isOneShot;
    }

    /// <summary>Gets the identifier the client holds this breakpoint by.</summary>
    internal string BreakpointId { get; }

    /// <summary>Gets whether hitting it takes it away.</summary>
    internal bool IsOneShot { get; }
}
