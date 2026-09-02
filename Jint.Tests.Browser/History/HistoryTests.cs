using Jint.Browser;
using Jint.Tests.Browser.Navigation;

namespace Jint.Tests.Browser.History;

/// <summary>
/// The session history: <c>pushState</c>, traversal, <c>popstate</c>, <c>hashchange</c>, and the
/// <c>location</c> members that start a real navigation.
/// </summary>
public sealed class HistoryTests
{
    private const string Router =
        """
        <title>router</title>
        <script>
          window.seen = [];
          addEventListener('popstate', function (e) { window.seen.push('popstate:' + JSON.stringify(e.state)); });
          addEventListener('hashchange', function (e) { window.seen.push('hashchange:' + new URL(e.newURL).hash); });
        </script>
        """;

    [Test]
    public async Task PushStateMovesTheUrlWithoutReloadingAndTraversalFiresPopstate()
    {
        await using var fixture = await LoopbackPage.CreateAsync(server => server.MapHtml("/app", Router));

        await fixture.Page.NavigateAsync(fixture.Url("/app"));

        await fixture.Page.EvaluateAsync("history.pushState({ page: 1 }, '', '/app/one')");
        await fixture.Page.EvaluateAsync("history.pushState({ page: 2 }, '', '/app/two')");

        fixture.Page.Url.Should().Be(fixture.Url("/app/two"));
        (await fixture.Page.EvaluateAsync<string>("location.pathname")).Should().Be("/app/two");
        (await fixture.Page.EvaluateAsync<double>("history.length")).Should().Be(3);
        (await fixture.Page.EvaluateAsync<double>("history.state.page")).Should().Be(2);

        // No new document: the script that registered the listeners is still the one running.
        (await fixture.Page.EvaluateAsync<double>("window.seen.length")).Should().Be(0);

        await fixture.Page.EvaluateAsync("history.back()");
        (await fixture.Page.WaitForIdleAsync(TimeSpan.FromSeconds(2))).Should().BeTrue();

        fixture.Page.Url.Should().Be(fixture.Url("/app/one"));
        (await fixture.Page.EvaluateAsync<double>("history.state.page")).Should().Be(1);
        (await fixture.Page.EvaluateAsync<string>("window.seen.join('|')")).Should().Be("popstate:{\"page\":1}");

        await fixture.Page.EvaluateAsync("history.forward()");
        (await fixture.Page.WaitForIdleAsync(TimeSpan.FromSeconds(2))).Should().BeTrue();

        fixture.Page.Url.Should().Be(fixture.Url("/app/two"));
        (await fixture.Page.EvaluateAsync<double>("history.state.page")).Should().Be(2);

        // One document throughout: the server was asked for it exactly once.
        fixture.Server.Received.Count(r => r.Path == "/app").Should().Be(1);
    }

    [Test]
    public async Task ATraversalIsAsynchronousSoTheNextStatementStillSeesTheOldUrl()
    {
        await using var fixture = await LoopbackPage.CreateAsync(server => server.MapHtml("/app", Router));

        await fixture.Page.NavigateAsync(fixture.Url("/app"));
        await fixture.Page.EvaluateAsync("history.pushState(null, '', '/app/one')");

        var immediate = await fixture.Page.EvaluateAsync<string>("history.back(); location.pathname");
        immediate.Should().Be("/app/one", "HTML queues a traversal rather than performing it inline");

        (await fixture.Page.WaitForIdleAsync(TimeSpan.FromSeconds(2))).Should().BeTrue();
        (await fixture.Page.EvaluateAsync<string>("location.pathname")).Should().Be("/app");
    }

    [Test]
    public async Task ReplaceStateRewritesTheCurrentEntryAndAddsNone()
    {
        await using var fixture = await LoopbackPage.CreateAsync(server => server.MapHtml("/app", Router));

        await fixture.Page.NavigateAsync(fixture.Url("/app"));

        var before = await fixture.Page.EvaluateAsync<double>("history.length");
        await fixture.Page.EvaluateAsync("history.replaceState({ replaced: true }, '', '/app/rewritten')");

        (await fixture.Page.EvaluateAsync<double>("history.length")).Should().Be(before);
        fixture.Page.Url.Should().Be(fixture.Url("/app/rewritten"));
        (await fixture.Page.EvaluateAsync<bool>("history.state.replaced")).Should().BeTrue();
    }

