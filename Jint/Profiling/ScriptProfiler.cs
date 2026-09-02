using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Jint.Native.Function;
using Jint.Runtime.Interpreter;

namespace Jint.Profiling;

/// <summary>
/// The recording half of a profiling session. Owned by <see cref="Runtime.CallStack.JintCallStack"/>, which
/// is the engine's single choke point for a function activation becoming visible: everything that gives a
/// call a frame goes through its push, and everything that ends one goes through its pop, replace or clear.
/// Hooking there is what makes the enter/exit stream balanced by construction — including for a throw, whose
/// unwinding pops through the same <c>finally</c>s the normal return does.
/// </summary>
/// <remarks>
/// <para>
/// Engine-thread only, like the call stack it hangs off. Nothing here is synchronized and nothing may be
/// touched while script is running on another thread — which the engine does not support anyway.
/// </para>
/// <para>
/// <b>What is not recorded.</b> A built-in dispatched through the interpreter's frameless leaf lane
/// (<c>FastCallShape.IsLeafFor</c>) never reaches the call stack and so never appears. That lane is entered
/// only when the built-in provably cannot reach user code with these arguments, so no <em>script</em>
/// function is ever missing from a profile and no recorded frame ever spans a gap: what is elided is the
/// self time of some trivial built-in calls, which is attributed to their caller instead.
/// </para>
/// </remarks>
internal sealed class ScriptProfiler
{
    /// <summary>
    /// Ticks-to-nanoseconds factor. Multiplying by a positive constant is monotone, so nanosecond
    /// timestamps are non-decreasing exactly as the underlying <see cref="Stopwatch"/> ticks are — which
    /// the speedscope format requires of an evented profile.
    /// </summary>
    private static readonly double NanosecondsPerTick = 1_000_000_000.0 / Stopwatch.Frequency;

    private readonly int _maxEvents;
    private readonly long _startTimestamp;

    private readonly List<RecordedEvent> _events = new();
    private readonly List<ScriptProfileFrame> _frames = new();
    private readonly Dictionary<object, int> _frameIndexes = new(ProfileFrames.FunctionIdentity);

    /// <summary>
    /// The frames currently open, innermost last — the profiler's own mirror of the call stack. It exists
    /// because a close has to name the frame it closes and re-deriving that from the popped element would
    /// be both slower and, after a truncation or a session started mid-call, wrong.
    /// </summary>
    private readonly List<int> _openFrames = new();

    private bool _recording = true;
    private bool _truncated;

    internal ScriptProfiler(int maxEvents)
    {
        _maxEvents = maxEvents;
        _startTimestamp = Stopwatch.GetTimestamp();
    }

    /// <summary>
    /// Records <paramref name="function"/> being entered.
    /// </summary>
    internal void RecordEnter(Function function)
    {
        if (!_recording)
        {
            return;
        }

        var timestamp = Stopwatch.GetTimestamp();
        if (!HasRoomForOneMoreCall())
        {
            Truncate(timestamp);
            return;
        }

        var frame = GetFrameIndex(function);
        _openFrames.Add(frame);
        _events.Add(new RecordedEvent(timestamp, frame));
    }

    /// <summary>
    /// Records the innermost open frame being left, by return or by exception.
    /// </summary>
    /// <remarks>
    /// A pop with nothing open is dropped rather than treated as an error: a host may start a session from
    /// inside a CLR callable that script invoked, in which case the frames already on the call stack were
    /// never opened here and their pops have nothing to match.
    /// </remarks>
    internal void RecordExit()
    {
        var open = _openFrames.Count;
        if (!_recording || open == 0)
        {
            return;
        }

        var frame = _openFrames[open - 1];
        _openFrames.RemoveAt(open - 1);
        _events.Add(new RecordedEvent(Stopwatch.GetTimestamp(), Close(frame)));
    }

    /// <summary>
    /// Records a proper tail call displacing the innermost open frame with <paramref name="function"/>.
    /// </summary>
    /// <remarks>
    /// A tail call is recorded as a close of the displaced function immediately followed by an open of its
    /// target, both at the same instant — which is what the frame replacement actually is, and the only
    /// shape a strictly nested event stream can express it in. So an unbounded tail recursion profiles as a
    /// flat run of sibling frames at constant depth, not as a call tree that grows without bound; the
    /// displaced function's activation is over as far as the profile is concerned, exactly as it is as far
    /// as the stack trace is concerned. (Recursion-depth accounting deliberately disagrees — the
    /// occurrence is retained there until the trampoline returns — but that is a budget, not a shape.)
    /// </remarks>
    internal void RecordTailReplace(Function function)
    {
        if (!_recording)
        {
            return;
        }

        var open = _openFrames.Count;
        if (open == 0)
        {
            // The displaced frame predates the session (see RecordExit); the target's is still worth having.
            RecordEnter(function);
            return;
        }

        var timestamp = Stopwatch.GetTimestamp();
        if (!HasRoomForOneMoreCall())
        {
            Truncate(timestamp);
            return;
        }

        _events.Add(new RecordedEvent(timestamp, Close(_openFrames[open - 1])));

        var frame = GetFrameIndex(function);
        _openFrames[open - 1] = frame;
        _events.Add(new RecordedEvent(timestamp, frame));
    }

