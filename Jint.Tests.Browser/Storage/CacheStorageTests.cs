using Jint.Browser;
using Jint.Tests.Browser.Navigation;
using Jint.WebApi;

namespace Jint.Tests.Browser.Storage;

public sealed class CacheStorageTests
{
    private const string Put = """
        (async () => {
            const cache = await caches.open('assets');
            await cache.put(new URL('/value', location.href), new Response('kept'));
            return await (await cache.match(new URL('/value', location.href))).text();
        })()
        """;

    [Test]
    public async Task CachesSurviveNavigationAndAreSharedWithSiblingPages()
    {
        await using var fixture = await LoopbackPage.CreateAsync(server => server
            .MapHtml("/one", "<title>one</title>").MapHtml("/two", "<title>two</title>"));
        await fixture.Page.NavigateAsync(fixture.Url("/one"));
        (await fixture.Page.EvaluateAndAwaitAsync<string>(Put)).Should().Be("kept");
        await fixture.Page.NavigateAsync(fixture.Url("/two"));
        (await fixture.Page.EvaluateAndAwaitAsync<string>("caches.match(new URL('/value', location.href)).then(r => r.text())"))
            .Should().Be("kept");

        var sibling = await fixture.NewPageAsync();
        await sibling.NavigateAsync(fixture.Url("/one"));
        (await sibling.EvaluateAndAwaitAsync<string>("caches.match(new URL('/value', location.href)).then(r => r.text())"))
            .Should().Be("kept");
        fixture.Page.Errors.Should().BeEmpty();
        sibling.Errors.Should().BeEmpty();
    }

    [Test]
    public async Task OriginsAndContextsAreIsolated()
    {
        using var other = new LoopbackServer();
        other.MapHtml("/page", "<title>other</title>");
        await using var fixture = await LoopbackPage.CreateAsync(
            server => server.MapHtml("/page", "<title>one</title>"),
            options => options.UrlFilter = uri => uri.IsLoopback);
        await fixture.Page.NavigateAsync(fixture.Url("/page"));
        await fixture.Page.EvaluateAndAwaitAsync(Put);
        var stranger = await fixture.NewIsolatedPageAsync();
        await stranger.NavigateAsync(fixture.Url("/page"));
        (await stranger.EvaluateAndAwaitAsync<bool>("caches.has('assets')")).Should().BeFalse();
        await fixture.Page.NavigateAsync(other.Url("/page"));
        (await fixture.Page.EvaluateAndAwaitAsync<bool>("caches.has('assets')")).Should().BeFalse();
        await fixture.Page.NavigateAsync(fixture.Url("/page"));
        (await fixture.Page.EvaluateAndAwaitAsync<bool>("caches.has('assets')")).Should().BeTrue();
    }

    [Test]
    public async Task CustomCachePartitionIsHonouredIndependentlyOfLocalStorage()
    {
        var partition = new CachePartition();
        await using var fixture = await LoopbackPage.CreateAsync(
            configureContext: options => options.StoragePartition = partition);
        await fixture.Page.SetContentAsync("<title>custom</title>", fixture.Url("/page"));
        await fixture.Page.EvaluateAndAwaitAsync(Put);
        partition.Provider.Contains("assets").Should().BeTrue();
        partition.Origins.Should().Equal(fixture.Server.Origin);
        (await fixture.Page.EvaluateAndAwaitAsync<string>("try { localStorage; } catch(e) { e.name }"))
            .Should().Be("SecurityError");
    }

    [Test]
    public async Task ExistingPartitionSubclassesDoNotAcquireANewGrant()
    {
        var partition = new NoCaches();
        partition.GetCacheStorage("https://example.test").Should().BeNull();
        await using var fixture = await LoopbackPage.CreateAsync(
            configureContext: options => options.StoragePartition = partition);
        await fixture.Page.SetContentAsync("<title>custom</title>", fixture.Url("/page"));
        (await fixture.Page.EvaluateAndAwaitAsync<bool>("'caches' in globalThis")).Should().BeFalse();
    }

    [TestCase("http://example.test/page", false)]
    [TestCase("https://example.test/page", true)]
    [TestCase("http://localhost/page", true)]
    [TestCase("http://sub.localhost/page", true)]
    [TestCase("http://127.0.0.1/page", true)]
    [TestCase("http://[::1]/page", true)]
    [TestCase("about:blank", false)]
    [TestCase("data:text/html,opaque", false)]
    public async Task OnlyTrustworthyNonOpaqueOriginsReceiveCacheStorage(string url, bool exposed)
    {
        await using var fixture = await LoopbackPage.CreateAsync();
        await fixture.Page.SetContentAsync("<title>origin</title>", url);
        (await fixture.Page.EvaluateAndAwaitAsync<bool>("'caches' in globalThis")).Should().Be(exposed);
        (await fixture.Page.EvaluateAndAwaitAsync<bool>("'CacheStorage' in globalThis")).Should().Be(exposed);
    }

    [Test]
    public async Task InitialOpaqueDocumentHasNoCaches()
    {
        await using var fixture = await LoopbackPage.CreateAsync();
        (await fixture.Page.EvaluateAndAwaitAsync<bool>("'caches' in globalThis")).Should().BeFalse();
        await fixture.Page.NavigateAsync("data:text/html,<title>opaque</title>");
        (await fixture.Page.EvaluateAndAwaitAsync<bool>("'caches' in globalThis")).Should().BeFalse();
    }

