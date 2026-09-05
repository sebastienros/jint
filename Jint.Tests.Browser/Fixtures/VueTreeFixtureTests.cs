namespace Jint.Tests.Browser.Fixtures;

public class VueTreeFixtureTests
{
    [Test]
    public async Task ATreeMountedWhileHiddenRendersFoldersAfterLoading()
    {
        await using var course = await FixtureCourse.OpenAsync("vue-folder-tree");
        await course.UntilAsync("resizeHeights.length", "1");
        (await course.Page.EvaluateAsync<double>("resizeHeights[0]")).Should().Be(0);

        await course.ClickAsync("#load");
        await course.UntilAsync("document.querySelector('#status').textContent", "Ready");
        await course.UntilAsync("document.querySelectorAll('#children').length", "1");
        await course.ClickAsync("#create");
        await course.UntilAsync("document.querySelectorAll('.folder-name').length", "2");

        (await course.TextsAsync(".folder-name")).Should().Be("Files|TestFolder");
        course.Server.Received.Should().ContainSingle(request =>
            request.Path == "/vue-folder-tree/created.json" && request.Method == "POST");
        course.ShouldHaveReportedNothing();
    }
}
