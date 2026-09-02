using System.Text.Json;
using Jint.DevTools;

namespace Jint.Tests.DevTools.Session;

/// <summary>
/// What a client is told about the scripts an engine has parsed, and what it can read back out of them.
/// </summary>
/// <remarks>
/// The Sources panel of a front end is built from the replay <c>Debugger.enable</c> performs, so the two
/// properties worth pinning are that a script parsed <i>before</i> the client asked still reaches it, and
/// that a script parsed afterwards does too.
/// </remarks>
[NonParallelizable]
public class DebuggerScriptTests
{
    private const string Source = """
        function add(a, b) {
            var sum = a + b;
            return sum;
        }
        var total = add(2, 3);
        """;

    [Test]
    public async Task EnableReplaysWhatTheEngineAlreadyParsed()
    {
        await using var session = await AttachedSession.CreateAsync();

        await session.Target.PostAsync(engine => engine.Execute(Source, "main.js"));
        await session.EnableDebuggerAsync();

        var parsed = await session.EventAsync("Debugger.scriptParsed");

        parsed.GetProperty("url").GetString().Should().Be("main.js");
        parsed.GetProperty("scriptId").GetString().Should().NotBeNullOrEmpty();
        parsed.GetProperty("startLine").GetInt32().Should().Be(0);
        parsed.GetProperty("endLine").GetInt32().Should().Be(4);
        parsed.GetProperty("executionContextId").GetInt32().Should().Be(1);
        parsed.GetProperty("hash").GetString().Should().NotBeNullOrEmpty();
        parsed.GetProperty("length").GetInt32().Should().Be(Source.Length);
        parsed.GetProperty("isModule").GetBoolean().Should().BeFalse();
        parsed.GetProperty("scriptLanguage").GetString().Should().Be("JavaScript");
    }

    [Test]
    public async Task AScriptRunAfterEnableIsAnnouncedOnce()
    {
        await using var session = await AttachedSession.CreateAsync();
        await session.EnableDebuggerAsync();

        var prepared = Engine.PrepareScript("var once = 1;", "prepared.js");

        await session.Target.PostAsync(engine => engine.Execute(prepared));
        await session.Target.PostAsync(engine => engine.Execute(prepared));

        var parsed = await session.EventAsync("Debugger.scriptParsed");
        parsed.GetProperty("url").GetString().Should().Be("prepared.js");

        // A cached program run twice is one script: the registry keys on the program itself, so the second
        // run answers the identifier the first one minted rather than announcing a script that is not new.
        session.EventsOf("Debugger.scriptParsed").Should().HaveCount(1);
    }

    [Test]
    public async Task GetScriptSourceAnswersTheTextTheParseRetained()
    {
        await using var session = await AttachedSession.CreateAsync();

        await session.Target.PostAsync(engine => engine.Execute(Source, "main.js"));
        await session.EnableDebuggerAsync();

        var scriptId = (await session.EventAsync("Debugger.scriptParsed")).GetProperty("scriptId").GetString();
        var result = await session.ResultAsync("Debugger.getScriptSource", $$"""{"scriptId":"{{scriptId}}"}""");

        result.GetProperty("scriptSource").GetString().Should().Be(Source);
    }

    [Test]
    public async Task GetScriptSourceSaysSoWhenNothingWasRetained()
    {
        await using var session = await AttachedSession.CreateAsync();
        await session.EnableDebuggerAsync();

        // A prepared script follows the parsing options it was prepared with, and those do not retain source
        // text by default -- which is the case a host meets without doing anything wrong.
        var prepared = Engine.PrepareScript("var quiet = 1;", "prepared.js");
        await session.Target.PostAsync(engine => engine.Execute(prepared));

        var scriptId = (await session.EventAsync("Debugger.scriptParsed")).GetProperty("scriptId").GetString();
        var error = await session.ErrorAsync("Debugger.getScriptSource", $$"""{"scriptId":"{{scriptId}}"}""");

        error.GetProperty("code").GetInt32().Should().Be(-32000);
        error.GetProperty("message").GetString().Should()
            .Be("No source text was retained for this script; enable Options.RetainFunctionSourceText");
    }

