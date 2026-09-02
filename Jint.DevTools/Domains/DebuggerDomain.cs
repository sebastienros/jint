using System.Text.RegularExpressions;
using Jint.DevTools.Protocol;
using Jint.DevTools.Protocol.Debugger;
using Jint.DevTools.Session;
using Jint.Runtime.Debugger;

namespace Jint.DevTools.Domains;

/// <summary>
/// The <c>Debugger</c> domain: the scripts an engine has parsed, the breakpoints set in them, and the pause
/// a client drives from.
/// </summary>
/// <remarks>
/// <para>
/// <b>One session debugs one engine.</b> Breakpoints, the step mode and the pause all live on the engine's
/// own <c>DebugHandler</c> rather than per session, so a second attachment enabling the domain would share
/// the first one's breakpoints and steal its pauses. It is refused with <c>-32000</c> instead, which is a
/// documented divergence from Chrome, where every session has its own.
/// </para>
/// <para>
/// <b>The pause is synchronous, and that is the whole design.</b> <c>DebugHandler</c> invokes the
/// <c>Break</c> and <c>Step</c> delegates inline and takes the next step mode from what they return; there is
/// no resume token. So the pause is a message loop running inside the handler, on the engine thread — V8's
/// <c>runMessageLoopOnPause</c> — and every consequence of that is in <c>DebuggerDomain.Pause.cs</c>.
/// </para>
/// <para>
/// See <see href="https://chromedevtools.github.io/devtools-protocol/tot/Debugger/"/>.
/// </para>
/// </remarks>
internal sealed partial class DebuggerDomain : DebuggerDomainBase
{
    private readonly EngineTarget _target;
    private readonly LogDomain _log;
    private readonly RemoteObjectMapper _objects;
    private readonly DebugHandler.DebugEventHandler _onBreak;
    private readonly DebugHandler.DebugEventHandler _onStep;
    private readonly DebugHandler.DebugEventHandler _onSkip;
    private readonly Action<RegisteredScript> _onScriptParsed;

    private int _handlersSubscribed;

    internal DebuggerDomain(EngineTarget target, LogDomain log)
    {
        _target = target;
        _log = log;
        _objects = new RemoteObjectMapper(target, this);
        _onBreak = OnBreak;
        _onStep = OnStep;
        _onSkip = OnSkip;
        _onScriptParsed = OnScriptParsed;
    }

    /// <summary>
    /// Enables the domain, claiming the engine's debugger for this attachment.
    /// </summary>
    /// <remarks>
    /// The identifier answered is the target's, because it names the debugger and one engine has one. Chrome
    /// answers a globally unique string that clients keep to address the target later; ours is unique in the
    /// same way and for the same lifetime.
    /// </remarks>
    protected override async ValueTask<EnableResponse> EnableAsync(EnableRequest parameters, CommandContext context)
    {
        RequireDebuggerEngine();

        if (!_target.TryClaimDebugger(this))
        {
            Throw.ServerError(
                "Another session already has the Debugger domain enabled on this target",
                "breakpoints and the step mode live on the engine rather than on a session, so one client debugs one engine at a time");
        }

        await MarkEnabledAsync(context).ConfigureAwait(false);
        return new EnableResponse { DebuggerId = _target.TargetId };
    }

    /// <summary>
    /// Disables the domain: removes this session's breakpoints, resumes a paused engine, and gives the
    /// debugger back.
    /// </summary>
    /// <remarks>
    /// <b>A disable while paused resumes.</b> Nothing else would: the engine thread is inside the pause loop,
    /// and the client that would have ended it has just said it is done.
    /// </remarks>
    protected override async ValueTask<EmptyResult> DisableAsync(EmptyParameters parameters, CommandContext context)
    {
        await MarkDisabledAsync(context).ConfigureAwait(false);
        return EmptyResult.Instance;
    }

    /// <inheritdoc/>
    protected override async ValueTask OnEnabledAsync(CommandContext context)
    {
        if (Interlocked.Exchange(ref _handlersSubscribed, 1) == 0)
        {
            var debugger = _target.Engine.Debugger;
            debugger.Break += _onBreak;
            debugger.Step += _onStep;
            _target.Scripts!.Parsed += _onScriptParsed;
        }

        ApplyPauseOnExceptions();

        // Every script parsed before the client asked, in the order the engine parsed them. A front end's
        // Sources panel is built from this replay and not from what arrives afterwards, so a client that
        // attached to a running engine sees what it is running.
        foreach (var script in _target.Scripts!.Snapshot())
        {
            await EmitAsync(DebuggerEvents.ScriptParsed(ScriptParsed(script)), context.CancellationToken).ConfigureAwait(false);
        }
    }

