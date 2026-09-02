using System.Collections.ObjectModel;
using System.IO;
using System.Text;
using Jint.Runtime;

namespace Jint.Profiling;

/// <summary>
/// The result of one profiling session: every function enter and leave the engine recorded between
/// <see cref="Engine.DiagnosticOperations.StartProfiling"/> and
/// <see cref="Engine.DiagnosticOperations.StopProfiling"/>, and the functions they refer to.
/// </summary>
/// <remarks>
/// <para>
/// Immutable and engine-independent: it retains no <c>Function</c>, no <c>JsValue</c> and no
/// <see cref="Engine"/>, so keeping a profile around does not keep the engine that produced it alive. A
/// frame does name the <see cref="ScriptProfileFrame.Program"/> it was parsed from, which is an identity to
/// compare and not a tree to walk from another thread.
/// </para>
/// <para>
/// <see cref="WriteSpeedscopeJson(TextWriter)"/> renders it in the
/// <see href="https://www.speedscope.app/file-format-schema.json">speedscope file format</see>, whose
/// evented profile is exactly this event stream.
/// </para>
/// </remarks>
public sealed class ScriptProfile
{
    private readonly ScriptProfileFrame[] _frames;
    private readonly ScriptProfileEvent[] _events;

    internal ScriptProfile(
        ScriptProfileFrame[] frames,
        ScriptProfileEvent[] events,
        bool truncated,
        long durationNanoseconds)
    {
        _frames = frames;
        _events = events;

        // Wrapped rather than handed over as the arrays themselves, so the immutability this type claims
        // survives a caller that casts an IReadOnlyList back to what it really is.
        Frames = new ReadOnlyCollection<ScriptProfileFrame>(frames);
        Events = new ReadOnlyCollection<ScriptProfileEvent>(events);

        Truncated = truncated;
        DurationNanoseconds = durationNanoseconds;
    }

    /// <summary>
    /// The distinct functions the session saw, in the order they were first entered.
    /// <see cref="ScriptProfileEvent.FrameIndex"/> indexes into this list.
    /// </summary>
    public IReadOnlyList<ScriptProfileFrame> Frames { get; }

    /// <summary>
    /// The enter and leave events, in the order they happened.
    /// </summary>
    public IReadOnlyList<ScriptProfileEvent> Events { get; }

    /// <summary>
    /// Whether recording stopped early because <see cref="Options.ProfilingOptions.MaxEvents"/> was reached.
    /// When true, <see cref="Events"/> describes the beginning of the run and says nothing about the rest of
    /// it; the stream is still balanced, every frame open at the cut-off having been closed there.
    /// </summary>
    public bool Truncated { get; }

    /// <summary>
    /// Wall-clock time between <see cref="Engine.DiagnosticOperations.StartProfiling"/> and
    /// <see cref="Engine.DiagnosticOperations.StopProfiling"/>, including whatever the host did in between
    /// that was not script. Equal to the speedscope profile's <c>endValue</c>.
    /// </summary>
    public long DurationNanoseconds { get; }

    /// <summary>
    /// <see cref="DurationNanoseconds"/> as a <see cref="TimeSpan"/>, rounded down to its 100ns resolution.
    /// </summary>
    public TimeSpan Duration => TimeSpan.FromTicks(DurationNanoseconds / NanosecondsPerTimeSpanTick);

    private const long NanosecondsPerTimeSpanTick = 100;

    /// <summary>
    /// Writes this profile as a speedscope evented profile, in nanoseconds. The output is a complete
    /// <c>.speedscope.json</c> document that <see href="https://www.speedscope.app">speedscope</see> opens
    /// as-is.
    /// </summary>
    /// <param name="writer">Where to write. Not flushed and not disposed.</param>
    /// <exception cref="ArgumentNullException"><paramref name="writer"/> is <see langword="null"/>.</exception>
    public void WriteSpeedscopeJson(TextWriter writer)
    {
        if (writer is null)
        {
            Throw.ArgumentNullException(nameof(writer));
        }

        SpeedscopeWriter.Write(writer, _frames, _events, DurationNanoseconds);
    }

    /// <summary>
    /// Writes this profile as a speedscope evented profile, UTF-8 encoded without a byte-order mark.
    /// </summary>
    /// <param name="stream">Where to write. Flushed, but left open.</param>
    /// <exception cref="ArgumentNullException"><paramref name="stream"/> is <see langword="null"/>.</exception>
    public void WriteSpeedscopeJson(Stream stream)
    {
        if (stream is null)
        {
            Throw.ArgumentNullException(nameof(stream));
        }

        // A UTF8Encoding of our own rather than Encoding.UTF8, whose GetPreamble() would put a BOM in front
        // of the document; JSON parsers are not obliged to skip one.
        using var writer = new StreamWriter(stream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false), 1024, leaveOpen: true);
        WriteSpeedscopeJson(writer);
    }
}
