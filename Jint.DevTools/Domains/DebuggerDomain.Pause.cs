using System.Globalization;
using Jint.DevTools.Protocol;
using Jint.DevTools.Protocol.Debugger;
using Jint.DevTools.Session;
using Jint.Native;
using Jint.Runtime;
using Jint.Runtime.Debugger;
using EngineCallFrame = Jint.Runtime.Debugger.CallFrame;
using ProtocolCallFrame = Jint.DevTools.Protocol.Debugger.CallFrame;
using ProtocolLocation = Jint.DevTools.Protocol.Debugger.Location;

namespace Jint.DevTools.Domains;

/// <summary>
/// The pause: a message loop that runs inside the engine's own debug handler, on the engine's own thread.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why a loop rather than a callback.</b> <c>DebugHandler</c> raises <c>Break</c> and <c>Step</c>
/// synchronously and takes the next <see cref="StepMode"/> from what the handler returns. Returning is what
/// resumes the script, so a handler that answered immediately would resume before the client had seen the
/// pause; and while it does not return, the engine thread is inside it and nothing posted through
/// <c>engine.Tasks.Post</c> will ever run, because that thread <i>is</i> the pump. So the handler drains the
/// mailbox itself — <see cref="EngineDispatcher.DrainPaused"/> — and answers commands inline until one of
/// them says what to do next. This is V8's <c>runMessageLoopOnPause</c>, and it is the one piece of this
/// package that cannot be written as ordinary asynchronous code.
/// </para>
/// <para>
/// <b>Three things end a pause, and every one of them has to.</b> A client command
/// (<c>resume</c>, <c>stepInto</c>, <c>stepOver</c>, <c>stepOut</c>, <c>continueToLocation</c>); the client
/// going away, which reaches <see cref="Detach"/> on a transport thread and resumes with
/// <see cref="StepMode.None"/>; and <see cref="DevToolsServerOptions.PauseTimeout"/>, which is what keeps a
/// client that walked away from wedging a host thread for ever. There is no fourth, and none of them may be
/// removed.
/// </para>
/// <para>
/// <b>Host work waits.</b> A host's <c>EngineTarget.Post</c> is work for a running engine; the paused drain
/// does not touch it, so it runs in order once the engine resumes.
/// </para>
/// </remarks>
internal sealed partial class DebuggerDomain
{
    /// <summary>What a scope handle, a <c>this</c> and a return value are billed to.</summary>
    /// <remarks>
    /// Chrome's own name for it. Everything a pause hands out lives exactly as long as that pause: the group
    /// is released on the way out, so a client that keeps a scope handle past the resume is told the handle
    /// has gone rather than shown values from a frame that no longer exists.
    /// </remarks>
    private const string BacktraceGroup = "backtrace";

    /// <summary>The sentinel in <see cref="_resumeMode"/> meaning nobody has said what to do next.</summary>
    private const int NotResuming = -1;

    /// <summary>
    /// How long one wait inside the pause loop parks for. Every arrival wakes it, so this bounds only how
    /// quickly a cancelled target or an elapsed bound is noticed.
    /// </summary>
    private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(100);

    /// <summary>The longest a bound may be, which is what the blocking primitives accept.</summary>
    private static readonly TimeSpan MaxPause = TimeSpan.FromMilliseconds(int.MaxValue);

    private readonly RemoteObjectRequest _backtrace = new() { Addressable = true, ObjectGroup = BacktraceGroup };

    private DebugCallStack? _stack;
    private string _pauseOnExceptions = SetPauseOnExceptionsRequestStateValues.None;
    private int _resumeMode = NotResuming;
    private int _pauseSerial;
    private int _paused;
    private int _pauseRequested;
    private int _skipSubscribed;
    private int _skipAllPauses;
    private int _exceptionId;

    /// <summary>Gets whether the engine is stopped inside this domain's pause loop right now.</summary>
    internal bool IsPaused => Volatile.Read(ref _paused) != 0;

    /// <summary>
    /// Gets what the client last asked to pause on, which nothing acts on yet.
    /// </summary>
    /// <remarks>
    /// Kept rather than discarded so that the state a client set is the state it can read back, and so that
    /// the engine seam that reports whether a throw was caught has somewhere to arrive.
    /// </remarks>
    internal string PauseOnExceptions => _pauseOnExceptions;

