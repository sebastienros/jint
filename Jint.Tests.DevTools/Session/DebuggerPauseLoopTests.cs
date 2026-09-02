using System.Text.Json;
using Jint.DevTools;
using Jint.WebApi;

namespace Jint.Tests.DevTools.Session;

/// <summary>
/// The pause loop itself: what it answers while it holds the engine thread, and every way out of it.
/// </summary>
/// <remarks>
/// A pause blocks the thread that runs the engine, so the failures this suite is looking for are the ones
/// that hang rather than the ones that throw. Every wait is bounded and every test ends with the engine
/// running again.
/// </remarks>
[NonParallelizable]
public class DebuggerPauseLoopTests
{
    private const string Source = """
        function add(a, b) {
            var sum = a + b;
            return sum;
        }
        var total = add(2, 3);
        """;

    /// <summary>
    /// <c>Debugger.pause</c> stops the engine without any breakpoint, at the next execution point it reaches
    /// — which for a pumped engine is inside the next timer callback.
    /// </summary>
    [Test]
    public async Task PauseStopsATimerLoopAtTheNextExecutionPoint()
    {
        await using var session = await AttachedSession.CreateAsync(
            configureOptions: options => options.WebApi.Features |= WebApiFeatures.Timers);

        await session.EnableDebuggerAsync();
        await session.Target.PostAsync(engine => engine.Execute("var n = 0; setInterval(function tick() { n = n + 1; }, 5);", "timer.js"));

        await session.ResultAsync("Debugger.pause");

        var paused = await session.EventAsync("Debugger.paused");
        paused.GetProperty("reason").GetString().Should().Be("other");
        paused.Optional("hitBreakpoints").Should().BeNull();

        var frames = paused.GetProperty("callFrames").EnumerateArray().ToArray();
        frames.Should().NotBeEmpty();

        await session.ResultAsync("Debugger.resume");
        await session.EventAsync("Debugger.resumed");
    }

    /// <summary>
    /// A client that goes away while the engine is paused has to release it, or the host's thread is wedged
    /// for as long as the process lives.
    /// </summary>
    [Test]
    public async Task AClientThatDisconnectsMidPauseLetsTheEngineFinish()
    {
        await using var session = await AttachedSession.CreateAsync();
        await session.EnableDebuggerAsync();
        await session.ResultAsync("Debugger.setBreakpointByUrl", """{"url":"main.js","lineNumber":2}""");

        var running = session.Target.PostAsync(engine => engine.Execute(Source, "main.js"));
        await session.EventAsync("Debugger.paused");

        // What the transport does when a socket closes: forget the conversation, which detaches everything
        // it held.
        session.Protocol.Server.CloseBrowserSession(session.Protocol.Browser);

        await running;

        // The script ran to completion, so the engine is usable by whoever attaches next.
        var total = await session.Target.PostAsync(engine => engine.Evaluate("total").AsNumber());
        total.Should().Be(5);
    }

    /// <summary>
    /// A pause is bounded, and the bound is the host's. Nothing a client does can remove it.
    /// </summary>
    [Test]
    public async Task APauseNobodyEndsTimesOutAndResumes()
    {
        await using var session = await AttachedSession.CreateAsync(
            serverOptions: new DevToolsServerOptions { PauseTimeout = TimeSpan.FromMilliseconds(500) });

        await session.EnableDebuggerAsync();
        await session.ResultAsync("Log.enable");
        await session.ResultAsync("Debugger.setBreakpointByUrl", """{"url":"main.js","lineNumber":2}""");

        var running = session.Target.PostAsync(engine => engine.Execute(Source, "main.js"));

        await session.EventAsync("Debugger.paused");

        // Nobody resumes. The bound does.
        await running;

        await session.EventAsync("Debugger.resumed");

        var entry = (await session.EventAsync("Log.entryAdded")).GetProperty("entry");
        entry.GetProperty("text").GetString().Should().Contain("The debugger resumed after");
    }

    /// <summary>
    /// Commands that would hand a suspended engine a whole new script to run are refused with a reason, and
    /// everything else is answered — because a client that serializes on one of them would otherwise deadlock
    /// against its own pause.
    /// </summary>
    [Test]
    public async Task WhatIsAnsweredWhilePausedAndWhatIsRefused()
    {
        await using var session = await AttachedSession.CreateAsync();
        await session.EnableDebuggerAsync();
        await session.ResultAsync("Debugger.setBreakpointByUrl", """{"url":"main.js","lineNumber":2}""");

        var running = session.Target.PostAsync(engine => engine.Execute(Source, "main.js"));
        await session.EventAsync("Debugger.paused");

        // Compiling runs nothing, so it is answered.
        var compiled = await session.ResultAsync(
            "Runtime.compileScript",
            """{"expression":"1 + 1","sourceURL":"compiled.js","persistScript":true}""");

        var scriptId = compiled.GetProperty("scriptId").GetString();

        // Running one re-enters a public engine entry on an engine suspended inside a statement.
        var refused = await session.ErrorAsync("Runtime.runScript", $$"""{"scriptId":"{{scriptId}}"}""");
        refused.GetProperty("code").GetInt32().Should().Be(-32000);
        refused.GetProperty("message").GetString().Should().Be("Not allowed while paused");

        // Reads about the engine and about the server are answered as usual.
        (await session.ResultAsync("Runtime.getIsolateId")).GetProperty("id").GetString().Should().NotBeNullOrEmpty();
        await session.Protocol.SendAsync("Schema.getDomains");

        await session.ResultAsync("Debugger.resume");
        await running;
    }

