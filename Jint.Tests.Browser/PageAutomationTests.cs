using System.Text.RegularExpressions;
using Jint.Browser;
using Jint.HtmlParser;
using Jint.Tests.Browser.Navigation;
using Jint.WebApi;
using NativeBrowser = Jint.Browser.Browser;

namespace Jint.Tests.Browser;

/// <summary>
/// The pieces of the in-process API a test suite written against Playwright reaches for: a context base URL,
/// the locator filter parts, a coordinate mouse with boxes, a response wait, console levels and a text pattern
/// wait.
/// </summary>
public sealed class PageAutomationTests
{
    private static readonly TimeSpan Patience = TimeSpan.FromSeconds(10);

    [Test]
    public async Task ARelativeAddressResolvesAgainstTheContextBaseUrl()
    {
        await using var site = await LoopbackPage.CreateAsync(
            server => server
                .MapHtml("/admin/index", "<!doctype html><title>index</title>")
                .MapHtml("/admin/users", "<!doctype html><title>users</title>"),
            context => context.BaseUrl = "http://127.0.0.1:1/unused");

        // A base URL on another port proves an absolute address is left alone.
        (await site.Page.NavigateAsync(site.Url("/admin/index")))!.Ok.Should().BeTrue();

        await using var browser = new NativeBrowser();
        var context = await browser.NewContextAsync(new BrowserContextOptions
        {
            BaseUrl = site.Server.Origin,
            UrlFilter = site.Server.Owns,
        });
        var page = await context.NewPageAsync();

        (await page.NavigateAsync("/admin/index"))!.Url.Should().Be(site.Url("/admin/index"));
        (await page.TitleAsync()).Should().Be("index");

        // Relative to the base URL, not to the current document: the host's address bar, not a link.
        (await page.NavigateAsync("admin/users"))!.Url.Should().Be(site.Url("/admin/users"));
        (await page.WaitForUrlAsync("/admin/users", Patience)).Should().BeTrue();
    }

    [Test]
    public void ABaseUrlMustBeAbsolute()
    {
        var options = new BrowserContextOptions();
        var assign = () => options.BaseUrl = "/relative";
        assign.Should().Throw<ArgumentException>();
        options.BaseUrl = null;
        options.BaseUrl.Should().BeNull();
    }

    [Test]
    public async Task FilterPartsNarrowThePreviousPartsMatches()
    {
        await using var browser = new NativeBrowser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync("""
            <!doctype html>
            <ul>
              <li id=a>Alpha <b>one</b></li>
              <li id=b hidden>Beta one</li>
              <li id=c>Gamma two</li>
              <li id=d style="display:none">Delta two</li>
            </ul>
            """);

        (await page.GetAttributeAsync("li >> nth=1", "id")).Should().Be("b");
        (await page.GetAttributeAsync("li >> nth=-1", "id")).Should().Be("d");
        (await page.CountAsync("li >> nth=9")).Should().Be(0);
        (await page.TextsAsync("li >> visible=true")).Should().Equal("Alpha one", "Gamma two");
        (await page.CountAsync("li >> visible=false")).Should().Be(2);
        (await page.CountAsync("li >> has-text=one")).Should().Be(2);
        (await page.CountAsync("li >> has-text=\"ONE\"")).Should().Be(2);
        (await page.CountAsync("li >> has-text=\"Alpha one\"s")).Should().Be(1);
        (await page.CountAsync("li >> internal:has-text=\"two\"i")).Should().Be(2);
        (await page.CountAsync("li >> has-not-text=one")).Should().Be(2);

        // Filters compose with each other and with the parts after them.
        (await page.GetAttributeAsync("li >> visible=true >> has-text=two", "id")).Should().Be("c");
        (await page.InnerTextAsync("li >> has-text=one >> nth=0 >> b")).Should().Be("one");
    }

    [TestCase("nth=0")]
    [TestCase("visible=true")]
    [TestCase("has-text=x >> li")]
    public async Task AFilterWithNothingBeforeItMatchesNothing(string target)
    {
        await using var browser = new NativeBrowser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync("<!doctype html><li>x</li>");

        (await page.ClickAsync(target)).Should().BeFalse();
        await FluentActions.Awaiting(() => page.CountAsync(target)).Should().ThrowAsync<DomException>();
    }

