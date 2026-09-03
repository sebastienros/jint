using System.Globalization;
using Jint.Browser;
using Jint.Tests.Browser.Navigation;

namespace Jint.Tests.Browser.Fixtures;

/// <summary>
/// The fixtures that are the platform rather than a library: a router, a module graph, a fetch, a form that
/// redirects, a cookie login, storage across navigations, the two observers, and dialogs.
/// </summary>
/// <remarks>
/// Nothing here is vendored. Each is the smallest page that makes one browser mechanism observable end to
/// end, and together they are what a framework fixture stands on — so a framework case that fails and a
/// platform case that passes localizes the fault to the library, and the other way round.
/// </remarks>
public class PlatformFixtureTests
{
    /// <summary>A router made of intercepted clicks, <c>pushState</c> and <c>popstate</c>.</summary>
    [Test]
    public async Task APushStateRouterMovesForwardsAndBackwards()
    {
        await using var course = await FixtureCourse.OpenAsync("spa-router");

        await course.UntilAsync("document.querySelector('#view').textContent", "the home view");

        await course.ClickAsync(".route[href='/spa-router/about']");
        await course.UntilAsync("document.querySelector('#view').textContent", "the about view");
        await course.UntilAsync("location.pathname", "/spa-router/about");

        await course.ClickAsync(".route[href='/spa-router/contact']");
        await course.UntilAsync("location.pathname", "/spa-router/contact");

        // A traversal among pushState siblings is same-document: popstate, no fetch, no new engine. The
        // router's own counter is what says the event arrived rather than merely the URL moving.
        await course.Page.EvaluateAsync("history.back()");
        await course.UntilAsync("location.pathname", "/spa-router/about");
        await course.UntilAsync("document.querySelector('#pops').textContent", "1");
        await course.UntilAsync("document.querySelector('#view').textContent", "the about view");

        await course.Page.EvaluateAsync("history.forward()");
        await course.UntilAsync("location.pathname", "/spa-router/contact");
        await course.UntilAsync("document.querySelector('#pops').textContent", "2");

        // The router intercepted nothing it did not claim, so this link is an ordinary navigation.
        await course.ClickAndNavigateAsync("#external");
        (await course.TextAsync("#left")).Should().Be("a real navigation, not a pushState");

        course.ShouldHaveReportedNothing();
    }

    /// <summary>An import map, a bare specifier, a mapped directory and a dynamic import.</summary>
    [Test]
    public async Task ModulesResolveThroughAnImportMap()
    {
        await using var course = await FixtureCourse.OpenAsync("modules-importmap");

        await course.UntilAsync("document.querySelector('#counter').textContent", "counter:1:2");
        (await course.TextAsync("#label")).Should().Be("[mapped]");

        // A module script is deferred, so the document was parsed before it ran.
        (await course.TextAsync("#order")).Should().BeOneOf("interactive", "complete");

        // And the map is still in force for an import() made after the graph was evaluated.
        await course.UntilAsync("document.querySelector('#late').textContent", "late, and the map still applies: 3");

        course.ShouldHaveReportedNothing();
    }

    /// <summary>A <c>fetch</c> of JSON, rendered — and the request the page's own log recorded.</summary>
    [Test]
    public async Task FetchRendersAListAndTheRequestIsInTheLog()
    {
        await using var course = await FixtureCourse.OpenAsync("fetch-json");

        await course.UntilAsync("document.querySelectorAll('#rows li').length", "3");
        (await course.TextsAsync("#rows li")).Should().Be("alpha (1)|beta (2)|gamma (3)");
        (await course.TextAsync("#status")).Should().Be("200 application/json");

        // The header the script set really went out, which is what says the fetch is the page's own.
        course.Server.Received.Single(request => request.Path == "/fetch-json/rows.json")
            .Header("X-Fixture").Should().Be("fetch-json");

        // The same request, in the log a host reads.
        course.Page.Requests.Should().Contain(request =>
            request.Url.EndsWith("/fetch-json/rows.json", StringComparison.Ordinal)
            && request.Status == 200
            && request.Initiator == RequestInitiator.Script);

        // And the button makes the same request again, which is a second fetch rather than a cache read:
        // there is no cache.
        await course.ClickAsync("#reload");
        await course.WaitForAsync(
            () => course.Server.Received.Count(request => request.Path == "/fetch-json/rows.json") == 2,
            "the reload button should have made a second request");

        course.ShouldHaveReportedNothing();
    }

