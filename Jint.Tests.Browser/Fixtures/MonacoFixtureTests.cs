namespace Jint.Tests.Browser.Fixtures;

public class MonacoFixtureTests
{
    [Test]
    public async Task MonacoCompletesItsAmdCssDependencyAndCreatesAModel()
    {
        // Assert AMD completion, not how quickly a shared CI runner executes the full editor bundle.
        await using var course = await FixtureCourse.OpenAsync(
            "monaco-amd",
            server => FixtureRoutes.Monaco(server),
            options => options.MaxTaskDuration = TimeSpan.FromSeconds(30));

        await course.UntilAsync("document.getElementById('status').textContent", "query { __typename }");
        (await course.Page.EvaluateAsync<string>("typeof monaco.editor.create")).Should().Be("function");
        (await course.Page.EvaluateAsync<int>("callbackCount")).Should().Be(1);
        (await course.Page.EvaluateAsync<bool>("callbackWasDeferred")).Should().BeTrue();
        (await course.Page.EvaluateAsync<string>("resourceEvents.join(',')")).Should().Be("css:true:true");
        course.Server.Received.Should().ContainSingle(request =>
            request.Path == "/monaco-amd/tenant/OrchardCore.Resources/Scripts/monaco/vs/editor/editor.main.js");
        course.Server.Received.Should().ContainSingle(request =>
            request.Path == "/monaco-amd/tenant/OrchardCore.Resources/Scripts/monaco/vs/editor/editor.main.css");
        course.ShouldHaveReportedNothing();
    }
}