    /// <inheritdoc/>
    protected override ValueTask OnDisabledAsync(CommandContext context)
    {
        Teardown();
        return default;
    }

    /// <summary>
    /// Releases everything this attachment holds of the engine's debugger, from whichever thread noticed.
    /// </summary>
    /// <remarks>
    /// Called when the client detaches or its connection goes, on a transport thread. <b>The order matters:
    /// the breakpoints go first and the resume last</b>, so that an engine released back into a script it was
    /// paused in does not walk straight into the next breakpoint of a client that has gone.
    /// </remarks>
    internal void Detach()
    {
        Teardown();
        _objects.ReleaseAll();
    }

    /// <inheritdoc/>
    protected override ValueTask<GetScriptSourceResponse> GetScriptSourceAsync(GetScriptSourceRequest parameters, CommandContext context)
    {
        var sourceText = RequireSourceText(RequireScript(parameters.ScriptId));
        return new ValueTask<GetScriptSourceResponse>(new GetScriptSourceResponse { ScriptSource = sourceText });
    }

    /// <summary>
    /// Answers every line of one script that holds a query, which is what a front end's search box sends.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>One match per line, not per occurrence</b>, which is the shape V8 answers and the shape a front end
    /// draws: the panel lists lines and highlights within them. Line numbers are the protocol's, counting
    /// from zero, and a line's content is reported without its terminator.
    /// </para>
    /// <para>
    /// The search is over the source text the parse retained, so a script that has none is refused for the
    /// same reason <c>getScriptSource</c> refuses it. A regular expression is the client's, so it is compiled
    /// with a bound: a pattern that backtracks for ever would otherwise hold the engine thread with it.
    /// </para>
    /// </remarks>
    protected override ValueTask<SearchInContentResponse> SearchInContentAsync(SearchInContentRequest parameters, CommandContext context)
    {
        var sourceText = RequireSourceText(RequireScript(parameters.ScriptId));
        var caseSensitive = parameters.CaseSensitive == true;
        var matches = new List<SearchMatch>();

        var pattern = parameters.IsRegex == true ? Compile(parameters.Query, caseSensitive) : null;
        var comparison = caseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;

        var line = 0;
        var start = 0;

        while (start <= sourceText.Length)
        {
            var terminator = sourceText.IndexOf('\n', start);
            var end = terminator < 0 ? sourceText.Length : terminator;

            // A checkout is CRLF on one operating system and LF on another; the content a client is shown
            // must not depend on which one the host read the script from.
            var content = sourceText.AsSpan(start, end - start).TrimEnd('\r').ToString();

            if (Matches(content, parameters.Query, comparison, pattern))
            {
                matches.Add(new SearchMatch { LineNumber = line, LineContent = content });
            }

            if (terminator < 0)
            {
                break;
            }

            start = terminator + 1;
            line++;
        }

        return new ValueTask<SearchInContentResponse>(new SearchInContentResponse { Result = [.. matches] });
    }

    private static bool Matches(string content, string query, StringComparison comparison, Regex? pattern)
    {
        if (pattern is null)
        {
            return content.Contains(query, comparison);
        }

        try
        {
            return pattern.IsMatch(content);
        }
        catch (RegexMatchTimeoutException)
        {
            return Throw.ServerError<bool>(
                "The query took too long to match",
                "the pattern is the client's and is bounded so that one cannot hold the engine thread; simplify it");
        }
    }

    /// <summary>Compiles a client's pattern, or says it is not one rather than searching for its text.</summary>
    private static Regex Compile(string query, bool caseSensitive)
    {
        try
        {
            return new Regex(
                query,
                caseSensitive ? RegexOptions.None : RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
                SearchTimeout);
        }
        catch (ArgumentException exception)
        {
            return Throw.ServerError<Regex>("The query is not a valid regular expression", exception.Message);
        }
    }

