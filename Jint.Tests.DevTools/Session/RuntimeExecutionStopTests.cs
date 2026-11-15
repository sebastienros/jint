using System.Text.Json;

namespace Jint.Tests.DevTools.Session;

/// <summary>
/// An evaluation an execution limit stopped is answered as a thrown error naming the limit, never as a protocol
/// error and never as the command timing out (#4619).
/// </summary>
/// <remarks>
/// <para>
/// Playwright rewrites every protocol error out of <c>Runtime.evaluate</c> and <c>Runtime.callFunctionOn</c>
/// into "Execution context was destroyed, most likely because of a navigation", so the shape of the answer is
/// the whole of what a client can tell a budget from a navigation by. Both clients surface a thrown error's
/// <c>description</c> verbatim, which is why the limit's sentence is there.
/// </para>
/// <para>
/// The limits here are the engine's own, on an engine target with no page: the rule is the protocol layer's,
/// and <c>Jint.Browser</c> only changes which sentence names a page's budget.
/// </para>
/// </remarks>
public class RuntimeExecutionStopTests
{
    [Test]
    public async Task AnEvaluationThatRunsOutOfTimeIsAThrownErrorNamingTheLimit()
    {
        await using var session = await AttachedSession.CreateAsync(
            configureOptions: options => options.LimitExecutionTime(TimeSpan.FromMilliseconds(100)));

        var result = await session.ResultAsync("Runtime.evaluate", """{"expression":"while (true) { }","returnByValue":true}""");

        AssertStopped(result, expected: null);
    }

    [Test]
    public async Task ACallThatRunsOutOfStatementsIsAThrownErrorNamingTheLimit()
    {
        await using var session = await AttachedSession.CreateAsync(
            configureOptions: options => options.LimitStatements(1_000));

        var result = await session.ResultAsync(
            "Runtime.callFunctionOn",
            """{"functionDeclaration":"() => { for (;;) { } }","executionContextId":1,"returnByValue":true,"awaitPromise":true}""");

        AssertStopped(result, expected: null);
    }

    /// <summary>
    /// The mailbox used to read a <see cref="TimeoutException"/> coming back out of the command as its own wait
    /// running out, and answered "Command timed out" the moment the script stopped — at exactly the budget,
    /// with nothing pointing at it.
    /// </summary>
    [Test]
    public async Task AHostTimeoutIsNotMistakenForTheCommandTimeout()
    {
        await using var session = await AttachedSession.CreateAsync(
            engine => engine.SetValue("hostTimesOut", new Action(() => throw new TimeoutException("the host gave up"))));

        var reply = await session.SendAsync("Runtime.evaluate", """{"expression":"hostTimesOut()"}""");

        reply.TryGetProperty("error", out var error).Should().BeFalse("a stop is the evaluation's answer, and it answered {0}", error);
        AssertStopped(reply.GetProperty("result"), expected: "the host gave up");
    }

    [Test]
    public async Task TheContextSurvivesTheStop()
    {
        await using var session = await AttachedSession.CreateAsync(
            configureOptions: options => options.LimitExecutionTime(TimeSpan.FromMilliseconds(100)));

        await session.ResultAsync("Runtime.evaluate", """{"expression":"globalThis.kept = 'before'"}""");
        await session.ResultAsync("Runtime.evaluate", """{"expression":"while (true) { }"}""");

        var after = await session.EvaluateAsync("kept");
        after.GetProperty("value").GetString().Should().Be("before");
    }

    private static void AssertStopped(JsonElement result, string? expected)
    {
        result.TryGetProperty("exceptionDetails", out var details).Should().BeTrue("a stop is reported as a thrown error, and the answer was {0}", result);
        details.GetProperty("text").GetString().Should().Be("Uncaught");

        var exception = details.GetProperty("exception");
        exception.GetProperty("subtype").GetString().Should().Be("error");

        var description = exception.GetProperty("description").GetString();
        description.Should().StartWith("Error: ").And.NotContain("Command timed out");
        if (expected is not null)
        {
            description.Should().Contain(expected);
        }

        result.GetProperty("result").GetProperty("description").GetString().Should().Be(description);
    }
}