    [Test]
    public async Task GetScriptSourceRefusesAnIdentifierThatNamesNothing()
    {
        await using var session = await AttachedSession.CreateAsync();
        await session.EnableDebuggerAsync();

        var error = await session.ErrorAsync("Debugger.getScriptSource", """{"scriptId":"nope"}""");

        error.GetProperty("code").GetInt32().Should().Be(-32000);
        error.GetProperty("message").GetString().Should().Be("No script with given id");
    }

    /// <summary>
    /// The positions the walk answers are the positions the engine stops at, which is the whole point of
    /// asking.
    /// </summary>
    [Test]
    public async Task GetPossibleBreakpointsAnswersWhereTheEngineStops()
    {
        await using var session = await AttachedSession.CreateAsync();

        await session.Target.PostAsync(engine => engine.Execute(Source, "main.js"));
        await session.EnableDebuggerAsync();

        var scriptId = (await session.EventAsync("Debugger.scriptParsed")).GetProperty("scriptId").GetString();

        var result = await session.ResultAsync(
            "Debugger.getPossibleBreakpoints",
            $$$"""{"start":{"scriptId":"{{{scriptId}}}","lineNumber":0,"columnNumber":0}}""");

        var locations = result.GetProperty("locations").EnumerateArray().ToArray();
        locations.Should().NotBeEmpty();

        foreach (var location in locations)
        {
            location.GetProperty("scriptId").GetString().Should().Be(scriptId);
        }

        // `var sum = a + b;` is indented by four, and the position the engine visits is the statement rather
        // than the start of the line.
        locations.Should().Contain(location =>
            location.GetProperty("lineNumber").GetInt32() == 1 &&
            location.GetProperty("columnNumber").GetInt32() == 4);

        // The implicit return point at the end of the function body, which the protocol has a name for.
        locations.Select(location => location.Optional("type")?.GetString()).Should().Contain("return");
    }

    [Test]
    public async Task GetPossibleBreakpointsBoundsTheRangeItWasGiven()
    {
        await using var session = await AttachedSession.CreateAsync();

        await session.Target.PostAsync(engine => engine.Execute(Source, "main.js"));
        await session.EnableDebuggerAsync();

        var scriptId = (await session.EventAsync("Debugger.scriptParsed")).GetProperty("scriptId").GetString();

        var result = await session.ResultAsync(
            "Debugger.getPossibleBreakpoints",
            $$$"""
            {"start":{"scriptId":"{{{scriptId}}}","lineNumber":1,"columnNumber":0},
             "end":{"scriptId":"{{{scriptId}}}","lineNumber":2,"columnNumber":0}}
            """);

        var locations = result.GetProperty("locations").EnumerateArray().ToArray();
        locations.Should().HaveCount(1);
        locations[0].GetProperty("lineNumber").GetInt32().Should().Be(1);
    }

    [Test]
    public async Task GetPossibleBreakpointsRefusesARangeSpanningTwoScripts()
    {
        await using var session = await AttachedSession.CreateAsync();

        await session.Target.PostAsync(engine => engine.Execute(Source, "main.js"));
        await session.EnableDebuggerAsync();

        var scriptId = (await session.EventAsync("Debugger.scriptParsed")).GetProperty("scriptId").GetString();

        var error = await session.ErrorAsync(
            "Debugger.getPossibleBreakpoints",
            $$$"""
            {"start":{"scriptId":"{{{scriptId}}}","lineNumber":0,"columnNumber":0},
             "end":{"scriptId":"other","lineNumber":9,"columnNumber":0}}
            """);

        error.GetProperty("message").GetString().Should().Be("Locations should contain the same scriptId");
    }

