using Jint.DevTools.Protocol.Runtime;
using Jint.DevTools.Session;
using Jint.Native;
using Jint.Runtime;
using Jint.WebApi;

namespace Jint.DevTools.Domains;

/// <summary>
/// What the <c>Runtime</c> domain says without being asked: what a script logged, and what it threw with
/// nothing to catch it.
/// </summary>
/// <remarks>
/// <para>
/// Every one of these arrives on the engine thread from <i>inside</i> the engine — a <c>console</c> call, a
/// promise settling, an exception escaping the pump — where there is no command to answer and nothing to
/// <c>await</c> onto. They go out through <c>EmitDetached</c>, which queues rather than writes and never
/// lets a transport failure erupt into the host's own loop.
/// </para>
/// <para>
/// A domain that is not enabled says nothing at all, which is the protocol's rule and also what keeps the
/// engine-side cost of an unattached target at zero.
/// </para>
/// </remarks>
internal sealed partial class RuntimeDomain
{
    /// <summary>
    /// The object group every <c>console</c> argument is billed to, which is the name Chrome uses and the
    /// one a client sends to <c>releaseObjectGroup</c> when it stops caring about its console history.
    /// </summary>
    private const string ConsoleObjectGroup = "console";

    /// <summary>Chrome's own sentinel for a location no script can be found for.</summary>
    private const string UnattributedScriptId = "0";

    /// <summary>
    /// How many unhandled rejections are remembered so that a later handler can revoke the right one. A
    /// rejection older than this is never revoked, which shows in a client as an error that stays on screen.
    /// </summary>
    private const int MaxTrackedRejections = 64;

    /// <summary>What Chrome says when a rejection turns out to have a handler after all.</summary>
    private const string RevokedReason = "Handler added to rejected promise";

    private readonly List<(JsValue Promise, int ExceptionId)> _rejections = [];

    /// <inheritdoc/>
    void ITargetObserver.ConsoleRecorded(ConsoleEntry entry)
    {
        if (!IsEnabled)
        {
            return;
        }

        EmitDetached(RuntimeEvents.ConsoleAPICalled(ConsoleCall(entry)));
    }

    /// <inheritdoc/>
    void ITargetObserver.ExceptionThrown(JavaScriptException exception)
    {
        if (!IsEnabled)
        {
            return;
        }

        EmitDetached(RuntimeEvents.ExceptionThrown(new ExceptionThrownEvent
        {
            Timestamp = EngineTarget.UnixMilliseconds(),
            ExceptionDetails = _objects.Exception(exception, NextExceptionId(), MainExecutionContextId),
        }));
    }

    /// <inheritdoc/>
    void ITargetObserver.RejectionThrown(JsValue promise, JsValue reason)
    {
        if (!IsEnabled)
        {
            return;
        }

        var exceptionId = NextExceptionId();
        Track(promise, exceptionId);

        EmitDetached(RuntimeEvents.ExceptionThrown(new ExceptionThrownEvent
        {
            Timestamp = EngineTarget.UnixMilliseconds(),
            ExceptionDetails = _objects.Rejection(reason, exceptionId, MainExecutionContextId),
        }));
    }

    /// <inheritdoc/>
    void ITargetObserver.RejectionHandled(JsValue promise)
    {
        if (!IsEnabled || !Untrack(promise, out var exceptionId))
        {
            return;
        }

        EmitDetached(RuntimeEvents.ExceptionRevoked(new ExceptionRevokedEvent
        {
            Reason = RevokedReason,
            ExceptionId = exceptionId,
        }));
    }