    [Test]
    public async Task TheCoordinateMouseDragsWithItsButtonHeld()
    {
        await using var browser = new NativeBrowser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync("""
            <!doctype html>
            <div id=handle>handle</div>
            <div id=target>target</div>
            <script>
              window.log = [];
              for (const type of ['pointerdown', 'mousedown', 'mousemove', 'mouseup', 'click']) {
                document.addEventListener(type, e => log.push(type + ':' + e.target.id + ':' + e.buttons));
              }
            </script>
            """);

        var handle = await page.BoundingBoxAsync("#handle");
        var target = await page.BoundingBoxAsync("#target");
        handle.Should().NotBeNull();
        target.Should().NotBeNull();
        target!.Value.Y.Should().BeGreaterThan(handle!.Value.Y);
        (await page.BoundingBoxAsync("#missing")).Should().BeNull();

        await page.MouseMoveAsync(handle.Value.X + 1, handle.Value.Y + 1);
        await page.MouseDownAsync();
        await page.MouseMoveAsync(target.Value.X + 1, target.Value.Y + 1);
        await page.MouseUpAsync();

        var log = await page.EvaluateAsync<string>("log.join(' ')");
        // The press and the release hit different elements, so the click goes to their nearest common
        // ancestor - the body, which has no id - as UI Events says and Chrome does.
        log.Should().Be("mousemove:handle:0 pointerdown:handle:1 mousedown:handle:1 mousemove:target:1 mouseup:target:0 click::0");

        // A glide is made of evenly spaced moves, each carrying the held buttons.
        await page.EvaluateAsync("log.length = 0");
        await page.MouseDownAsync();
        await page.MouseMoveAsync(handle.Value.X + 1, handle.Value.Y + 1, steps: 4);
        await page.MouseUpAsync();
        (await page.EvaluateAsync<int>("log.filter(e => e.startsWith('mousemove') && e.endsWith(':1')).length")).Should().Be(4);
        (await page.EvaluateAsync<string>("log.slice(-2).join(' ')")).Should().Be("mouseup:handle:0 click::0");

        // A press and a release on the same element is a click.
        await page.EvaluateAsync("log.length = 0");
        await page.MouseDownAsync();
        await page.MouseUpAsync();
        (await page.EvaluateAsync<string>("log.join(' ')")).Should().Be("pointerdown:handle:1 mousedown:handle:1 mouseup:handle:0 click:handle:0");
    }

    [Test]
    public async Task AResponseWaitAnswersOnlyARequestSentAfterIt()
    {
        await using var site = await LoopbackPage.CreateAsync(server => server
            .MapHtml("/", "<!doctype html><title>home</title>")
            .Map("/api/data", _ => LoopbackResponse.Json("{\"n\":1}"))
            .Map("/api/missing", _ => new LoopbackResponse { Status = 404, Reason = "Not Found" }));

        await site.Page.NavigateAsync(site.Url("/"));
        await site.Page.EvaluateAndAwaitAsync("fetch('/api/data').then(r => r.text())");

        // Already answered before the wait started: never the answer.
        var stale = site.Page.WaitForResponseAsync(r => r.Url.EndsWith("/api/data", StringComparison.Ordinal), TimeSpan.FromMilliseconds(300));
        (await stale).Should().BeNull();

        var missing = site.Page.WaitForResponseAsync(r => r.Url.EndsWith("/api/missing", StringComparison.Ordinal), Patience);
        await site.Page.EvaluateAsync("fetch('/api/missing')");
        var response = await missing;
        response.Should().NotBeNull();
        response!.Status.Should().Be(404);
    }

    [Test]
    public async Task ConsoleEntriesKeepTheLevelOfEachMessage()
    {
        await using var browser = new NativeBrowser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync("<!doctype html><script>console.log('a'); console.error('b', 1); console.warn('c')</script>");

        page.ConsoleEntries.Should().Equal(
            new PageConsoleMessage(ConsoleLogLevel.Log, "a"),
            new PageConsoleMessage(ConsoleLogLevel.Error, "b 1"),
            new PageConsoleMessage(ConsoleLogLevel.Warn, "c"));
        page.ConsoleMessages.Should().Equal("a", "b 1", "c");
    }

    [Test]
    public async Task ATextPatternWaitMatchesTheRenderedText()
    {
        await using var browser = new NativeBrowser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync("<!doctype html><span id=count>13</span><script>setTimeout(() => count.textContent = '3', 50)</script>");

        // "3" is a substring of "13": an exact count needs an anchored pattern.
        (await page.WaitForTextAsync("#count", new Regex("^3$"), Patience)).Should().BeTrue();
        (await page.WaitForTextAsync("#missing", new Regex(".*"), TimeSpan.FromMilliseconds(100))).Should().BeFalse();
    }
}