    /// <summary>
    /// One engine, one debugging client. Breakpoints and the step mode are the engine's, so a second session
    /// enabling the domain is told rather than silently sharing the first one's.
    /// </summary>
    [Test]
    public async Task ASecondSessionsDebuggerEnableIsRefused()
    {
        await using var session = await AttachedSession.CreateAsync();
        await session.EnableDebuggerAsync();

        await using var other = session.Protocol.OpenSecondConversation();
        var second = await other.AttachAsync(session.Target);
        var reply = await other.SendAsync("Debugger.enable", null, second);

        reply.TryGetProperty("error", out var error).Should().BeTrue("a second Debugger.enable is refused, and it answered {0}", reply);
        error.GetProperty("code").GetInt32().Should().Be(-32000);
        error.GetProperty("message").GetString().Should()
            .Be("Another session already has the Debugger domain enabled on this target");
    }

    /// <summary>
    /// A second session may enable the domain once the first has given it back, which is what makes the
    /// refusal a queue rather than a wall.
    /// </summary>
    [Test]
    public async Task ASecondSessionMayDebugOnceTheFirstHasDisabled()
    {
        await using var session = await AttachedSession.CreateAsync();
        await session.EnableDebuggerAsync();
        await session.ResultAsync("Debugger.disable");

        await using var other = session.Protocol.OpenSecondConversation();
        var second = await other.AttachAsync(session.Target);
        var reply = await other.SendAsync("Debugger.enable", null, second);

        reply.TryGetProperty("error", out var error).Should().BeFalse("the first session gave the debugger back, and it answered {0}", error);
    }

    /// <summary>
    /// An engine the host did not build with the debugger cannot be paused, and says which switch is missing
    /// rather than answering something untrue.
    /// </summary>
    [Test]
    public async Task EnableIsRefusedOnAnEngineWithoutTheDebugger()
    {
        await using var protocol = ProtocolSession.Create();
        var target = protocol.AddTarget(new EngineTargetOptions { ThreadMode = ThreadMode.LibraryOwned }, new Engine());
        var sessionId = await protocol.AttachAsync(target);

        var reply = await protocol.SendAsync("Debugger.enable", null, sessionId);

        reply.TryGetProperty("error", out var error).Should().BeTrue("the engine has no debugger, and it answered {0}", reply);
        error.GetProperty("code").GetInt32().Should().Be(-32000);
        error.GetProperty("message").GetString().Should().Be("The engine was not built with the debugger enabled");
    }

    /// <summary>
    /// The front end sends these while connecting and reads nothing back but the success; refusing one would
    /// make an ordinary connection fail over a feature the client can do without.
    /// </summary>
    [TestCase("Debugger.setAsyncCallStackDepth", """{"maxDepth":32}""")]
    [TestCase("Debugger.setBlackboxPatterns", """{"patterns":["/node_modules/"]}""")]
    [TestCase("Debugger.setBlackboxedRanges", """{"scriptId":"1.1","positions":[]}""")]
    [TestCase("Debugger.setPauseOnExceptions", """{"state":"uncaught"}""")]
    [TestCase("Debugger.setInstrumentationBreakpoint", """{"instrumentation":"beforeScriptExecution"}""")]
    public async Task TheConnectHandshakeIsAnswered(string method, string parameters)
    {
        await using var session = await AttachedSession.CreateAsync();
        await session.EnableDebuggerAsync();

        var reply = await session.SendAsync(method, parameters);
        reply.TryGetProperty("error", out var error).Should().BeFalse("'{0}' is what a front end sends on connect, and it answered {1}", method, error);
    }

    [Test]
    public async Task SetPauseOnExceptionsKeepsWhatTheClientSet()
    {
        await using var session = await AttachedSession.CreateAsync();
        await session.EnableDebuggerAsync();

        await session.ResultAsync("Debugger.setPauseOnExceptions", """{"state":"all"}""");

        session.Target.ActiveDebugger!.PauseOnExceptions.Should().Be("all");

        var error = await session.ErrorAsync("Debugger.setPauseOnExceptions", """{"state":"sometimes"}""");
        error.GetProperty("message").GetString().Should().Be("Unknown pause on exceptions mode: sometimes");
    }
}
