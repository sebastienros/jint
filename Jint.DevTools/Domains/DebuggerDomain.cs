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
        var script = RequireScript(parameters.ScriptId);

        if (!_target.Scripts!.TryGetSourceText(script, out var sourceText) || sourceText is null)
        {
            // Source text is retained through the same switch Function.prototype.toString uses, and a module
            // a loader built follows the parsing options that loader was given -- which is why a host can do
            // everything right and still meet this for one script. Issue #3588 tracks the loader half.
            Throw.ServerError(
                "No source text was retained for this script; enable Options.RetainFunctionSourceText",
                "Options.UseDevTools() sets it for what the engine parses itself; a prepared script or a loader-supplied module follows the parsing options it was built with");
        }

        return new ValueTask<GetScriptSourceResponse>(new GetScriptSourceResponse { ScriptSource = sourceText });
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
    /// Records what a client asked to pause on, and pauses on none of it yet.
    /// </summary>
    /// <remarks>
    /// The engine raises <c>DebugHandler.ExceptionThrown</c> for every throw and says nothing about whether a
    /// <c>catch</c> is waiting, so <c>uncaught</c> could not be honoured and <c>all</c> would stop on every
    /// internally handled throw a library makes. The state is accepted and kept — the front end sends
    /// <c>none</c> on connect and a client reads its own setting back from what it sent — and the pause
    /// itself arrives with the engine's caught-or-not seam.
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

        return new ValueTask<EmptyResult>(EmptyResult.Instance);
    }

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

    /// <summary>
    /// The one execution context an engine target has, which every location and frame names.
    /// </summary>
    private const int MainExecutionContextId = 1;
}
