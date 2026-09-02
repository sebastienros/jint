#nullable enable

using System;
using System.IO;
using System.Linq;
using System.Text;
using Jint.Profiling;

#pragma warning disable JINT0002 // the sampling profiler is a preview area

namespace Jint.Tests.PublicInterface;

/// <summary>
/// The sampling profiler from an embedder's vantage point: <see cref="SamplingOptions"/>,
/// <see cref="Engine.DiagnosticOperations.StartSampling"/> / <see cref="Engine.DiagnosticOperations.StopSampling"/>
/// and the <see cref="SampledProfile"/> they produce, reached with nothing but the public surface — this
/// suite has no <c>InternalsVisibleTo</c>, so a test here is proof a third party can do the same.
///
/// <para>
/// The exported document is checked by parsing it back through a <em>second</em> engine's <c>JSON.parse</c>,
/// which is the same route an embedder has and needs no JSON dependency in this project.
/// </para>
/// </summary>
public class HostSamplingProfilerTests
{
    private const string Script = """
        function hot(n) {
            var sum = 0;
            for (var i = 0; i < n; i++) { sum += i % 7; }
            return sum;
        }
        function cold() { return hot(2000); }
        for (var round = 0; round < 20; round++) { cold(); }
        """;

    [Test]
    public void ADefaultEngineRefusesToSample()
    {
        var engine = new Engine();

        engine.Diagnostics.IsSampling.Should().BeFalse();

        var exception = Assert.Throws<InvalidOperationException>(() => engine.Diagnostics.StartSampling())!;
        exception.Message.Should().Contain("Options.Profiling.Enabled");
    }

    [Test]
    public void AHostCanSampleAnEngineItBuiltForIt()
    {
        var engine = new Engine(options => options.Profiling.Enabled = true);

        engine.Diagnostics.StartSampling(new SamplingOptions { Interval = TimeSpan.Zero });
        engine.Diagnostics.IsSampling.Should().BeTrue();
        engine.Execute(Script, "host.js");
        var profile = engine.Diagnostics.StopSampling();

        engine.Diagnostics.IsSampling.Should().BeFalse();
        profile.SampleCount.Should().BeGreaterThan(0);
        profile.DroppedSampleCount.Should().Be(0);
        profile.Duration.Should().BeGreaterThanOrEqualTo(TimeSpan.Zero);
        profile.Interval.Should().Be(TimeSpan.Zero);
    }

    [Test]
    public void OneOptionsInstanceServesManyEnginesIndependently()
    {
        // Options are meant to be shared; a session belongs to the engine, not to the options it came from.
        var options = new Options();
        options.Profiling.Enabled = true;

        var first = new Engine(options);
        var second = new Engine(options);

        first.Diagnostics.StartSampling(new SamplingOptions { Interval = TimeSpan.Zero });
        first.Execute(Script);
        first.Diagnostics.StopSampling().SampleCount.Should().BeGreaterThan(0);

        second.Diagnostics.IsSampling.Should().BeFalse();
        second.Diagnostics.StartSampling(new SamplingOptions { Interval = TimeSpan.Zero });
        second.Execute(Script);
        second.Diagnostics.StopSampling().SampleCount.Should().BeGreaterThan(0);
    }

    [Test]
    public void MaxSamplesBoundsWhatASessionCanCost()
    {
        var engine = new Engine(options => options.Profiling.Enabled = true);

        engine.Diagnostics.StartSampling(new SamplingOptions { Interval = TimeSpan.Zero, MaxSamples = 10 });
        engine.Execute(Script);
        var profile = engine.Diagnostics.StopSampling();

        profile.SampleCount.Should().Be(10);
        profile.DroppedSampleCount.Should().BeGreaterThan(0);
    }

    [Test]
    public void TheExportedDocumentIsAValidFirefoxProcessedProfile()
    {
        var engine = new Engine(options => options.Profiling.Enabled = true);
        engine.Diagnostics.StartSampling(new SamplingOptions { Interval = TimeSpan.Zero });
        engine.Execute(Script, "host.js");
        var profile = engine.Diagnostics.StopSampling();

        using var stream = new MemoryStream();
        profile.WriteTo(stream);
        var json = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false).GetString(stream.ToArray());

        // Parsed by a plain engine through the language's own JSON parser — an embedder's route exactly.
        var reader = new Engine();
        reader.SetValue("json", json);
        reader.Execute("var doc = JSON.parse(json);");

