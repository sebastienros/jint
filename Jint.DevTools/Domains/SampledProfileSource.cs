using Jint.Profiling;

#pragma warning disable JINT0002 // the sampling profiler is the engine's preview area; this is what it is for

namespace Jint.DevTools.Domains;

/// <summary>
/// The engine's sampling profiler, which notes what the call stack looks like at the engine's own check
/// points.
/// </summary>
/// <remarks>
/// <para>
/// This is the instrument a Performance panel asks for: a client sets a rate with
/// <c>Profiler.setSamplingInterval</c> and gets back where the time went, at a cost the recording does not
/// change. What it cannot show is a call it never sampled — a function entered and left between two check
/// points is not in the profile at all, where <see cref="EventedProfileSource"/> records every one of them
/// and charges the run for it.
/// </para>
/// <para>
/// <b>Samples become a balanced stream of enters and leaves</b>, which is what
/// <see cref="RecordedProfile"/> speaks and what makes the seam fit both instruments. The direction is
/// lossless: two consecutive samples differ by a suffix of the stack, so the difference between them is a
/// run of leaves followed by a run of enters, all at the later sample's timestamp. Time between two samples
/// therefore belongs to the stack observed at the earlier one, which is exactly what the sampler saw.
/// </para>
/// <para>
/// The sampler's own synthetic <c>(program)</c> root is dropped rather than carried through, because
/// <see cref="ProfileBuilder"/> has one of its own and takes the time no script function was on the stack.
/// Two would be a tree with the node twice in it.
/// </para>
/// </remarks>
internal sealed class SampledProfileSource : IProfileSource
{
    /// <summary>
    /// The function index of the sampler's synthetic root, which every sampled stack hangs off.
    /// </summary>
    private const int ProgramFunction = 0;

    private const long NanosecondsPerTick = 100;

    private readonly Engine _engine;

    internal SampledProfileSource(Engine engine)
    {
        _engine = engine;
    }

    /// <inheritdoc/>
    public bool IsRecording => _engine.Diagnostics.IsSampling;

    /// <inheritdoc/>
    public void Start(TimeSpan interval)
        => _engine.Diagnostics.StartSampling(new SamplingOptions { Interval = interval });

    /// <inheritdoc/>
    public RecordedProfile Stop()
    {
        var profile = _engine.Diagnostics.StopSampling();

        var functions = new ProfileFunction[profile.Functions.Count];
        for (var i = 0; i < functions.Length; i++)
        {
            var function = profile.Functions[i];
            functions[i] = new ProfileFunction(function.Name, function.File, function.Line, function.Column, function.Program);
        }

        var activations = new List<ProfileActivation>();
        var previous = new List<int>();
        var current = new List<int>();

        foreach (var sample in profile.Samples)
        {
            Read(profile, sample.Stack, current);

            var at = Nanoseconds(sample.Time);
            var shared = SharedPrefix(previous, current);

            for (var i = previous.Count - 1; i >= shared; i--)
            {
                activations.Add(new ProfileActivation(Entered: false, previous[i], at));
            }

            for (var i = shared; i < current.Count; i++)
            {
                activations.Add(new ProfileActivation(Entered: true, current[i], at));
            }

            (previous, current) = (current, previous);
        }

        // Whatever was on the stack at the last sample was there until the session ended.
        var duration = Nanoseconds(profile.Duration);
        for (var i = previous.Count - 1; i >= 0; i--)
        {
            activations.Add(new ProfileActivation(Entered: false, previous[i], duration));
        }

        // A session that reached MaxSamples describes the beginning of the run and says nothing about the
        // rest of it, which is what a client reads Truncated for.
        return new RecordedProfile(functions, activations, duration, profile.DroppedSampleCount > 0);
    }

    /// <summary>
    /// Reads one sampled stack into <paramref name="stack"/>, outermost function first and without the
    /// sampler's synthetic root.
    /// </summary>
    private static void Read(SampledProfile profile, int node, List<int> stack)
    {
        stack.Clear();

        // The tree is parent-linked, so the walk is innermost first and the order is put right afterwards.
        for (var index = node; index >= 0; index = profile.Stacks[index].Parent)
        {
            var function = profile.Frames[profile.Stacks[index].Frame].Function;
            if (function != ProgramFunction)
            {
                stack.Add(function);
            }
        }

        stack.Reverse();
    }

    /// <summary>How much of two consecutive stacks is the same run of calls, which stays open across them.</summary>
    private static int SharedPrefix(List<int> previous, List<int> current)
    {
        var length = Math.Min(previous.Count, current.Count);
        for (var i = 0; i < length; i++)
        {
            if (previous[i] != current[i])
            {
                return i;
            }
        }

        return length;
    }

    private static long Nanoseconds(TimeSpan value) => value.Ticks * NanosecondsPerTick;
}