    private bool SkipAllPauses => Volatile.Read(ref _skipAllPauses) != 0;

    /// <summary>
    /// Asks the engine to stop at the next execution point it reaches.
    /// </summary>
    /// <remarks>
    /// <b>It arms a subscription rather than interrupting anything.</b> The command itself runs as an
    /// event-loop job, so the engine is between turns when it arrives; what it leaves behind is a
    /// <c>Skip</c> subscription, and the next execution point the engine reaches pauses on it. An engine
    /// running one long statement — a <c>while</c> loop with no calls in it — reaches no such point and no
    /// command reaches it either, which is what <c>Options.Constraints</c> is for rather than this.
    /// </remarks>
    protected override ValueTask<EmptyResult> PauseAsync(EmptyParameters parameters, CommandContext context)
    {
        RequireDebuggerEngine();
        ArmPause();
        return new ValueTask<EmptyResult>(EmptyResult.Instance);
    }

    /// <inheritdoc/>
    protected override ValueTask<EmptyResult> ResumeAsync(ResumeRequest parameters, CommandContext context)
        => Resuming(StepMode.None);

    /// <summary>
    /// Steps into the next call, or to the next execution point when there is none.
    /// </summary>
    /// <remarks>
    /// <c>breakOnAsyncCall</c> is accepted and ignored: the engine keeps no stack across a promise reaction,
    /// so there is no asynchronous call to break at. <c>skipList</c> is ignored for the reason blackboxing
    /// is — a step that silently skipped positions would mean something different from what the engine did.
    /// </remarks>
    protected override ValueTask<EmptyResult> StepIntoAsync(StepIntoRequest parameters, CommandContext context)
        => Resuming(StepMode.Into);

    /// <inheritdoc cref="StepIntoAsync"/>
    protected override ValueTask<EmptyResult> StepOverAsync(StepOverRequest parameters, CommandContext context)
        => Resuming(StepMode.Over);

    /// <inheritdoc/>
    protected override ValueTask<EmptyResult> StepOutAsync(EmptyParameters parameters, CommandContext context)
        => Resuming(StepMode.Out);

    /// <summary>
    /// Stops the engine pausing at all, without forgetting a single breakpoint.
    /// </summary>
    /// <remarks>
    /// What a front end's "deactivate breakpoints while I do this" sends. A step in progress is cancelled
    /// too, because a step is a pause the client asked for in advance.
    /// </remarks>
    protected override ValueTask<EmptyResult> SetSkipAllPausesAsync(SetSkipAllPausesRequest parameters, CommandContext context)
    {
        Volatile.Write(ref _skipAllPauses, parameters.Skip ? 1 : 0);
        return new ValueTask<EmptyResult>(EmptyResult.Instance);
    }

    /// <summary>
    /// Evaluates an expression in a paused frame's own environment.
    /// </summary>
    /// <remarks>
    /// <b>The top frame only.</b> <c>DebugHandler.Evaluate</c> runs in the engine's <i>active</i> execution
    /// context, which is the frame the engine stopped in; evaluating in an outer frame needs an engine seam
    /// that takes a call frame, and answering an outer frame's request against the top one would silently
    /// read the wrong variables. A client asking for another frame is told so.
    /// </remarks>
    protected override ValueTask<EvaluateOnCallFrameResponse> EvaluateOnCallFrameAsync(EvaluateOnCallFrameRequest parameters, CommandContext context)
    {
        var frame = RequireFrame(parameters.CallFrameId);
        if (frame != 0)
        {
            Throw.ServerError(
                "Only the top call frame can be evaluated",
                "the engine evaluates in its active execution context, and an outer frame's environment needs a debugger seam that takes a call frame");
        }

        var request = RemoteObjectRequest.From(parameters.ReturnByValue, parameters.GeneratePreview, parameters.ObjectGroup ?? BacktraceGroup);

        try
        {
            var value = _target.Engine.Debugger.Evaluate(parameters.Expression);
            return new ValueTask<EvaluateOnCallFrameResponse>(new EvaluateOnCallFrameResponse
            {
                Result = _objects.Describe(value, request),
            });
        }
        catch (DebugEvaluationException exception) when (exception.InnerException is JavaScriptException thrown)
        {
            return new ValueTask<EvaluateOnCallFrameResponse>(new EvaluateOnCallFrameResponse
            {
                Result = _objects.Describe(thrown.Error, request with { ByValue = false, Addressable = true }),
                ExceptionDetails = _objects.Exception(thrown, NextExceptionId(), MainExecutionContextId),
            });
        }
        catch (DebugEvaluationException exception)
        {
            // A parse failure, or the debugger refusing to evaluate at all. Not a JavaScript error object, so
            // there is nothing to describe and the reason is what the client gets.
            return Throw.ServerError<ValueTask<EvaluateOnCallFrameResponse>>(exception.Message, exception.InnerException?.Message);
        }
        catch (JavaScriptException thrown)
        {
            // The debugger propagates a host exception rather than wrapping it, and this is the one shape a
            // client can act on.
            return new ValueTask<EvaluateOnCallFrameResponse>(new EvaluateOnCallFrameResponse
            {
                Result = _objects.Describe(thrown.Error, request with { ByValue = false, Addressable = true }),
                ExceptionDetails = _objects.Exception(thrown, NextExceptionId(), MainExecutionContextId),
            });
        }
    }

