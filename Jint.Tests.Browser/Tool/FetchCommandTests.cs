using System.Globalization;
using Jint.Tests.Browser.Navigation;

namespace Jint.Tests.Browser.Tool;

/// <summary>
/// <c>jint-browser fetch</c>, against a server on the loopback interface so that a run really opens a socket.
/// </summary>
/// <remarks>
/// The four representations, the four exit codes and the options that change what is loaded. Every test here
/// runs the tool's own entry point, so what is asserted is what a user typing the same command would see.
/// </remarks>
public sealed class FetchCommandTests
{
    private const string Document = """
        <!doctype html>
        <html>
        <head><title>A page</title></head>
        <body>
          <nav><a href="/other">navigation</a></nav>
          <main>
            <h1>Heading</h1>
            <p>Body text with <b>emphasis</b>.</p>
            <a href="/other">a link</a>
          </main>
          <script>document.querySelector('h1').textContent = 'Heading from script';</script>
        </body>
        </html>
        """;

    [Test]
    public async Task HtmlIsTheDocumentAsTheScriptsLeftIt()
    {
        using var server = Serve();

        var run = await ToolRun.RunAsync("fetch", server.Url("/"), "--dump", "html");

        run.ExitCode.Should().Be(0);
        run.Output.Should().Contain("<!DOCTYPE html>").And.Contain("Heading from script");
        run.Error.Should().BeEmpty();
    }

    [Test]
    public async Task MarkdownIsTheDefaultAndIsCommonMark()
    {
        using var server = Serve();

        var run = await ToolRun.RunAsync("fetch", server.Url("/"));

        run.ExitCode.Should().Be(0);
        run.Output.Should().Contain("# Heading from script").And.Contain("**emphasis**");
    }

    [Test]
    public async Task TextIsTheRenderedTextAndCarriesNoMarkup()
    {
        using var server = Serve();

        var run = await ToolRun.RunAsync("fetch", server.Url("/"), "--dump", "text");

        run.ExitCode.Should().Be(0);
        run.Output.Should().Contain("Heading from script").And.NotContain("<h1>");
    }

    [Test]
    public async Task AxIsTheAccessibilityTreeWithRolesAndNames()
    {
        using var server = Serve();

        var run = await ToolRun.RunAsync("fetch", server.Url("/"), "--dump", "ax");

        run.ExitCode.Should().Be(0);
        run.Output.Should().Contain("heading \"Heading from script\"").And.Contain("link \"a link\"");
    }

    [Test]
    public async Task MainContentDropsWhatIsOutsideTheMainElement()
    {
        using var server = Serve();

        var whole = await ToolRun.RunAsync("fetch", server.Url("/"));
        var main = await ToolRun.RunAsync("fetch", server.Url("/"), "--main-content");

        whole.Output.Should().Contain("navigation");
        main.Output.Should().NotContain("navigation", "the <nav> is outside <main>");
        main.Output.Should().Contain("Heading from script");
    }

    [Test]
    public async Task MaxLengthCutsTheAnswerAndSaysSo()
    {
        using var server = Serve();

        var run = await ToolRun.RunAsync("fetch", server.Url("/"), "--max-length", "40");

        run.ExitCode.Should().Be(0);
        run.Output.TrimEnd().Should().EndWith("[truncated]");
        run.Output.TrimEnd().Length.Should().BeLessThanOrEqualTo(40);
    }

    [Test]
    public async Task NarrowingOrCuttingTheMarkupIsRefusedRatherThanIgnored()
    {
        var narrowed = await ToolRun.RunAsync("fetch", "https://example.com", "--dump", "html", "--main-content");
        var cut = await ToolRun.RunAsync("fetch", "https://example.com", "--dump", "html", "--max-length", "10");

        narrowed.ExitCode.Should().Be(1);
        cut.ExitCode.Should().Be(1);
        narrowed.Error.Should().Contain("'--dump html' is the whole document");
    }

    [Test]
    public async Task AFourOhFourIsADocumentRatherThanAFailure()
    {
        using var server = Serve();

        var run = await ToolRun.RunAsync("fetch", server.Url("/missing"), "--dump", "text");

        run.ExitCode.Should().Be(0, "a status is not a failure; the error page is what a caller scraping one needs");
        run.Output.Should().Contain("404");
    }

