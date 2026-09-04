using Jint.Browser;

namespace Jint.Tests.Browser.Navigation;

/// <summary>
/// A navigation is a fetch and a new engine: the document comes off the wire through Jint's own transport,
/// and the document it replaces is unloaded and disposed.
/// </summary>
public sealed class NavigationTests
{
    [Test]
    public async Task ADocumentIsFetchedParsedAndItsScriptsRun()
    {
        await using var fixture = await LoopbackPage.CreateAsync(server => server.MapHtml(
            "/index.html",
            "<html><head><title>Hello</title></head><body><p id='greeting'>before</p>"
            + "<script>document.getElementById('greeting').textContent = 'after'</script></body></html>"));

        var response = await fixture.Page.NavigateAsync(fixture.Url("/index.html"));

        response.Should().NotBeNull();
        response!.Status.Should().Be(200);
        response.Ok.Should().BeTrue();
        response.Redirected.Should().BeFalse();

        (await fixture.Page.TitleAsync()).Should().Be("Hello");
        (await fixture.Page.EvaluateAsync<string>("document.getElementById('greeting').textContent")).Should().Be("after");
        fixture.Page.Url.Should().Be(fixture.Url("/index.html"));
        fixture.Page.Errors.Should().BeEmpty();
    }

    [Test]
    public async Task AFourOhFourStillRenders()
    {
        await using var fixture = await LoopbackPage.CreateAsync();

        var response = await fixture.Page.NavigateAsync(fixture.Url("/missing.html"));

        response.Should().NotBeNull();
        response!.Status.Should().Be(404);
        response.Ok.Should().BeFalse();

        (await fixture.Page.TitleAsync()).Should().Be("Not found");
        (await fixture.Page.EvaluateAsync<string>("document.querySelector('h1').textContent")).Should().Be("404");
    }

    [Test]
    public async Task APlainTextResponseBecomesADocumentWhoseBodyIsTheText()
    {
        await using var fixture = await LoopbackPage.CreateAsync(server => server.Map(
            "/notes.txt",
            _ => LoopbackResponse.Text("line one\n<not markup>")));

        await fixture.Page.NavigateAsync(fixture.Url("/notes.txt"));

        (await fixture.Page.EvaluateAsync<string>("document.querySelector('pre').textContent"))
            .Should().Be("line one\n<not markup>", "a plain-text document is the text inside a <pre>, not markup");
    }

    [Test]
    public async Task AContentTypeAPageCannotRenderIsRefusedWithTheTypeInTheMessage()
    {
        await using var fixture = await LoopbackPage.CreateAsync(server => server.Map(
            "/data.json",
            _ => LoopbackResponse.Json("{\"a\":1}")));

        var act = async () => await fixture.Page.NavigateAsync(fixture.Url("/data.json"));

        (await act.Should().ThrowAsync<NavigationFailedException>())
            .WithMessage("*application/json*");
    }

    [Test]
    public async Task ARedirectIsFollowedAndTheFinalUrlIsTheDocumentsUrl()
    {
        await using var fixture = await LoopbackPage.CreateAsync(server => server
            .Map("/start", _ => LoopbackResponse.Redirect(302, "/end"))
            .MapHtml("/end", "<title>arrived</title>"));

        var response = await fixture.Page.NavigateAsync(fixture.Url("/start"));

        response!.Redirected.Should().BeTrue();
        response.Url.Should().Be(fixture.Url("/end"));
        fixture.Page.Url.Should().Be(fixture.Url("/end"));
        (await fixture.Page.TitleAsync()).Should().Be("arrived");
        (await fixture.Page.EvaluateAsync<string>("location.pathname")).Should().Be("/end");
    }

    [Test]
    public async Task ASeeOtherTurnsAFormPostIntoAGet()
    {
        await using var fixture = await LoopbackPage.CreateAsync(server => server
            .MapHtml("/form.html", "<form id='f' method='post' action='/submit'><input name='a' value='1'><button type='submit'>go</button></form>")
            .Map("/submit", _ => LoopbackResponse.Redirect(303, "/done"))
            .MapHtml("/done", "<title>done</title>"));

        await fixture.Page.NavigateAsync(fixture.Url("/form.html"));
        await fixture.Page.SubmitFormAsync("#f");

        var submit = fixture.Server.Received.Single(r => r.Path == "/submit");
        var done = fixture.Server.Received.Single(r => r.Path == "/done");

        submit.Method.Should().Be("POST");
        done.Method.Should().Be("GET", "303 rewrites the method to GET and drops the body");
        done.Body.Should().BeEmpty();
    }