    /// <summary>
    /// Answers the success a client expects and synthesizes no asynchronous frames.
    /// </summary>
    /// <remarks>
    /// The engine retains no stack across a promise reaction or a timer callback, and inventing one that is
    /// wrong is worse than reporting none. Every recorded client sends this while connecting, so refusing it
    /// would make an ordinary connection fail over a feature the client can do without.
    /// </remarks>
    protected override ValueTask<EmptyResult> SetAsyncCallStackDepthAsync(SetAsyncCallStackDepthRequest parameters, CommandContext context)
    {
        return new ValueTask<EmptyResult>(EmptyResult.Instance);
    }

    /// <summary>
    /// Answers the success a client expects and skips no frames.
    /// </summary>
    /// <remarks>
    /// Stepping is the engine's, and a filter that silently skipped frames would make a step mean something
    /// different from what <c>DebugHandler</c> did — which is the one thing a debugger's step must not do.
    /// The front end sends this on connect with its own list of library patterns.
    /// </remarks>
    protected override ValueTask<EmptyResult> SetBlackboxPatternsAsync(SetBlackboxPatternsRequest parameters, CommandContext context)
    {
        return new ValueTask<EmptyResult>(EmptyResult.Instance);
    }

    /// <inheritdoc cref="SetBlackboxPatternsAsync"/>
    protected override ValueTask<EmptyResult> SetBlackboxedRangesAsync(SetBlackboxedRangesRequest parameters, CommandContext context)
    {
        return new ValueTask<EmptyResult>(EmptyResult.Instance);
    }

    /// <summary>
    /// Sets which thrown exceptions stop the engine, in the client's four states.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Three of the four are the engine's own <see cref="ExceptionPauseMode"/>. <c>caught</c> is not: the
    /// engine has no mode that stops only where something will catch, so it is asked for
    /// <see cref="ExceptionPauseMode.All"/> and the uncaught half is dropped in the pause decision.
    /// </para>
    /// <para>
    /// <b>The mode is the attachment's, not the engine's.</b> It reaches the engine only while this domain is
    /// enabled and goes back to <see cref="ExceptionPauseMode.None"/> on disable or detach, so a client that
    /// walked away does not leave a host's engine stopping on throws with nobody to answer the pause.
    /// </para>
    /// </remarks>
    protected override ValueTask<EmptyResult> SetPauseOnExceptionsAsync(SetPauseOnExceptionsRequest parameters, CommandContext context)
    {
        _pauseOnExceptions = parameters.State switch
        {
            SetPauseOnExceptionsRequestStateValues.None => SetPauseOnExceptionsRequestStateValues.None,
            SetPauseOnExceptionsRequestStateValues.Caught => SetPauseOnExceptionsRequestStateValues.Caught,
            SetPauseOnExceptionsRequestStateValues.Uncaught => SetPauseOnExceptionsRequestStateValues.Uncaught,
            SetPauseOnExceptionsRequestStateValues.All => SetPauseOnExceptionsRequestStateValues.All,
            _ => Throw.ServerError<string>("Unknown pause on exceptions mode: " + parameters.State),
        };

        ApplyPauseOnExceptions();
        return new ValueTask<EmptyResult>(EmptyResult.Instance);
    }

    /// <summary>Pushes the client's pause mode onto the engine, or takes it back off.</summary>
    /// <remarks>
    /// Called from the command that sets it, from <c>enable</c> — a client may send the state before it
    /// enables, and the front end does — and from the teardown that resets it. Safe from a transport thread:
    /// it writes one property and runs no engine code.
    /// </remarks>
    private void ApplyPauseOnExceptions()
    {
        if (_target.Scripts is null)
        {
            return;
        }

        _target.Engine.Debugger.PauseOnExceptions = IsEnabled ? EnginePauseMode(_pauseOnExceptions) : ExceptionPauseMode.None;
    }

    /// <summary>The engine mode that answers a client's state, which the protocol's four map onto one each.</summary>
    private static ExceptionPauseMode EnginePauseMode(string state) => state switch
    {
        SetPauseOnExceptionsRequestStateValues.Uncaught => ExceptionPauseMode.Uncaught,
        SetPauseOnExceptionsRequestStateValues.Caught => ExceptionPauseMode.Caught,
        SetPauseOnExceptionsRequestStateValues.All => ExceptionPauseMode.All,
        _ => ExceptionPauseMode.None,
    };

