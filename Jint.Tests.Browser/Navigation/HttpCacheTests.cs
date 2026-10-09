using System.Net;
using System.Net.Http;
using Jint.Browser;

namespace Jint.Tests.Browser.Navigation;

public sealed class HttpCacheTests
{
    [Test]
    public async Task PagesShareTheCacheAcrossDocumentsSubresourcesFetchAndXhr()
    {
        var documentRequests = 0;
        var scriptRequests = 0;
        var bodyRequests = 0;
        await using var fixture = await LoopbackPage.CreateAsync(server => server
            .Map("/page", _ => { Interlocked.Increment(ref documentRequests); return LoopbackResponse.Html("<script src='/script.js'></script>").With("Cache-Control", "max-age=60"); })
            .Map("/script.js", _ => { Interlocked.Increment(ref scriptRequests); return LoopbackResponse.Script("window.ran = true;").With("Cache-Control", "max-age=60"); })
            .Map("/body", _ => { Interlocked.Increment(ref bodyRequests); return LoopbackResponse.Text("cached").With("Cache-Control", "max-age=60"); }),
            options => options.HttpCache.Storage = BrowserHttpCacheStorage.Memory);
        await fixture.Page.NavigateAsync(fixture.Url("/page"));
        (await fixture.Page.EvaluateAndAwaitAsync<string>("fetch('/body').then(r => r.text())")).Should().Be("cached");
        var second = await fixture.NewPageAsync();
        await second.NavigateAsync(fixture.Url("/page"));
        (await second.EvaluateAsync<bool>("ran")).Should().BeTrue();
        (await second.EvaluateAndAwaitAsync<string>("""
            new Promise((resolve, reject) => {
                const x = new XMLHttpRequest(); x.open('GET', '/body');
                x.onload = () => resolve(x.responseText); x.onerror = reject; x.send();
            })
            """)).Should().Be("cached");
        documentRequests.Should().Be(1);
        scriptRequests.Should().Be(1);
        bodyRequests.Should().Be(1);
        second.Requests.Where(r => r.FromCache).Should().HaveCount(3);
        second.Requests.Where(r => r.FromCache).Should().OnlyContain(r => r.TransferredBodyLength == 0 && r.BodyLength > 0);
        (await second.EvaluateAsync<bool>("performance.getEntriesByType('resource').every(e => e.transferSize === 0 && e.decodedBodySize > 0)")).Should().BeTrue();
        second.Errors.Should().BeEmpty();
        fixture.Context.ClearHttpCache();
        await second.NavigateAsync(fixture.Url("/page"));
        documentRequests.Should().Be(2);
        scriptRequests.Should().Be(2);
    }

    [Test]
    public async Task OpaquePagesDoNotShareCachedFetchRepresentations()
    {
        var requests = 0;
        await using var fixture = await LoopbackPage.CreateAsync(server => server.Map("/body", _ =>
        {
            Interlocked.Increment(ref requests);
            return LoopbackResponse.Text("private").With("Cache-Control", "max-age=60");
        }), options => options.HttpCache.Storage = BrowserHttpCacheStorage.Memory);
        var second = await fixture.NewPageAsync();
        var script = $"fetch('{fixture.Url("/body")}').then(r => r.text())";
        (await fixture.Page.EvaluateAndAwaitAsync<string>(script)).Should().Be("private");
        (await second.EvaluateAndAwaitAsync<string>(script)).Should().Be("private");
        requests.Should().Be(2);
        second.Requests.Should().OnlyContain(r => !r.FromCache);
    }

    [Test]
    public async Task CachedImagesStylesModulesFramesAndWorkersStillCompleteTheirConsumers()
    {
        var counts = new System.Collections.Concurrent.ConcurrentDictionary<string, int>(StringComparer.Ordinal);
        LoopbackResponse Cached(string path, LoopbackResponse response)
        {
            counts.AddOrUpdate(path, 1, (_, count) => count + 1);
            return response.With("Cache-Control", "max-age=60");
        }
        await using var fixture = await LoopbackPage.CreateAsync(server => server
            .MapHtml("/page", """
                <link rel="stylesheet" href="/style.css">
                <script type="module" src="/module.js"></script>
                <img src="/image.png" onload="window.imageLoaded = true">
                <iframe src="/frame"></iframe>
                """)
            .Map("/style.css", _ => Cached("style", LoopbackResponse.Css("@import '/import.css'; body { color: red; }")))
            .Map("/import.css", _ => Cached("import", LoopbackResponse.Css("body { margin: 0; }")))
            .Map("/module.js", _ => Cached("module", LoopbackResponse.Script("window.moduleRan = true;")))
            .Map("/image.png", _ => Cached("image", LoopbackResponse.Raw(Convert.FromBase64String(
                "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+aG1kAAAAASUVORK5CYII="), "image/png")))
            .Map("/frame", _ => Cached("frame", LoopbackResponse.Html("<title>frame</title>")))
            .Map("/worker.js", _ => Cached("worker", LoopbackResponse.Script("self.onmessage = () => fetch('/data').then(r => r.text()).then(t => self.postMessage(t));")))
            .Map("/data", _ => Cached("data", LoopbackResponse.Text("worker reply"))),
            options => options.HttpCache.Storage = BrowserHttpCacheStorage.Memory);
        for (var i = 0; i < 2; i++)
        {
            await fixture.Page.NavigateAsync(fixture.Url("/page"));
            (await fixture.Page.EvaluateAsync<bool>("imageLoaded && moduleRan && document.querySelector('img').naturalWidth === 1 && document.querySelector('iframe').contentDocument.title === 'frame'"))
                .Should().BeTrue();
            (await fixture.Page.EvaluateAndAwaitAsync<string>("""
                new Promise((resolve, reject) => {
                    const worker = new Worker('/worker.js', {type:'module'});
                    worker.onmessage = e => { worker.terminate(); resolve(e.data); };
                    worker.onerror = reject;
                    worker.postMessage('go');
                })
                """)).Should().Be("worker reply");
            fixture.Page.Errors.Should().BeEmpty();
        }
        counts.Should().HaveCount(7);
        counts.Values.Should().OnlyContain(count => count == 1);
        fixture.Page.Requests.Count(r => r.FromCache).Should().BeGreaterThanOrEqualTo(5);
    }