    [Test]
    public async Task AMovedPermanentlyTurnsAFormPostIntoAGetToo()
    {
        await using var fixture = await LoopbackPage.CreateAsync(server => server
            .MapHtml("/form.html", "<form id='f' method='post' action='/submit'><input name='a' value='1'></form>")
            .Map("/submit", _ => LoopbackResponse.Redirect(301, "/moved"))
            .MapHtml("/moved", "<title>moved</title>"));

        await fixture.Page.NavigateAsync(fixture.Url("/form.html"));
        await fixture.Page.SubmitFormAsync("#f");

        var moved = fixture.Server.Received.Single(r => r.Path == "/moved");
        moved.Method.Should().Be("GET", "301 and 302 rewrite a POST to a GET");
        moved.Body.Should().BeEmpty();
        (await fixture.Page.TitleAsync()).Should().Be("moved");
    }

    [Test]
    public async Task BlockPrivateNetworkRefusesALoopbackOrigin()
    {
        await using var fixture = await LoopbackPage.CreateAsync(
            server => server.MapHtml("/index.html", "<title>never</title>"),
            options =>
            {
                // The context's own filter is cleared, so only the private-network rule is deciding.
                options.UrlFilter = null;
                options.BlockPrivateNetwork = true;
            });

        var act = async () => await fixture.Page.NavigateAsync(fixture.Url("/index.html"));

        (await act.Should().ThrowAsync<NavigationFailedException>()).WithMessage("*URL filter*");
        fixture.Server.Received.Should().BeEmpty();
    }

    [Test]
    public async Task TheBrowsersOwnBlockPrivateNetworkReachesEveryContextThatDidNotChoose()
    {
        await using var fixture = await LoopbackPage.CreateAsync(
            server => server.MapHtml("/index.html", "<title>never</title>"),
            options => options.UrlFilter = null,
            browser => browser.BlockPrivateNetwork = true);

        var act = async () => await fixture.Page.NavigateAsync(fixture.Url("/index.html"));

        (await act.Should().ThrowAsync<NavigationFailedException>()).WithMessage("*URL filter*");
        fixture.Server.Received.Should().BeEmpty();
    }

    [Test]
    public async Task AContextKeepsItsOwnChoiceOverTheBrowsersAndOverTheProfile()
    {
        await using var fixture = await LoopbackPage.CreateAsync(
            server => server.MapHtml("/index.html", "<title>loaded</title>"),
            options =>
            {
                options.UrlFilter = null;
                options.BlockPrivateNetwork = false;
            },
            browser => browser.ForUntrustedContent());

        await fixture.Page.NavigateAsync(fixture.Url("/index.html"));

        (await fixture.Page.TitleAsync()).Should().Be("loaded", "false really means 'and I mean it'");
    }

    [Test]
    public void BlockPrivateNetworkReadsBackWhatTheContextsWillBeGiven()
    {
        new BrowserOptions().BlockPrivateNetwork.Should().BeFalse();
        new BrowserOptions().ForUntrustedContent().BlockPrivateNetwork.Should().BeTrue();

        var chosen = new BrowserOptions { BlockPrivateNetwork = false };
        chosen.ForUntrustedContent();
        chosen.BlockPrivateNetwork.Should().BeFalse("a value the host assigned is one the profile leaves alone");
    }

    [Test]
    public async Task ATemporaryRedirectKeepsTheMethodAndTheBody()
    {
        await using var fixture = await LoopbackPage.CreateAsync(server => server
            .MapHtml("/form.html", "<form id='f' method='post' action='/submit'><input name='a' value='1'></form>")
            .Map("/submit", _ => LoopbackResponse.Redirect(307, "/again"))
            .MapHtml("/again", "<title>again</title>"));

        await fixture.Page.NavigateAsync(fixture.Url("/form.html"));
        await fixture.Page.SubmitFormAsync("#f");

        var again = fixture.Server.Received.Single(r => r.Path == "/again");
        again.Method.Should().Be("POST");
        again.Body.Should().Be("a=1");
    }

