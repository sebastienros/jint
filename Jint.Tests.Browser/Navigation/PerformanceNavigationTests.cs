using Jint.Browser;
using Jint.Native.Object;

namespace Jint.Tests.Browser.Navigation;

public sealed class PerformanceNavigationTests
{
    private const string Document = """
        <!doctype html><title>Navigation</title>
        <script>
          window.initialNavigation = JSON.stringify(performance.navigation);
          addEventListener('load', function () {
            var entries = window.performance && window.performance.getEntriesByType('navigation');
            window.usePlaceholder = entries && entries.length
              ? 'back_forward' !== entries[0].type
              : 2 !== window.performance.navigation.type;
          });
        </script>
        """;

    [Test]
    public async Task BootstrapSelectCanDetectNavigationFromALoadListener()
    {
        await using var fixture = await LoopbackPage.CreateAsync(server => server.MapHtml("/page", Document));
        await fixture.Page.NavigateAsync(fixture.Url("/page"));

        (await fixture.Page.EvaluateAsync<bool>("usePlaceholder")).Should().BeTrue();
        (await fixture.Page.EvaluateAsync<string>("initialNavigation"))
            .Should().Be("""{"type":0,"redirectCount":0}""");
        fixture.Page.Errors.Should().BeEmpty();
    }

    [Test]
    public async Task NavigationHasTheWebIdlIdentityAttributesAndBrand()
    {
        await using var fixture = await LoopbackPage.CreateAsync();
        var page = fixture.Page;

        (await page.EvaluateAsync<string>("""
            (() => {
              const navigation = performance.navigation;
              const descriptor = Object.getOwnPropertyDescriptor(Performance.prototype, 'navigation');
              return [
                navigation === performance.navigation,
                navigation instanceof PerformanceNavigation,
                Object.getPrototypeOf(navigation) === PerformanceNavigation.prototype,
                Object.prototype.toString.call(navigation),
                descriptor.enumerable, descriptor.configurable, typeof descriptor.set,
                Object.hasOwn(performance, 'navigation'),
                PerformanceNavigation.TYPE_NAVIGATE, navigation.TYPE_RELOAD,
                navigation.TYPE_BACK_FORWARD, navigation.TYPE_RESERVED,
                JSON.stringify(navigation), Object.keys(navigation).length
              ].join('|');
            })()
            """)).Should().Be("true|true|true|[object PerformanceNavigation]|true|true|undefined|false|0|1|2|255|{\"type\":0,\"redirectCount\":0}|0");

        (await page.EvaluateAsync<int>("""
            (() => {
              const p = PerformanceNavigation.prototype;
              const calls = [
                () => new PerformanceNavigation(),
                () => PerformanceNavigation(),
                () => Object.getOwnPropertyDescriptor(Performance.prototype, 'navigation').get.call({}),
                () => Object.getOwnPropertyDescriptor(Performance.prototype, 'navigation').get.call(undefined),
                () => Object.getOwnPropertyDescriptor(p, 'type').get.call({}),
                () => Object.getOwnPropertyDescriptor(p, 'redirectCount').get.call(undefined),
                () => p.toJSON.call({}),
                () => { 'use strict'; performance.navigation = {}; },
                () => { 'use strict'; performance.navigation.type = 255; },
                () => { 'use strict'; performance.navigation.redirectCount = 255; }
              ];
              return calls.filter(call => { try { call(); } catch (e) { return e instanceof TypeError; } }).length;
            })()
            """)).Should().Be(10);

        (await page.RunOnLoopAsync(engine =>
            engine.Advanced.HasSharedShape((ObjectInstance) engine.Evaluate("Performance.prototype"))))
            .Should().BeTrue("installing navigation must not discard the core Performance prototype's shared shape");
    }

    [Test]
    public async Task ReloadAndCrossDocumentTraversalAreVisibleBeforeTheFirstScript()
    {
        await using var fixture = await LoopbackPage.CreateAsync(server => server
            .MapHtml("/one", Document)
            .MapHtml("/two", Document));
        var page = fixture.Page;

        await page.NavigateAsync(fixture.Url("/one"));
        await page.ReloadAsync();
        (await page.EvaluateAsync<string>("initialNavigation")).Should().Be("""{"type":1,"redirectCount":0}""");

        await fixture.NavigateByScriptAsync("location.reload()");
        (await page.EvaluateAsync<string>("initialNavigation")).Should().Be("""{"type":1,"redirectCount":0}""");

        await page.NavigateAsync(fixture.Url("/two"));
        (await page.EvaluateAsync<string>("initialNavigation")).Should().Be("""{"type":0,"redirectCount":0}""");
        (await page.GoBackAsync(TimeSpan.FromSeconds(30))).Should().BeTrue();
        (await page.EvaluateAsync<string>("initialNavigation")).Should().Be("""{"type":2,"redirectCount":0}""");
        (await page.EvaluateAsync<bool>("usePlaceholder")).Should().BeFalse();
        (await page.GoForwardAsync(TimeSpan.FromSeconds(30))).Should().BeTrue();
        (await page.EvaluateAsync<string>("initialNavigation")).Should().Be("""{"type":2,"redirectCount":0}""");
        page.Errors.Should().BeEmpty();
    }