    /// <summary>A <c>POST</c>, a <c>303</c>, and the <c>GET</c> that shows what the server read.</summary>
    /// <remarks>
    /// Three things at once: the entry list a form builds (a checked box is in it, an unchecked one is not,
    /// the submitter's own name/value is, and a <c>select</c> contributes its selected option), the
    /// urlencoded body, and a <c>303</c> turning the method into <c>GET</c> — which is the whole reason the
    /// pattern exists.
    /// </remarks>
    [Test]
    public async Task AFormPostsIsRedirectedAndTheNextPageShowsWhatWasSubmitted()
    {
        string? body = null;
        string? method = null;

        await using var course = await FixtureCourse.OpenAsync("form-redirect", server => server.Map(
            "/form-redirect/submit",
            request =>
            {
                method = request.Method;
                body = request.Body;
                return LoopbackResponse.Redirect(303, "/form-redirect/done.html?" + request.Body);
            }));

        await course.ClickAndNavigateAsync("#place");

        method.Should().Be("POST");
        body.Should().Be("item=a+widget&quantity=3&colour=blue&gift=yes&token=abc123&action=place");

        (await course.TextAsync("#method")).Should().Be("arrived by GET at /form-redirect/done.html");
        (await course.TextsAsync("#order dt")).Should().Be("action|colour|gift|item|quantity|token");
        (await course.TextsAsync("#order dd")).Should().Be("place|blue|yes|a widget|3|abc123");

        // The second request really was a GET, and the server saw no body on it.
        var redirected = course.Server.Received.Single(request => request.Path == "/form-redirect/done.html");
        redirected.Method.Should().Be("GET");
        redirected.Body.Should().BeEmpty();

        course.ShouldHaveReportedNothing();
    }

    /// <summary>
    /// A form sets a cookie through <c>Set-Cookie</c>, the next request carries it, and a protected page
    /// renders.
    /// </summary>
    [Test]
    public async Task ACookieLoginReachesAProtectedPage()
    {
        await using var course = await FixtureCourse.OpenAsync("cookie-login", server =>
        {
            server.Map("/cookie-login/session", request =>
            {
                var credentials = request.Body.Contains("user=ada", StringComparison.Ordinal)
                    && request.Body.Contains("password=lovelace", StringComparison.Ordinal);

                if (!credentials)
                {
                    return LoopbackResponse.Redirect(303, "/cookie-login/index.html");
                }

                return LoopbackResponse.Redirect(303, "/cookie-login/secret")
                    .With("Set-Cookie", "session=ada-is-in; Path=/; HttpOnly")
                    .With("Set-Cookie", "theme=dark; Path=/");
            });

            server.Map("/cookie-login/secret", request =>
            {
                var cookie = request.Header("Cookie") ?? "";

                return cookie.Contains("session=ada-is-in", StringComparison.Ordinal)
                    ? LoopbackResponse.Html(
                        "<!doctype html><html><head><title>Secret</title></head><body>"
                        + "<p id='welcome'>welcome, ada</p>"
                        + "<p id='cookies'></p>"
                        + "<script>document.getElementById('cookies').textContent = document.cookie;</script>"
                        + "</body></html>")
                    : new LoopbackResponse
                    {
                        Status = 401,
                        Reason = "Unauthorized",
                        Body = "<!doctype html><html><body><p id='denied'>no session</p></body></html>",
                    }.With("Content-Type", "text/html; charset=utf-8");
            });
        });

        (await course.TextAsync("#cookies")).Should().Be("no cookies yet");

        await course.ClickAndNavigateAsync("#sign-in");

        (await course.TextAsync("#welcome")).Should().Be("welcome, ada");

        // The jar carried the cookie to the protected page…
        course.Server.Received.Single(request => request.Path == "/cookie-login/secret")
            .Header("Cookie").Should().Contain("session=ada-is-in");

        // …and the HttpOnly half of it is invisible to the document, which is what HttpOnly is for.
        var visible = await course.TextAsync("#cookies");
        visible.Should().Contain("theme=dark");
        visible.Should().NotContain("session=", "an HttpOnly cookie is not readable from script");

        course.ShouldHaveReportedNothing();
    }

