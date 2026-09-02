using System.Text.Json;

namespace Jint.Tests.DevTools.Session;

/// <summary>
/// Breakpoint → paused → look around → step → resumed, over the mailbox and the pause loop.
/// </summary>
/// <remarks>
/// <para>
/// Every test here starts the script with <c>EngineTarget.PostAsync</c> and does <b>not</b> wait for it:
/// the script is what pauses, so a test that awaited it before resuming would be waiting for itself. What it
/// waits for instead is the <c>Debugger.paused</c> event, which is what a real client waits for too.
/// </para>
/// <para>
/// Every wait is bounded. A pause that never arrives, or a resume that never takes, is the defect this suite
/// exists to catch, and a test that hangs on one reports nothing.
/// </para>
/// </remarks>
[NonParallelizable]
public class DebuggerBreakpointTests
{
    /// <summary>
    /// Line 0 is <c>function add</c>, line 1 the indented <c>var sum</c>, line 2 the indented
    /// <c>return sum</c>, line 3 the closing brace and line 4 the call.
    /// </summary>
    private const string Source = """
        function add(a, b) {
            var sum = a + b;
            return sum;
        }
        var total = add(2, 3);
        """;

    /// <summary>
    /// The whole of what P4 delivers, in the order a client does it.
    /// </summary>
    [Test]
    public async Task ABreakpointPausesAndTheClientCanLookAroundStepAndResume()
    {
        await using var session = await AttachedSession.CreateAsync();
        await session.EnableDebuggerAsync();

        // Set before the script exists, which is what a client that means to debug a run actually does.
        var set = await session.ResultAsync(
            "Debugger.setBreakpointByUrl",
            """{"url":"main.js","lineNumber":2,"columnNumber":0}""");

        var breakpointId = set.GetProperty("breakpointId").GetString();
        breakpointId.Should().NotBeNullOrEmpty();
        set.GetProperty("locations").GetArrayLength().Should().Be(0, "no script matches the url yet");

        var running = session.Target.PostAsync(engine => engine.Execute(Source, "main.js"));

        // The script is parsed, so the pending request is placed and the client is told where.
        var resolved = await session.EventAsync("Debugger.breakpointResolved");
        resolved.GetProperty("breakpointId").GetString().Should().Be(breakpointId);
        resolved.GetProperty("location").GetProperty("lineNumber").GetInt32().Should().Be(2);

        // A column of zero on an indented line snaps onto the statement.
        resolved.GetProperty("location").GetProperty("columnNumber").GetInt32().Should().Be(4);

        var paused = await session.EventAsync("Debugger.paused");
        paused.GetProperty("reason").GetString().Should().Be("other");
        paused.GetProperty("hitBreakpoints").EnumerateArray().Select(id => id.GetString()).Should().Equal(breakpointId);

        var frames = paused.GetProperty("callFrames").EnumerateArray().ToArray();
        frames.Should().HaveCount(2, "the stack is `add` called from the global frame");
        frames[0].GetProperty("functionName").GetString().Should().Be("add");
        frames[0].GetProperty("url").GetString().Should().Be("main.js");
        frames[0].GetProperty("location").GetProperty("lineNumber").GetInt32().Should().Be(2);
        frames[1].GetProperty("functionName").GetString().Should().Be("", "the global frame has no name in the protocol");

        var callFrameId = frames[0].GetProperty("callFrameId").GetString();
        callFrameId.Should().NotBeNullOrEmpty();

        // The scope chain: the local scope first, and every entry addressable.
        var scopes = frames[0].GetProperty("scopeChain").EnumerateArray().ToArray();
        scopes[0].GetProperty("type").GetString().Should().Be("local");

        var localScope = scopes[0].GetProperty("object").GetProperty("objectId").GetString()!;
        var locals = await session.PropertiesAsync(localScope, ownProperties: true);
        locals.Names().Should().Contain(["a", "b", "sum"]);
        locals.Property("sum").GetProperty("value").GetProperty("value").GetInt32().Should().Be(5);

        scopes.Select(scope => scope.GetProperty("type").GetString()).Should().Contain("global");

        // Evaluating while paused sees the frame the client is looking at, not the global scope.
        var evaluated = await session.EvaluateAsync("sum + 1", returnByValue: true);
        evaluated.GetProperty("value").GetInt32().Should().Be(6);

        // evaluateOnCallFrame reads the same environment.
        var onFrame = await session.ResultAsync(
            "Debugger.evaluateOnCallFrame",
            $$"""{"callFrameId":"{{callFrameId}}","expression":"a * b","returnByValue":true}""");
        onFrame.GetProperty("result").GetProperty("value").GetInt32().Should().Be(6);

        // Writing a binding writes through to the engine, which the script's own result proves.
        await session.ResultAsync(
            "Debugger.setVariableValue",
            $$"""{"scopeNumber":0,"variableName":"sum","newValue":{"value":99},"callFrameId":"{{callFrameId}}"}""");

        (await session.EvaluateAsync("sum", returnByValue: true)).GetProperty("value").GetInt32().Should().Be(99);

        // Stepping over the return statement stops again, at the function's return point.
        await session.ResultAsync("Debugger.stepOver");

        var stepped = await session.EventAsync("Debugger.paused", index: 1);
        stepped.GetProperty("callFrames").EnumerateArray().First().GetProperty("functionName").GetString().Should().Be("add");

        await session.ResultAsync("Debugger.resume");
        await session.EventAsync("Debugger.resumed", index: 1);

        await running;

        // What the debugger wrote is what the script returned.
        var total = await session.EvaluateAsync("total", returnByValue: true);
        total.GetProperty("value").GetInt32().Should().Be(99);
    }

