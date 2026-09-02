using System.Collections;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Runtime.InteropServices;
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

        // Views rather than copies: a session of a hundred thousand samples publishes its tables without
        // allocating a second set of them, and the rows are made as a reader asks for them.
        Functions = new ProfileTableView<ScriptProfileFrame>(funcs.Length, index => funcs[index]);
        Frames = new ProfileTableView<SampledProfileFrame>(frames.Length, index =>
        {
            var frame = frames[index];
            return new SampledProfileFrame(
                frame.Func,
                funcCategories[frame.Func],
                frame.Line < 0 ? null : frame.Line,
                frame.Column < 0 ? null : frame.Column);
        });
        Stacks = new ProfileTableView<SampledProfileStack>(stacks.Length, index =>
        {
            var stack = stacks[index];
            return new SampledProfileStack(stack.Prefix, stack.Frame);
        });
        Samples = new ProfileTableView<SampledProfileSample>(
            sampleStacks.Length,
            index => new SampledProfileSample(TimeSpan.FromMilliseconds(sampleTimes[index]), sampleStacks[index]));
    }

    /// <summary>
    /// How many samples the session recorded.
    /// </summary>
    public int SampleCount => _sampleStacks.Length;

    /// <summary>
    /// Gets the distinct functions the session saw, which <see cref="SampledProfileFrame.Function"/> indexes
    /// into.
    /// </summary>
    /// <remarks>
    /// Index <c>0</c> is the synthetic <c>(program)</c> function every sampled stack is rooted at: the
    /// program itself, which is not on the call stack because only function activations are.
    /// </remarks>
    public IReadOnlyList<ScriptProfileFrame> Functions { get; }

    /// <summary>
    /// Gets the positions the session observed, which <see cref="SampledProfileStack.Frame"/> indexes into.
    /// </summary>
    public IReadOnlyList<SampledProfileFrame> Frames { get; }

    /// <summary>
    /// Gets the stack tree as parent links, which <see cref="SampledProfileSample.Stack"/> indexes into.
    /// </summary>
    /// <remarks>
    /// A node names its frame and the node it hangs off, so a stack is read by walking
    /// <see cref="SampledProfileStack.Parent"/> to <c>-1</c>, which yields the frames innermost first.
    /// Two stacks sharing a prefix share every node of it, which is what makes a long session's table small.
    /// </remarks>
    public IReadOnlyList<SampledProfileStack> Stacks { get; }

    /// <summary>
    /// Gets what the stack looked like at each sample point, oldest first.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A sample's time is measured from the start of the session, and the gap to the next one — to
    /// <see cref="Duration"/> for the last — is how long that stack was observed for. The gaps vary, because
    /// the sampler fires at the engine's own check points rather than on a timer, so a reader that weights
    /// samples equally under-reports exactly the stacks the engine could not be interrupted in.
    /// </para>
    /// <para>
    /// The document <see cref="WriteTo(TextWriter)"/> writes carries that as a weight of
    /// <c>round(gap / <see cref="Interval"/>)</c>, at least one.
    /// </para>
    /// </remarks>
    public IReadOnlyList<SampledProfileSample> Samples { get; }

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

/// <summary>
/// One row of a sampled profile's frame table: a function, and the position inside it that was executing.
/// </summary>
/// <param name="Function">Index into <see cref="SampledProfile.Functions"/> of the function this is a position in.</param>
/// <param name="Category">Whose code that function is, which is the classification a CLR profiler cannot make.</param>
/// <param name="Line">One-based line the frame was executing, or <see langword="null"/> when there is none.</param>
/// <param name="Column">One-based column the frame was executing, or <see langword="null"/> with <paramref name="Line"/>.</param>
/// <remarks>
/// <para>
/// One function appears as several frames when it was observed at several positions, so a reader building a
/// tree of functions groups by <see cref="Function"/> and a reader showing where the time went inside one
/// does not.
/// </para>
/// <para>
/// Only a function parsed from source has a position, so <see cref="Line"/> is <see langword="null"/> for a
/// built-in and for a host callable. The one for the synthetic <c>(program)</c> function is the call site of
/// the outermost frame on the stack, or the node the engine last prepared for when nothing is on it.
/// </para>
/// <para>
/// This type is in a preview area, declared to the compiler as <c>JINT0002</c>; see
/// <see cref="JintDiagnosticIds"/>.
/// </para>
/// </remarks>
[Experimental(JintDiagnosticIds.PreviewDiagnostic)]
[StructLayout(LayoutKind.Auto)]
public readonly record struct SampledProfileFrame(int Function, ProfileFrameCategory Category, int? Line, int? Column);

/// <summary>
/// One node of a sampled profile's stack tree: a frame, and the node it hangs off.
/// </summary>
/// <param name="Parent">Index into <see cref="SampledProfile.Stacks"/> of the calling node, or <c>-1</c> for a root.</param>
/// <param name="Frame">Index into <see cref="SampledProfile.Frames"/> of the position this node is.</param>
/// <remarks>
/// This type is in a preview area, declared to the compiler as <c>JINT0002</c>; see
/// <see cref="JintDiagnosticIds"/>.
/// </remarks>
[Experimental(JintDiagnosticIds.PreviewDiagnostic)]
[StructLayout(LayoutKind.Auto)]
public readonly record struct SampledProfileStack(int Parent, int Frame);

/// <summary>
/// One sample: when it was taken, and what the stack looked like.
/// </summary>
/// <param name="Time">How long after the session started the sample was taken.</param>
/// <param name="Stack">Index into <see cref="SampledProfile.Stacks"/> of the stack that was observed.</param>
/// <remarks>
/// This type is in a preview area, declared to the compiler as <c>JINT0002</c>; see
/// <see cref="JintDiagnosticIds"/>.
/// </remarks>
[Experimental(JintDiagnosticIds.PreviewDiagnostic)]
[StructLayout(LayoutKind.Auto)]
public readonly record struct SampledProfileSample(TimeSpan Time, int Stack);

/// <summary>
/// A read-only view over one of a profile's columnar tables, projecting a row as it is asked for so that
/// publishing a table copies nothing.
/// </summary>
internal sealed class ProfileTableView<T> : IReadOnlyList<T>
{
    private readonly int _count;
    private readonly Func<int, T> _row;

    internal ProfileTableView(int count, Func<int, T> row)
    {
        _count = count;
        _row = row;
    }

    public T this[int index]
    {
        get
        {
            if ((uint) index >= (uint) _count)
            {
                Throw.ArgumentOutOfRangeException(nameof(index), "Index was out of range of the profile's table.");
            }

            return _row(index);
        }
    }

    public int Count => _count;

    public IEnumerator<T> GetEnumerator()
    {
        for (var i = 0; i < _count; i++)
        {
            yield return _row(i);
        }
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