    /// <summary>Storage that outlives a navigation, and the one kind that does not outlive a page.</summary>
    [Test]
    public async Task LocalStorageSurvivesTwoNavigationsAndSessionStorageDoesNotLeaveThePage()
    {
        await using var course = await FixtureCourse.OpenAsync("local-storage");

        (await course.TextAsync("#visits")).Should().Be("1");
        (await course.TextAsync("#note")).Should().Be("none");

        await course.ClickAsync("#remember");
        (await course.TextAsync("#note")).Should().Be("written on visit 1");

        // A navigation is a new engine and a new document, and the partition is the context's.
        await course.GoAsync("local-storage");
        (await course.TextAsync("#visits")).Should().Be("2");
        (await course.TextAsync("#note")).Should().Be("written on visit 1");
        (await course.TextAsync("#session")).Should().Be("2");

        await course.GoAsync("local-storage");
        (await course.TextAsync("#visits")).Should().Be("3");

        // A second page of the same context shares the local partition and not the session one.
        var second = await course.Context.NewPageAsync();
        await second.NavigateAsync(course.Url("/local-storage/index.html"));

        (await second.EvaluateAsync<string>("document.querySelector('#visits').textContent")).Should().Be("4");
        (await second.EvaluateAsync<string>("document.querySelector('#session').textContent")).Should().Be("1");
        (await second.EvaluateAsync<string>("document.querySelector('#note').textContent")).Should().Be("written on visit 1");

        second.Errors.Should().BeEmpty();
        course.ShouldHaveReportedNothing();
    }

    /// <summary>
    /// A lazy list on <c>IntersectionObserver</c>, which loads <i>every</i> page because everything
    /// intersects exactly once.
    /// </summary>
    /// <remarks>
    /// <b>This asserts the documented divergence, not the browser behaviour.</b> With no layout there is
    /// nothing that can stop intersecting, so a target is reported once and never again — and the honest
    /// consequence is that an infinite list is a finite one loaded all at once. The alternative, "never
    /// intersecting", leaves every such list and every reveal-on-scroll panel permanently empty, which is
    /// worse for the reader this browser is for.
    /// </remarks>
    [Test]
    public async Task ALazyListLoadsEveryPageBecauseEverythingIntersectsOnce()
    {
        await using var course = await FixtureCourse.OpenAsync("intersection-observer");

        await course.UntilAsync("document.querySelector('#pages').textContent", "3");
        (await course.CountAsync("#rows li")).Should().Be(6);
        (await course.TextsAsync("#rows li")).Should().Be(
            "page 1 row 1|page 1 row 2|page 2 row 1|page 2 row 2|page 3 row 1|page 3 row 2");

        // Every sentinel was consumed, so nothing is left waiting for a scroll that will never happen.
        (await course.CountAsync(".sentinel")).Should().Be(0);

        // The reveal half, with a real rectangle from the flat box model rather than a zero.
        await course.UntilAsync("document.querySelector('#reveal').classList.contains('visible')", "true");
        (await course.TextAsync("#revealed")).Should().StartWith("yes at ratio 1 of ");
        (await course.TextAsync("#revealed")).Should().NotEndWith(" 0px");

        course.ShouldHaveReportedNothing();
    }