    /// <summary>
    /// A handle onto a paused frame belongs to that pause, and the client is told so rather than shown a
    /// scope of a frame that no longer exists.
    /// </summary>
    [Test]
    public async Task ScopeHandlesAreReleasedOnResume()
    {
        await using var session = await AttachedSession.CreateAsync();
        await session.EnableDebuggerAsync();
        await session.ResultAsync("Debugger.setBreakpointByUrl", """{"url":"main.js","lineNumber":2}""");

        var running = session.Target.PostAsync(engine => engine.Execute(Source, "main.js"));
        var paused = await session.EventAsync("Debugger.paused");

        var localScope = paused.GetProperty("callFrames").EnumerateArray().First()
            .GetProperty("scopeChain").EnumerateArray().First()
            .GetProperty("object").GetProperty("objectId").GetString()!;

        var callFrameId = paused.GetProperty("callFrames").EnumerateArray().First().GetProperty("callFrameId").GetString();

        await session.ResultAsync("Debugger.resume");
        await running;

        var error = await session.ErrorAsync("Runtime.getProperties", $$"""{"objectId":"{{localScope}}"}""");
        error.GetProperty("message").GetString().Should().Be("Could not find object with given id");

        var stale = await session.ErrorAsync(
            "Debugger.evaluateOnCallFrame",
            $$"""{"callFrameId":"{{callFrameId}}","expression":"1"}""");
        stale.GetProperty("message").GetString().Should().Be("Can only perform operation while paused.");
    }

    [Test]
    public async Task AConditionalBreakpointOnlyStopsWhenItsConditionHolds()
    {
        await using var session = await AttachedSession.CreateAsync();
        await session.EnableDebuggerAsync();

        await session.ResultAsync(
            "Debugger.setBreakpointByUrl",
            """{"url":"loop.js","lineNumber":1,"condition":"i === 3"}""");

        var running = session.Target.PostAsync(engine => engine.Execute(
            """
            for (var i = 0; i < 5; i++) {
                var seen = i;
            }
            """,
            "loop.js"));

        var paused = await session.EventAsync("Debugger.paused");
        var callFrameId = paused.GetProperty("callFrames").EnumerateArray().First().GetProperty("callFrameId").GetString();

        var seen = await session.ResultAsync(
            "Debugger.evaluateOnCallFrame",
            $$"""{"callFrameId":"{{callFrameId}}","expression":"i","returnByValue":true}""");
        seen.GetProperty("result").GetProperty("value").GetInt32().Should().Be(3);

        await session.ResultAsync("Debugger.resume");
        await running;

        session.EventsOf("Debugger.paused").Should().HaveCount(1, "the condition held exactly once");
    }