    [Test]
    public async Task AUrlThatReachesNothingIsExitCodeTwo()
    {
        // Not `using`: the port is taken and released before the tool is run, so what it navigates to is an
        // address nothing is listening on. Disposing twice would be the test's own failure rather than the
        // tool's.
        var server = new LoopbackServer();
        var url = server.Url("/");
        server.Dispose();

        var run = await ToolRun.RunAsync("fetch", url, "--timeout", "5s");

        run.ExitCode.Should().Be(2);
        run.Error.Should().Contain("Navigation to");
    }

    [Test]
    public async Task AnUnknownOptionIsExitCodeOne()
    {
        var run = await ToolRun.RunAsync("fetch", "https://example.com", "--dmup", "text");

        run.ExitCode.Should().Be(1);
        run.Error.Should().Contain("unknown option '--dmup'");
        run.Output.Should().BeEmpty("nothing was loaded, so nothing is written to standard output");
    }

    [Test]
    public async Task AWordThatIsNotADumpFormatIsExitCodeOne()
    {
        var run = await ToolRun.RunAsync("fetch", "https://example.com", "--dump", "pixels");

        run.ExitCode.Should().Be(1);
        run.Error.Should().Contain("html, text, markdown, ax");
    }

    [Test]
    public async Task AScriptThatRunsOutOfItsBudgetIsExitCodeThreeAndTheDocumentIsStillWritten()
    {
        using var server = new LoopbackServer();
        server.MapHtml("/", """
            <!doctype html>
            <html><body><p id="marker">parsed</p>
            <script>var until = Date.now() + 5000; while (Date.now() < until) { }</script>
            </body></html>
            """);

        var run = await ToolRun.RunAsync("fetch", server.Url("/"), "--dump", "text", "--max-task-duration", "100ms");

        run.ExitCode.Should().Be(3);
        run.Output.Should().Contain("parsed", "the page survives its scripts, and the half that loaded is the half a caller wanted");
        run.Error.Should().NotBeEmpty();
    }

    [Test]
    public async Task UntrustedRefusesAPrivateNetworkUrl()
    {
        using var server = Serve();

        var run = await ToolRun.RunAsync("fetch", server.Url("/"), "--untrusted");

        run.ExitCode.Should().Be(2);
        run.Error.Should().Contain("URL filter", "loopback is the private network, and --untrusted blocks it");
    }

    [Test]
    public async Task AllowPrivateNetworkSurvivesUntrusted()
    {
        using var server = Serve();

        var run = await ToolRun.RunAsync("fetch", server.Url("/"), "--untrusted", "--allow-private-network");

        run.ExitCode.Should().Be(0);
        run.Output.Should().Contain("Heading");
    }

    [Test]
    public async Task BlockAndAllowTogetherIsExitCodeOne()
    {
        var run = await ToolRun.RunAsync("fetch", "https://example.com", "--block-private-network", "--allow-private-network");

        run.ExitCode.Should().Be(1);
        run.Error.Should().Contain("opposite things");
    }

    [Test]
    public async Task AHeaderRidesOnEveryRequest()
    {
        using var server = Serve();

        var run = await ToolRun.RunAsync("fetch", server.Url("/"), "--header", "X-Api-Key: secret", "--dump", "text");

        run.ExitCode.Should().Be(0);
        server.Received[0].Header("x-api-key").Should().Be("secret");
    }

    [Test]
    public async Task ACookieIsSeededBeforeTheLoad()
    {
        using var server = Serve();

        var run = await ToolRun.RunAsync("fetch", server.Url("/"), "--cookie", "session=abc", "--dump", "text");

        run.ExitCode.Should().Be(0);
        server.Received[0].Header("cookie").Should().Contain("session=abc");
    }

    [Test]
    public async Task AUserAgentIsWhatThePageSendsAndWhatItReports()
    {
        using var server = new LoopbackServer();
        server.MapHtml("/", "<!doctype html><html><body><script>document.body.textContent = navigator.userAgent;</script></body></html>");

        var run = await ToolRun.RunAsync("fetch", server.Url("/"), "--user-agent", "Agent/1.0", "--dump", "text");

        run.ExitCode.Should().Be(0);
        run.Output.Should().Contain("Agent/1.0");
        server.Received[0].Header("user-agent").Should().Be("Agent/1.0");
    }