    [Test]
    public async Task TheSecondNavigationCarriesTheFirstDocumentAsItsReferer()
    {
        await using var fixture = await LoopbackPage.CreateAsync(server => server
            .MapHtml("/one.html", "<title>one</title>")
            .MapHtml("/two.html", "<title>two</title>"));

        await fixture.Page.NavigateAsync(fixture.Url("/one.html"));
        await fixture.Page.NavigateAsync(fixture.Url("/two.html"));

        var second = fixture.Server.Received.Single(r => r.Path == "/two.html");
        second.Header("Referer").Should().Be(fixture.Url("/one.html"));

        (await fixture.Page.EvaluateAsync<string>("document.referrer")).Should().Be(fixture.Url("/one.html"));
    }

    [Test]
    public async Task TheFirstNavigationCarriesNoRefererFromAboutBlank()
    {
        await using var fixture = await LoopbackPage.CreateAsync(server => server.MapHtml("/one.html", "<title>one</title>"));

        await fixture.Page.NavigateAsync(fixture.Url("/one.html"));

        fixture.Server.Received.Single().Header("Referer").Should().BeNull("about:blank has an opaque origin and is nobody's referrer");
    }

    [Test]
    public async Task TheOutgoingDocumentIsUnloadedInOrderAndItsEngineIsDisposed()
    {
        await using var fixture = await LoopbackPage.CreateAsync(server => server
            .MapHtml(
                "/one.html",
                """
                <title>one</title>
                <script>
                  window.addEventListener('beforeunload', function () { console.log('beforeunload'); });
                  window.addEventListener('pagehide', function (e) { console.log('pagehide:' + e.persisted); });
                  window.addEventListener('unload', function () { console.log('unload'); });
                  window.marker = 'first document';
                  setInterval(function () { console.log('tick'); }, 5);
                </script>
                """)
            .MapHtml("/two.html", "<title>two</title>"));

        await fixture.Page.NavigateAsync(fixture.Url("/one.html"));
        await fixture.Page.WaitForIdleAsync(TimeSpan.FromMilliseconds(60));

        var first = await fixture.Page.RunOnLoopAsync(engine => engine);

        await fixture.Page.NavigateAsync(fixture.Url("/two.html"));

        fixture.Page.ConsoleMessages.Should().ContainInOrder("beforeunload", "pagehide:false", "unload");

        var second = await fixture.Page.RunOnLoopAsync(engine => engine);
        second.Should().NotBeSameAs(first, "a navigation is a new realm and therefore a new engine");

        (await fixture.Page.EvaluateAsync<string>("typeof window.marker"))
            .Should().Be("undefined", "the new document is a new realm");

        // The previous engine was disposed on the page loop, so nothing of it is pumped any more: its
        // interval, which was firing before the navigation, is silent afterwards.
        var ticksAtNavigation = fixture.Page.ConsoleMessages.Count(m => m == "tick");
        await fixture.Page.WaitForIdleAsync(TimeSpan.FromMilliseconds(60));
        fixture.Page.ConsoleMessages.Count(m => m == "tick").Should().Be(ticksAtNavigation);
    }

    [Test]
    public async Task ABeforeUnloadHandlerCannotStopANavigationUnlessTheCallerAllowsIt()
    {
        await using var fixture = await LoopbackPage.CreateAsync(server => server
            .MapHtml("/one.html", "<title>one</title><script>window.onbeforeunload = function () { return 'stay?' }</script>")
            .MapHtml("/two.html", "<title>two</title>"));

        await fixture.Page.NavigateAsync(fixture.Url("/one.html"));

        // The default: the handler runs, and the navigation happens anyway.
        await fixture.Page.NavigateAsync(fixture.Url("/two.html"));
        (await fixture.Page.TitleAsync()).Should().Be("two");

        await fixture.Page.NavigateAsync(fixture.Url("/one.html"));

        var act = async () => await fixture.Page.NavigateAsync(
            fixture.Url("/two.html"),
            new NavigationOptions { AllowCancel = true });

        (await act.Should().ThrowAsync<NavigationFailedException>()).WithMessage("*beforeunload*");
        (await fixture.Page.TitleAsync()).Should().Be("one", "the page stayed where it was");
    }

