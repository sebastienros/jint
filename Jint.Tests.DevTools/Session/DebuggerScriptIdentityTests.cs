using System.Text.Json;
using Jint.DevTools.Protocol;
using Jint.DevTools.Protocol.Profiler;

namespace Jint.Tests.DevTools.Session;

/// <summary>
/// Which script a position belongs to, when the source name cannot say.
/// </summary>
/// <remarks>
/// Every <c>engine.Execute(code)</c> given no source argument is parsed under one name, so a server matching
/// a position back to a script by name answers for all of them at once. The engine hands the program itself
/// to a call frame, a profile frame and a coverage source, and this suite is what says the registry looks it
/// up rather than guessing.
/// </remarks>
[NonParallelizable]
public class DebuggerScriptIdentityTests
{
    /// <summary>
    /// Declares the function and stops in it, without calling it — so the pause happens later, while a
    /// second script of the same name is the newest one.
    /// </summary>
    private const string Definition = """
        function work() {
            debugger;
            return 1;
        }
        """;

    /// <summary>
    /// The caller, parsed under the same name and <em>longer</em> than the definition, so the position the
    /// pause happens at lies inside this script's range as well. That is the whole of what a name-and-range
    /// match has to go on, and it picks the newest.
    /// </summary>
    private const string Caller = """
        // A second script under the one name every sourceless Execute is parsed with.
        // It is deliberately taller and wider than the definition above, so that every
        // position in that one is inside this one's range too.
        work();
        """;

    [Test]
    public async Task TwoSourcelessScriptsWithOverlappingPositionsKeepTheirOwnScriptIds()
    {
        await using var session = await AttachedSession.CreateAsync();
        await session.EnableDebuggerAsync();

        var running = session.Target.PostAsync(engine =>
        {
            engine.Execute(Definition);
            engine.Execute(Caller);
        });

        var paused = await session.EventAsync("Debugger.paused");
        await session.ResultAsync("Debugger.resume");
        await running;

        var parsed = session.EventsOf("Debugger.scriptParsed")
            .Select(sent => sent.GetProperty("params"))
            .ToList();

        var announced = parsed.Select(script => script.GetProperty("scriptId").GetString()).ToList();

        announced.Should().HaveCount(2, "two parses are two scripts, whatever they were named");
        announced.Should().OnlyHaveUniqueItems();

        parsed.Select(script => script.GetProperty("url").GetString())
            .Distinct().Should().ContainSingle("both were parsed under the one name a sourceless Execute gets");

        var frames = paused.GetProperty("callFrames").EnumerateArray().ToList();
        frames.Should().HaveCount(2);

        // The two frames of one pause belong to two different scripts of one name, which is the answer a
        // match on that name cannot give.
        ScriptIdOf(frames[0]).Should().Be(announced[0], "the function it stopped in was declared in the first script");
        ScriptIdOf(frames[1]).Should().Be(announced[1], "the top-level frame is running the second");

        // the frame's function was declared in the program the frame is running
        FunctionScriptIdOf(frames[0]).Should().Be(announced[0]);
    }

    /// <summary>
    /// The other half of the same seam: a cached program is one script however often it runs, so a client
    /// stepping through a second run addresses the frames by the identifier it already has.
    /// </summary>
    [Test]
    public async Task APreparedScriptRunTwiceIsOneScriptInEveryPause()
    {
        await using var session = await AttachedSession.CreateAsync();
        await session.EnableDebuggerAsync();

        var prepared = Engine.PrepareScript(Definition + "\nwork();");

        var running = session.Target.PostAsync(engine =>
        {
            engine.Execute(prepared);
            engine.Execute(prepared);
        });

        var first = await session.EventAsync("Debugger.paused");
        await session.ResultAsync("Debugger.resume");

        var second = await session.EventAsync("Debugger.paused", index: 1);
        await session.ResultAsync("Debugger.resume");

        await running;

        session.EventsOf("Debugger.scriptParsed").Should().HaveCount(1);
        ScriptIdOf(TopFrame(second)).Should().Be(ScriptIdOf(TopFrame(first)));
    }

    /// <summary>
    /// Coverage names the parse it counted, so two runs of one text are two scripts in the Coverage panel
    /// rather than one whose counts are the sum.
    /// </summary>
    [Test]
    public async Task CoverageReportsEachParseUnderItsOwnScript()
    {
        await using var session = await AttachedSession.CreateAsync(
            configureOptions: options => options.Coverage.Enabled = true);

        await session.ResultAsync("Profiler.startPreciseCoverage", """{"callCount":true,"detailed":false}""");
        await session.Target.PostAsync(engine =>
        {
            engine.Execute("function used() { return 1; } used();");
            engine.Execute("function used() { return 1; } used();");
        });

        var result = await session.ResultAsync("Profiler.takePreciseCoverage");
        var scripts = result.GetProperty("result").Deserialize(ProtocolJsonContext.Default.ProfilerScriptCoverageArray)!;

        var unattributed = scripts.Where(script => script.ScriptId == "0").ToList();
        unattributed.Should().BeEmpty("every parse was announced, so every source resolves");

        scripts.Should().HaveCount(2);
        scripts.Select(script => script.ScriptId).Should().OnlyHaveUniqueItems();
        scripts.Select(script => script.Url).Distinct().Should().ContainSingle();
        scripts.Should().AllSatisfy(script =>
            script.Functions.Single(function => function.FunctionName == "used").Ranges[0].Count.Should().Be(1));
    }

    /// <summary>
    /// A function value names the script it was declared in, which two parses of one text is the case no
    /// source name can answer: both are <c>&lt;anonymous&gt;</c>, and both ranges hold the other's positions.
    /// </summary>
    [Test]
    public async Task TwoFunctionsFromTwoParsesOfOneTextCarryTheirOwnFunctionLocation()
    {
        // Every sourceless Execute is parsed under one name, and these two parses are character for
        // character the same text - so the declaration's own position is inside both ranges and a match on
        // name and range can only ever answer the newest.
        const string Declaration = "globalThis.fns = globalThis.fns || []; fns.push(function work() { return 1; });";

        await using var session = await AttachedSession.CreateAsync();
        await session.EnableDebuggerAsync();

        await session.Target.PostAsync(engine =>
        {
            engine.Execute(Declaration);
            engine.Execute(Declaration);
        });

        var announced = session.EventsOf("Debugger.scriptParsed")
            .Select(sent => sent.GetProperty("params").GetProperty("scriptId").GetString())
            .ToList();

        announced.Should().HaveCount(2, "two parses are two scripts, whatever they were named");

        (await FunctionLocationScriptIdAsync(session, "fns[0]")).Should().Be(
            announced[0], "the first function was declared in the first parse");
        (await FunctionLocationScriptIdAsync(session, "fns[1]")).Should().Be(
            announced[1], "the second function was declared in the second parse");
    }

    private static async Task<string?> FunctionLocationScriptIdAsync(AttachedSession session, string expression)
    {
        var handle = await session.HandleAsync(expression);
        var properties = await session.PropertiesAsync(handle, ownProperties: true);

        return properties.Internal("[[FunctionLocation]]")
            .GetProperty("value").GetProperty("value").GetProperty("scriptId").GetString();
    }

    private static string? ScriptIdOf(JsonElement frame)
        => frame.GetProperty("location").GetProperty("scriptId").GetString();

    private static string? FunctionScriptIdOf(JsonElement frame)
        => frame.GetProperty("functionLocation").GetProperty("scriptId").GetString();

    private static JsonElement TopFrame(JsonElement paused)
        => paused.GetProperty("callFrames").EnumerateArray().First();
}
