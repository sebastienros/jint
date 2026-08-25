#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using Jint.Profiling;

namespace Jint.Tests.PublicInterface;

/// <summary>
/// The profiler from an embedder's vantage point: <see cref="Options.ProfilingOptions"/>,
/// <see cref="Engine.DiagnosticOperations.StartProfiling"/> / <see cref="Engine.DiagnosticOperations.StopProfiling"/>
/// and the <see cref="ScriptProfile"/> they produce, reached with nothing but the public surface — this suite
/// has no <c>InternalsVisibleTo</c>, so a test here is proof a third party can do the same.
///
/// <para>
/// The exported document is checked by parsing it back through a <em>second</em> engine's <c>JSON.parse</c>,
/// which is the same route an embedder has and needs no JSON dependency in this project.
/// </para>
/// </summary>
public class HostScriptProfilingTests
{
    private const string SchemaUrl = "https://www.speedscope.app/file-format-schema.json";

    private const string Script = """
        function leaf() { return 1; }
        function middle() { return leaf(); }
        function root() { return middle() + leaf(); }
        root();
        """;

    [Fact]
    public void ADefaultEngineRefusesToProfile()
    {
        var engine = new Engine();

        engine.Diagnostics.IsProfiling.Should().BeFalse();

        var exception = Assert.Throws<InvalidOperationException>(() => engine.Diagnostics.StartProfiling());
        exception.Message.Should().Contain("Options.Profiling.Enabled");
    }

    [Fact]
    public void AHostCanProfileAnEngineItBuiltForIt()
    {
        var engine = new Engine(options => options.Profiling.Enabled = true);

        engine.Diagnostics.StartProfiling();
        engine.Diagnostics.IsProfiling.Should().BeTrue();
        engine.Execute(Script, "host.js");
        var profile = engine.Diagnostics.StopProfiling();

        engine.Diagnostics.IsProfiling.Should().BeFalse();
        profile.Truncated.Should().BeFalse();
        profile.Events.Should().NotBeEmpty();
        profile.Frames.Select(static f => f.Name).Should().BeEquivalentTo(["root", "middle", "leaf"]);
        profile.Frames.Should().OnlyContain(f => f.File == "host.js");
        profile.DurationNanoseconds.Should().BeGreaterThanOrEqualTo(0);
    }

    [Fact]
    public void OneOptionsInstanceServesManyEnginesIndependently()
    {
        // Options are meant to be shared; a session belongs to the engine, not to the options it came from.
        var options = new Options();
        options.Profiling.Enabled = true;
        options.Profiling.MaxEvents = 5_000;

        var first = new Engine(options);
        var second = new Engine(options);

        first.Diagnostics.StartProfiling();
        second.Diagnostics.StartProfiling();

        first.Execute("function a() { return 1; } a();");
        second.Execute("function b() { return 1; } b();");

        first.Diagnostics.StopProfiling().Frames.Select(static f => f.Name).Should().Equal("a");
        second.Diagnostics.StopProfiling().Frames.Select(static f => f.Name).Should().Equal("b");
    }

    [Fact]
    public void MaxEventsBoundsWhatASessionCanCost()
    {
        var engine = new Engine(options =>
        {
            options.Profiling.Enabled = true;
            options.Profiling.MaxEvents = 50;
        });

        engine.Diagnostics.StartProfiling();
        engine.Execute("function f() { return 1; } for (var i = 0; i < 10000; i++) { f(); }");
        var profile = engine.Diagnostics.StopProfiling();

        profile.Truncated.Should().BeTrue();
        profile.Events.Count.Should().BeLessThanOrEqualTo(50);
    }

    [Fact]
    public void TheExportedDocumentIsAValidSpeedscopeEventedProfile()
    {
        var engine = new Engine(options => options.Profiling.Enabled = true);
        engine.Diagnostics.StartProfiling();
        engine.Execute(Script, "host.js");
        var profile = engine.Diagnostics.StopProfiling();

        using var stream = new MemoryStream();
        profile.WriteSpeedscopeJson(stream);
        var json = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false).GetString(stream.ToArray());

        // Parsed by a plain engine through the language's own JSON parser — an embedder's route exactly.
        var reader = new Engine();
        reader.SetValue("json", json);
        reader.Execute("var doc = JSON.parse(json);");