        reader.Evaluate("doc.meta.product").AsString().Should().Be("Jint");
        reader.Evaluate("doc.meta.preprocessedProfileVersion").AsNumber().Should().Be(51);
        reader.Evaluate("doc.meta.categories[0].color").AsString().Should().Be("grey");
        reader.Evaluate("doc.threads.length").AsNumber().Should().Be(1);
        reader.Evaluate("doc.threads[0].samples.length").AsNumber().Should().Be(profile.SampleCount);
        reader.Evaluate("doc.threads[0].samples.weightType").AsString().Should().Be("samples");

        // Every table's length matches every one of its columns, and every cross-reference resolves: a
        // sample names a stack, a stack node names a frame and an earlier node, a frame names a function,
        // and a function names a string.
        reader.Evaluate("""
            (function () {
                var t = doc.threads[0];
                function checkTable(table, name) {
                    for (var column in table) {
                        if (column === 'length' || column === 'weightType') { continue; }
                        var value = table[column];
                        if (Array.isArray(value) && value.length !== table.length) {
                            return name + '.' + column + ' has ' + value.length + ' of ' + table.length;
                        }
                    }
                    return null;
                }

                var bad = checkTable(t.samples, 'samples')
                    || checkTable(t.stackTable, 'stackTable')
                    || checkTable(t.frameTable, 'frameTable')
                    || checkTable(t.funcTable, 'funcTable');
                if (bad) { return bad; }

                for (var i = 0; i < t.samples.length; i++) {
                    if (t.samples.stack[i] < 0 || t.samples.stack[i] >= t.stackTable.length) { return 'stack out of range'; }
                    if (!(t.samples.weight[i] >= 1)) { return 'weight below one'; }
                    if (i > 0 && t.samples.time[i] < t.samples.time[i - 1]) { return 'time went backwards'; }
                }

                for (var s = 0; s < t.stackTable.length; s++) {
                    var prefix = t.stackTable.prefix[s];
                    if (prefix !== null && !(prefix >= 0 && prefix < s)) { return 'prefix is not an earlier node'; }
                    if (t.stackTable.frame[s] >= t.frameTable.length) { return 'frame out of range'; }
                }

                for (var f = 0; f < t.frameTable.length; f++) {
                    if (t.frameTable.func[f] >= t.funcTable.length) { return 'func out of range'; }
                    if (t.frameTable.category[f] >= doc.meta.categories.length) { return 'category out of range'; }
                }

                for (var n = 0; n < t.funcTable.length; n++) {
                    if (t.funcTable.name[n] >= t.stringArray.length) { return 'name out of range'; }
                }

                return 'ok';
            })()
            """).AsString().Should().Be("ok");

