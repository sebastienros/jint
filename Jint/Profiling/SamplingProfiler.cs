using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Jint.Native.Function;
using Jint.Runtime.CallStack;

namespace Jint.Profiling;

/// <summary>
/// The recording half of a sampling session: it reads the engine's own call stack, on the engine's own
/// thread, whenever the configured interval has elapsed, and interns what it finds into the columnar tables
/// a Firefox Profiler document is made of.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why on-thread.</b> A <see cref="Jint.Native.JsValue"/> and the engine that owns it are thread-affine,
/// so the stack may only be read by the thread running it — which rules out the usual sampling thread and
/// rules in the check the interpreter already makes. <see cref="Check"/> hangs off
/// <c>Engine.CheckAmortizedConstraints</c>, the once-per-64-statements hook the timeout and cancellation
/// constraints ride, and does one <see cref="Stopwatch.GetTimestamp"/> read before deciding whether this is
/// a sample point.
/// </para>
/// <para>
/// <b>What that costs and what it bounds.</b> The cadence is the sampler's resolution: nothing is sampled
/// between two check points, so one long-running built-in or one long host call is a gap, and the sample
/// taken at the call site before it carries the gap's weight (see <see cref="SampledProfile"/>). Nothing is
/// synchronized and nothing here may be touched while script is running on another thread — which the
/// engine does not support anyway.
/// </para>
/// </remarks>
internal sealed class SamplingProfiler
{
    /// <summary>
    /// The synthetic root every sampled stack hangs off: the program, which is not on the call stack
    /// because only function activations are. Its own executing position is the call site of the outermost
    /// function frame, or the node the engine last prepared for when nothing is on the stack — so a sample
    /// taken in top-level code still says which line it was on.
    /// </summary>
    private const int ProgramFuncIndex = 0;

    private const int NoStack = -1;
    private const int NoPosition = -1;

    private readonly long _intervalTicks;
    private readonly int _maxSamples;
    private readonly long _startTimestamp;
    private readonly long _startUnixMilliseconds;
    private readonly TimeSpan _interval;

    private long _nextSampleTimestamp;
    private int _droppedSamples;

    private readonly List<ScriptProfileFrame> _funcs = new();
    private readonly List<ProfileFrameCategory> _funcCategories = new();
    private readonly Dictionary<object, int> _funcIndexes = new(ProfileFrames.FunctionIdentity);

    private readonly List<SampledFrame> _frames = new();
    private readonly Dictionary<SampledFrame, int> _frameIndexes = new();

    private readonly List<SampledStackNode> _stacks = new();
    private readonly Dictionary<long, int> _stackIndexes = new();

    private readonly List<int> _sampleStacks = new();
    private readonly List<long> _sampleTimestamps = new();

    internal SamplingProfiler(TimeSpan interval, int maxSamples)
    {
        _interval = interval;
        _maxSamples = maxSamples;
        _intervalTicks = (long) (interval.TotalSeconds * Stopwatch.Frequency);
        _startTimestamp = Stopwatch.GetTimestamp();
        _startUnixMilliseconds = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        // Deliberately not start + interval: the first check point at or after the session opens is a
        // sample, so a session bracketing a script shorter than one interval still records where it was.
        _nextSampleTimestamp = _startTimestamp;

        _funcs.Add(new ScriptProfileFrame(ProfileFrames.ProgramFrameName, File: null, Line: null, Column: null));
        _funcCategories.Add(ProfileFrameCategory.Script);
        _funcIndexes.Add(ProgramFrameKey, ProgramFuncIndex);
    }

    /// <summary>
    /// The interning key of the synthetic program frame. An object of its own, so it can share the map with
    /// the real functions without any of them ever colliding with it.
    /// </summary>
    private static readonly object ProgramFrameKey = new();