    [Test]
    public async Task CachingIsDisabledByDefaultAndContextsDoNotShareRepresentations()
    {
        var requests = 0;
        await using var fixture = await LoopbackPage.CreateAsync(server => server.Map("/page", _ =>
        {
            Interlocked.Increment(ref requests);
            return LoopbackResponse.Html("<title>cache</title>").With("Cache-Control", "max-age=60");
        }));
        await fixture.Page.NavigateAsync(fixture.Url("/page"));
        await fixture.Page.NavigateAsync(fixture.Url("/page"));
        requests.Should().Be(2);
        var options = new BrowserContextOptions { UrlFilter = fixture.Server.Owns };
        options.HttpCache.Storage = BrowserHttpCacheStorage.Memory;
        await using var one = await fixture.Browser.NewContextAsync(options);
        await using var two = await fixture.Browser.NewContextAsync(options);
        var a = await one.NewPageAsync(); var b = await two.NewPageAsync();
        await a.NavigateAsync(fixture.Url("/page"));
        await b.NavigateAsync(fixture.Url("/page"));
        requests.Should().Be(4);
        await a.NavigateAsync(fixture.Url("/page"));
        requests.Should().Be(4);
    }

    [Test]
    public async Task PersistentContextsRequireIdentityAndOnlyReuseTheChosenVisitorPartition()
    {
        var directory = Path.Combine(Path.GetTempPath(), "jint-browser-cache-" + Guid.NewGuid().ToString("N"));
        var requests = 0;
        await using var fixture = await LoopbackPage.CreateAsync(server => server.Map("/page", _ =>
        {
            Interlocked.Increment(ref requests);
            return LoopbackResponse.Html("<title>disk</title>").With("Cache-Control", "max-age=60");
        }));
        var options = new BrowserContextOptions { UrlFilter = fixture.Server.Owns };
        options.HttpCache.Storage = BrowserHttpCacheStorage.Disk;
        options.HttpCache.Directory = directory;
        await Assert.ThrowsAsync<ArgumentException>(async () => await fixture.Browser.NewContextAsync(options));
        try
        {
            options.HttpCache.PartitionKey = "visitor-a";
            await using (var first = await fixture.Browser.NewContextAsync(options))
            {
                await (await first.NewPageAsync()).NavigateAsync(fixture.Url("/page"));
                await Assert.ThrowsAsync<IOException>(async () => await fixture.Browser.NewContextAsync(options));
            }
            options.HttpCache.PartitionKey = "visitor-b";
            await using (var other = await fixture.Browser.NewContextAsync(options))
                await (await other.NewPageAsync()).NavigateAsync(fixture.Url("/page"));
            requests.Should().Be(2);
            options.HttpCache.PartitionKey = "visitor-a";
            await using (var reopened = await fixture.Browser.NewContextAsync(options))
            {
                var page = await reopened.NewPageAsync();
                await page.NavigateAsync(fixture.Url("/page"));
                page.Requests.Single().FromCache.Should().BeTrue();
            }
            requests.Should().Be(2);
        }
        finally { Directory.Delete(directory, recursive: true); }
    }

