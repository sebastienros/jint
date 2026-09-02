using Jint.Browser;
using Jint.Tests.Browser.Navigation;

namespace Jint.Tests.Browser.Storage;

/// <summary>
/// Storage is partitioned by origin: <c>localStorage</c> per context and origin, <c>sessionStorage</c> per
/// page, and neither for a document that has no origin at all.
/// </summary>
public sealed class StorageTests
{
    private const string Page = "<title>storage</title>";

    [Test]
    public async Task TwoPagesOfOneOriginInOneContextShareLocalStorage()
    {
        await using var fixture = await LoopbackPage.CreateAsync(server => server.MapHtml("/index.html", Page));

        await fixture.Page.NavigateAsync(fixture.Url("/index.html"));
        await fixture.Page.EvaluateAsync("localStorage.setItem('token', 'shared')");

        var sibling = await fixture.NewPageAsync();
        await sibling.NavigateAsync(fixture.Url("/index.html"));

        (await sibling.EvaluateAsync<string>("localStorage.getItem('token')")).Should().Be("shared");
    }

    [Test]
    public async Task LocalStorageSurvivesANavigationWithinTheSamePage()
    {
        await using var fixture = await LoopbackPage.CreateAsync(server => server
            .MapHtml("/one.html", Page)
            .MapHtml("/two.html", Page));

        await fixture.Page.NavigateAsync(fixture.Url("/one.html"));
        await fixture.Page.EvaluateAsync("localStorage.setItem('kept', 'yes')");

        await fixture.Page.NavigateAsync(fixture.Url("/two.html"));

        (await fixture.Page.EvaluateAsync<string>("localStorage.getItem('kept')"))
            .Should().Be("yes", "a navigation is a new engine, and storage outlives an engine");
    }

    [Test]
    public async Task TwoContextsDoNotShareLocalStorage()
    {
        await using var fixture = await LoopbackPage.CreateAsync(server => server.MapHtml("/index.html", Page));

        await fixture.Page.NavigateAsync(fixture.Url("/index.html"));
        await fixture.Page.EvaluateAsync("localStorage.setItem('token', 'first-context')");

        var stranger = await fixture.NewIsolatedPageAsync();
        await stranger.NavigateAsync(fixture.Url("/index.html"));

        (await stranger.EvaluateAsync<string>("localStorage.getItem('token')")).Should().BeNull();
    }

    [Test]
    public async Task TwoOriginsDoNotShareLocalStorage()
    {
        using var other = new LoopbackServer();
        other.MapHtml("/index.html", Page);

        await using var fixture = await LoopbackPage.CreateAsync(
            server => server.MapHtml("/index.html", Page),
            options => options.UrlFilter = uri => uri.IsLoopback);

        await fixture.Page.NavigateAsync(fixture.Url("/index.html"));
        await fixture.Page.EvaluateAsync("localStorage.setItem('token', 'first-origin')");

        // A different port is a different origin, which is what the storage partition keys on.
        await fixture.Page.NavigateAsync(other.Url("/index.html"));

        (await fixture.Page.EvaluateAsync<string>("localStorage.getItem('token')")).Should().BeNull();
        (await fixture.Page.EvaluateAsync<double>("localStorage.length")).Should().Be(0);
    }

    [Test]
    public async Task SessionStorageIsPerPageAndSurvivesANavigation()
    {
        await using var fixture = await LoopbackPage.CreateAsync(server => server
            .MapHtml("/one.html", Page)
            .MapHtml("/two.html", Page));

        await fixture.Page.NavigateAsync(fixture.Url("/one.html"));
        await fixture.Page.EvaluateAsync("sessionStorage.setItem('tab', 'first')");

        await fixture.Page.NavigateAsync(fixture.Url("/two.html"));
        (await fixture.Page.EvaluateAsync<string>("sessionStorage.getItem('tab')")).Should().Be("first");

        var sibling = await fixture.NewPageAsync();
        await sibling.NavigateAsync(fixture.Url("/one.html"));

        (await sibling.EvaluateAsync<string>("sessionStorage.getItem('tab')"))
            .Should().BeNull("sessionStorage is per page, which is the lifetime its name promises");
    }

    [Test]
    public async Task ADocumentWithAnOpaqueOriginGetsASecurityError()
    {
        await using var fixture = await LoopbackPage.CreateAsync();

        foreach (var member in (string[]) ["localStorage", "sessionStorage"])
        {
            var name = await fixture.Page.EvaluateAsync<string>(
                "try { " + member + "; 'no throw' } catch (e) { e.name }");

            name.Should().Be("SecurityError", member + " is unreachable from a document with no origin");
        }

        // A data: URL and content a host set are the same case.
        await fixture.Page.NavigateAsync("data:text/html,<title>opaque</title>");
        (await fixture.Page.EvaluateAsync<string>("try { localStorage; 'no throw' } catch (e) { e.name }"))
            .Should().Be("SecurityError");

        await fixture.Page.SetContentAsync("<title>set</title>");
        (await fixture.Page.EvaluateAsync<string>("try { localStorage; 'no throw' } catch (e) { e.name }"))
            .Should().Be("SecurityError");
    }

    [Test]
    public async Task ContentGivenAnOriginBearingBaseUrlReachesThatOriginsStorage()
    {
        await using var fixture = await LoopbackPage.CreateAsync(server => server.MapHtml("/index.html", Page));

        await fixture.Page.NavigateAsync(fixture.Url("/index.html"));
        await fixture.Page.EvaluateAsync("localStorage.setItem('token', 'from-the-network')");

        await fixture.Page.SetContentAsync("<title>set</title>", fixture.Url("/other.html"));

        (await fixture.Page.EvaluateAsync<string>("localStorage.getItem('token')"))
            .Should().Be("from-the-network", "a base URL is an origin, and storage is partitioned by origin");
    }

    [Test]
    public async Task AHostsOwnPartitionIsWhatAPageReaches()
    {
        var partition = new InMemoryStoragePartitionProvider();

        await using var fixture = await LoopbackPage.CreateAsync(
            server => server.MapHtml("/index.html", Page),
            options => options.StoragePartition = partition);

        fixture.Context.StoragePartition.Should().BeSameAs(partition);

        await fixture.Page.NavigateAsync(fixture.Url("/index.html"));
        await fixture.Page.EvaluateAsync("localStorage.setItem('seen', 'by the host')");

        partition.GetLocalStorage(fixture.Server.Origin)!.GetItem("seen").Should().Be("by the host");
    }

    [Test]
    public async Task APartitionThatRefusesAnOriginGivesItTheSameSecurityError()
    {
        await using var fixture = await LoopbackPage.CreateAsync(
            server => server.MapHtml("/index.html", Page),
            options => options.StoragePartition = new NoStorage());

        await fixture.Page.NavigateAsync(fixture.Url("/index.html"));

        (await fixture.Page.EvaluateAsync<string>("try { localStorage; 'no throw' } catch (e) { e.name }"))
            .Should().Be("SecurityError");
    }

    private sealed class NoStorage : StoragePartitionProvider
    {
        public override Jint.WebApi.StorageProvider? GetLocalStorage(string origin) => null;
    }
}