    [Test]
    public async Task SecureOpaqueFileDocumentExposesAGetterThatRejectsTheStorageKey()
    {
        await using var fixture = await LoopbackPage.CreateAsync();
        await fixture.Page.SetContentAsync("<title>file</title>", "file:///opaque.html");
        (await fixture.Page.EvaluateAsync<string>("'caches' in globalThis && (() => { try { caches; } catch(e) { return e.name; } })()"))
            .Should().Be("SecurityError");
    }

    [Test]
    public async Task BlankDocumentInheritsTheCreatorsCachePartition()
    {
        await using var fixture = await LoopbackPage.CreateAsync(server => server.MapHtml("/page", "<title>creator</title>"));
        await fixture.Page.NavigateAsync(fixture.Url("/page"));
        await fixture.Page.EvaluateAndAwaitAsync(Put);
        await fixture.NavigateByScriptAsync("location.href = 'about:blank'");
        (await fixture.Page.EvaluateAndAwaitAsync<bool>("caches.has('assets')")).Should().BeTrue();
        (await fixture.Page.EvaluateAsync<bool>("""
            (() => {
                const descriptor = Object.getOwnPropertyDescriptor(globalThis, 'caches');
                return caches === caches && descriptor.enumerable && descriptor.configurable
                    && typeof descriptor.get === 'function' && descriptor.set === undefined;
            })()
            """)).Should().BeTrue();
    }

    [Test]
    public async Task QuotaIsAtomicAndSharedAcrossNamedAndDeletedCaches()
    {
        await using var fixture = await LoopbackPage.CreateAsync(
            configureBrowser: options => options.MaxCacheStorageBytes = 2048);
        await fixture.Page.SetContentAsync("<title>quota</title>", fixture.Url("/page"));
        (await fixture.Page.EvaluateAndAwaitAsync<string>(Put)).Should().Be("kept");
        (await fixture.Page.EvaluateAndAwaitAsync<string>("""
            (async () => {
                const cache = await caches.open('assets');
                try {
                    await cache.put(new URL('/value', location.href), new Response('x'.repeat(2048)));
                } catch (e) {
                    return [e.name, e.quota === 2048, e.requested > e.quota,
                        await (await cache.match(new URL('/value', location.href))).text()].join(',');
                }
            })()
            """)).Should().Be("QuotaExceededError,true,true,kept");
        (await fixture.Page.EvaluateAndAwaitAsync<string>("""
            (async () => {
                const cache = await caches.open('assets');
                await cache.put(new URL('/large', location.href), new Response('x'.repeat(800)));
                await caches.delete('assets');
                globalThis.retainedCache = cache;
                const other = await caches.open('other');
                try {
                    await other.put(new URL('/large', location.href), new Response('x'.repeat(800)));
                } catch (e) { return e.name; }
            })()
            """)).Should().Be("QuotaExceededError");
    }

    [Test]
    public async Task ConcurrentPagesDoNotLoseOrDuplicateCacheEntries()
    {
        await using var fixture = await LoopbackPage.CreateAsync();
        await fixture.Page.SetContentAsync("<title>one</title>", fixture.Url("/page"));
        var sibling = await fixture.NewPageAsync();
        await sibling.SetContentAsync("<title>two</title>", fixture.Url("/page"));
        const string write = """
            (async () => {
                const cache = await caches.open('race');
                for (let i = 0; i < 100; i++) {
                    await cache.put(new URL('/same', location.href), new Response(String(i)));
                    await cache.put(new URL('/' + prefix + i, location.href), new Response(prefix));
                }
                return true;
            })()
            """;
        await fixture.Page.EvaluateAndAwaitAsync("globalThis.prefix = 'a'");
        await sibling.EvaluateAndAwaitAsync("globalThis.prefix = 'b'");
        await Task.WhenAll(fixture.Page.EvaluateAndAwaitAsync(write), sibling.EvaluateAndAwaitAsync(write));
        (await fixture.Page.EvaluateAndAwaitAsync<double>("caches.open('race').then(c => c.keys()).then(k => k.length)"))
            .Should().Be(201);
    }

    [Test]
    public async Task DedicatedWorkerSharesItsPagesCachePartition()
    {
        await using var fixture = await LoopbackPage.CreateAsync(server => server.Map("/worker.js", _ => LoopbackResponse.Script("""
            onmessage = async () => {
                const [request] = await (await caches.open('assets')).keys();
                const response = await caches.match(request);
                postMessage(await response.text());
            };
            """)));
        await fixture.Page.SetContentAsync("<title>worker</title>", fixture.Url("/page"));
        await fixture.Page.EvaluateAndAwaitAsync(Put);
        (await fixture.Page.EvaluateAndAwaitAsync<string>("""
            new Promise((resolve, reject) => {
                const worker = new Worker('/worker.js', { type: 'module' });
                worker.onmessage = e => { resolve(e.data); worker.terminate(); };
                worker.onerror = e => { reject(e.message); worker.terminate(); };
                worker.postMessage('read');
            })
            """)).Should().Be("kept");
    }

    private sealed class NoCaches : StoragePartitionProvider
    {
        public override StorageProvider? GetLocalStorage(string origin) => null;
    }

    private sealed class CachePartition : StoragePartitionProvider
    {
        internal InMemoryCacheStorageProvider Provider { get; } = new();
        internal List<string> Origins { get; } = [];
        public override StorageProvider? GetLocalStorage(string origin) => null;
        public override CacheStorageProvider? GetCacheStorage(string origin)
        {
            Origins.Add(origin);
            return Provider;
        }
    }
}
