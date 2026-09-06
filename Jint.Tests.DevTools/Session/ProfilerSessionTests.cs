using System.Text.Json;
using Jint.DevTools.Protocol;
using Jint.DevTools.Protocol.Profiler;

#pragma warning disable JINT0002 // the sampling profiler is the engine's preview area

namespace Jint.Tests.DevTools.Session;

/// <summary>
/// <c>Profiler.start</c> and <c>Profiler.stop</c>: the document a front end's Performance panel loads.
/// </summary>
/// <remarks>
/// <para>
/// The assertions are about the two halves the panel reads — a tree of nodes, and a series of samples with
/// the time between them — plus the one property that makes the second half meaningful: the deltas add up to
/// the recording. A profile whose tree is right and whose deltas are not renders as an empty flame chart.
/// </para>
/// <para>
/// The instrument behind them is the engine's <em>sampler</em>, so the script is written to be sampled: the
/// call it should be caught in is the one it spends its time in, and every recording asks for the fastest
/// rate the protocol can name so that a run this short is observed more than once.
/// </para>
/// </remarks>
[NonParallelizable]
public class ProfilerSessionTests
{
    private const string Source = """
        function inner(n) {
            var total = 0;
            for (var i = 0; i < n; i++) { total += i % 7; }
            return total;
        }

        function outer(n) {
            return inner(n) + inner(n);
        }

        function work(times) {
            var total = 0;
            for (var i = 0; i < times; i++) {
                total = total + outer(50);
            }

            return total;
        }
        """;

    /// <summary>Enough work to be sampled many times over, and short enough to be a unit test.</summary>
    private const string Workload = "work(200)";

    /// <summary>
    /// The same work, forty times over. It exists only for
    /// <see cref="ACpuBoundScriptAttributesMostOfItsTimeToItsHotFunction"/>, whose assertion is a *share* and
    /// is therefore the one assertion here a loaded machine can move.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A time delta is the gap to the next sample, so an uninterruptible moment is charged whole to the frame
    /// observed before it — right for a panel, and enough to <b>reorder</b> a small profile when the moment
    /// lands on <c>outer</c> rather than on <c>inner</c>. That is what this test failed on, at
    /// <see cref="Workload"/>'s size, with no profiler change in the pull request.
    /// </para>
    /// <para>
    /// <b>Measured, because the size is the whole point.</b> <see cref="Workload"/> records <b>31</b> samples
    /// over about 35 ms, of which <c>outer</c> holds roughly 430 units against <c>inner</c>'s 34,300: one
    /// stall of about 34 ms — the length of the whole recording — landing on the single <c>outer</c> sample
    /// reorders it. This one records <b>1,007</b> samples over about 530 ms with <c>inner</c> at 0.971–0.976,
    /// so the same reordering needs a stall roughly <b>fifteen times longer</b> and has about thirty times as
    /// many samples to miss. Forty times the work does not make a hiccup less likely; it makes it
    /// proportionally negligible, which is the only lever a test has over a scheduler.
    /// </para>
    /// </remarks>
    private const string DominantWorkload = "work(8000)";

    [Test]
    public async Task AProfileHasANodeForEveryFunctionThatRan()
    {
        await using var session = await AttachedSession.CreateAsync();
        await session.EnableDebuggerAsync();
        await session.Target.PostAsync(engine => engine.Execute(Source, "main.js"));

        var scriptId = (await session.EventAsync("Debugger.scriptParsed")).GetProperty("scriptId").GetString();

        var profile = await RecordAsync(session, Workload);

        var byName = profile.Nodes.ToDictionary(node => node.CallFrame.FunctionName, node => node);

        byName.Should().ContainKeys("(root)", "(program)", "work", "outer", "inner");
        byName["(root)"].Id.Should().Be(1, "every reader of a V8 profile assumes the root is the first node");

        foreach (var name in new[] { "work", "outer", "inner" })
        {
            var frame = byName[name].CallFrame;
            frame.ScriptId.Should().Be(scriptId, "'{0}' was declared in the script the registry announced", name);
            frame.Url.Should().Be("main.js");
            frame.LineNumber.Should().BeGreaterThanOrEqualTo(0);
            byName[name].HitCount.Should().BeGreaterThan(0, "'{0}' ran, so time was attributed to it", name);
        }

        // A node names a call position, not a function: `inner` is reached only from `outer`, so it is that
        // node's child and not the root's.
        var outer = byName["outer"];
        outer.Children.Should().NotBeNull().And.Contain(byName["inner"].Id);
        byName["(root)"].Children.Should().NotBeNull().And.NotContain(byName["inner"].Id);
    }

