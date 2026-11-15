using Jint.Browser;
using Microsoft.Playwright;

namespace Jint.Tests.Browser.DevTools;

public partial class PlaywrightCourseTests
{
    /// <summary>
    /// <c>page.evaluate</c> of a task over <see cref="BrowserOptions.MaxTaskDuration"/> fails naming the budget,
    /// not as "Execution context was destroyed, most likely because of a navigation" — and the page goes on
    /// answering (#4619).
    /// </summary>
    /// <remarks>
    /// Playwright rewrites any protocol error out of an evaluation into the navigation sentence, so this is the
    /// client that decides whether the reply's shape is right; the in-process suite only shows what was sent.
    /// </remarks>
    [Test]
    public async Task PlaywrightIsToldWhichBudgetStoppedAnEvaluation()
    {
        await using var lane = await ClientLane.OpenAsync(options: new BrowserOptions { MaxTaskDuration = TimeSpan.FromMilliseconds(200) });
        var page = await lane.Context.NewPageAsync();

        var failure = await Caught.ExceptionAsync(() => page.EvaluateAsync<string>("() => { for (;;) { } }"));

        failure.Should().BeOfType<PlaywrightException>().Which.Message
            .Should().Contain("Task exceeded MaxTaskDuration (200ms)")
            .And.NotContain("Execution context was destroyed");

        (await page.EvaluateAsync<int>("() => 1 + 1")).Should().Be(2);
        await page.CloseAsync();
    }
}