    [Test]
    public async Task NetworkIdleWaitsForWhatAScriptFetchedAfterLoad()
    {
        using var server = new LoopbackServer();
        server.Map("/late", _ => LoopbackResponse.Text("late"));
        server.MapHtml("/", """
            <!doctype html>
            <html><body><p id="marker">before</p>
            <script>
              addEventListener('load', () => {
                fetch('/late').then(r => r.text()).then(t => { document.getElementById('marker').textContent = t; });
              });
            </script>
            </body></html>
            """);

        var loaded = await ToolRun.RunAsync("fetch", server.Url("/"), "--dump", "text", "--wait-until", "load");
        var idle = await ToolRun.RunAsync("fetch", server.Url("/"), "--dump", "text", "--wait-until", "networkidle");

        loaded.ExitCode.Should().Be(0);
        idle.ExitCode.Should().Be(0);
        idle.Output.Should().Contain("late", "the fetch the load handler started had finished by the time the network went quiet");
    }

    [Test]
    public async Task AFileIsShownWithItsOwnUrlAsTheBase()
    {
        var path = Path.Combine(Path.GetTempPath(), "jint-browser-tool-" + Guid.NewGuid().ToString("N") + ".html");
        await File.WriteAllTextAsync(path, Document);

        try
        {
            var relative = Path.GetRelativePath(Directory.GetCurrentDirectory(), path);

            var absolute = await ToolRun.RunAsync("fetch", path, "--dump", "text");
            var byUri = await ToolRun.RunAsync("fetch", new Uri(path).AbsoluteUri, "--dump", "text");
            var byRelativePath = await ToolRun.RunAsync("fetch", relative, "--dump", "text");

            absolute.ExitCode.Should().Be(0);
            byUri.ExitCode.Should().Be(0);
            byRelativePath.ExitCode.Should().Be(0);

            absolute.Output.Should().Contain("Heading from script");
            byUri.Output.Should().Contain("Heading from script");
            byRelativePath.Output.Should().Contain("Heading from script");
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Test]
    public async Task ADataUrlIsADocument()
    {
        var run = await ToolRun.RunAsync("fetch", "data:text/html,<h1>Inline</h1>", "--dump", "text");

        run.ExitCode.Should().Be(0);
        run.Output.Should().Contain("Inline");
    }

    [Test]
    public async Task AnArgumentThatIsNeitherAUrlNorAFileIsExitCodeOne()
    {
        var run = await ToolRun.RunAsync("fetch", "example.com/page");

        run.ExitCode.Should().Be(1);
        run.Error.Should().Contain("neither an absolute URL nor a file that exists");
    }

    [Test]
    public async Task ASchemeThisBrowserDoesNotLoadIsExitCodeOne()
    {
        var run = await ToolRun.RunAsync("fetch", "ftp://example.com/page");

        run.ExitCode.Should().Be(1);
        run.Error.Should().Contain("http:, https:, file:, data: and about:");
    }

    [Test]
    public async Task NoArgumentsPrintsTheHelpAndIsExitCodeOne()
    {
        var run = await ToolRun.RunAsync();

        run.ExitCode.Should().Be(1);
        run.Output.Should().Contain("Usage: jint-browser");
    }

    [Test]
    public async Task HelpIsExitCodeZeroAndNamesEveryCommand()
    {
        var run = await ToolRun.RunAsync("--help");

        run.ExitCode.Should().Be(0);
        run.Output.Should().Contain("serve").And.Contain("fetch").And.Contain("eval").And.Contain("version");
    }

    [Test]
    public async Task VersionIsTheAssemblysOwn()
    {
        var run = await ToolRun.RunAsync("version");

        run.ExitCode.Should().Be(0);
        run.Output.Trim().Should().StartWith(
            typeof(global::Jint.Browser.Browser).Assembly.GetName().Version!.Major.ToString(CultureInfo.InvariantCulture));
    }

    private static LoopbackServer Serve()
    {
        var server = new LoopbackServer();
        server.MapHtml("/", Document);
        server.MapHtml("/other", "<!doctype html><html><head><title>Other</title></head><body>other</body></html>");
        return server;
    }
}