        // The functions the script defined are in the document, with the source they came from.
        reader.Evaluate("""
            (function () {
                var t = doc.threads[0];
                for (var i = 0; i < t.funcTable.length; i++) {
                    if (t.stringArray[t.funcTable.name[i]] === 'hot') {
                        return t.funcTable.fileName[i] === null ? '<none>' : t.stringArray[t.funcTable.fileName[i]];
                    }
                }
                return '<missing>';
            })()
            """).AsString().Should().Be("host.js");
    }

    [Test]
    public void TheDocumentIsWrittenWithoutAByteOrderMark()
    {
        var engine = new Engine(options => options.Profiling.Enabled = true);
        engine.Diagnostics.StartSampling();
        var profile = engine.Diagnostics.StopSampling();

        using var stream = new MemoryStream();
        profile.WriteTo(stream);

        stream.ToArray()[0].Should().Be((byte) '{');
    }

    [Test]
    public void SamplingOptionsRefuseAValueThatCouldNotWork()
    {
        var options = new SamplingOptions();

        Assert.Throws<ArgumentOutOfRangeException>(() => options.Interval = TimeSpan.FromTicks(-1));
        Assert.Throws<ArgumentOutOfRangeException>(() => options.MaxSamples = 0);

        options.Interval.Should().Be(TimeSpan.FromMilliseconds(1));
        options.MaxSamples.Should().Be(SamplingOptions.DefaultMaxSamples);
    }

    [Test]
    public void TheTwoProfilersAreIndependentInstruments()
    {
        // One gate, two sessions: a host may run both at once, and stopping one does not stop the other.
        var engine = new Engine(options => options.Profiling.Enabled = true);

        engine.Diagnostics.StartProfiling();
        engine.Diagnostics.StartSampling(new SamplingOptions { Interval = TimeSpan.Zero });
        engine.Execute(Script);

        var sampled = engine.Diagnostics.StopSampling();
        engine.Diagnostics.IsProfiling.Should().BeTrue();

        var evented = engine.Diagnostics.StopProfiling();
        sampled.SampleCount.Should().BeGreaterThan(0);
        evented.Events.Should().NotBeEmpty();
    }

    [Test]
    public void ANullWriterIsRefused()
    {
        var engine = new Engine(options => options.Profiling.Enabled = true);
        engine.Diagnostics.StartSampling();
        var profile = engine.Diagnostics.StopSampling();

        Assert.Throws<ArgumentNullException>(() => profile.WriteTo((Stream) null!));
        Assert.Throws<ArgumentNullException>(() => profile.WriteTo((TextWriter) null!));
    }

    // ---- the tables, which is what a tool that is not the Firefox Profiler reads ----

    /// <summary>
    /// The four tables are the document without the document: a host that renders a profile itself, or
    /// speaks a protocol of its own, reads them rather than writing JSON and parsing it back.
    /// </summary>
    [Test]
    public void TheTablesAnswerWhatTheDocumentWrites()
    {
        var engine = new Engine(options => options.Profiling.Enabled = true);

        engine.Diagnostics.StartSampling(new SamplingOptions { Interval = TimeSpan.Zero });
        engine.Execute(Script, "host.js");
        var profile = engine.Diagnostics.StopSampling();

        profile.Samples.Should().HaveCount(profile.SampleCount);
        profile.Functions.Should().NotBeEmpty();
        profile.Frames.Should().NotBeEmpty();
        profile.Stacks.Should().NotBeEmpty();

        // Index 0 is the synthetic program function every stack is rooted at.
        profile.Functions[0].Name.Should().Be("(program)");
        profile.Functions.Select(function => function.Name).Should().Contain(["hot", "cold"]);

        // every index is in range, and every stack walk terminates at a root
        foreach (var sample in profile.Samples)
        {
            sample.Time.Should().BeGreaterThanOrEqualTo(TimeSpan.Zero);
            sample.Stack.Should().BeInRange(0, profile.Stacks.Count - 1);

            var node = sample.Stack;
            var depth = 0;
            while (node >= 0)
            {
                var stack = profile.Stacks[node];
                stack.Frame.Should().BeInRange(0, profile.Frames.Count - 1);
                profile.Frames[stack.Frame].Function.Should().BeInRange(0, profile.Functions.Count - 1);

                node = stack.Parent;
                (++depth).Should().BeLessThan(1000, "a stack walk terminates");
            }
        }

        // the samples are in the order they were taken
        profile.Samples.Select(sample => sample.Time).Should().BeInAscendingOrder();
    }

    /// <summary>
    /// The classification a CLR profiler cannot make, which is half the diagnosis for an embedder: whose
    /// code was on the stack.
    /// </summary>
    [Test]
    public void AFrameSaysWhoseCodeItIsRunning()
    {
        var engine = new Engine(options => options.Profiling.Enabled = true);
        engine.SetValue("host", new Func<int, int>(value => value + 1));

        engine.Diagnostics.StartSampling(new SamplingOptions { Interval = TimeSpan.Zero });
        engine.Execute("function work() { var t = 0; for (var i = 0; i < 5000; i++) { t += host(i); } return t; } work();", "host.js");
        var profile = engine.Diagnostics.StopSampling();

        var categories = profile.Frames.Select(frame => frame.Category).Distinct().ToList();
        categories.Should().Contain(ProfileFrameCategory.Script);

        // a script function names its position and its program; a native one has neither
        foreach (var frame in profile.Frames)
        {
            var function = profile.Functions[frame.Function];
            if (frame.Category != ProfileFrameCategory.Script)
            {
                function.File.Should().BeNull();
                function.Program.Should().BeNull();
                frame.Line.Should().BeNull();
            }
        }

        var work = profile.Functions.Single(function => function.Name == "work");
        work.File.Should().Be("host.js");
        work.Program.Should().NotBeNull();
    }

    /// <summary>
    /// A table is a view over what the session recorded, so reading it twice is reading one thing and costs
    /// no second copy of it.
    /// </summary>
    [Test]
    public void ATableIsAStableViewRatherThanACopy()
    {
        var engine = new Engine(options => options.Profiling.Enabled = true);
        engine.Diagnostics.StartSampling(new SamplingOptions { Interval = TimeSpan.Zero });
        engine.Execute(Script);
        var profile = engine.Diagnostics.StopSampling();

        profile.Samples.Should().BeSameAs(profile.Samples);
        profile.Frames.Should().BeSameAs(profile.Frames);
        profile.Stacks.Should().BeSameAs(profile.Stacks);
        profile.Functions.Should().BeSameAs(profile.Functions);

        Assert.Throws<ArgumentOutOfRangeException>(() => _ = profile.Samples[profile.Samples.Count]);
        Assert.Throws<ArgumentOutOfRangeException>(() => _ = profile.Stacks[-1]);
    }
}