    [Test]
    public async Task PreventDefaultInABeforeUnloadListenerCancelsToo()
    {
        await using var fixture = await LoopbackPage.CreateAsync(server => server
            .MapHtml("/one.html", "<title>one</title><script>addEventListener('beforeunload', function (e) { e.preventDefault(); })</script>")
            .MapHtml("/two.html", "<title>two</title>"));

        await fixture.Page.NavigateAsync(fixture.Url("/one.html"));

        var act = async () => await fixture.Page.NavigateAsync(
            fixture.Url("/two.html"),
            new NavigationOptions { AllowCancel = true });

        await act.Should().ThrowAsync<NavigationFailedException>();
        (await fixture.Page.TitleAsync()).Should().Be("one");
    }

    [Test]
    public async Task AUrlTheContextsFilterRefusesNeverReachesTheSocket()
    {
        await using var fixture = await LoopbackPage.CreateAsync(
            configureContext: options => options.UrlFilter = _ => false);

        var act = async () => await fixture.Page.NavigateAsync(fixture.Url("/index.html"));

        (await act.Should().ThrowAsync<NavigationFailedException>()).WithMessage("*URL filter*");
        fixture.Server.Received.Should().BeEmpty();
    }

    [Test]
    public async Task ARedirectHopIsCheckedAgainstTheFilterToo()
    {
        LoopbackServer? server = null;

        await using var fixture = await LoopbackPage.CreateAsync(
            s =>
            {
                server = s;
                s.Map("/start", _ => LoopbackResponse.Redirect(302, "/forbidden"));
                s.MapHtml("/forbidden", "<title>should never be parsed</title>");
            },
            options => options.UrlFilter = uri => !uri.AbsolutePath.Contains("forbidden", StringComparison.Ordinal));

        var act = async () => await fixture.Page.NavigateAsync(fixture.Url("/start"));

        await act.Should().ThrowAsync<NavigationFailedException>();
        server!.Received.Should().ContainSingle(r => r.Path == "/start");
        server.Received.Should().NotContain(r => r.Path == "/forbidden");
    }

    [Test]
    public async Task AQueuedRendererNavigationKeepsTheBaseUrlItWasRequestedAgainst()
    {
        using var releaseFirst = new ManualResetEventSlim();
        using var releaseSecond = new ManualResetEventSlim();
        var firstRequested = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var secondRequested = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        var nextFilterChecks = 0;
        LoopbackServer? origin = null;

        LoopbackResponse Next(LoopbackRequest request)
        {
            secondRequested.TrySetResult(request.Path);
            releaseSecond.Wait(TimeSpan.FromSeconds(10));
            return LoopbackResponse.Html("<title>next</title>");
        }

        await using var fixture = await LoopbackPage.CreateAsync(
            server =>
            {
                origin = server;
                server.MapHtml("/base/index.html", "<title>base</title>");
                server.Map("/other/index.html", _ =>
                {
                    firstRequested.TrySetResult();
                    releaseFirst.Wait(TimeSpan.FromSeconds(10));
                    return LoopbackResponse.Html("<title>other</title>");
                });
                server.Map("/base/next.html", Next);
                server.Map("/other/next.html", Next);
            },
            options => options.UrlFilter = uri =>
            {
                if (uri.AbsolutePath.EndsWith("/next.html", StringComparison.Ordinal))
                {
                    Interlocked.Increment(ref nextFilterChecks);
                }

                return origin!.Owns(uri);
            });

        await fixture.Page.NavigateAsync(fixture.Url("/base/index.html"));

        var first = fixture.Page.NavigateAsync(fixture.Url("/other/index.html"));
        await firstRequested.Task.WaitAsync(TimeSpan.FromSeconds(10));
        await fixture.Page.EvaluateAsync("location.href = 'next.html'");
        releaseFirst.Set();
        await first;

        var secondPath = await secondRequested.Task.WaitAsync(TimeSpan.FromSeconds(10));
        var committed = fixture.Page.WaitForNavigationAsync(TimeSpan.FromSeconds(10));
        releaseSecond.Set();

        (await committed).Should().BeTrue();
        secondPath.Should().Be("/base/next.html");
        nextFilterChecks.Should().Be(1, "the request-time filter result is reused for the same absolute URL");
        fixture.Page.Url.Should().Be(fixture.Url("/base/next.html"));
        fixture.Server.Received.Single(request => request.Path == secondPath).Header("Referer")
            .Should().Be(fixture.Url("/base/index.html"));
        (await fixture.Page.EvaluateAsync<string>("document.referrer")).Should().Be(fixture.Url("/base/index.html"));
    }

