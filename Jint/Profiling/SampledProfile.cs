using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Text;
using Jint.Runtime;

namespace Jint.Profiling;

/// <summary>
/// The result of one sampling session: what the engine's call stack looked like at each sample point,
/// written out in the <see href="https://profiler.firefox.com">Firefox Profiler</see>'s processed format.
/// </summary>
/// <remarks>
/// <para>
/// Immutable and engine-independent: it retains no <c>Function</c>, no <c>JsValue</c> and no
/// <see cref="Engine"/>, so keeping a profile around does not keep the engine that produced it alive. A
/// frame does name the <see cref="ScriptProfileFrame.Program"/> it was parsed from, which is an identity to
/// compare and not a tree to walk from another thread.
/// </para>
/// <para>
/// <b>What a sample weighs.</b> The sampler fires at the engine's check points rather than on a timer, so
/// the gap between two samples varies, and the document therefore carries a weight per sample: the number
/// of whole <see cref="Interval"/>s that stack was observed for, never fewer than one. A step the engine
/// cannot interrupt — one long built-in call, one long host callback — is a gap in the samples and shows up
/// as weight on the sample before it, which is to say at the call site. That is exactly where the time went
/// at the resolution this instrument has; it is not a claim about what happened inside the call.
/// </para>
/// <para>
/// <b>And what it does not.</b> Sampling measures wall clock, not CPU: a thread descheduled by the
/// operating system attributes the time it was away to whatever it was executing when it lost the CPU. That
/// is the standard caveat of every sampling profiler and applies here too.
/// </para>
/// <para>
/// <b>Which frames can appear.</b> The profile is built from the engine's call stack, so a call that does
/// not push a frame is not one of them: a built-in taken through the frameless fast-call lane, and a
/// callback a built-in invokes per element (<c>arr.sort(cb)</c> is sampled as <c>sort</c>, not as
/// <c>cb</c>). Nothing is lost — the time is attributed to the nearest enclosing frame — but the tree the
/// profile shows is the call stack's, not the source's.
/// </para>
/// <para>
/// This type is in a preview area, declared to the compiler as <c>JINT0002</c>; see
/// <see cref="JintDiagnosticIds"/>.
/// </para>
/// </remarks>
[Experimental(JintDiagnosticIds.PreviewDiagnostic)]
public sealed class SampledProfile
{
    internal readonly ScriptProfileFrame[] _funcs;
    internal readonly ProfileFrameCategory[] _funcCategories;
    internal readonly SampledFrame[] _frames;
    internal readonly SampledStackNode[] _stacks;
    internal readonly int[] _sampleStacks;
    internal readonly double[] _sampleTimes;
    internal readonly double _durationMilliseconds;
    internal readonly long _startUnixMilliseconds;

    internal SampledProfile(
        ScriptProfileFrame[] funcs,
        ProfileFrameCategory[] funcCategories,
        SampledFrame[] frames,
        SampledStackNode[] stacks,
        int[] sampleStacks,
        double[] sampleTimes,
        int droppedSampleCount,
        TimeSpan interval,
        double durationMilliseconds,
        long startUnixMilliseconds)
    {
        _funcs = funcs;
        _funcCategories = funcCategories;
        _frames = frames;
        _stacks = stacks;
        _sampleStacks = sampleStacks;
        _sampleTimes = sampleTimes;
        _durationMilliseconds = durationMilliseconds;
        _startUnixMilliseconds = startUnixMilliseconds;

        DroppedSampleCount = droppedSampleCount;
        Interval = interval;
    }

    /// <summary>
    /// How many samples the session recorded.
    /// </summary>
    public int SampleCount => _sampleStacks.Length;

    /// <summary>
    /// How many sample points were refused because <see cref="SamplingOptions.MaxSamples"/> had been
    /// reached. Non-zero means the profile describes the beginning of the run and says nothing about the
    /// rest of it.
    /// </summary>
    public int DroppedSampleCount { get; }

    /// <summary>
    /// The interval the session was configured with, which is what one unit of sample weight means in the
    /// document.
    /// </summary>
    public TimeSpan Interval { get; }

    /// <summary>
    /// Wall-clock time between <see cref="Engine.DiagnosticOperations.StartSampling"/> and
    /// <see cref="Engine.DiagnosticOperations.StopSampling"/>, including whatever the host did in between
    /// that was not script.
    /// </summary>
    public TimeSpan Duration => TimeSpan.FromMilliseconds(_durationMilliseconds);

    /// <summary>
    /// Writes this profile as a Firefox Profiler processed profile. The output is a complete JSON document
    /// that <see href="https://profiler.firefox.com">profiler.firefox.com</see> opens as-is, from a local
    /// file that never leaves the machine.
    /// </summary>
    /// <param name="writer">Where to write. Not flushed and not disposed.</param>
    /// <exception cref="ArgumentNullException"><paramref name="writer"/> is <see langword="null"/>.</exception>
    public void WriteTo(TextWriter writer)
    {
        if (writer is null)
        {
            Throw.ArgumentNullException(nameof(writer));
        }

        FirefoxProfileWriter.Write(writer, this);
    }

    /// <summary>
    /// Writes this profile as a Firefox Profiler processed profile, UTF-8 encoded without a byte-order mark.
    /// </summary>
    /// <remarks>
    /// The viewer also opens a gzipped document, which is what a long session is worth storing as: wrap the
    /// destination in a <see cref="System.IO.Compression.GZipStream"/> and name the file <c>.json.gz</c>.
    /// </remarks>
    /// <param name="stream">Where to write. Flushed, but left open.</param>
    /// <exception cref="ArgumentNullException"><paramref name="stream"/> is <see langword="null"/>.</exception>
    public void WriteTo(Stream stream)
    {
        if (stream is null)
        {
            Throw.ArgumentNullException(nameof(stream));
        }

        // A UTF8Encoding of our own rather than Encoding.UTF8, whose GetPreamble() would put a BOM in front
        // of the document; JSON parsers are not obliged to skip one.
        using var writer = new StreamWriter(stream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false), 1024, leaveOpen: true);
        WriteTo(writer);
    }
}