    /// <summary>
    /// Takes a sample if the interval has elapsed. The whole cost of an armed session at a check point that
    /// is not a sample point: one timestamp read and one comparison.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void Check(Engine engine)
    {
        var now = Stopwatch.GetTimestamp();
        if (now >= _nextSampleTimestamp)
        {
            Capture(engine, now);
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private void Capture(Engine engine, long now)
    {
        // Re-armed from now rather than from the deadline: a sampler that caught up on a deadline it
        // missed would invent samples for a stack it never observed. The time actually spent is not lost —
        // it becomes the weight of the sample before the gap.
        _nextSampleTimestamp = now + _intervalTicks;

        if (_sampleStacks.Count >= _maxSamples)
        {
            _droppedSamples++;
            return;
        }

        var callStack = engine.CallStack.Stack;
        var frames = callStack._array;
        var depth = callStack._size;

        // The node an error would be reported against, which is the finest-grained position the engine
        // maintains: the line executing inside the innermost script frame.
        var innermost = engine._lastSyntaxElement?.Location ?? default;

        // The program's own executing position is the call site of the outermost function on the stack,
        // and the innermost position when there is none because the program itself is what is running.
        var stack = InternStack(NoStack, InternFrame(ProgramFuncIndex, depth > 0 ? CallSiteOf(frames[0]) : innermost));

        for (var i = 0; i < depth; i++)
        {
            var funcIndex = InternFunc(frames[i].Function);

            // The position to report for a frame is a position inside its own code, so only a script
            // function has one: the call site of the frame above it, or the engine's last node for the
            // innermost frame.
            var position = _funcCategories[funcIndex] == ProfileFrameCategory.Script
                ? (i + 1 < depth ? CallSiteOf(frames[i + 1]) : innermost)
                : default;

            stack = InternStack(stack, InternFrame(funcIndex, position));
        }

        _sampleStacks.Add(stack);
        _sampleTimestamps.Add(now);
    }

    /// <summary>
    /// The location of the call expression that pushed <paramref name="element"/>, which lies in the
    /// <em>caller's</em> source. Deliberately not <see cref="CallStackElement.Location"/>, which falls back
    /// to the callee's own declaration when there is no call expression — a position in the wrong function,
    /// and the profile would report it as a line of the caller.
    /// </summary>
    private static SourceLocation CallSiteOf(CallStackElement element)
    {
        var expression = element.Expression;
        return expression is null ? default : expression._expression.Location;
    }

    private int InternFunc(Function function)
    {
        // Interned by definition where there is one, so all closures of a source function share an entry;
        // by object otherwise, there being nothing else for a built-in or host callable to share.
        var definition = function._functionDefinition;
        var key = (object?) definition ?? function;

        if (_funcIndexes.TryGetValue(key, out var index))
        {
            return index;
        }

        index = _funcs.Count;
        _funcs.Add(ProfileFrames.Describe(function, definition));
        _funcCategories.Add(ProfileFrames.Classify(function));
        _funcIndexes.Add(key, index);
        return index;
    }

    private int InternFrame(int func, SourceLocation position)
    {
        var line = NoPosition;
        var column = NoPosition;
        if (position != default)
        {
            line = position.Start.Line;

            // One-based, matching the column Jint puts in a stack trace; the parser's is an index.
            column = position.Start.Column + 1;
        }

        var frame = new SampledFrame(func, line, column);
        if (_frameIndexes.TryGetValue(frame, out var index))
        {
            return index;
        }

        index = _frames.Count;
        _frames.Add(frame);
        _frameIndexes.Add(frame, index);
        return index;
    }

    /// <summary>
    /// Interns one node of the stack tree, which is what makes steady-state sampling cheap: two stacks
    /// sharing a prefix share every node of it, so a sample costs one dictionary probe per frame and
    /// usually adds nothing.
    /// </summary>
    private int InternStack(int prefix, int frame)
    {
        // Both halves are non-negative array indices (prefix is offset by one so a root's -1 fits), so one
        // long holds the pair exactly.
        var key = ((long) (prefix + 1) << 32) | (uint) frame;
        if (_stackIndexes.TryGetValue(key, out var index))
        {
            return index;
        }

        index = _stacks.Count;
        _stacks.Add(new SampledStackNode(prefix, frame));
        _stackIndexes.Add(key, index);
        return index;
    }

    /// <summary>
    /// Ends the session and hands over what it recorded, as arrays of strings and numbers that retain no
    /// function, no AST and no engine.
    /// </summary>
    internal SampledProfile Complete()
    {
        var endTimestamp = Stopwatch.GetTimestamp();

        var times = new double[_sampleTimestamps.Count];
        for (var i = 0; i < times.Length; i++)
        {
            times[i] = ToMilliseconds(_sampleTimestamps[i] - _startTimestamp);
        }

        var profile = new SampledProfile(
            _funcs.ToArray(),
            _funcCategories.ToArray(),
            _frames.ToArray(),
            _stacks.ToArray(),
            _sampleStacks.ToArray(),
            times,
            _droppedSamples,
            _interval,
            ToMilliseconds(endTimestamp - _startTimestamp),
            _startUnixMilliseconds);

        // The interning maps are dropped, so the session stops retaining the functions (and through them
        // the closures, ASTs and engine) it was built from.
        _funcs.Clear();
        _funcCategories.Clear();
        _funcIndexes.Clear();
        _frames.Clear();
        _frameIndexes.Clear();
        _stacks.Clear();
        _stackIndexes.Clear();
        _sampleStacks.Clear();
        _sampleTimestamps.Clear();

        return profile;
    }

    private static double ToMilliseconds(long ticks) => ticks * 1000.0 / Stopwatch.Frequency;
}

/// <summary>
/// One row of the profile's frame table: a function, and the position inside it that was executing.
/// <see cref="Line"/> and <see cref="Column"/> are <c>-1</c> when there is none.
/// </summary>
[StructLayout(LayoutKind.Auto)]
internal readonly record struct SampledFrame(int Func, int Line, int Column);

/// <summary>
/// One node of the profile's stack tree: a frame, and the node it hangs off (<c>-1</c> for a root).
/// </summary>
[StructLayout(LayoutKind.Auto)]
internal readonly record struct SampledStackNode(int Prefix, int Frame);