    /// <summary>
    /// Writes one binding of one paused scope.
    /// </summary>
    /// <remarks>
    /// <b>This works for every frame, not only the top one</b>, because a scope's environment record is what
    /// is written and the engine hands one over per frame. It is also the only way to change a paused
    /// engine's state: the scope objects a client expands are read-only snapshots.
    /// </remarks>
    protected override ValueTask<EmptyResult> SetVariableValueAsync(SetVariableValueRequest parameters, CommandContext context)
    {
        var index = RequireFrame(parameters.CallFrameId);
        var scopes = _stack![index].ScopeChain;

        if ((uint) parameters.ScopeNumber >= (uint) scopes.Count)
        {
            Throw.ServerError("Could not find scope with given number");
        }

        var value = CallArguments.Resolve(_target, parameters.NewValue);

        try
        {
            scopes[parameters.ScopeNumber].SetBindingValue(parameters.VariableName, value);
        }
        catch (JavaScriptException exception)
        {
            // Writing a `const`, or a binding still in its temporal dead zone. The engine's own message says
            // which, and a client asked to change something it may not.
            Throw.ServerError(exception.Message);
        }

        return new ValueTask<EmptyResult>(EmptyResult.Instance);
    }

    /// <summary>Says what to do next and lets the pause loop notice, from whichever thread got here.</summary>
    private void RequestResume(StepMode mode)
    {
        Interlocked.Exchange(ref _resumeMode, (int) mode);
        _target.Dispatcher.Wake();
    }

    private ValueTask<EmptyResult> Resuming(StepMode mode)
    {
        RequirePaused();
        RequestResume(mode);
        return new ValueTask<EmptyResult>(EmptyResult.Instance);
    }

    /// <summary>Arms the pause a client asked for, subscribing the engine's catch-all execution point.</summary>
    /// <remarks>
    /// <b>The subscription exists only while a pause is outstanding.</b> <c>Skip</c> fires at every execution
    /// point the engine reaches and its return value <i>sets the step mode</i>, so a subscription left in
    /// place would have to keep answering with the mode the engine already has — which it has no way to read.
    /// Subscribing on demand and unsubscribing in the handler makes the only answer it ever gives the right
    /// one.
    /// </remarks>
    private void ArmPause()
    {
        Volatile.Write(ref _pauseRequested, 1);

        if (Interlocked.Exchange(ref _skipSubscribed, 1) == 0)
        {
            _target.Engine.Debugger.Skip += _onSkip;
        }
    }

    /// <summary>Cancels an outstanding pause request, and takes the subscription back off.</summary>
    private void DisarmPause()
    {
        Volatile.Write(ref _pauseRequested, 0);
        UnsubscribeSkip();
    }

    private void UnsubscribeSkip()
    {
        if (Interlocked.Exchange(ref _skipSubscribed, 0) != 0 && _target.Scripts is not null)
        {
            _target.Engine.Debugger.Skip -= _onSkip;
        }
    }