    /// <summary>A widget kept in step with its subtree by a <c>MutationObserver</c>.</summary>
    [Test]
    public async Task AMutationObserverWidgetFollowsTheTree()
    {
        await using var course = await FixtureCourse.OpenAsync("mutation-observer");

        (await course.TextAsync("#count")).Should().Be("0", "nothing has been mutated yet");

        // Two mutations in one turn are one batch, which is what "delivered at the microtask checkpoint"
        // means and what a widget depends on for not doing its work twice.
        await course.Page.EvaluateAsync("__add('two'); __add('three');");
        await course.SettleAsync();

        await course.UntilAsync("document.querySelector('#count').textContent", "3");
        (await course.TextAsync("#batches")).Should().Be("1");

        // An attribute the observer filtered for, with its old value.
        await course.Page.EvaluateAsync("document.getElementById('widget').setAttribute('data-state', 'busy')");
        await course.SettleAsync();
        await course.UntilAsync("document.querySelector('#state').textContent", "ready -> busy");

        // One it did not ask for changes nothing.
        var batches = await course.TextAsync("#batches");
        await course.Page.EvaluateAsync("document.getElementById('widget').setAttribute('data-other', 'x')");
        await course.SettleAsync();
        (await course.TextAsync("#batches")).Should().Be(batches);

        // And character data, with its old value.
        await course.Page.EvaluateAsync("document.querySelector('#items li').firstChild.data = 'ONE'");
        await course.SettleAsync();
        await course.UntilAsync("document.querySelector('#text').textContent", "one -> ONE");

        course.ShouldHaveReportedNothing();
    }

    /// <summary>The three dialogs, answered by a host that holds a standing decision.</summary>
    [Test]
    public async Task DialogsAreAnsweredByTheHost()
    {
        await using var course = await FixtureCourse.OpenAsync("dialogs");

        var seen = new List<string>();

        course.Page.DialogOpened += (_, args) =>
        {
            seen.Add(args.Kind + ":" + args.Message + ":" + args.DefaultPromptText);
            args.Accepted = true;
            args.PromptText = "Ada";
        };

        await course.ClickAsync("#warn");
        await course.UntilAsync("document.querySelector('#alerted').textContent", "yes");

        await course.ClickAsync("#ask");
        await course.UntilAsync("document.querySelector('#confirmed').textContent", "true");

        await course.ClickAsync("#name");
        await course.UntilAsync("document.querySelector('#prompted').textContent", "Ada");

        seen.Should().Equal(
            "Alert:the file could not be saved:",
            "Confirm:delete everything?:",
            "Prompt:what is your name?:anonymous");

        course.ShouldHaveReportedNothing();
    }

    /// <summary>With no handler, every dialog is dismissed — which is what a browser does to an unattended tab.</summary>
    [Test]
    public async Task AnUnhandledDialogIsDismissed()
    {
        await using var course = await FixtureCourse.OpenAsync("dialogs");

        await course.ClickAsync("#warn");
        await course.UntilAsync("document.querySelector('#alerted').textContent", "yes");

        await course.ClickAsync("#ask");
        await course.UntilAsync("document.querySelector('#confirmed').textContent", "false");

        await course.ClickAsync("#name");
        await course.UntilAsync("document.querySelector('#prompted').textContent", "null");

        course.ShouldHaveReportedNothing();
    }

    /// <summary>Every fixture answers over the socket rather than out of a string.</summary>
    /// <remarks>
    /// A course whose files were inlined into <c>SetContentAsync</c> would be a course with no origin, no
    /// subresource, no cookie jar and no redirect — so this is the one assertion that keeps the rest honest.
    /// </remarks>
    [Test]
    public async Task EveryFixtureIsFetchedFromTheOrigin()
    {
        await using var course = await FixtureCourse.OpenAsync("fetch-json");

        course.Server.Received.Should().Contain(request => request.Path == "/fetch-json/index.html");
        course.Server.Received.Should().Contain(request => request.Path == "/fetch-json/app.js");

        course.Page.Url.Should().StartWith(course.Server.Origin);
        course.Page.Requests.Count.Should().BeGreaterThan(
            2,
            "the document, its script and its data are three requests: " + course.Page.Requests.Count.ToString(CultureInfo.InvariantCulture));
    }
}