    [Test]
    public async Task TheNetworkLogRecordsTheDocumentFetchAndAScriptsOwnRequest()
    {
        await using var fixture = await LoopbackPage.CreateAsync(server => server
            .MapHtml(
                "/index.html",
                """
                <title>logged</title>
                <script>
                  var xhr = new XMLHttpRequest();
                  xhr.open('GET', '/api', false);
                  xhr.send();
                  window.answer = xhr.responseText;
                </script>
                """)
            .Map("/api", _ => LoopbackResponse.Text("42")));

        await fixture.Page.NavigateAsync(fixture.Url("/index.html"));

        (await fixture.Page.EvaluateAsync<string>("window.answer")).Should().Be("42");

        var requests = fixture.Page.Requests;
        requests.Should().Contain(r => r.Url == fixture.Url("/index.html") && r.Initiator == RequestInitiator.Document && r.Status == 200);
        requests.Should().Contain(r => r.Url == fixture.Url("/api") && r.Initiator == RequestInitiator.Script && r.Status == 200);
    }

    /// <summary>A navigation that ran out of its deadline says so, rather than only saying it in English.</summary>
    /// <remarks>
    /// A timeout is the one failure a caller routinely tells apart — it says nothing about the page — and
    /// the flag is what makes telling it apart possible: the <c>Page</c> domain matched on the message, and
    /// a client library that reports its own timeout for the navigation it started needs the same answer.
    /// </remarks>
    [Test]
    public async Task ANavigationThatRanOutOfTimeSaysItTimedOut()
    {
        using var release = new SemaphoreSlim(0, 1);

        await using var fixture = await LoopbackPage.CreateAsync(server => server.Map("/slow.html", _ =>
        {
            release.Wait(TimeSpan.FromSeconds(10));
            return LoopbackResponse.Html("<title>too late</title>");
        }));

        var act = async () => await fixture.Page.NavigateAsync(
            fixture.Url("/slow.html"),
            new NavigationOptions { Timeout = TimeSpan.FromMilliseconds(50) });

        var failure = await act.Should().ThrowAsync<NavigationFailedException>();
        failure.Which.TimedOut.Should().BeTrue();
        failure.Which.Url.Should().Be(fixture.Url("/slow.html"));

        release.Release();
    }

    /// <summary>Every other failure is not a timeout, so a caller that retries a timeout does not retry it.</summary>
    [Test]
    public async Task ANavigationRefusedByTheFilterIsNotATimeout()
    {
        await using var fixture = await LoopbackPage.CreateAsync(
            server => server.MapHtml("/forbidden.html", "<title>no</title>"),
            options => options.UrlFilter = _ => false);

        var act = async () => await fixture.Page.NavigateAsync(fixture.Url("/forbidden.html"));

        var failure = await act.Should().ThrowAsync<NavigationFailedException>();
        failure.Which.TimedOut.Should().BeFalse();
    }

    [Test]
    public async Task ClosingThePageWhileANavigationIsInFlightCancelsIt()
    {
        var release = new SemaphoreSlim(0, 1);

        await using var fixture = await LoopbackPage.CreateAsync(server => server.Map("/slow.html", _ =>
        {
            release.Wait(TimeSpan.FromSeconds(10));
            return LoopbackResponse.Html("<title>too late</title>");
        }));

        var navigation = fixture.Page.NavigateAsync(fixture.Url("/slow.html"));

        // The server is holding the response, so the navigation is inside the fetch when the page closes.
        var closing = Task.Run(async () =>
        {
            await Task.Delay(50).ConfigureAwait(false);
            await fixture.Page.CloseAsync().ConfigureAwait(false);
        });

        var act = async () => await navigation;
        await act.Should().ThrowAsync<OperationCanceledException>();

        release.Release();
        await closing;
    }