    /// <summary>
    /// Records the whole call stack being abandoned — an unhandled exception unwinding an evaluation, or a
    /// host calling <see cref="Engine.AdvancedOperations.ResetCallStack"/>. Everything open is closed, and
    /// recording continues from depth zero, which is where the call stack now is too.
    /// </summary>
    internal void RecordAbandon()
    {
        if (!_recording || _openFrames.Count == 0)
        {
            return;
        }

        CloseOpenFrames(Stopwatch.GetTimestamp());
    }

    /// <summary>
    /// Ends the session and renders it. Anything still open — a host stopping the profiler from inside a
    /// callback script invoked — is closed at the stop instant, so the returned stream is balanced whatever
    /// the engine was in the middle of.
    /// </summary>
    internal ScriptProfile Complete()
    {
        var endTimestamp = Stopwatch.GetTimestamp();

        if (_recording)
        {
            CloseOpenFrames(endTimestamp);
            _recording = false;
        }

        var events = new ScriptProfileEvent[_events.Count];
        for (var i = 0; i < events.Length; i++)
        {
            var recorded = _events[i];
            var encoded = recorded.EncodedFrame;
            var open = encoded >= 0;
            events[i] = new ScriptProfileEvent(
                open ? ScriptProfileEventKind.Open : ScriptProfileEventKind.Close,
                open ? encoded : ~encoded,
                ToNanoseconds(recorded.Timestamp - _startTimestamp));
        }

        // The frame table is handed over as plain strings and the interning map dropped, so the profile
        // stops retaining the functions (and through them the closures, ASTs and engine) it was built from.
        var frames = _frames.ToArray();
        _frames.Clear();
        _frameIndexes.Clear();
        _events.Clear();

        return new ScriptProfile(frames, events, _truncated, ToNanoseconds(endTimestamp - _startTimestamp));
    }

    /// <summary>
    /// Whether one more call can be recorded without the eventual balancing closes exceeding the cap.
    /// </summary>
    /// <remarks>
    /// The budget for a call is two events — its own open plus the close it will need — on top of the
    /// closes every currently open frame will need. Checking that before each open maintains
    /// <c>_events.Count + _openFrames.Count &lt;= _maxEvents</c> as an invariant, which is what lets
    /// <see cref="Truncate"/> close everything open and still land inside the cap.
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool HasRoomForOneMoreCall() => _events.Count + _openFrames.Count + 2 <= _maxEvents;

    private void Truncate(long timestamp)
    {
        CloseOpenFrames(timestamp);
        _truncated = true;
        _recording = false;
    }

    private void CloseOpenFrames(long timestamp)
    {
        for (var i = _openFrames.Count - 1; i >= 0; i--)
        {
            _events.Add(new RecordedEvent(timestamp, Close(_openFrames[i])));
        }

        _openFrames.Clear();
    }

    private int GetFrameIndex(Function function)
    {
        // Interned by definition where there is one, so all closures of a source function share a frame;
        // by object otherwise, there being nothing else for a built-in or host callable to share.
        var definition = function._functionDefinition;
        var key = (object?) definition ?? function;

        if (_frameIndexes.TryGetValue(key, out var index))
        {
            return index;
        }

        index = _frames.Count;
        _frames.Add(ProfileFrames.Describe(function, definition));
        _frameIndexes.Add(key, index);
        return index;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int Close(int frame) => ~frame;

    private static long ToNanoseconds(long ticks) => (long) (ticks * NanosecondsPerTick);

    /// <summary>
    /// An event as recorded: the raw <see cref="Stopwatch"/> timestamp (converted only at readout, so the
    /// hot path is one <see cref="Stopwatch.GetTimestamp"/> call and no arithmetic) and the frame index,
    /// bit-complemented to mark a close. Kept to two fields so a million events cost 16 bytes each.
    /// </summary>
    [StructLayout(LayoutKind.Auto)]
    private readonly record struct RecordedEvent(long Timestamp, int EncodedFrame);
}
