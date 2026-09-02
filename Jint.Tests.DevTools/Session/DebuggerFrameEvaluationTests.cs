using System.Text.Json;

namespace Jint.Tests.DevTools.Session;

/// <summary>
/// <c>Debugger.evaluateOnCallFrame</c> against a frame that is not the innermost one, which is the whole
/// reason a front end's call-stack pane is clickable.
/// </summary>
/// <remarks>
/// The property worth pinning is <b>shadowing</b>. An implementation that quietly answered every frame from
/// the active execution context would pass a test whose frames declare different names, and would read the
/// wrong variable for every real program — so the fixture declares the same name in both frames and the
/// assertions are on which of the two values comes back.
/// </remarks>
[NonParallelizable]
public class DebuggerFrameEvaluationTests
{
    private const string Source = """
        function outer() {
            var value = "outer";
            function inner() {
                var value = "inner";
                debugger;
                return value;
            }
            inner();
            return value;
        }
        """;

    /// <summary>Each frame answers from its own scope chain, not from the one the engine is running in.</summary>
    [Test]
    public async Task AnOuterFrameAnswersTheBindingTheTopFrameShadows()
    {
        await using var session = await AttachedSession.CreateAsync();
        await session.EnableDebuggerAsync();
        await session.Target.PostAsync(engine => engine.Execute(Source, "main.js"));

        var running = session.Target.PostAsync(engine => engine.Evaluate("outer()").ToString());
        var frames = await FramesAsync(session);

        frames.Length.Should().BeGreaterThanOrEqualTo(3, "inner, outer and the global frame are all on the stack");
        frames[0].GetProperty("functionName").GetString().Should().Be("inner");
        frames[1].GetProperty("functionName").GetString().Should().Be("outer");

        (await ValueAsync(session, frames[0], "value")).Should().Be("inner");
        (await ValueAsync(session, frames[1], "value")).Should().Be("outer");

        // `this` and `arguments` are the frame's own too, so the global frame is answered as the global frame.
        (await ValueAsync(session, frames[^1], "typeof inner")).Should().Be("undefined");

        await session.ResultAsync("Debugger.resume");
        await running;
    }

    /// <summary>
    /// An evaluation in an outer frame writes to that frame, because it is that frame's environment the
    /// expression resolves against.
    /// </summary>
    [Test]
    public async Task WritingInAnOuterFrameChangesWhatThatFrameReturns()
    {
        await using var session = await AttachedSession.CreateAsync();
        await session.EnableDebuggerAsync();
        await session.Target.PostAsync(engine => engine.Execute(Source, "main.js"));

        var running = session.Target.PostAsync(engine => engine.Evaluate("outer()").ToString());
        var frames = await FramesAsync(session);

        await ValueAsync(session, frames[1], "value = 'written'");

        await session.ResultAsync("Debugger.resume");
        (await running).Should().Be("written");
    }

    /// <summary>
    /// A frame identifier names the pause it was minted in, so a client acting on a <c>paused</c> event it
    /// has already resumed from is told rather than answered about a different frame.
    /// </summary>
    [Test]
    public async Task AFrameIdentifierFromAnEarlierPauseIsRefused()
    {
        await using var session = await AttachedSession.CreateAsync();
        await session.EnableDebuggerAsync();
        await session.Target.PostAsync(engine => engine.Execute(Source, "main.js"));

        var first = session.Target.PostAsync(engine => engine.Evaluate("outer()").ToString());
        var stale = (await FramesAsync(session))[0].GetProperty("callFrameId").GetString();

        await session.ResultAsync("Debugger.resume");
        await first;

        var second = session.Target.PostAsync(engine => engine.Evaluate("outer()").ToString());
        await FramesAsync(session, index: 1);

        var error = await session.ErrorAsync(
            "Debugger.evaluateOnCallFrame",
            $$"""{"callFrameId":"{{stale}}","expression":"value"}""");

        error.GetProperty("code").GetInt32().Should().Be(-32000);
        error.GetProperty("message").GetString().Should().Be("Invalid call frame id");

        await session.ResultAsync("Debugger.resume");
        await second;
    }

    /// <summary>
    /// A client asking for an evaluation that must not run anything observable is refused, because the only
    /// alternative is running the very code it asked not to be run.
    /// </summary>
    [Test]
    public async Task SideEffectFreeEvaluationIsRefusedRatherThanRun()
    {
        await using var session = await AttachedSession.CreateAsync();
        await session.EnableDebuggerAsync();
        await session.Target.PostAsync(engine => engine.Execute(Source, "main.js"));

        var running = session.Target.PostAsync(engine => engine.Evaluate("outer()").ToString());
        var frames = await FramesAsync(session);
        var frameId = frames[0].GetProperty("callFrameId").GetString();

        var error = await session.ErrorAsync(
            "Debugger.evaluateOnCallFrame",
            $$"""{"callFrameId":"{{frameId}}","expression":"value","throwOnSideEffect":true}""");

        error.GetProperty("code").GetInt32().Should().Be(-32000);
        error.GetProperty("message").GetString().Should().Be("Side-effect free evaluation is not supported");

        await session.ResultAsync("Debugger.resume");
        await running;
    }

    /// <summary>An expression that throws is answered as a result plus the details, not as a failed command.</summary>
    [Test]
    public async Task AThrowingExpressionAnswersExceptionDetails()
    {
        await using var session = await AttachedSession.CreateAsync();
        await session.EnableDebuggerAsync();
        await session.Target.PostAsync(engine => engine.Execute(Source, "main.js"));

        var running = session.Target.PostAsync(engine => engine.Evaluate("outer()").ToString());
        var frames = await FramesAsync(session);
        var frameId = frames[1].GetProperty("callFrameId").GetString();

        var result = await session.ResultAsync(
            "Debugger.evaluateOnCallFrame",
            $$"""{"callFrameId":"{{frameId}}","expression":"missing.property"}""");

        result.GetProperty("exceptionDetails").GetProperty("text").GetString().Should().NotBeNullOrEmpty();
        result.GetProperty("result").GetProperty("subtype").GetString().Should().Be("error");

        await session.ResultAsync("Debugger.resume");
        await running;
    }

    /// <summary>The frames of the pause at <paramref name="index"/>, once it has arrived.</summary>
    private static async Task<JsonElement[]> FramesAsync(AttachedSession session, int index = 0)
    {
        var paused = await session.EventAsync("Debugger.paused", index).ConfigureAwait(false);
        return [.. paused.GetProperty("callFrames").EnumerateArray()];
    }

    /// <summary>Evaluates <paramref name="expression"/> in one frame and answers the value it described.</summary>
    private static async Task<string?> ValueAsync(AttachedSession session, JsonElement frame, string expression)
    {
        var frameId = frame.GetProperty("callFrameId").GetString();
        var parameters = JsonSerializer.Serialize(new Dictionary<string, object>
        {
            ["callFrameId"] = frameId!,
            ["expression"] = expression,
            ["returnByValue"] = true,
        });

        var result = await session.ResultAsync("Debugger.evaluateOnCallFrame", parameters).ConfigureAwait(false);
        result.TryGetProperty("exceptionDetails", out var details).Should().BeFalse("'{0}' was expected to succeed, and it threw {1}", expression, details);
        return result.GetProperty("result").GetProperty("value").GetString();
    }
}