    [Test]
    public async Task TheTimeDeltasAddUpToTheRecording()
    {
        await using var session = await AttachedSession.CreateAsync();
        await session.Target.PostAsync(engine => engine.Execute(Source, "main.js"));

        var profile = await RecordAsync(session, Workload);

        profile.Samples.Should().NotBeNull();
        profile.TimeDeltas.Should().NotBeNull();
        profile.Samples!.Length.Should().Be(profile.TimeDeltas!.Length, "the panel reads them as one series");
        profile.EndTime.Should().BeGreaterThanOrEqualTo(profile.StartTime);

        // The deltas are the intervals between the moments the source observed, so they telescope: whatever
        // the instrument was, they cover the recording rather than approximating it. The only loss is the
        // truncation of each interval to whole microseconds.
        var total = profile.TimeDeltas.Sum(delta => (long) delta);
        var recorded = (long) (profile.EndTime - profile.StartTime);

        total.Should().BeLessThanOrEqualTo(recorded);
        total.Should().BeGreaterThanOrEqualTo(recorded - profile.TimeDeltas.Length, "each interval loses at most one microsecond to truncation");

        // Every sample names a node the profile declares.
        var ids = profile.Nodes.Select(node => node.Id).ToHashSet();
        profile.Samples.Should().OnlyContain(sample => ids.Contains(sample));
    }

    /// <summary>Time when no script function was on the stack belongs to the host, and is said to.</summary>
    [Test]
    public async Task TimeWithNothingOnTheStackIsAttributedToTheProgram()
    {
        await using var session = await AttachedSession.CreateAsync();

        await session.ResultAsync("Profiler.enable");
        await session.ResultAsync("Profiler.start");

        // Nothing at all runs between the start and the stop, so every microsecond of it is the host's.
        var profile = await StopAsync(session);
        var program = profile.Nodes.Single(node => node.CallFrame.FunctionName == "(program)");

        profile.Samples!.Should().OnlyContain(sample => sample == program.Id);
        program.HitCount.Should().BeGreaterThan(0);
    }

    [Test]
    public async Task StoppingWithoutStartingIsRefused()
    {
        await using var session = await AttachedSession.CreateAsync();
        await session.ResultAsync("Profiler.enable");

        var error = await session.ErrorAsync("Profiler.stop");
        error.GetProperty("code").GetInt32().Should().Be(-32000);
        error.GetProperty("message").GetString().Should().Be("No profile is being recorded");
    }

    /// <summary>A client that lost track of its own state does not lose its recording over it.</summary>
    [Test]
    public async Task StartingTwiceKeepsTheFirstRecording()
    {
        await using var session = await AttachedSession.CreateAsync();
        await session.Target.PostAsync(engine => engine.Execute(Source, "main.js"));

        await session.ResultAsync("Profiler.enable");
        await session.ResultAsync("Profiler.start");
        await session.Target.PostAsync(engine => engine.Evaluate(Workload));
        await session.ResultAsync("Profiler.start");

        var profile = await StopAsync(session);
        profile.Nodes.Select(node => node.CallFrame.FunctionName).Should().Contain("work", "the first recording was still running");
    }

    /// <summary>
    /// The rate a client asks for is what the sampler is armed with, which is what the command is for.
    /// </summary>
    [Test]
    public async Task ASamplingIntervalIsAccepted()
    {
        await using var session = await AttachedSession.CreateAsync();

        await session.ResultAsync("Profiler.enable");
        await session.ResultAsync("Profiler.setSamplingInterval", """{"interval":100}""");
        await session.ResultAsync("Profiler.start");

        (await session.Target.PostAsync(engine => engine.Diagnostics.IsSampling)).Should().BeTrue();

        var profile = await StopAsync(session);
        profile.Nodes.Should().NotBeEmpty();
    }

