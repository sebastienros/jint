namespace Jint.Tests.Browser.Fixtures;

// The real application must finish each task within its unchanged 30-second budget. Run it in NUnit's
// isolated shift, like the client workflows, so unrelated fixtures do not spend that budget for it.
[NonParallelizable]
public class SwaggerFixtureTests
{
    [Test]
    public async Task SwaggerExpandsAnOperationThroughItsSummaryWrapper()
    {
        await using var course = await FixtureCourse.OpenAsync(
            "swagger-ui", configureBrowser: options => options.MaxTaskDuration = TimeSpan.FromSeconds(30));
        const string operation = "#operations-GetEndpoint-ApiGetContentItem";
        await course.UntilAsync($"document.querySelectorAll('{operation}').length", "1");
        await course.ClickAsync(operation + " .opblock-summary");
        await course.UntilAsync($"document.querySelectorAll('{operation} button.try-out__btn').length", "1");
        course.ShouldHaveReportedNothing();
    }
}