    /// <summary>Replays the journal to a client that enabled the domain after the fact.</summary>
    /// <remarks>
    /// V8 does the same, and it is what makes a front end opened halfway through a run useful rather than
    /// empty. The context announcement goes first, because every one of these names it.
    /// </remarks>
    private async ValueTask ReplayConsoleAsync(CommandContext context)
    {
        foreach (var entry in _target.Console.Snapshot())
        {
            await EmitAsync(RuntimeEvents.ConsoleAPICalled(ConsoleCall(entry)), context.CancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Turns one journalled call into the event, minting a handle per argument in the console group.
    /// </summary>
    /// <remarks>
    /// The identifiers are recorded on the entry, so that the entry falling out of the journal releases
    /// them. A client may release the whole lot early with
    /// <c>Runtime.releaseObjectGroup("console")</c>, which is what its own "clear console" does.
    /// </remarks>
    private ConsoleAPICalledEvent ConsoleCall(ConsoleEntry entry)
    {
        var request = new RemoteObjectRequest
        {
            Addressable = true,
            GeneratePreview = true,
            ObjectGroup = ConsoleObjectGroup,
        };

        var arguments = entry.Arguments;
        var mapped = new RemoteObject[arguments.Length];

        for (var i = 0; i < arguments.Length; i++)
        {
            var described = _objects.Describe(arguments[i], request);
            if (described.ObjectId is { } objectId)
            {
                entry.Minted(objectId);
            }

            mapped[i] = described;
        }

        return new ConsoleAPICalledEvent
        {
            Type = ConsoleType(entry.Method),
            Args = mapped,
            ExecutionContextId = MainExecutionContextId,
            Timestamp = entry.Timestamp,
            StackTrace = StackTraceOf(entry.StackTrace),
        };
    }

    /// <summary>
    /// Which of the protocol's console types a <see cref="ConsoleMethod"/> is.
    /// </summary>
    /// <remarks>
    /// Four of the engine's methods have no type of their own in the protocol and are folded into the
    /// nearest one that exists: <c>countReset</c> into <c>count</c>, and the three timer methods into
    /// <c>timeEnd</c>, which is the only timer type the protocol has. <c>timeStamp</c> is a marker Chrome
    /// routes to its timeline rather than to its console, so it arrives as an ordinary log line. A method
    /// the engine adds later reads as <c>log</c> rather than as an error, which is what the enum's own
    /// documentation asks of a reader.
    /// </remarks>
    private static string ConsoleType(ConsoleMethod method) => method switch
    {
        ConsoleMethod.Debug => ConsoleAPICalledEventTypeValues.Debug,
        ConsoleMethod.Info => ConsoleAPICalledEventTypeValues.Info,
        ConsoleMethod.Warn => ConsoleAPICalledEventTypeValues.Warning,
        ConsoleMethod.Error => ConsoleAPICalledEventTypeValues.Error,
        ConsoleMethod.Trace => ConsoleAPICalledEventTypeValues.Trace,
        ConsoleMethod.Assert => ConsoleAPICalledEventTypeValues.Assert,
        ConsoleMethod.Dir => ConsoleAPICalledEventTypeValues.Dir,
        ConsoleMethod.Table => ConsoleAPICalledEventTypeValues.Table,
        ConsoleMethod.Group => ConsoleAPICalledEventTypeValues.StartGroup,
        ConsoleMethod.GroupCollapsed => ConsoleAPICalledEventTypeValues.StartGroupCollapsed,
        ConsoleMethod.GroupEnd => ConsoleAPICalledEventTypeValues.EndGroup,
        ConsoleMethod.Count or ConsoleMethod.CountReset => ConsoleAPICalledEventTypeValues.Count,
        ConsoleMethod.Time or ConsoleMethod.TimeLog or ConsoleMethod.TimeEnd => ConsoleAPICalledEventTypeValues.TimeEnd,
        _ => ConsoleAPICalledEventTypeValues.Log,
    };

    /// <summary>
    /// The frames a console call was made from, in the protocol's own counting.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This is the source anchor a front end prints on the right of every console line, and the reason
    /// <see cref="DevToolsConsoleSink.WantsStackTrace"/> asks the engine for frames on every method rather
    /// than on <c>console.trace</c> alone.
    /// </para>
    /// <para>
    /// The engine counts lines and columns from one and the protocol counts both from zero. Each frame is
    /// matched back to a registered script so the anchor is clickable; a location no script claims is
    /// reported against the identifier <c>0</c>, which is Chrome's own sentinel, and the front end falls
    /// back to the URL.
    /// </para>
    /// </remarks>
    private StackTrace? StackTraceOf(ConsoleStackFrame[]? frames)
    {
        if (frames is null || frames.Length == 0)
        {
            return null;
        }

        var registry = _target.Scripts;
        var callFrames = new CallFrame[frames.Length];
        for (var i = 0; i < frames.Length; i++)
        {
            var frame = frames[i];
            var script = registry?.At(frame.Source, frame.Line, Math.Max(0, frame.Column - 1));

            callFrames[i] = new CallFrame
            {
                FunctionName = frame.FunctionName,
                ScriptId = script?.ScriptId ?? UnattributedScriptId,
                Url = script?.Url ?? ScriptUrl.From(frame.Source),
                LineNumber = Math.Max(0, frame.Line - 1),
                ColumnNumber = Math.Max(0, frame.Column - 1),
            };
        }

        return new StackTrace { CallFrames = callFrames };
    }

    private void Track(JsValue promise, int exceptionId)
    {
        if (_rejections.Count >= MaxTrackedRejections)
        {
            _rejections.RemoveAt(0);
        }

        _rejections.Add((promise, exceptionId));
    }

    private bool Untrack(JsValue promise, out int exceptionId)
    {
        for (var i = 0; i < _rejections.Count; i++)
        {
            if (ReferenceEquals(_rejections[i].Promise, promise))
            {
                exceptionId = _rejections[i].ExceptionId;
                _rejections.RemoveAt(i);
                return true;
            }
        }

        exceptionId = 0;
        return false;
    }
}