    [Test]
    public async Task ANavigationThatOutlivesTheCloseStillEndsInCancellation()
    {
        var reached = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new SemaphoreSlim(0, 1);
        Func<Uri, bool>? owns = null;

        await using var fixture = await LoopbackPage.CreateAsync(
            server => server.MapHtml("/slow.html", "<title>too late</title>"),
            options =>
            {
                owns = options.UrlFilter;
                options.UrlFilter = uri =>
                {
                    // A host filter that takes a moment - a policy lookup, a name resolved by hand - is the
                    // ordinary shape, and it is what holds the navigation off the page's own thread while the
                    // page closes under it.
                    if (uri.AbsolutePath == "/slow.html")
                    {
                        reached.TrySetResult();
                        release.Wait(TimeSpan.FromSeconds(10));
                    }

                    return owns!(uri);
                };
            });

        var navigation = fixture.Page.NavigateAsync(fixture.Url("/slow.html"));

        await reached.Task.WaitAsync(TimeSpan.FromSeconds(10));

        // The whole page - its loop, its engine and the cancellation source behind them - is gone before the
        // filter returns, so every step the navigation has left meets a page that no longer exists.
        await fixture.Page.CloseAsync();
        release.Release();

        var act = async () => await navigation;

        await act.Should().ThrowAsync<OperationCanceledException>(
            "a caller who closed the page asked for the cancellation, whichever step of the navigation the close won");
    }

    [Test]
    public async Task ADataUrlAndAboutBlankStillLoadWithoutTheNetwork()
    {
        await using var fixture = await LoopbackPage.CreateAsync();

        var response = await fixture.Page.NavigateAsync("data:text/html,<title>inline</title>");

        response.Should().BeNull("nothing reached the network, so there is no response");
        (await fixture.Page.TitleAsync()).Should().Be("inline");

        await fixture.Page.NavigateAsync("about:blank");
        fixture.Page.Url.Should().Be("about:blank");
        fixture.Server.Received.Should().BeEmpty();
    }

    [Test]
    public async Task WaitUntilCommitAnswersBeforeTheLoadEventsHaveRun()
    {
        await using var fixture = await LoopbackPage.CreateAsync(server => server.MapHtml(
            "/index.html",
            "<title>committed</title>"));

        var response = await fixture.Page.NavigateAsync(
            fixture.Url("/index.html"),
            new NavigationOptions { WaitUntil = WaitUntilState.Commit });

        response!.Status.Should().Be(200);

        // Whatever the caller observed, the page still finishes the load: the next request queues behind it.
        (await fixture.Page.TitleAsync()).Should().Be("committed");
    }

    [Test]
    public async Task AFinishedNavigationCannotSatisfyAWaitArmedAfterIt()
    {
        await using var fixture = await LoopbackPage.CreateAsync(server => server
            .MapHtml(
                "/one",
                // The load handler holds the page's thread, which is what makes the window below a fixed
                // length rather than a handful of instructions only a loaded machine can lose the race in.
                "<title>one</title><script>addEventListener('load', function () {"
                + " var t = Date.now(); while (Date.now() - t < 200) {} });</script>")
            .MapHtml("/two", "<title>two</title>"));

        // Every phase signal NavigateAsync may answer on is raised inside the parse, so a page that woke its
        // navigation waiters afterwards would wake this one with the navigation that has already finished —
        // and the caller would go on to read the document it was waiting to leave. WaitUntil.Commit answers
        // before the load events are dispatched, so the wait below is armed while the page's thread is still
        // inside the handler above, and a wake placed after it would arrive squarely in the middle of it.
        await fixture.Page.NavigateAsync(fixture.Url("/one"), new NavigationOptions { WaitUntil = WaitUntilState.Commit });

        (await fixture.Page.WaitForNavigationAsync(TimeSpan.FromMilliseconds(300)))
            .Should().BeFalse("the navigation just awaited is over, so nothing is left to wait for");

        await fixture.NavigateByScriptAsync("location.assign('/two')");
        (await fixture.Page.TitleAsync()).Should().Be("two");
    }

    [Test]
    public async Task ABaseElementMovesTheDocumentsBaseUrlWithoutMovingItsUrl()
    {
        await using var fixture = await LoopbackPage.CreateAsync(server => server.MapHtml(
            "/deep/index.html",
            "<base href='/root/'><title>based</title>"));

        await fixture.Page.NavigateAsync(fixture.Url("/deep/index.html"));

        (await fixture.Page.EvaluateAsync<string>("document.baseURI")).Should().Be(fixture.Url("/root/"));
        (await fixture.Page.EvaluateAsync<string>("document.URL")).Should().Be(fixture.Url("/deep/index.html"));
        (await fixture.Page.EvaluateAsync<string>("location.href")).Should().Be(fixture.Url("/deep/index.html"));
    }
}