    /// <summary>A breakpoint or a <c>debugger</c> statement.</summary>
    private StepMode OnBreak(object sender, DebugInformation information) => Stop(information);

    /// <summary>The next position of a step the client asked for.</summary>
    private StepMode OnStep(object sender, DebugInformation information) => Stop(information);

    /// <summary>
    /// Every other execution point, which is where a <c>Debugger.pause</c> takes effect.
    /// </summary>
    /// <remarks>
    /// Returning <see cref="StepMode.None"/> when no pause is outstanding is not a neutral answer — the
    /// engine takes it as "stop stepping" — and it is nevertheless the right one: the subscription is only
    /// ever in place while a pause is armed, so getting here without one means the client disarmed it, which
    /// is <c>disable</c> or a detach, and clearing the step mode is what both of those mean.
    /// </remarks>
    private StepMode OnSkip(object sender, DebugInformation information)
    {
        // The flag is taken first and the subscription second, so that the pause a client asked for is not
        // lost between the two.
        var requested = Interlocked.Exchange(ref _pauseRequested, 0) != 0;
        UnsubscribeSkip();

        return requested ? Stop(information) : StepMode.None;
    }

    /// <summary>Decides whether this execution point pauses, and what the client is told it stopped on.</summary>
    private StepMode Stop(DebugInformation information)
    {
        if (SkipAllPauses || !IsEnabled)
        {
            return StepMode.None;
        }

        string[]? hitBreakpoints = null;

        if (information.BreakPoint is DevToolsBreakPoint hit)
        {
            if (hit.IsOneShot)
            {
                // continueToLocation's breakpoint: the client ran to a line, it did not set a breakpoint
                // there. It is taken away before the pause so that a client stepping on from here does not
                // meet it again.
                Forget(hit.BreakpointId);
            }
            else
            {
                hitBreakpoints = [hit.BreakpointId];
            }
        }

        return RunPauseLoop(information, hitBreakpoints);
    }

    /// <summary>
    /// Holds the engine thread inside the debugger's handler, answering the client, until it says what to do
    /// next.
    /// </summary>
    private StepMode RunPauseLoop(DebugInformation information, string[]? hitBreakpoints)
    {
        var dispatcher = _target.Dispatcher;
        var serial = Interlocked.Increment(ref _pauseSerial);

        // A pause a client asked for is satisfied by any pause, not only by the one the Skip subscription
        // would have produced: a breakpoint reached first is still the engine stopping where the client
        // wanted it stopped, and leaving the request armed would stop it a second time after the resume.
        DisarmPause();

        _stack = information.CallStack;
        Interlocked.Exchange(ref _resumeMode, NotResuming);
        Volatile.Write(ref _paused, 1);

        try
        {
            EmitDetached(DebuggerEvents.Paused(Paused(serial, hitBreakpoints)));

            var deadline = Environment.TickCount64 + (long) Bound(_target.PauseTimeout).TotalMilliseconds;
            var token = _target.StoppingToken;

            while (true)
            {
                // Reset first, then drain: an arrival during the drain sets the flag again, so the wait
                // below returns at once rather than parking on work that is already queued.
                dispatcher.ResetWake();
                dispatcher.DrainPaused();

                var decided = Interlocked.Exchange(ref _resumeMode, NotResuming);
                if (decided != NotResuming)
                {
                    return (StepMode) decided;
                }

                if (token.IsCancellationRequested)
                {
                    return StepMode.None;
                }

                var remaining = deadline - Environment.TickCount64;
                if (remaining <= 0)
                {
                    ReportTimedOut();
                    return StepMode.None;
                }

                dispatcher.WaitForWake(TimeSpan.FromMilliseconds(Math.Min(remaining, PollInterval.TotalMilliseconds)), token);
            }
        }
        finally
        {
            Volatile.Write(ref _paused, 0);
            _stack = null;

            // The frames are gone, so the handles onto them are too. Releasing before the event means a
            // client acting on `resumed` never races a handle it is about to lose.
            _objects.ReleaseGroup(BacktraceGroup);
            EmitDetached(DebuggerEvents.Resumed());
        }
    }

