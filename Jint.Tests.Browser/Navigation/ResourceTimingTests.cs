namespace Jint.Tests.Browser.Navigation;

public sealed class ResourceTimingTests
{
    [Test]
    public async Task SubresourcesHaveTheCorrectInitiatorsAndDocumentHasNavigationTiming()
    {
        await using var fixture = await LoopbackPage.CreateAsync(server => server
            .MapHtml("/page", """
                <!doctype html><title>timing</title>
                <link rel="stylesheet" href="/style.css">
                <script src="/script.js"></script>
                <script type="module" src="/module.js"></script>
                <img src="/image.png">
                <iframe src="/frame"></iframe>
                """)
            .Map("/style.css", _ => LoopbackResponse.Css("@import '/import.css'; body { color: red; }"))
            .Map("/import.css", _ => LoopbackResponse.Css("body { margin: 0; }"))
            .Map("/script.js", _ => LoopbackResponse.Script("window.classic = true;"))
            .Map("/module.js", _ => LoopbackResponse.Script("window.module = true;"))
            .Map("/image.png", _ => LoopbackResponse.Raw(Convert.FromBase64String(
                "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+aG1kAAAAASUVORK5CYII="), "image/png"))
            .MapHtml("/frame", "<title>frame</title><script src='/child.js'></script>")
            .Map("/child.js", _ => LoopbackResponse.Script("window.childScript = true;")));

        await fixture.Page.NavigateAsync(fixture.Url("/page"));
        (await fixture.Page.EvaluateAsync<string>("""
            performance.getEntriesByType('resource').map(e => new URL(e.name).pathname + ':' + e.initiatorType).sort().join(',')
            """)).Should().Be("/frame:iframe,/image.png:img,/import.css:css,/module.js:script,/script.js:script,/style.css:link");
        (await fixture.Page.EvaluateAsync<bool>("""
            (() => {
                const n = performance.getEntriesByType('navigation')[0];
                return n instanceof PerformanceNavigationTiming && n instanceof PerformanceResourceTiming
                    && n.entryType === 'navigation' && n.initiatorType === 'navigation' && n.type === 'navigate'
                    && n.startTime === 0 && n.fetchStart >= 0 && n.responseStart >= n.fetchStart
                    && n.responseEnd >= n.responseStart && n.domInteractive >= n.responseEnd
                    && n.domContentLoadedEventStart >= n.domInteractive
                    && n.domContentLoadedEventEnd >= n.domContentLoadedEventStart
                    && n.domComplete >= n.domContentLoadedEventEnd && n.loadEventStart >= n.domComplete
                    && n.loadEventEnd >= n.loadEventStart && n.duration === n.loadEventEnd
                    && n.responseStatus === 200 && n.decodedBodySize > 0 && n.nextHopProtocol === 'http/1.1'
                    && n.toJSON().loadEventEnd === n.loadEventEnd
                    && performance.getEntriesByType('navigation').length === 1;
            })()
            """)).Should().BeTrue();
        (await fixture.Page.EvaluateAsync<string>("""
            document.querySelector('iframe').contentWindow.performance.getEntriesByType('resource')
                .map(e => new URL(e.name).pathname).join(',')
            """)).Should().Be("/child.js");
        fixture.Page.Errors.Should().BeEmpty();
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task CrossOriginScriptTimingHonoursTimingAllowOrigin(bool allow)
    {
        using var other = new LoopbackServer();
        await using var fixture = await LoopbackPage.CreateAsync(
            server => server.MapHtml("/page", $"<script src='{other.Url("/script.js")}'></script>"),
            options => options.UrlFilter = uri => uri.IsLoopback);
        other.Map("/script.js", _ =>
        {
            var response = LoopbackResponse.Script("window.crossOriginScript = true;");
            return allow ? response.With("Timing-Allow-Origin", fixture.Server.Origin) : response;
        });
        await fixture.Page.NavigateAsync(fixture.Url("/page"));
        (await fixture.Page.EvaluateAsync<bool>("""
            (() => {
                const e = performance.getEntriesByType('resource')[0];
                return e.responseStart > 0 && e.transferSize > 0 && e.decodedBodySize > 0 && e.responseStatus === 200;
            })()
            """)).Should().Be(allow);
        (await fixture.Page.EvaluateAsync<bool>("crossOriginScript")).Should().BeTrue();
    }

    [Test]
    public async Task FetchXhrAndBufferedObserversBelongToTheirDocument()
    {
        await using var fixture = await LoopbackPage.CreateAsync(server => server
            .MapHtml("/page", "<title>timing</title>").Map("/body", _ => LoopbackResponse.Text("hello")));
        await fixture.Page.NavigateAsync(fixture.Url("/page"));
        await fixture.Page.EvaluateAndAwaitAsync("""
            Promise.all([
                fetch('/body').then(r => r.text()),
                new Promise(resolve => { const x = new XMLHttpRequest(); x.open('GET', '/body'); x.onload = resolve; x.send(); })
            ]).then(() => true)
            """);
        (await fixture.Page.EvaluateAndAwaitAsync<string>("""
            new Promise(resolve => new PerformanceObserver(list => {
                resolve(list.getEntries().map(e => e.initiatorType).sort().join(','));
            }).observe({type: 'resource', buffered: true}))
            """)).Should().Be("fetch,xmlhttprequest");
        var sibling = await fixture.NewPageAsync();
        await sibling.NavigateAsync(fixture.Url("/page"));
        (await sibling.EvaluateAsync<int>("performance.getEntriesByType('resource').length")).Should().Be(0);
        await fixture.Page.ReloadAsync();
        (await fixture.Page.EvaluateAsync<int>("performance.getEntriesByType('resource').length")).Should().Be(0);
        (await fixture.Page.EvaluateAsync<string>("performance.getEntriesByType('navigation')[0].type")).Should().Be("reload");
    }

    [Test]
    public async Task NavigationEntryExistsBeforeScriptsAndIsNotReplacedForSameDocumentNavigation()
    {
        await using var fixture = await LoopbackPage.CreateAsync(server => server.MapHtml("/page", """
            <script>
                const n = performance.getEntriesByType('navigation')[0];
                window.initialTiming = n.loadEventEnd === 0 && n.responseEnd <= performance.now();
                window.savedTiming = n;
                window.readinessTimings = [];
                document.addEventListener('readystatechange', () => {
                    const timestamp = document.readyState === 'interactive' ? n.domInteractive : n.domComplete;
                    readinessTimings.push(timestamp > 0 && timestamp <= performance.now());
                });
                window.navigationAttributes = Object.getOwnPropertyNames(PerformanceNavigationTiming.prototype)
                    .filter(name => name !== 'constructor').every(name =>
                        Object.getOwnPropertyDescriptor(PerformanceNavigationTiming.prototype, name).enumerable);
                window.observed = [];
                new PerformanceObserver(list => observed.push(...list.getEntries()))
                    .observe({type: 'navigation'});
            </script>
            """));
        await fixture.Page.NavigateAsync(fixture.Url("/page"));
        await fixture.Page.NavigateAsync(fixture.Url("/page#fragment"));
        (await fixture.Page.EvaluateAsync<bool>("""
            initialTiming && navigationAttributes && savedTiming === performance.getEntriesByType('navigation')[0]
                && observed.length === 1 && observed[0].loadEventEnd > 0
                && readinessTimings.length === 2 && readinessTimings.every(Boolean)
                && PerformanceObserver.supportedEntryTypes.includes('navigation')
            """)).Should().BeTrue();
    }
}
