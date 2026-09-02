using System.Runtime.InteropServices;
using Acornima.Ast;
using Jint.Profiling;

namespace Jint.DevTools.Domains;

/// <summary>
/// Where a <c>Profiler.Profile</c> comes from: something that records an engine and hands back a stack over
/// time.
/// </summary>
/// <remarks>
/// <para>
/// The seam exists because the engine has two profilers and the protocol has one profile, and both of them
/// fit through it: <see cref="SampledProfileSource"/> over
/// <c>Engine.Diagnostics.StartSampling</c>/<c>StopSampling</c>, which is what a client asking for a
/// <c>Profile</c> at a <c>samplingInterval</c> means, and <see cref="EventedProfileSource"/> over
/// <c>StartProfiling</c>/<c>StopProfiling</c>, which records every call instead. <c>ProfilerDomain</c>
/// chooses per recording. The seam speaks a function table and a balanced stream of enters and leaves
/// deliberately, rather than either profiler's own type: a seam that names one instrument is a seam only
/// that instrument fits through.
/// </para>
/// <para>
/// <b>Everything here runs on the engine thread</b>, like every other domain member, and the
/// <see cref="RecordedProfile"/> it hands back holds no <c>JsValue</c> and no engine — a function names the
/// program it was parsed from, which is the identity a script identifier is looked up by and nothing to
/// walk.
/// </para>
/// </remarks>
internal interface IProfileSource
{
    /// <summary>Gets whether a recording is running.</summary>
    bool IsRecording { get; }

    /// <summary>
    /// Starts recording, at the interval the client asked for if the source has one.
    /// </summary>
    /// <param name="interval">
    /// What <c>Profiler.setSamplingInterval</c> last asked for. An exact source ignores it: it records every
    /// call rather than sampling, so there is no rate to set.
    /// </param>
    /// <exception cref="InvalidOperationException">
    /// The engine cannot record — profiling was not enabled when it was built, or something is already
    /// recording on it.
    /// </exception>
    void Start(TimeSpan interval);

    /// <summary>Ends the recording and hands back what it saw.</summary>
    /// <exception cref="InvalidOperationException">Nothing is recording.</exception>
    RecordedProfile Stop();
}

/// <summary>
/// One function in a profile: what to call it, and where its source is.
/// </summary>
/// <param name="Name">The function's name, or the engine's placeholder for one that has none.</param>
/// <param name="File">The source name the function was parsed under, or <see langword="null"/> for a built-in.</param>
/// <param name="Line">One-based line of the function's declaration, or <see langword="null"/> with <paramref name="File"/>.</param>
/// <param name="Column">One-based column of the function's declaration, or <see langword="null"/> with <paramref name="File"/>.</param>
/// <param name="Program">
/// The program the function was parsed as part of, which is what gives it a script identifier, or
/// <see langword="null"/> for one the engine names no program for.
/// </param>
/// <remarks>
/// Deliberately not the engine's own <see cref="ScriptProfileFrame"/>, even though it is the same five
/// members today: a seam that names one profiler's type is a seam only that profiler fits through.
/// </remarks>
[StructLayout(LayoutKind.Auto)]
internal readonly record struct ProfileFunction(string Name, string? File, int? Line, int? Column, Program? Program);

/// <summary>
/// One function entering or leaving the stack, timestamped against the start of the recording.
/// </summary>
/// <param name="Entered">Whether the function was entered rather than left.</param>
/// <param name="FunctionIndex">Which of <see cref="RecordedProfile.Functions"/> moved.</param>
/// <param name="TimestampNanoseconds">Nanoseconds since the recording started, non-decreasing.</param>
[StructLayout(LayoutKind.Auto)]
internal readonly record struct ProfileActivation(bool Entered, int FunctionIndex, long TimestampNanoseconds);

/// <summary>
/// What one recording saw: a table of functions, and the balanced stream of activations over them.
/// </summary>
/// <remarks>
/// The stream is well-formed — every enter is matched by a later leave of the same function, properly nested
/// — which is what lets it be replayed into a call tree by pushing and popping. A source that samples rather
/// than records converts its samples into this shape at its own boundary, which is a lossless direction: two
/// consecutive samples differ by a suffix of the stack, and that difference is a run of leaves and enters.
/// </remarks>
internal sealed class RecordedProfile
{
    internal RecordedProfile(
        IReadOnlyList<ProfileFunction> functions,
        IReadOnlyList<ProfileActivation> activations,
        long durationNanoseconds,
        bool truncated)
    {
        Functions = functions;
        Activations = activations;
        DurationNanoseconds = durationNanoseconds;
        Truncated = truncated;
    }

    /// <summary>Gets the distinct functions the recording saw, in the order it first saw them.</summary>
    internal IReadOnlyList<ProfileFunction> Functions { get; }

    /// <summary>Gets the enters and leaves, in the order they happened.</summary>
    internal IReadOnlyList<ProfileActivation> Activations { get; }

    /// <summary>Gets the wall-clock time the recording covered, including whatever was not script.</summary>
    internal long DurationNanoseconds { get; }

    /// <summary>Gets whether the recording stopped early, so it describes only the beginning of the run.</summary>
    internal bool Truncated { get; }
}

/// <summary>
/// The engine's exact profiler, which records at the call boundary on the engine's own thread.
/// </summary>
/// <remarks>
/// It is the exact instrument rather than the statistical one, so the profile a client gets is not a
/// sample of where time went but a record of every call that happened. Two consequences a client should be
/// told about and one it should not care about: a profiled run is slower than an unprofiled one, and
/// <c>setSamplingInterval</c> means nothing to it — while the shape the front end draws is the same either
/// way, because a `Profile` is a tree of nodes and a series of weighted samples whichever instrument filled
/// it in.
/// </remarks>
internal sealed class EventedProfileSource : IProfileSource
{
    private readonly Engine _engine;

    internal EventedProfileSource(Engine engine)
    {
        _engine = engine;
    }

    /// <inheritdoc/>
    public bool IsRecording => _engine.Diagnostics.IsProfiling;

    /// <inheritdoc/>
    public void Start(TimeSpan interval) => _engine.Diagnostics.StartProfiling();

    /// <inheritdoc/>
    public RecordedProfile Stop()
    {
        var profile = _engine.Diagnostics.StopProfiling();

        var functions = new ProfileFunction[profile.Frames.Count];
        for (var i = 0; i < functions.Length; i++)
        {
            var frame = profile.Frames[i];
            functions[i] = new ProfileFunction(frame.Name, frame.File, frame.Line, frame.Column, frame.Program);
        }

        var activations = new ProfileActivation[profile.Events.Count];
        for (var i = 0; i < activations.Length; i++)
        {
            var recorded = profile.Events[i];
            activations[i] = new ProfileActivation(
                recorded.Kind == ScriptProfileEventKind.Open,
                recorded.FrameIndex,
                recorded.TimestampNanoseconds);
        }

        return new RecordedProfile(functions, activations, profile.DurationNanoseconds, profile.Truncated);
    }
}
