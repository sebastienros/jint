using System.Text.Json;
using Jint.Browser;
using Jint.Browser.Runtime;

namespace Jint.Tests.Browser.DevTools;

/// <summary>
/// A protocol command the page's turn budget stops is answered as an error naming
/// <see cref="BrowserOptions.MaxTaskDuration"/>, and the execution context survives it (#4619).
/// </summary>
/// <remarks>
/// <para>
/// Before, the budget's <see cref="TimeoutException"/> came back out of the mailbox's own wait and was read as
/// the command timing out, so the client was answered <c>-32000 "Command timed out"</c> at exactly the budget —
/// which Playwright rewrites to "Execution context was destroyed, most likely because of a navigation". And a
/// command awaiting a promise whose reaction the budget discarded was never answered at all.
/// </para>
/// <para>
/// The budget is short so the suite is quick; what is asserted is the shape of the answer and the sentence in
/// it, never how long the stop took.
/// </para>
/// </remarks>
public class TaskBudgetProtocolTests
{
    private static readonly TimeSpan Budget = TimeSpan.FromMilliseconds(200);

    [Test]
    public async Task AnEvaluationOverTheBudgetIsAThrownErrorNamingIt()
    {
        await using var session = await PageSession.CreateAsync(options: new BrowserOptions { MaxTaskDuration = Budget });
        var attachment = await session.OpenPageAsync();

        var result = await session.ResultAsync("Runtime.evaluate", """{"expression":"while (true) { }","returnByValue":true}""", attachment);

        AssertNamesTheBudget(result);
    }

    /// <summary>
    /// <c>Runtime.callFunctionOn</c> with <c>awaitPromise</c> is what <c>page.evaluate</c> sends in both
    /// clients, and one synchronous <c>axe.run</c> over a large page is exactly this shape.
    /// </summary>
    [Test]
    public async Task ACallOverTheBudgetIsAThrownErrorNamingIt()
    {
        await using var session = await PageSession.CreateAsync(options: new BrowserOptions { MaxTaskDuration = Budget });
        var attachment = await session.OpenPageAsync();

        var result = await session.ResultAsync(
            "Runtime.callFunctionOn",
            """{"functionDeclaration":"() => { for (;;) { } }","returnByValue":true,"awaitPromise":true}""",
            attachment);

        AssertNamesTheBudget(result);
    }

    /// <summary>
    /// The reaction the command waits on runs in the command's own microtask checkpoint, so the budget that
    /// stops the checkpoint discards it; the command is answered from the stop rather than left waiting.
    /// </summary>
    [Test]
    public async Task APromiseWhoseReactionTheBudgetStoppedIsAnsweredRatherThanLeftWaiting()
    {
        await using var session = await PageSession.CreateAsync(options: new BrowserOptions { MaxTaskDuration = Budget });
        var attachment = await session.OpenPageAsync();

        var reply = session.SendAsync(
            "Runtime.evaluate",
            """{"expression":"Promise.resolve().then(() => { for (;;) { } })","returnByValue":true,"awaitPromise":true}""",
            attachment);

        var completed = await Task.WhenAny(reply, Task.Delay(TestBudgets.WedgeCeiling));
        completed.Should().BeSameAs(reply, "a command whose own turn was stopped must still be answered");

        AssertNamesTheBudget((await reply).GetProperty("result"));
    }

    /// <summary>
    /// The context is not destroyed: no <c>executionContextDestroyed</c> or <c>executionContextsCleared</c>
    /// reaches the client, and the next command runs in the same document, against the same context.
    /// </summary>
    [Test]
    public async Task TheContextSurvivesTheStopAndThePageStaysDrivable()
    {
        await using var session = await PageSession.CreateAsync(options: new BrowserOptions { MaxTaskDuration = Budget });
        var page = await session.NewPageAsync();
        var target = await session.TargetForAsync(page);
        var attachment = await session.AttachAsync(target);
        await session.EnablePageAsync(attachment);

        await session.EvaluateAsync("globalThis.kept = 'before'", attachment);
        await session.ResultAsync("Runtime.evaluate", """{"expression":"while (true) { }"}""", attachment);

        (await session.EvaluateAsync("kept", attachment)).GetProperty("value").GetString().Should().Be("before");
        session.EventsOf("Runtime.executionContextDestroyed", attachment).Should().BeEmpty();
        session.EventsOf("Runtime.executionContextsCleared", attachment).Should().BeEmpty();
        page.Errors.Should().BeEmpty("a stop the client was told about is the client's, not a page error");
    }

    [Test]
    public async Task ADescriptionNamesTheOptionAndTheCommandLineSwitch()
    {
        await using var session = await PageSession.CreateAsync(options: new BrowserOptions { MaxTaskDuration = TimeSpan.FromSeconds(5) });
        var page = await session.NewPageAsync();
        var target = await session.TargetForAsync(page);

        // A TimeoutException the turn deadline did not raise — under the untrusted profile the per-entry
        // LimitExecutionTime raises the same type — keeps the constraint's own message.
        target.DescribeExecutionStop(new TimeoutException("someone else's")).Should().Be("someone else's");
        target.DescribeExecutionStop(new InvalidOperationException("not a stop")).Should().BeNull();
    }

    [TestCase(200, "200ms")]
    [TestCase(5_000, "5s")]
    [TestCase(1_500, "1.5s")]
    [TestCase(120_000, "2m")]
    public void ADurationIsWrittenTheWayTheCommandLineTakesIt(int milliseconds, string expected)
        => PageBudget.Format(TimeSpan.FromMilliseconds(milliseconds)).Should().Be(expected);

    private static void AssertNamesTheBudget(JsonElement result)
    {
        result.TryGetProperty("exceptionDetails", out var details).Should().BeTrue("a stop is reported as a thrown error, and the answer was {0}", result);

        var description = details.GetProperty("exception").GetProperty("description").GetString();
        description.Should().StartWith("Error: Task exceeded MaxTaskDuration (200ms)")
            .And.Contain("BrowserOptions.MaxTaskDuration")
            .And.Contain("--max-task-duration");
    }
}