    [Test]
    public async Task RemovingABreakpointStopsItPausing()
    {
        await using var session = await AttachedSession.CreateAsync();
        await session.EnableDebuggerAsync();

        var set = await session.ResultAsync("Debugger.setBreakpointByUrl", """{"url":"main.js","lineNumber":2}""");
        var breakpointId = set.GetProperty("breakpointId").GetString();

        await session.ResultAsync("Debugger.removeBreakpoint", $$"""{"breakpointId":"{{breakpointId}}"}""");

        await session.Target.PostAsync(engine => engine.Execute(Source, "main.js"));

        session.EventsOf("Debugger.paused").Should().BeEmpty();
    }

    [Test]
    public async Task SettingTheSameBreakpointTwiceIsRefused()
    {
        await using var session = await AttachedSession.CreateAsync();
        await session.EnableDebuggerAsync();

        await session.ResultAsync("Debugger.setBreakpointByUrl", """{"url":"main.js","lineNumber":2}""");
        var error = await session.ErrorAsync("Debugger.setBreakpointByUrl", """{"url":"main.js","lineNumber":2}""");

        error.GetProperty("message").GetString().Should().Be("Breakpoint at specified location already exists.");
    }

    [Test]
    public async Task SetBreakpointByUrlNeedsOneOfTheThreeSelectors()
    {
        await using var session = await AttachedSession.CreateAsync();
        await session.EnableDebuggerAsync();

        var error = await session.ErrorAsync("Debugger.setBreakpointByUrl", """{"lineNumber":2}""");
        error.GetProperty("message").GetString().Should().Be("Either url or urlRegex must be specified.");
    }

    [Test]
    public async Task ABreakpointMayNameItsScriptByPattern()
    {
        await using var session = await AttachedSession.CreateAsync();
        await session.EnableDebuggerAsync();
        await session.ResultAsync("Debugger.setBreakpointByUrl", """{"urlRegex":"^ma.n\\.js$","lineNumber":2}""");

        var running = session.Target.PostAsync(engine => engine.Execute(Source, "main.js"));
        await session.EventAsync("Debugger.paused");

        await session.ResultAsync("Debugger.resume");
        await running;
    }

    /// <summary>
    /// A breakpoint set against a script that already exists is placed straight away, and the client is told
    /// where the engine will really stop.
    /// </summary>
    [Test]
    public async Task SetBreakpointPlacesAgainstOneScriptAndSnapsIt()
    {
        await using var session = await AttachedSession.CreateAsync();
        await session.Target.PostAsync(engine => engine.Execute(Source, "main.js"));
        await session.EnableDebuggerAsync();

        var scriptId = (await session.EventAsync("Debugger.scriptParsed")).GetProperty("scriptId").GetString();

        var set = await session.ResultAsync(
            "Debugger.setBreakpoint",
            $$$"""{"location":{"scriptId":"{{{scriptId}}}","lineNumber":1,"columnNumber":0}}""");

        var actual = set.GetProperty("actualLocation");
        actual.GetProperty("scriptId").GetString().Should().Be(scriptId);
        actual.GetProperty("lineNumber").GetInt32().Should().Be(1);
        actual.GetProperty("columnNumber").GetInt32().Should().Be(4);
    }

    [Test]
    public async Task SetBreakpointsActiveFalseRunsStraightPast()
    {
        await using var session = await AttachedSession.CreateAsync();
        await session.EnableDebuggerAsync();

        await session.ResultAsync("Debugger.setBreakpointByUrl", """{"url":"main.js","lineNumber":2}""");
        await session.ResultAsync("Debugger.setBreakpointsActive", """{"active":false}""");

        await session.Target.PostAsync(engine => engine.Execute(Source, "main.js"));

        session.EventsOf("Debugger.paused").Should().BeEmpty();
    }

