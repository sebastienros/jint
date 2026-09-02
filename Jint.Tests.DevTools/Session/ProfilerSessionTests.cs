using System.Text.Json;
using Jint.DevTools.Protocol;
using Jint.DevTools.Protocol.Profiler;

namespace Jint.Tests.DevTools.Session;

/// <summary>
/// <c>Profiler.start</c> and <c>Profiler.stop</c>: the document a front end's Performance panel loads.
/// </summary>
/// <remarks>
/// The assertions are about the two halves the panel reads — a tree of nodes, and a series of samples with
/// the time between them — plus the one property that makes the second half meaningful: the deltas add up to
/// the recording. A profile whose tree is right and whose deltas are not renders as an empty flame chart.
/// </remarks>
[NonParallelizable]
public class ProfilerSessionTests
{
    private const string Source = """
        function inner(n) {
            return n * 2;
        }

        function outer(n) {
            return inner(n) + inner(n + 1);
        }

        function work(times) {
            var total = 0;
            for (var i = 0; i < times; i++) {
                total = total + outer(i);
            }

            return total;
        }
        """;

    [Test]
    public async Task AProfileHasANodeForEveryFunctionThatRan()
    {
        await using var session = await AttachedSession.CreateAsync();
        await session.EnableDebuggerAsync();
        await session.Target.PostAsync(engine => engine.Execute(Source, "main.js"));

        var scriptId = (await session.EventAsync("Debugger.scriptParsed")).GetProperty("scriptId").GetString();

        var profile = await RecordAsync(session, "work(50)");

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

        var profile = await RecordAsync(session, "work(200)");

        profile.Samples.Should().NotBeNull();
        profile.TimeDeltas.Should().NotBeNull();
        profile.Samples!.Length.Should().Be(profile.TimeDeltas!.Length, "the panel reads them as one series");
        profile.EndTime.Should().BeGreaterThanOrEqualTo(profile.StartTime);

        // The source records when every call happened rather than sampling for it, so this is an equality
        // and not an approximation: the only loss is the truncation of each interval to whole microseconds.
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
        await session.Target.PostAsync(engine => engine.Evaluate("work(10)"));
        await session.ResultAsync("Profiler.start");

        var profile = await StopAsync(session);
        profile.Nodes.Select(node => node.CallFrame.FunctionName).Should().Contain("work", "the first recording was still running");
    }

    /// <summary>
    /// The rate a client asks for is accepted, because every front end sends it before every recording.
    /// </summary>
    [Test]
    public async Task ASamplingIntervalIsAccepted()
    {
        await using var session = await AttachedSession.CreateAsync();

        await session.ResultAsync("Profiler.enable");
        await session.ResultAsync("Profiler.setSamplingInterval", """{"interval":100}""");
        await session.ResultAsync("Profiler.start");

        var profile = await StopAsync(session);
        profile.Nodes.Should().NotBeEmpty("the profiler answers whatever rate it was told, having none of its own");
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

        (await session.Target.PostAsync(engine => engine.Diagnostics.IsProfiling)).Should().BeFalse();

        var error = await session.ErrorAsync("Profiler.stop");
        error.GetProperty("message").GetString().Should().Be("No profile is being recorded");
    }

    /// <summary>Records <paramref name="expression"/> and hands back the profile of it.</summary>
    private static async Task<Profile> RecordAsync(AttachedSession session, string expression)
    {
        await session.ResultAsync("Profiler.enable").ConfigureAwait(false);
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
