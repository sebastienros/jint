using Jint.Browser;
using Jint.Tests.Browser.Navigation;

namespace Jint.Tests.Browser.Extraction;

using Browser = global::Jint.Browser.Browser;

/// <summary>
/// The three representations and the network wait a <see cref="Page"/> answers, over a real page.
/// </summary>
/// <remarks>
/// The extractors have suites of their own; what these hold is the half that only exists once a page does —
/// that the answer is computed on the loop thread after the page's own scripts have run, that
/// <c>mainContentOnly</c> and <c>maxLength</c> reach all three, and that the network's quiet is timed off
/// the loop rather than by holding it.
/// </remarks>
public sealed class PageContentTests
{
    private const string Document = """
        <!doctype html>
        <html>
        <head><title>A page</title></head>
        <body>
          <nav>navigation</nav>
          <main><h1>Heading</h1><p>Body text.</p></main>
          <script>document.querySelector('h1').textContent = 'Heading from script';</script>
        </body>
        </html>
        """;

    [Test]
    public async Task EachRepresentationIsOfTheDocumentTheScriptsLeft()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync(Document);

        (await page.MarkdownAsync()).Should().Contain("# Heading from script");
        (await page.TextAsync()).Should().Contain("Heading from script").And.NotContain("<h1>");
        (await page.AccessibilitySnapshotAsync()).Should().Contain("heading \"Heading from script\"");
    }

    [Test]
    public async Task MainContentOnlyNarrowsAllThree()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync(Document);

        (await page.MarkdownAsync(mainContentOnly: true)).Should().NotContain("navigation");
        (await page.TextAsync(mainContentOnly: true)).Should().NotContain("navigation");
        (await page.AccessibilitySnapshotAsync(mainContentOnly: true)).Should().NotContain("navigation");

        (await page.TextAsync(mainContentOnly: true)).Should().Contain("Heading from script");
    }

    [Test]
    public async Task MaxLengthCutsAllThreeAndSaysSo()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync(Document);

        foreach (var answer in new[]
        {
            await page.MarkdownAsync(maxLength: 30),
            await page.TextAsync(maxLength: 30),
            await page.AccessibilitySnapshotAsync(maxLength: 30),
        })
        {
            answer.Should().EndWith("[truncated]");
            answer.Length.Should().BeLessThanOrEqualTo(30);
        }
    }

    [Test]
    public async Task ABlankPageAnswersNothingRatherThanFailing()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();

        (await page.MarkdownAsync()).Should().BeEmpty();
        (await page.TextAsync()).Should().BeEmpty();
        (await page.AccessibilitySnapshotAsync()).Trim().Should().Be("- RootWebArea", "a document with nothing in it still has a root");
    }

    [Test]
    public async Task WaitForNetworkIdleWaitsForWhatALoadHandlerStarted()
    {
        await using var fixture = await LoopbackPage.CreateAsync(server =>
        {
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
        });

        await fixture.Page.NavigateAsync(fixture.Url("/"));

        (await fixture.Page.WaitForNetworkIdleAsync(TimeSpan.FromSeconds(30))).Should().BeTrue();
        (await fixture.Page.TextAsync()).Should().Contain("late");
    }

    [Test]
    public async Task WaitForNetworkIdleGivesUpRatherThanHangs()
    {
        await using var fixture = await LoopbackPage.CreateAsync(server =>
        {
            // A request that never answers, so the page never goes quiet. The timeout is what has to end the
            // wait; a wait that could only end when the network did would hang a caller for ever.
            server.Map("/hang", _ =>
            {
                Thread.Sleep(TimeSpan.FromSeconds(30));
                return LoopbackResponse.Text("too late");
            });

            server.MapHtml("/", "<!doctype html><html><body><script>fetch('/hang');</script></body></html>");
        });

        await fixture.Page.NavigateAsync(fixture.Url("/"));

        (await fixture.Page.WaitForNetworkIdleAsync(TimeSpan.FromMilliseconds(300))).Should().BeFalse();
    }
}