    [Test]
    public async Task SetSkipAllPausesRunsStraightPast()
    {
        await using var session = await AttachedSession.CreateAsync();
        await session.EnableDebuggerAsync();

        await session.ResultAsync("Debugger.setBreakpointByUrl", """{"url":"main.js","lineNumber":2}""");
        await session.ResultAsync("Debugger.setSkipAllPauses", """{"skip":true}""");

        await session.Target.PostAsync(engine => engine.Execute(Source, "main.js"));

        session.EventsOf("Debugger.paused").Should().BeEmpty();
    }

    /// <summary>
    /// Running to a line stops once, and the breakpoint that did it is gone afterwards.
    /// </summary>
    [Test]
    public async Task ContinueToLocationStopsOnceAndTakesItselfAway()
    {
        await using var session = await AttachedSession.CreateAsync();
        await session.EnableDebuggerAsync();
        await session.ResultAsync("Debugger.setBreakpointByUrl", """{"url":"twice.js","lineNumber":1}""");

        var running = session.Target.PostAsync(engine => engine.Execute(
            """
            function step(n) {
                var here = n;
                var later = n + 1;
                return later;
            }
            step(1);
            step(2);
            """,
            "twice.js"));

        var first = await session.EventAsync("Debugger.paused");
        var scriptId = first.GetProperty("callFrames").EnumerateArray().First()
            .GetProperty("location").GetProperty("scriptId").GetString();

        // Run on to `var later`, which is the very next statement.
        await session.ResultAsync(
            "Debugger.continueToLocation",
            $$$"""{"location":{"scriptId":"{{{scriptId}}}","lineNumber":2,"columnNumber":0}}""");

        var second = await session.EventAsync("Debugger.paused", index: 1);
        second.GetProperty("callFrames").EnumerateArray().First()
            .GetProperty("location").GetProperty("lineNumber").GetInt32().Should().Be(2);
        second.Optional("hitBreakpoints").Should().BeNull("a run-to-location breakpoint is not one the client set");

        await session.ResultAsync("Debugger.resume");

        // The second call hits the ordinary breakpoint on line 1 and not the one-shot on line 2.
        var third = await session.EventAsync("Debugger.paused", index: 2);
        third.GetProperty("callFrames").EnumerateArray().First()
            .GetProperty("location").GetProperty("lineNumber").GetInt32().Should().Be(1);

        await session.ResultAsync("Debugger.resume");
        await running;

        session.EventsOf("Debugger.paused").Should().HaveCount(3);
    }

    /// <summary>
    /// <c>debugger;</c> stops without any breakpoint being set, which is the switch <c>UseDevTools</c> turns
    /// on.
    /// </summary>
    [Test]
    public async Task ADebuggerStatementPauses()
    {
        await using var session = await AttachedSession.CreateAsync();
        await session.EnableDebuggerAsync();

        var running = session.Target.PostAsync(engine => engine.Execute(
            """
            var before = 1;
            debugger;
            var after = 2;
            """,
            "statement.js"));

        var paused = await session.EventAsync("Debugger.paused");

        // The protocol's reason enum has no member for a debugger statement, and V8 answers `other` for one
        // too; what tells a client which statement it is, is the location.
        paused.GetProperty("reason").GetString().Should().Be("other");
        paused.Optional("hitBreakpoints").Should().BeNull();
        paused.GetProperty("callFrames").EnumerateArray().First()
            .GetProperty("location").GetProperty("lineNumber").GetInt32().Should().Be(1);

        await session.ResultAsync("Debugger.resume");
        await running;
    }

    [Test]
    public async Task DisableRemovesTheBreakpointsTheSessionSet()
    {
        await using var session = await AttachedSession.CreateAsync();
        await session.EnableDebuggerAsync();

        await session.ResultAsync("Debugger.setBreakpointByUrl", """{"url":"main.js","lineNumber":2}""");
        await session.ResultAsync("Debugger.disable");
        await session.EnableDebuggerAsync();

        await session.Target.PostAsync(engine => engine.Execute(Source, "main.js"));

        session.EventsOf("Debugger.paused").Should().BeEmpty();
    }
}