    [Test]
    public async Task PushStateRefusesACrossOriginUrl()
    {
        await using var fixture = await LoopbackPage.CreateAsync(server => server.MapHtml("/app", Router));

        await fixture.Page.NavigateAsync(fixture.Url("/app"));

        var name = await fixture.Page.EvaluateAsync<string>(
            "try { history.pushState(null, '', 'https://example.org/elsewhere'); 'no throw' } catch (e) { e.name }");

        name.Should().Be("SecurityError");
        fixture.Page.Url.Should().Be(fixture.Url("/app"));
    }

    [Test]
    public async Task AFragmentNavigationKeepsTheDocumentAndFiresHashchange()
    {
        await using var fixture = await LoopbackPage.CreateAsync(server => server.MapHtml("/app", Router));

        await fixture.Page.NavigateAsync(fixture.Url("/app"));

        await fixture.Page.EvaluateAsync("location.hash = '#section'");
        (await fixture.Page.WaitForNavigationAsync(TimeSpan.FromSeconds(2))).Should().BeTrue();

        fixture.Page.Url.Should().Be(fixture.Url("/app#section"));
        (await fixture.Page.EvaluateAsync<string>("location.hash")).Should().Be("#section");
        (await fixture.Page.EvaluateAsync<string>("window.seen.join('|')")).Should().Be("hashchange:#section");
        fixture.Server.Received.Count(r => r.Path == "/app").Should().Be(1, "a fragment navigation reaches no network");
    }

    [Test]
    public async Task LocationAssignNavigatesAndLocationReplaceLeavesNoEntry()
    {
        await using var fixture = await LoopbackPage.CreateAsync(server => server
            .MapHtml("/one", "<title>one</title>")
            .MapHtml("/two", "<title>two</title>")
            .MapHtml("/three", "<title>three</title>"));

        await fixture.Page.NavigateAsync(fixture.Url("/one"));
        var afterFirst = await fixture.Page.EvaluateAsync<double>("history.length");

        await fixture.Page.EvaluateAsync("location.assign('/two')");
        (await fixture.Page.WaitForNavigationAsync(TimeSpan.FromSeconds(5))).Should().BeTrue();

        (await fixture.Page.TitleAsync()).Should().Be("two");
        (await fixture.Page.EvaluateAsync<double>("history.length")).Should().Be(afterFirst + 1);

        await fixture.Page.EvaluateAsync("location.replace('/three')");
        (await fixture.Page.WaitForNavigationAsync(TimeSpan.FromSeconds(5))).Should().BeTrue();

        (await fixture.Page.TitleAsync()).Should().Be("three");
        (await fixture.Page.EvaluateAsync<double>("history.length"))
            .Should().Be(afterFirst + 1, "replace rewrites the current entry rather than adding one");
    }

    [Test]
    public async Task AssigningALocationComponentIsANavigation()
    {
        await using var fixture = await LoopbackPage.CreateAsync(server => server
            .MapHtml("/one", "<title>one</title>")
            .MapHtml("/moved", "<title>moved</title>"));

        await fixture.Page.NavigateAsync(fixture.Url("/one"));

        await fixture.Page.EvaluateAsync("location.pathname = '/moved'");
        (await fixture.Page.WaitForNavigationAsync(TimeSpan.FromSeconds(5))).Should().BeTrue();

        (await fixture.Page.TitleAsync()).Should().Be("moved");
        fixture.Page.Url.Should().Be(fixture.Url("/moved"));
    }

    [Test]
    public async Task LocationReadsEveryComponentFromThePagesOwnUrl()
    {
        await using var fixture = await LoopbackPage.CreateAsync(server => server.MapHtml("/deep/page", "<title>deep</title>"));

        await fixture.Page.NavigateAsync(fixture.Url("/deep/page?q=1#frag"));

        (await fixture.Page.EvaluateAsync<string>("location.protocol")).Should().Be("http:");
        (await fixture.Page.EvaluateAsync<string>("location.hostname")).Should().Be("127.0.0.1");
        (await fixture.Page.EvaluateAsync<string>("location.port")).Should().Be(fixture.Server.Port.ToString(System.Globalization.CultureInfo.InvariantCulture));
        (await fixture.Page.EvaluateAsync<string>("location.pathname")).Should().Be("/deep/page");
        (await fixture.Page.EvaluateAsync<string>("location.search")).Should().Be("?q=1");
        (await fixture.Page.EvaluateAsync<string>("location.hash")).Should().Be("#frag");
        (await fixture.Page.EvaluateAsync<string>("location.origin")).Should().Be(fixture.Server.Origin);
        (await fixture.Page.EvaluateAsync<string>("String(location)")).Should().Be(fixture.Url("/deep/page?q=1#frag"));
    }