    /// <summary>Announces one newly parsed program, and resolves whatever was waiting for it.</summary>
    /// <remarks>
    /// Runs on the engine thread, from inside <c>BeforeEvaluate</c> — before the program executes, so a
    /// breakpoint a client set by URL before the script existed is in place by the time its first statement
    /// runs. That ordering is the whole point of resolving here rather than on the next command.
    /// </remarks>
    private void OnScriptParsed(RegisteredScript script)
    {
        if (!IsEnabled)
        {
            return;
        }

        EmitDetached(DebuggerEvents.ScriptParsed(ScriptParsed(script)));
        ResolvePending(script);
    }

    private static ScriptParsedEvent ScriptParsed(RegisteredScript script) => new()
    {
        ScriptId = script.ScriptId,
        Url = script.Url,
        StartLine = script.StartLine,
        StartColumn = script.StartColumn,
        EndLine = script.EndLine,
        EndColumn = script.EndColumn,
        ExecutionContextId = MainExecutionContextId,
        Hash = script.Hash,

        // The protocol requires the member and describes a build identifier a compiler stamped; nothing
        // compiles here, and an empty string is what Chrome sends when it has none either.
        BuildId = "",
        IsModule = script.IsModule,
        Length = script.Length,
        ScriptLanguage = ScriptLanguageValues.JavaScript,
        EmbedderName = string.IsNullOrEmpty(script.Url) ? null : script.Url,
    };

    /// <summary>Undoes everything <c>enable</c> did, in the order that leaves the engine running.</summary>
    private void Teardown()
    {
        if (Interlocked.Exchange(ref _handlersSubscribed, 0) != 0)
        {
            var debugger = _target.Engine.Debugger;
            debugger.Break -= _onBreak;
            debugger.Step -= _onStep;
            _target.Scripts!.Parsed -= _onScriptParsed;
        }

        DisarmPause();
        RemoveAllBreakpoints();
        Volatile.Write(ref _skipAllPauses, 0);
        _pauseOnExceptions = SetPauseOnExceptionsRequestStateValues.None;
        ApplyPauseOnExceptions();

        // Last, and from whichever thread got here: the engine thread may be parked in the pause loop, and
        // nothing else is going to release it.
        RequestResume(StepMode.None);

        _target.ReleaseDebugger(this);
    }

    /// <summary>Refuses an engine the host did not build with the debugger switched on.</summary>
    private void RequireDebuggerEngine()
    {
        if (_target.Scripts is null)
        {
            Throw.ServerError(
                "The engine was not built with the debugger enabled",
                "Options.Debugger.Enabled is read once, when the engine is constructed; build it with options.UseDevTools()");
        }
    }

    /// <summary>Answers the script an identifier names, in Chrome's wording when there is none.</summary>
    private RegisteredScript RequireScript(string scriptId)
    {
        RequireDebuggerEngine();

        return _target.Scripts!.ById(scriptId)
            ?? Throw.ServerError<RegisteredScript>("No script with given id");
    }

    /// <summary>Answers a script's retained source, or says why there is none to answer with.</summary>
    /// <remarks>
    /// Source text is retained through the same switch <c>Function.prototype.toString</c> uses. A module a
    /// host loader supplied follows the engine's own setting since
    /// https://github.com/sebastienros/jint/issues/3588, so the two ways left to meet this are a
    /// <c>Prepared</c> program and a loader that named parsing options of its own — both of them the host
    /// having decided, which is why the answer names them rather than an engine setting to flip.
    /// </remarks>
    private string RequireSourceText(RegisteredScript script)
    {
        if (!_target.Scripts!.TryGetSourceText(script, out var sourceText) || sourceText is null)
        {
            Throw.ServerError(
                "No source text was retained for this script; enable Options.RetainFunctionSourceText",
                "Options.UseDevTools() sets it for what the engine parses itself and for a module a host loader supplied; a prepared script, or a module built with parsing options the loader named, follows those options instead");
        }

        return sourceText;
    }

    /// <summary>
    /// How long one line of one client-supplied pattern may take, which is a bound on the engine thread
    /// rather than on the search.
    /// </summary>
    private static readonly TimeSpan SearchTimeout = TimeSpan.FromSeconds(1);

    /// <summary>
    /// The one execution context an engine target has, which every location and frame names.
    /// </summary>
    private const int MainExecutionContextId = 1;
}