        reader.Evaluate("doc['$schema']").AsString().Should().Be(SchemaUrl);
        reader.Evaluate("doc.profiles.length").AsNumber().Should().Be(1);
        reader.Evaluate("doc.profiles[0].type").AsString().Should().Be("evented");
        reader.Evaluate("doc.profiles[0].unit").AsString().Should().Be("nanoseconds");
        reader.Evaluate("doc.profiles[0].startValue").AsNumber().Should().Be(0);
        reader.Evaluate("doc.profiles[0].endValue").AsNumber().Should().Be(profile.DurationNanoseconds);
        reader.Evaluate("doc.shared.frames.length").AsNumber().Should().Be(profile.Frames.Count);
        reader.Evaluate("doc.shared.frames.every(function (f) { return typeof f.name === 'string'; })")
            .AsBoolean().Should().BeTrue();

        // Every event carries the three required members, the types are exactly O and C, timestamps never go
        // backwards, and the stream closes exactly what it opens.
        reader.Evaluate("""
            (function () {
                var open = 0, previous = 0;
                var events = doc.profiles[0].events;
                for (var i = 0; i < events.length; i++) {
                    var e = events[i];
                    if (e.type !== 'O' && e.type !== 'C') { return 'bad type ' + e.type; }
                    if (typeof e.frame !== 'number' || typeof e.at !== 'number') { return 'missing member'; }
                    if (e.frame < 0 || e.frame >= doc.shared.frames.length) { return 'frame out of range'; }
                    if (e.at < previous) { return 'timestamps went backwards'; }
                    previous = e.at;
                    open += e.type === 'O' ? 1 : -1;
                    if (open < 0) { return 'closed more than was open'; }
                }
                if (open !== 0) { return open + ' frames left open'; }
                if (previous > doc.profiles[0].endValue) { return 'event after endValue'; }
                return 'ok';
            })();
            """).AsString().Should().Be("ok");
    }

    /// <summary>
    /// A profile is a value, not a view onto the engine that produced it: it holds strings and numbers, never
    /// a function, an AST or an <see cref="Engine"/>. A host that profiles a pooled or per-request engine and
    /// keeps the profiles must not thereby keep the engines.
    /// </summary>
    [Fact]
    public void AProfileDoesNotRetainTheEngineThatProducedIt()
    {
        const int Count = 10;
        var profiles = new List<ScriptProfile>(Count);
        var references = new List<WeakReference>(Count);

        for (var i = 0; i < Count; i++)
        {
            references.Add(ProfileOnceAndForget(profiles));
        }

        GC.Collect(2, GCCollectionMode.Forced, blocking: true);
        GC.WaitForPendingFinalizers();
        GC.Collect(2, GCCollectionMode.Forced, blocking: true);

        var alive = references.Count(static r => r.IsAlive);

        // Doubles as the keep-alive for the profiles, and as the assertion that they were ever worth holding.
        profiles.Should().OnlyContain(p => p.Frames.Count > 0);

        alive.Should().Be(0, $"{alive} of {Count} engines were not collected — a profile still pins its engine.");

        // NoInlining so the engine reference cannot be stack-rooted in the caller's frame across the collect.
        [MethodImpl(MethodImplOptions.NoInlining)]
        static WeakReference ProfileOnceAndForget(List<ScriptProfile> sink)
        {
            var engine = new Engine(options => options.Profiling.Enabled = true);
            engine.Diagnostics.StartProfiling();
            engine.Execute(Script);
            sink.Add(engine.Diagnostics.StopProfiling());
            return new WeakReference(engine);
        }
    }

    [Fact]
    public void StoppingWithoutStartingIsAnError()
    {
        var engine = new Engine(options => options.Profiling.Enabled = true);

        Assert.Throws<InvalidOperationException>(() => engine.Diagnostics.StopProfiling());
    }

    [Fact]
    public void MaxEventsRejectsANonPositiveBudget()
    {
        var options = new Options();

        Assert.Throws<ArgumentOutOfRangeException>(() => options.Profiling.MaxEvents = 0);
        Assert.Throws<ArgumentOutOfRangeException>(() => options.Profiling.MaxEvents = -5);
        options.Profiling.MaxEvents.Should().Be(Options.ProfilingOptions.DefaultMaxEvents);
    }
}