    /// <summary>
    /// What a Performance panel is opened for: a script that spends its time in one function is a profile
    /// that says so.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The assertion is on the time deltas rather than on the count of samples, because that is what the
    /// panel adds up per node — and it is what makes this a statement about where the time went rather than
    /// about how often the instrument happened to fire.
    /// </para>
    /// <para>
    /// It is about the <em>script's</em> time. A recording is bracketed by two protocol commands, so the
    /// wall clock between them includes the round trips that carried them, and that time belongs to
    /// <c>(program)</c> — correctly, and in a proportion no test can pin.
    /// </para>
    /// </remarks>
    [Test]
    public async Task ACpuBoundScriptAttributesMostOfItsTimeToItsHotFunction()
    {
        await using var session = await AttachedSession.CreateAsync();
        await session.Target.PostAsync(engine => engine.Execute(Source, "main.js"));

        var profile = await RecordAsync(session, DominantWorkload);

        var byId = profile.Nodes.ToDictionary(node => node.Id, node => node.CallFrame.FunctionName);
        var byFunction = new Dictionary<string, long>(StringComparer.Ordinal);

        for (var i = 0; i < profile.Samples!.Length; i++)
        {
            var name = byId[profile.Samples[i]];
            byFunction[name] = byFunction.GetValueOrDefault(name) + profile.TimeDeltas![i];
        }

        var script = byFunction
            .Where(entry => entry.Key is not "(program)" and not "(root)")
            .OrderByDescending(entry => entry.Value)
            .ToList();

        var total = script.Sum(entry => entry.Value);
        total.Should().BeGreaterThan(0, "the script ran, so some of the recording is its");

        script[0].Key.Should().Be("inner", "that is the function the loop is in");
        script[0].Value.Should().BeGreaterThan(total / 2, "most of the script's time was spent there");
    }

    /// <summary>
    /// Disabling ends a recording nobody is going to stop, which is a cost the engine would keep paying.
    /// </summary>
    [Test]
    public async Task DisablingEndsARecordingInFlight()
    {
        await using var session = await AttachedSession.CreateAsync();

        await session.ResultAsync("Profiler.enable");
        await session.ResultAsync("Profiler.start");
        await session.ResultAsync("Profiler.disable");

        (await session.Target.PostAsync(engine => engine.Diagnostics.IsSampling)).Should().BeFalse();

        var error = await session.ErrorAsync("Profiler.stop");
        error.GetProperty("message").GetString().Should().Be("No profile is being recorded");
    }

    /// <summary>
    /// The engine allows one sampling session, and it may be the host's own — a client that arrives then is
    /// given the exact profiler rather than a refusal.
    /// </summary>
    [Test]
    public async Task AHostAlreadySamplingLeavesTheClientTheOtherInstrument()
    {
        await using var session = await AttachedSession.CreateAsync();
        await session.Target.PostAsync(engine => engine.Execute(Source, "main.js"));
        await session.Target.PostAsync(engine => engine.Diagnostics.StartSampling());

        await session.ResultAsync("Profiler.enable");
        await session.ResultAsync("Profiler.start");
        await session.Target.PostAsync(engine => engine.Evaluate(Workload));

        var profile = await StopAsync(session);
        profile.Nodes.Select(node => node.CallFrame.FunctionName).Should().Contain("inner");

        // the host's own session is untouched by the one the client asked for
        (await session.Target.PostAsync(engine => engine.Diagnostics.IsSampling)).Should().BeTrue();
        await session.Target.PostAsync(engine => engine.Diagnostics.StopSampling());
    }

    /// <summary>Records <paramref name="expression"/> and hands back the profile of it.</summary>
    /// <remarks>
    /// The rate is the fastest the protocol can name — a client's zero means "as fast as you can" — because
    /// a workload short enough for a unit test has to be observed more than once.
    /// </remarks>
    private static async Task<Profile> RecordAsync(AttachedSession session, string expression)
    {
        await session.ResultAsync("Profiler.enable").ConfigureAwait(false);
        await session.ResultAsync("Profiler.setSamplingInterval", """{"interval":0}""").ConfigureAwait(false);
        await session.ResultAsync("Profiler.start").ConfigureAwait(false);
        await session.Target.PostAsync(engine => engine.Evaluate(expression)).ConfigureAwait(false);

        return await StopAsync(session).ConfigureAwait(false);
    }

    /// <summary>
    /// Stops the recording and reads the reply back through the generated data transfer objects.
    /// </summary>
    /// <remarks>
    /// Deserializing rather than picking the JSON apart is the point: the types are generated from the
    /// pinned <c>js_protocol.json</c>, so a reply that does not fit them is a reply a client would reject,
    /// and <c>required</c> members make a missing one a failure rather than a default.
    /// </remarks>
    private static async Task<Profile> StopAsync(AttachedSession session)
    {
        var result = await session.ResultAsync("Profiler.stop").ConfigureAwait(false);
        var profile = result.GetProperty("profile").Deserialize(ProtocolJsonContext.Default.ProfilerProfile);

        profile.Should().NotBeNull();
        return profile!;
    }
}