    /// <summary>
    /// A promise cannot settle while the engine is paused, so the command answers what the promise is rather
    /// than waiting for a reaction that cannot run.
    /// </summary>
    [Test]
    public async Task AwaitPromiseAnswersThePendingPromiseWhilePaused()
    {
        await using var session = await AttachedSession.CreateAsync();
        await session.EnableDebuggerAsync();
        await session.ResultAsync("Debugger.setBreakpointByUrl", """{"url":"main.js","lineNumber":2}""");

        var handle = await session.HandleAsync("globalThis.waiting = new Promise(function () {});");

        var running = session.Target.PostAsync(engine => engine.Execute(Source, "main.js"));
        await session.EventAsync("Debugger.paused");

        var settled = await session.ResultAsync("Runtime.awaitPromise", $$"""{"promiseObjectId":"{{handle}}"}""");

        settled.GetProperty("result").GetProperty("type").GetString().Should().Be("object");
        settled.GetProperty("result").GetProperty("subtype").GetString().Should().Be("promise");
        settled.TryGetProperty("exceptionDetails", out _).Should().BeFalse();

        await session.ResultAsync("Debugger.resume");
        await running;
    }

    /// <summary>
    /// Evaluating in an outer frame would silently read the wrong variables, so it is refused rather than
    /// answered against the top one.
    /// </summary>
    [Test]
    public async Task OnlyTheTopFrameCanBeEvaluated()
    {
        await using var session = await AttachedSession.CreateAsync();
        await session.EnableDebuggerAsync();
        await session.ResultAsync("Debugger.setBreakpointByUrl", """{"url":"main.js","lineNumber":2}""");

        var running = session.Target.PostAsync(engine => engine.Execute(Source, "main.js"));
        var paused = await session.EventAsync("Debugger.paused");

        var outerFrame = paused.GetProperty("callFrames").EnumerateArray().Last();
        var outer = outerFrame.GetProperty("callFrameId").GetString();

        var error = await session.ErrorAsync(
            "Debugger.evaluateOnCallFrame",
            $$"""{"callFrameId":"{{outer}}","expression":"1"}""");

        error.GetProperty("message").GetString().Should().Be("Only the top call frame can be evaluated");

        // The outer frame can still be written to, because a scope's environment record is what is written.
        var scopes = outerFrame.GetProperty("scopeChain").EnumerateArray().ToArray();
        var globalScope = Array.FindIndex(scopes, scope => scope.GetProperty("type").GetString() == "global");
        globalScope.Should().BeGreaterThanOrEqualTo(0);

        await session.ResultAsync(
            "Debugger.setVariableValue",
            $$"""{"scopeNumber":{{globalScope}},"variableName":"total","newValue":{"value":7},"callFrameId":"{{outer}}"}""");

        await session.ResultAsync("Debugger.resume");
        await running;
    }

    [Test]
    public async Task ResumingWhenNothingIsPausedIsRefused()
    {
        await using var session = await AttachedSession.CreateAsync();
        await session.EnableDebuggerAsync();

        var error = await session.ErrorAsync("Debugger.resume");
        error.GetProperty("message").GetString().Should().Be("Can only perform operation while paused.");
    }

    /// <summary>
    /// A pause that a client's own command caused is serviced from inside that command, which is the nesting
    /// the pause loop exists for.
    /// </summary>
    [Test]
    public async Task AnEvaluationThatHitsABreakpointIsStillAnswered()
    {
        await using var session = await AttachedSession.CreateAsync();
        await session.EnableDebuggerAsync();

        await session.Target.PostAsync(engine => engine.Execute(Source, "main.js"));
        await session.ResultAsync("Debugger.setBreakpointByUrl", """{"url":"main.js","lineNumber":2}""");

        // Sent and deliberately not awaited: it is what pauses, so awaiting it here would be waiting for
        // this very test to resume it.
        var pending = session.SendAsync("Runtime.evaluate", """{"expression":"add(20, 22)","returnByValue":true}""");

        await session.EventAsync("Debugger.paused");
        await session.ResultAsync("Debugger.resume");

        var reply = await pending;
        reply.GetProperty("result").GetProperty("result").GetProperty("value").GetInt32().Should().Be(42);
    }
}