    [Test]
    public async Task FetchCacheModesAndSameOriginRestrictionReachTheTransport()
    {
        var requests = 0;
        await using var fixture = await LoopbackPage.CreateAsync(server => server
            .MapHtml("/page", "<title>cache</title>")
            .Map("/body", _ => { Interlocked.Increment(ref requests); return LoopbackResponse.Text("value").With("Cache-Control", "max-age=60"); }),
            options => options.HttpCache.Storage = BrowserHttpCacheStorage.Memory);
        await fixture.Page.NavigateAsync(fixture.Url("/page"));
        foreach (var mode in new[] { "default", "default", "no-cache", "reload", "no-store", "force-cache", "only-if-cached" })
            (await fixture.Page.EvaluateAndAwaitAsync<string>($"fetch('/body', {{cache:'{mode}', mode:'same-origin'}}).then(r => r.text())")).Should().Be("value");
        requests.Should().Be(4);
        var sent = fixture.Server.Received.Where(r => r.Path == "/body").ToArray();
        sent[1].Header("Cache-Control").Should().Be("max-age=0");
        sent[2].Header("Cache-Control").Should().Be("no-cache");
        sent[2].Header("Pragma").Should().Be("no-cache");
        sent[3].Header("Cache-Control").Should().Be("no-cache");
        (await fixture.Page.EvaluateAndAwaitAsync<bool>("fetch('/missing', {cache:'only-if-cached',mode:'same-origin'}).then(() => false, () => true)")).Should().BeTrue();
        (await fixture.Page.EvaluateAndAwaitAsync<bool>("fetch('http://different.invalid/body', {cache:'only-if-cached',mode:'same-origin'}).then(() => false, () => true)")).Should().BeTrue();
    }

    [Test]
    public async Task ValidationRetainsTheDeliveredBodyAndReportsZeroNetworkBodyBytes()
    {
        var requests = 0;
        await using var fixture = await LoopbackPage.CreateAsync(server => server
            .MapHtml("/page", "<title>cache</title>")
            .Map("/body", request =>
            {
                Interlocked.Increment(ref requests);
                return request.Header("If-None-Match") is "\"one\""
                    ? new LoopbackResponse { Status = 304, Reason = "Not Modified" }.With("Cache-Control", "max-age=60").With("X-Validated", "yes")
                    : LoopbackResponse.Text("retained body").With("Cache-Control", "max-age=0").With("ETag", "\"one\"");
            }), options => options.HttpCache.Storage = BrowserHttpCacheStorage.Memory);
        await fixture.Page.NavigateAsync(fixture.Url("/page"));
        (await fixture.Page.EvaluateAndAwaitAsync<string>("fetch('/body').then(r => r.text())")).Should().Be("retained body");
        (await fixture.Page.EvaluateAndAwaitAsync<string>("fetch('/body').then(async r => r.status + ':' + r.headers.get('X-Validated') + ':' + await r.text())"))
            .Should().Be("200:yes:retained body");
        var validated = fixture.Page.Requests.Single(r => r.Revalidated);
        validated.FromCache.Should().BeFalse();
        validated.BodyLength.Should().Be(13);
        validated.TransferredBodyLength.Should().Be(0);
        (await fixture.Page.EvaluateAsync<double>("performance.getEntriesByType('resource')[1].transferSize")).Should().Be(300);
        (await fixture.Page.EvaluateAndAwaitAsync<string>("fetch('/body').then(r => r.text())")).Should().Be("retained body");
        requests.Should().Be(2);
    }

    [Test]
    public async Task AnEngineClientOverrideCannotReuseTheContextClientsRepresentations()
    {
        HttpClient? overrideClient = null;
        await using var fixture = await LoopbackPage.CreateAsync(server => server
            .MapHtml("/page", "<title>cache</title>")
            .Map("/body", _ => LoopbackResponse.Text("context visitor").With("Cache-Control", "max-age=60")),
            options => options.HttpCache.Storage = BrowserHttpCacheStorage.Memory,
            configureBrowser: options => options.ConfigureEngine(engine =>
            {
                if (overrideClient is { } client) engine.WebApi.Fetch.HttpClient = client;
            }));
        await fixture.Page.NavigateAsync(fixture.Url("/page"));
        (await fixture.Page.EvaluateAndAwaitAsync<string>("fetch('/body').then(r => r.text())")).Should().Be("context visitor");
        using var client = new HttpClient(new VisitorHandler());
        overrideClient = client;
        var second = await fixture.NewPageAsync();
        await second.NavigateAsync(fixture.Url("/page"));
        (await second.EvaluateAndAwaitAsync<string>("fetch('/body').then(r => r.text())")).Should().Be("engine visitor");
        second.Requests.Last().FromCache.Should().BeFalse();
    }

    private sealed class VisitorHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var response = new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("engine visitor") };
            response.Headers.CacheControl = new() { MaxAge = TimeSpan.FromMinutes(1) };
            return Task.FromResult(response);
        }
    }

    [Test]
    public async Task ACacheHitStillPassesTheUrlFilter()
    {
        var allowed = true;
        var requests = 0;
        await using var fixture = await LoopbackPage.CreateAsync(server => server
            .MapHtml("/page", "<title>cache</title>")
            .Map("/body", _ => { Interlocked.Increment(ref requests); return LoopbackResponse.Text("value").With("Cache-Control", "max-age=60"); }),
            options => { options.HttpCache.Storage = BrowserHttpCacheStorage.Memory; options.UrlFilter = uri => uri.IsLoopback && Volatile.Read(ref allowed); });
        await fixture.Page.NavigateAsync(fixture.Url("/page"));
        await fixture.Page.EvaluateAndAwaitAsync("fetch('/body').then(r => r.text())");
        Volatile.Write(ref allowed, false);
        (await fixture.Page.EvaluateAndAwaitAsync<bool>("fetch('/body').then(() => false, () => true)")).Should().BeTrue();
        requests.Should().Be(1);
    }
}