    [Test]
    public async Task ReloadRefetchesEvenFromAUrlThatCarriesAFragment()
    {
        await using var fixture = await LoopbackPage.CreateAsync(server => server.MapHtml("/page", "<title>page</title>"));

        await fixture.Page.NavigateAsync(fixture.Url("/page#section"));
        fixture.Server.Received.Count(r => r.Path == "/page").Should().Be(1);

        await fixture.Page.EvaluateAsync("location.reload()");
        (await fixture.Page.WaitForNavigationAsync(TimeSpan.FromSeconds(5))).Should().BeTrue();

        fixture.Server.Received.Count(r => r.Path == "/page")
            .Should().Be(2, "reload says so outright, so the fragment does not turn it into a no-op");
        (await fixture.Page.EvaluateAsync<string>("location.hash")).Should().Be("#section");
    }

    [Test]
    public async Task NavigatingToTheSameUrlWithoutAFragmentIsAReload()
    {
        await using var fixture = await LoopbackPage.CreateAsync(server => server.MapHtml("/page", "<title>page</title>"));

        await fixture.Page.NavigateAsync(fixture.Url("/page"));
        await fixture.Page.NavigateAsync(fixture.Url("/page"));

        fixture.Server.Received.Count(r => r.Path == "/page")
            .Should().Be(2, "a URL with a null fragment is never a fragment navigation");
    }

    [Test]
    public async Task NavigatingToTheSameUrlWithAFragmentKeepsTheDocument()
    {
        await using var fixture = await LoopbackPage.CreateAsync(server => server.MapHtml(
            "/page",
            "<title>page</title><script>window.marker = 'first load'</script>"));

        await fixture.Page.NavigateAsync(fixture.Url("/page"));
        await fixture.Page.NavigateAsync(fixture.Url("/page#section"));

        fixture.Server.Received.Count(r => r.Path == "/page").Should().Be(1);
        (await fixture.Page.EvaluateAsync<string>("window.marker"))
            .Should().Be("first load", "a fragment navigation keeps the engine, so nothing on it is lost");
        (await fixture.Page.EvaluateAsync<string>("location.hash")).Should().Be("#section");
    }

    [Test]
    public async Task GoingBackAcrossDocumentsLoadsTheOldOneAgain()
    {
        await using var fixture = await LoopbackPage.CreateAsync(server => server
            .MapHtml("/one", "<title>one</title>")
            .MapHtml("/two", "<title>two</title>"));

        await fixture.Page.NavigateAsync(fixture.Url("/one"));
        await fixture.Page.NavigateAsync(fixture.Url("/two"));

        await fixture.Page.EvaluateAsync("history.back()");
        (await fixture.Page.WaitForNavigationAsync(TimeSpan.FromSeconds(5))).Should().BeTrue();

        (await fixture.Page.TitleAsync()).Should().Be("one");
        fixture.Page.Url.Should().Be(fixture.Url("/one"));
        fixture.Server.Received.Count(r => r.Path == "/one").Should().Be(2, "there is no back/forward cache, so the document loads again");
    }

    [Test]
    public async Task GoingBackFromTheFirstEntryDoesNothing()
    {
        await using var fixture = await LoopbackPage.CreateAsync(server => server.MapHtml("/only", "<title>only</title>"));

        await fixture.Page.NavigateAsync(fixture.Url("/only"));

        await fixture.Page.EvaluateAsync("history.go(-10)");
        (await fixture.Page.WaitForIdleAsync(TimeSpan.FromMilliseconds(200))).Should().BeTrue();

        fixture.Page.Url.Should().Be(fixture.Url("/only"));
        fixture.Page.Errors.Should().BeEmpty();
    }

    [Test]
    public async Task ANewNavigationDropsTheForwardEntries()
    {
        await using var fixture = await LoopbackPage.CreateAsync(server => server.MapHtml("/app", Router));

        await fixture.Page.NavigateAsync(fixture.Url("/app"));
        await fixture.Page.EvaluateAsync("history.pushState(null, '', '/app/one')");
        await fixture.Page.EvaluateAsync("history.pushState(null, '', '/app/two')");

        await fixture.Page.EvaluateAsync("history.back()");
        (await fixture.Page.WaitForIdleAsync(TimeSpan.FromSeconds(2))).Should().BeTrue();

        await fixture.Page.EvaluateAsync("history.pushState(null, '', '/app/three')");

        (await fixture.Page.EvaluateAsync<double>("history.length")).Should().Be(3, "/app, /app/one and /app/three");
        fixture.Page.Url.Should().Be(fixture.Url("/app/three"));
    }
}