    /// <summary>Tells whoever is listening that the engine gave up waiting for a client.</summary>
    private void ReportTimedOut()
    {
        _log.Report(
            string.Create(
                CultureInfo.InvariantCulture,
                $"The debugger resumed after {_target.PauseTimeout.TotalSeconds:0.###} seconds without a resume or step command. Raise DevToolsServerOptions.PauseTimeout for longer pauses."),
            url: null,
            line: 0);
    }

    private PausedEvent Paused(int serial, string[]? hitBreakpoints)
    {
        var stack = _stack!;
        var frames = new ProtocolCallFrame[stack.Count];

        for (var i = 0; i < stack.Count; i++)
        {
            frames[i] = Frame(serial, i, stack[i]);
        }

        return new PausedEvent
        {
            CallFrames = frames,

            // The protocol's reason enum has no member for a `debugger` statement, and V8 answers `other` for
            // one too; a value outside the enum would be one no client could map. A breakpoint is also
            // `other`, told apart by hitBreakpoints.
            Reason = PausedEventReasonValues.Other,
            HitBreakpoints = hitBreakpoints,
        };
    }

    private ProtocolCallFrame Frame(int serial, int index, EngineCallFrame frame)
    {
        var location = frame.Location;
        var script = _target.Scripts!.At(location.SourceFile, location.Start.Line, location.Start.Column);

        return new ProtocolCallFrame
        {
            CallFrameId = string.Create(CultureInfo.InvariantCulture, $"{serial}.{index}"),
            FunctionName = FunctionName(frame.FunctionName),
            FunctionLocation = FunctionLocation(frame),
            Location = At(script, location.Start.Line, location.Start.Column),
            Url = script?.Url ?? location.SourceFile ?? "",
            ScopeChain = Scopes(frame),
            This = _objects.Describe(This(frame), _backtrace),
            ReturnValue = frame.ReturnValue is { } value ? _objects.Describe(value, _backtrace) : null,
        };
    }

    /// <summary>
    /// The scopes of one frame, each as a handle a client expands with <c>Runtime.getProperties</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>A declarative scope is answered as a snapshot, and that is a real limitation.</b> The engine's
    /// environment records are not objects, so the bindings are copied into one — built straight into the
    /// shaped representation, running no script and invoking no getter, which is the promise every other
    /// describing path here makes. A global or a <c>with</c> scope is answered as the object it already is,
    /// so those are live.
    /// </para>
    /// <para>
    /// Two consequences worth knowing. A binding still in its temporal dead zone is absent rather than shown
    /// as <c>undefined</c>, because the engine reports no value for one and inventing <c>undefined</c> would
    /// be a lie about a <c>let</c> that is about to be assigned. And a value changed by an
    /// <c>evaluateOnCallFrame</c> during the same pause is not reflected in a snapshot already handed out;
    /// <c>Debugger.setVariableValue</c> writes through to the environment either way.
    /// </para>
    /// </remarks>
    private Scope[] Scopes(EngineCallFrame frame)
    {
        var chain = frame.ScopeChain;
        var scopes = new Scope[chain.Count];

        for (var i = 0; i < chain.Count; i++)
        {
            var scope = chain[i];
            scopes[i] = new Scope
            {
                Type = ScopeType(scope.ScopeType),
                Object = _objects.Describe(scope.BindingObject ?? Snapshot(scope), _backtrace),
            };
        }

        return scopes;
    }

    private JsObject Snapshot(DebugScope scope)
    {
        var names = scope.BindingNames;
        var entries = new List<KeyValuePair<string, JsValue>>(names.Count);

        for (var i = 0; i < names.Count; i++)
        {
            var name = names[i];
            if (scope.GetBindingValue(name) is { } value)
            {
                entries.Add(new KeyValuePair<string, JsValue>(name, value));
            }
        }

        return JsObject.CreateFromEntries(_target.Engine, entries);
    }

    /// <summary>The value of <c>this</c>, or nothing when the frame has none to give yet.</summary>
    /// <remarks>
    /// A derived constructor before its <c>super()</c> call has an uninitialized <c>this</c> and asking for
    /// it throws. The protocol wants a value on every frame, so that frame reports <c>undefined</c> rather
    /// than the whole pause failing.
    /// </remarks>
    private static JsValue This(EngineCallFrame frame)
    {
        try
        {
            return frame.This;
        }
        catch (JavaScriptException)
        {
            return JsValue.Undefined;
        }
    }

