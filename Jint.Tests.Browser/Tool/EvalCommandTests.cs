using Jint.Tests.Browser.Navigation;

namespace Jint.Tests.Browser.Tool;

/// <summary>
/// <c>jint-browser eval</c>: one expression in a loaded page, answered as the page's own JSON.
/// </summary>
public sealed class EvalCommandTests
{
    [Test]
    public async Task AnExpressionIsAnsweredAsJson()
    {
        using var server = Serve();

        var run = await ToolRun.RunAsync("eval", server.Url("/"), "({ title: document.title, items: document.querySelectorAll('li').length })");

        run.ExitCode.Should().Be(0);
        run.Output.Trim().Should().Be("""{"title":"A page","items":2}""");
        run.Error.Should().BeEmpty();
    }

    [Test]
    public async Task TheDocumentsOwnScriptsHaveRunFirst()
    {
        using var server = Serve();

        var run = await ToolRun.RunAsync("eval", server.Url("/"), "document.body.dataset.ready");

        run.Output.Trim().Should().Be("\"yes\"");
    }

    [Test]
    public async Task AValueJsonHasNoFormForIsNull()
    {
        using var server = Serve();

        var run = await ToolRun.RunAsync("eval", server.Url("/"), "undefined");

        run.ExitCode.Should().Be(0);
        run.Output.Trim().Should().Be("null");
    }

    [Test]
    public async Task AnExpressionThatThrowsIsExitCodeFour()
    {
        using var server = Serve();

        var run = await ToolRun.RunAsync("eval", server.Url("/"), "notDefinedAnywhere()");

        run.ExitCode.Should().Be(4);
        run.Error.Should().Contain("notDefinedAnywhere");
        run.Output.Should().BeEmpty();
    }

    [Test]
    public async Task AnExpressionStartingWithADashIsReachedThroughTwoDashes()
    {
        using var server = Serve();

        var run = await ToolRun.RunAsync("eval", "--", server.Url("/"), "-1 + 2");

        run.ExitCode.Should().Be(0);
        run.Output.Trim().Should().Be("1");
    }

    [Test]
    public async Task NoImagesFetchesNoImage()
    {
        using var server = new LoopbackServer();
        server.MapHtml("/", """<!doctype html><html><body><img src="/a.png"></body></html>""");
        server.Map("/a.png", _ => LoopbackResponse.Raw(Jint.Tests.Browser.Parsing.ImageBytes.Png(20, 10), "image/png"));
        const string Expression = "[document.images[0].complete, document.images[0].naturalWidth]";

        var withImages = await ToolRun.RunAsync("eval", server.Url("/"), Expression);
        withImages.ExitCode.Should().Be(0);
        withImages.Output.Trim().Should().Be("[true,20]", "the same page loads its image without the flag");
        server.Received.Count(request => request.Path == "/a.png").Should().Be(1);

        var withoutImages = await ToolRun.RunAsync("eval", server.Url("/"), Expression, "--no-images");

        withoutImages.ExitCode.Should().Be(0);
        withoutImages.Output.Trim().Should().Be("[false,0]");
        server.Received.Count(request => request.Path == "/a.png").Should().Be(1, "--no-images opens no socket for an image");
    }

    [Test]
    public async Task TooFewArgumentsIsExitCodeOne()
    {
        var run = await ToolRun.RunAsync("eval", "https://example.com");

        run.ExitCode.Should().Be(1);
        run.Error.Should().Contain("usage: jint-browser eval");
    }

    [Test]
    public async Task ACommandThatIsNotOneIsExitCodeOne()
    {
        var run = await ToolRun.RunAsync("screenshot", "https://example.com");

        run.ExitCode.Should().Be(1);
        run.Error.Should().Contain("'screenshot' is not a command");
    }

    private static LoopbackServer Serve()
    {
        var server = new LoopbackServer();
        server.MapHtml("/", """
            <!doctype html>
            <html>
            <head><title>A page</title></head>
            <body><ul><li>one</li><li>two</li></ul>
            <script>document.body.dataset.ready = 'yes';</script>
            </body>
            </html>
            """);

        return server;
    }
}