    [Test]
    public async Task SameDocumentAndFailedNavigationsLeaveTheInformationAlone()
    {
        await using var fixture = await LoopbackPage.CreateAsync(server => server.MapHtml("/page", Document));
        var page = fixture.Page;
        await page.NavigateAsync(fixture.Url("/page"));
        await page.ReloadAsync();
        await page.EvaluateAsync("window.savedNavigation = performance.navigation; history.pushState({}, '', '#state');");
        (await page.GoBackAsync(TimeSpan.FromSeconds(30))).Should().BeTrue();
        await page.NavigateAsync(fixture.Url("/page#fragment"));

        var denied = async () => await page.NavigateAsync("https://blocked.invalid/");
        await denied.Should().ThrowAsync<NavigationFailedException>();

        (await page.EvaluateAsync<bool>("savedNavigation === performance.navigation")).Should().BeTrue();
        (await page.EvaluateAsync<string>("JSON.stringify(performance.navigation)"))
            .Should().Be("""{"type":1,"redirectCount":0}""");
    }

    [Test]
    public async Task SameOriginRedirectsAreCountedAndFormPostsAreNotReloads()
    {
        await using var fixture = await LoopbackPage.CreateAsync(server => server
            .MapHtml("/form", "<form id='f' action='/first' method='post'><input name='token' value='test'></form>")
            .Map("/first", _ => LoopbackResponse.Redirect(303, "/second"))
            .Map("/second", _ => LoopbackResponse.Redirect(302, "/done"))
            .MapHtml("/done", Document));

        await fixture.Page.NavigateAsync(fixture.Url("/form"));
        await fixture.Page.SubmitFormAsync("#f");
        (await fixture.Page.EvaluateAsync<string>("initialNavigation"))
            .Should().Be("""{"type":0,"redirectCount":2}""");
        fixture.Page.Errors.Should().BeEmpty();
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task ACrossOriginHopHidesTheEntireRedirectCount(bool returnsToOriginalOrigin)
    {
        using var other = new LoopbackServer();
        await using var fixture = await LoopbackPage.CreateAsync(server =>
        {
            server.Map("/start", _ => LoopbackResponse.Redirect(302, other.Url("/other")));
            server.MapHtml("/done", Document);
            other.Map("/other", _ => returnsToOriginalOrigin
                ? LoopbackResponse.Redirect(302, server.Url("/done"))
                : LoopbackResponse.Html(Document));
        }, configureContext: options =>
        {
            var original = options.UrlFilter!;
            options.UrlFilter = url => original(url) || other.Owns(url);
        });

        await fixture.Page.NavigateAsync(fixture.Url("/start"));
        (await fixture.Page.EvaluateAsync<string>("initialNavigation"))
            .Should().Be("""{"type":0,"redirectCount":0}""");
        fixture.Page.Errors.Should().BeEmpty();
    }

    [Test]
    public async Task NavigationObjectsBelongToTheirDocumentAndRealm()
    {
        await using var fixture = await LoopbackPage.CreateAsync(server => server.MapHtml("/page", Document));
        var first = await fixture.Page.RunOnLoopAsync(engine => engine.Evaluate("performance.navigation"));
        var other = await fixture.NewPageAsync();
        var second = await other.RunOnLoopAsync(engine => engine.Evaluate("performance.navigation"));
        first.Should().NotBeSameAs(second);

        await fixture.Page.NavigateAsync(fixture.Url("/page"));
        var replacement = await fixture.Page.RunOnLoopAsync(engine => engine.Evaluate("performance.navigation"));
        replacement.Should().NotBeSameAs(first);
    }

    [Test]
    public async Task DisablingPerformanceDoesNotInstallItsNavigationInterface()
    {
        await using var fixture = await LoopbackPage.CreateAsync(configureBrowser: options =>
            options.ConfigureEngine(engineOptions => engineOptions.WebApi.Features &= ~WebApiFeatures.Performance));

        (await fixture.Page.EvaluateAsync<string>("typeof performance + '|' + typeof PerformanceNavigation"))
            .Should().Be("undefined|undefined");
    }
}