    private ProtocolLocation? FunctionLocation(EngineCallFrame frame)
    {
        if (frame.FunctionLocation is not { } location)
        {
            return null;
        }

        var script = _target.Scripts!.At(location.SourceFile, location.Start.Line, location.Start.Column);
        return At(script, location.Start.Line, location.Start.Column);
    }

    /// <summary>
    /// One position, in the protocol's counting and against the script it belongs to.
    /// </summary>
    /// <remarks>
    /// A frame whose script the registry does not hold — one evicted past
    /// <see cref="ScriptRegistry.MaxScripts"/>, or code the engine reached through <c>eval</c> — is reported
    /// against the identifier <c>0</c>, which is what Chrome uses for a location it cannot attribute.
    /// </remarks>
    private static ProtocolLocation At(RegisteredScript? script, int line, int column) => new()
    {
        ScriptId = script?.ScriptId ?? "0",
        LineNumber = Math.Max(0, line - 1),
        ColumnNumber = column,
    };

    /// <summary>
    /// The frame index a call-frame identifier names, refusing one that belongs to an earlier pause.
    /// </summary>
    /// <remarks>
    /// The identifier carries the pause it was minted in, so a client acting on a stale <c>paused</c> event
    /// is told rather than answered about the wrong frame of the current one.
    /// </remarks>
    private int RequireFrame(string callFrameId)
    {
        RequirePaused();

        var separator = callFrameId.IndexOf('.', StringComparison.Ordinal);
        if (separator > 0 &&
            int.TryParse(callFrameId.AsSpan(0, separator), NumberStyles.None, CultureInfo.InvariantCulture, out var serial) &&
            int.TryParse(callFrameId.AsSpan(separator + 1), NumberStyles.None, CultureInfo.InvariantCulture, out var index) &&
            serial == Volatile.Read(ref _pauseSerial) &&
            (uint) index < (uint) _stack!.Count)
        {
            return index;
        }

        // Chrome's wording, and what a client matches on to know its frames went stale.
        return Throw.ServerError<int>("Invalid call frame id");
    }

    private void RequirePaused()
    {
        if (!IsPaused)
        {
            // V8's wording, verbatim: a client feature-detects a lost pause by it.
            Throw.ServerError("Can only perform operation while paused.");
        }
    }

    /// <summary>
    /// The name the protocol uses for a frame that has none.
    /// </summary>
    /// <remarks>
    /// The engine spells an anonymous function and the global frame alike as <c>(anonymous)</c>; Chrome
    /// spells both as the empty string and lets the front end draw its own placeholder.
    /// </remarks>
    private static string FunctionName(string name)
        => string.Equals(name, "(anonymous)", StringComparison.Ordinal) ? "" : name;

    /// <summary>The protocol's name for one of the engine's scope types, which were written to mirror it.</summary>
    private static string ScopeType(DebugScopeType type) => type switch
    {
        DebugScopeType.Global => ScopeTypeValues.Global,
        DebugScopeType.Script => ScopeTypeValues.Script,
        DebugScopeType.Local => ScopeTypeValues.Local,
        DebugScopeType.Block => ScopeTypeValues.Block,
        DebugScopeType.Catch => ScopeTypeValues.Catch,
        DebugScopeType.Closure => ScopeTypeValues.Closure,
        DebugScopeType.With => ScopeTypeValues.With,
        DebugScopeType.Eval => ScopeTypeValues.Eval,
        DebugScopeType.Module => ScopeTypeValues.Module,
        _ => ScopeTypeValues.WasmExpressionStack,
    };

    /// <summary>Clamps a host's bound to what the blocking primitives accept, with no way to remove it.</summary>
    /// <remarks>
    /// There is deliberately no infinite setting. A pause holds the thread that runs the engine — the host's
    /// own, for a host-owned target — and a bound that could be switched off is a host that a client can wedge
    /// for ever by walking away from a breakpoint.
    /// </remarks>
    private static TimeSpan Bound(TimeSpan timeout)
    {
        if (timeout <= TimeSpan.Zero)
        {
            return TimeSpan.Zero;
        }

        return timeout > MaxPause ? MaxPause : timeout;
    }

    private int NextExceptionId() => ++_exceptionId;
}
