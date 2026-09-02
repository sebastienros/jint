using Jint.Browser;
using Jint.Tests.Browser.Navigation;

namespace Jint.Tests.Browser.Parsing;

/// <summary>
/// What a load is once the parser driver owns it: the readiness transitions, the subresources it fetches and
/// the ones it deliberately does not, and the timing the baton buys.
/// </summary>
public class DocumentLoadTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(30);

    [Test]
    public async Task ReadinessMovesFromLoadingToInteractiveToComplete()
    {
        await using var loopback = await LoopbackPage.CreateAsync(server => server
            .Map("/d.js", _ => LoopbackResponse.Script("window.states.push('defer@' + document.readyState);"))
            .MapHtml("/", """
                <!doctype html><html><head>
                <script>
                  window.states = ['script@' + document.readyState];
                  document.addEventListener('readystatechange', () => window.states.push('change@' + document.readyState));
                </script>
                <script defer src="/d.js"></script>
                </head><body>done</body></html>
                """));

        await loopback.Page.NavigateAsync(loopback.Url("/"));

        // https://html.spec.whatwg.org/multipage/parsing.html#the-end: "interactive" is set before the
        // deferred scripts run, and "complete" before load fires.
        (await loopback.Page.EvaluateAsync<string>("window.states.join(',')"))
            .Should().Be("script@loading,change@interactive,defer@interactive,change@complete");

        (await loopback.Page.EvaluateAsync("document.readyState")).Should().Be("complete");
    }

    [Test]
    public async Task TimersFireWhileAParserBlockingScriptIsOnItsWay()
    {
        await using var loopback = await LoopbackPage.CreateAsync(server => server
            .Map("/slow.js", _ =>
            {
                // The server takes its time answering, which is the only way to observe that the page loop is
                // free while the parser waits.
                Thread.Sleep(400);
                return LoopbackResponse.Script("window.ticksWhenSlowRan = window.ticks;");
            })
            .MapHtml("/", """
                <!doctype html><html><head>
                <script>
                  window.ticks = 0;
                  setInterval(() => window.ticks++, 5);
                </script>
                <script src="/slow.js"></script>
                </head><body>done</body></html>
                """));

        await loopback.Page.NavigateAsync(loopback.Url("/"));

        // The baton's proof: the parser was parked on the fetch, the loop held the baton and pumped, so the
        // interval ran — which is exactly what a browser does and what a blocking parse could not.
        (await loopback.Page.EvaluateAsync<int>("window.ticksWhenSlowRan"))
            .Should().BeGreaterThan(0, "the page loop must keep running timers while a script is fetched");
    }

    [Test]
    public async Task AnExternalStyleSheetIsFetchedAndGetComputedStyleAnswersFromIt()
    {
        await using var loopback = await LoopbackPage.CreateAsync(server => server
            .Map("/site.css", _ => LoopbackResponse.Css("#p { color: rgb(1, 2, 3); font-size: 33px; }"))
            .MapHtml("/", """
                <!doctype html><html><head>
                <link rel="stylesheet" href="/site.css">
                </head><body><p id="p">styled</p></body></html>
                """));

        await loopback.Page.NavigateAsync(loopback.Url("/"));

        (await loopback.Page.EvaluateAsync<string>("getComputedStyle(document.getElementById('p')).fontSize"))
            .Should().Be("33px");

        // AngleSharp.Css serializes every colour with an alpha channel; see the divergence table in
        // Jint.Browser/AGENTS.md. What matters here is that the sheet was fetched and cascaded at all.
        (await loopback.Page.EvaluateAsync<string>("getComputedStyle(document.getElementById('p')).color"))
            .Should().Contain("1, 2, 3");
    }

    [Test]
    public async Task TheRequestLogNamesEveryReferenceIncludingTheOnesThePageWouldNotFetch()
    {
        await using var loopback = await LoopbackPage.CreateAsync(server => server
            .Map("/app.js", _ => LoopbackResponse.Script("window.ok = true;"))
            .Map("/site.css", _ => LoopbackResponse.Css("body { color: rgb(4, 5, 6); }"))
            .MapHtml("/", """
                <!doctype html><html><head>
                <link rel="stylesheet" href="/site.css">
                <script src="/app.js"></script>
                </head><body>
                <img src="/logo.png">
                <iframe src="/frame.html"></iframe>
                </body></html>
                """));

        await loopback.Page.NavigateAsync(loopback.Url("/"));

        var requests = loopback.Page.Requests;

        requests.Should().ContainSingle(r => r.Url.EndsWith("/", StringComparison.Ordinal) && r.Initiator == RequestInitiator.Document);
        requests.Should().ContainSingle(r => r.Url.EndsWith("/app.js", StringComparison.Ordinal)
            && r.Initiator == RequestInitiator.Subresource
            && r.NotFetchedReason == null
            && r.Status == 200);
        requests.Should().ContainSingle(r => r.Url.EndsWith("/site.css", StringComparison.Ordinal)
            && r.Initiator == RequestInitiator.Subresource
            && r.NotFetchedReason == null);

        requests.Should().ContainSingle(r => r.Url.EndsWith("/logo.png", StringComparison.Ordinal)
            && r.NotFetchedReason != null);
        requests.Should().ContainSingle(r => r.Url.EndsWith("/frame.html", StringComparison.Ordinal)
            && r.NotFetchedReason != null);

        // What is recorded as not fetched really was not: the server never saw either.
        loopback.Server.Received.Should().NotContain(request => request.Path == "/logo.png");
        loopback.Server.Received.Should().NotContain(request => request.Path == "/frame.html");
    }

    [Test]
    public async Task WaitingForDomContentLoadedReturnsAfterTheDeferredScripts()
    {
        await using var loopback = await LoopbackPage.CreateAsync(server => server
            .Map("/d.js", _ => LoopbackResponse.Script("window.deferred = true;"))
            .MapHtml("/", """
                <!doctype html><html><head>
                <script defer src="/d.js"></script>
                </head><body>done</body></html>
                """));

        await loopback.Page.NavigateAsync(
            loopback.Url("/"),
            new NavigationOptions { WaitUntil = WaitUntilState.DomContentLoaded });

        // HTML's order, pinned: DOMContentLoaded fires after every deferred script, so a caller that waited
        // for it can read what they did.
        (await loopback.Page.EvaluateAsync<bool>("window.deferred === true")).Should().BeTrue();
    }

    [Test]
    public async Task ANavigationAScriptStartsDuringTheParseReplacesTheDocumentCleanly()
    {
        await using var loopback = await LoopbackPage.CreateAsync(server => server
            .MapHtml("/second", "<!doctype html><html><body><p id='second'>second</p></body></html>")
            .MapHtml("/", """
                <!doctype html><html><head>
                <script>location.href = '/second';</script>
                </head><body><p id="first">first</p></body></html>
                """));

        await loopback.Page.NavigateAsync(loopback.Url("/"));

        // The navigation the script asked for waits behind the one that is committing, and then replaces it.
        var arrived = await WaitForAsync(loopback.Page, "document.getElementById('second') !== null");
        arrived.Should().BeTrue("the navigation the parse started should have committed");

        loopback.Page.Url.Should().Be(loopback.Url("/second"));
        loopback.Page.Errors.Should().BeEmpty();
    }

    [Test]
    public async Task ThePageSurvivesAScriptThatCannotBeReachedAtAll()
    {
        await using var loopback = await LoopbackPage.CreateAsync(
            server => server.MapHtml("/", """
                <!doctype html><html><head>
                <script src="http://blocked.example/evil.js"></script>
                </head><body><p id="p">still here</p></body></html>
                """));

        await loopback.Page.NavigateAsync(loopback.Url("/"));

        (await loopback.Page.EvaluateAsync("document.getElementById('p').textContent")).Should().Be("still here");
        loopback.Page.Errors.Should().ContainSingle(error => error.Message.Contains("evil.js", StringComparison.Ordinal));
        loopback.Page.Requests.Should().ContainSingle(r => r.Url.Contains("evil.js", StringComparison.Ordinal)
            && r.NotFetchedReason != null);
    }

    private static async Task<bool> WaitForAsync(Page page, string condition)
    {
        var deadline = DateTime.UtcNow + Timeout;

        while (DateTime.UtcNow < deadline)
        {
            if (await page.EvaluateAsync<bool>(condition).ConfigureAwait(false))
            {
                return true;
            }

            await Task.Delay(20).ConfigureAwait(false);
        }

        return false;
    }
}
