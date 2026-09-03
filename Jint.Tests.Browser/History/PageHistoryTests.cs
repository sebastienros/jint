using Jint.Tests.Browser.Navigation;

namespace Jint.Tests.Browser.History;

/// <summary>
/// The three buttons above the page: back, forward and reload, over the session history a page's own
/// <c>history</c> object moves.
/// </summary>
public sealed class PageHistoryTests
{
    private static readonly TimeSpan Patience = TimeSpan.FromSeconds(30);

    [Test]
    public async Task BackAndForwardMoveAcrossDocuments()
    {
        await using var fixture = await LoopbackPage.CreateAsync(server => server
            .MapHtml("/one", "<!doctype html><title>One</title>")
            .MapHtml("/two", "<!doctype html><title>Two</title>"));

        await fixture.Page.NavigateAsync(fixture.Url("/one"));
        await fixture.Page.NavigateAsync(fixture.Url("/two"));

        (await fixture.Page.GoBackAsync(Patience)).Should().BeTrue();
        (await fixture.Page.TitleAsync()).Should().Be("One");

        (await fixture.Page.GoForwardAsync(Patience)).Should().BeTrue();
        (await fixture.Page.TitleAsync()).Should().Be("Two");
    }

    [Test]
    public async Task AStepWithNowhereToGoIsFalseAtOnce()
    {
        await using var fixture = await LoopbackPage.CreateAsync(
            server => server.MapHtml("/one", "<!doctype html><title>One</title>"));

        await fixture.Page.NavigateAsync(fixture.Url("/one"));

        // A generous ceiling, because what is being asserted is that it does not wait it out: an answer that
        // took the whole timeout would still be false and the test would still pass on the value alone.
        var started = System.Diagnostics.Stopwatch.GetTimestamp();
        (await fixture.Page.GoForwardAsync(Patience)).Should().BeFalse();

        System.Diagnostics.Stopwatch.GetElapsedTime(started).Should().BeLessThan(TimeSpan.FromSeconds(5));
    }

    [Test]
    public async Task BackAcrossAPushStateStaysInTheDocument()
    {
        await using var fixture = await LoopbackPage.CreateAsync(server => server
            .MapHtml("/app", """
                <!doctype html><title>App</title>
                <script>
                  window.pops = 0;
                  addEventListener('popstate', () => window.pops++);
                  history.pushState({}, '', '/app/second');
                </script>
                """));

        await fixture.Page.NavigateAsync(fixture.Url("/app"));
        fixture.Page.Url.Should().EndWith("/app/second");

        (await fixture.Page.GoBackAsync(Patience)).Should().BeTrue();

        fixture.Page.Url.Should().EndWith("/app");
        (await fixture.Page.EvaluateAsync<double>("window.pops")).Should().Be(1, "a step inside one document is a popstate, not a fetch");
    }

    [Test]
    public async Task AReloadFetchesAgainAndDoesNotLengthenTheHistory()
    {
        await using var fixture = await LoopbackPage.CreateAsync(
            server => server.MapHtml("/page", "<!doctype html><title>Page</title>"));

        await fixture.Page.NavigateAsync(fixture.Url("/page"));
        var before = await fixture.Page.EvaluateAsync<double>("history.length");

        var response = await fixture.Page.ReloadAsync();

        response.Should().NotBeNull();
        fixture.Server.Received.Count(r => r.Path == "/page").Should().Be(2);
        (await fixture.Page.EvaluateAsync<double>("history.length")).Should().Be(before);
    }
}
