using System.Runtime.CompilerServices;
using Jint.Browser;
using Jint.Browser.Runtime;
using Jint.WebApi;
using Jint.Tests.Browser.Navigation;

namespace Jint.Tests.Browser.Storage;

public sealed class ContextStorageQuotaTests
{
    private const string Helpers = """
        function req(r){return new Promise((resolve,reject)=>{r.onsuccess=()=>resolve(r.result);r.onerror=()=>reject(r.error);});}
        function done(t){return new Promise((resolve,reject)=>{t.oncomplete=resolve;t.onabort=()=>reject(t.error);});}
        async function create(){
            const r=indexedDB.open('db');r.onupgradeneeded=()=>{r.result.createObjectStore('a');r.result.createObjectStore('b');};
            globalThis.db=await req(r);
        }
        async function write(){
            const t=db.transaction(['a','b'],'readwrite'),finished=done(t);
            t.objectStore('a').put('x'.repeat(400),1);t.objectStore('b').put('kept',1);
            try{await finished;return 'complete';}catch(e){return e.name;}
        }
        """;

    [Test]
    public async Task IndexedDbQuotaSpansOriginsAndRejectsAllStoresAtomically()
    {
        await using var f = await LoopbackPage.CreateAsync(configureBrowser: o => o.MaxTotalStorageBytes = 2048);
        await f.Page.SetContentAsync("<title>a</title>", "https://a.test/");
        await f.Page.EvaluateAsync(Helpers);
        await f.Page.EvaluateAndAwaitAsync("create()");
        (await f.Page.EvaluateAndAwaitAsync<string>("write()")).Should().Be("complete");
        var sibling = await f.NewPageAsync();
        await sibling.SetContentAsync("<title>b</title>", "https://b.test/");
        await sibling.EvaluateAsync(Helpers);
        await sibling.EvaluateAndAwaitAsync("create()");
        (await sibling.EvaluateAndAwaitAsync<string>("write()")).Should().Be("QuotaExceededError");
        (await sibling.EvaluateAndAwaitAsync<int>("req(db.transaction('b').objectStore('b').count())")).Should().Be(0);
        (await f.Page.EvaluateAndAwaitAsync<string>("req(db.transaction('b').objectStore('b').get(1))")).Should().Be("kept");
        await f.Page.EvaluateAndAwaitAsync("db.close();req(indexedDB.deleteDatabase('db'))");
        (await sibling.EvaluateAndAwaitAsync<string>("write()")).Should().Be("complete");
        f.Page.Errors.Should().BeEmpty();
        sibling.Errors.Should().BeEmpty();
    }

    [Test]
    public async Task LocalStorageAndCachesCompeteWithIndexedDbAndReleaseDeletedEntries()
    {
        await using var f = await LoopbackPage.CreateAsync(configureBrowser: o => o.MaxTotalStorageBytes = 2048);
        await f.Page.SetContentAsync("<title>a</title>", "https://a.test/");
        await f.Page.EvaluateAsync("localStorage.setItem('k','x'.repeat(700))");
        var sibling = await f.NewPageAsync();
        await sibling.SetContentAsync("<title>b</title>", "https://b.test/");
        await sibling.EvaluateAsync(Helpers);
        await sibling.EvaluateAndAwaitAsync("create()");
        (await sibling.EvaluateAndAwaitAsync<string>("write()")).Should().Be("QuotaExceededError");
        (await sibling.EvaluateAndAwaitAsync<string>("""
            (async()=>{globalThis.c=await caches.open('c');try{await c.put('https://b.test/x',new Response('x'.repeat(700)));}
            catch(e){return e.name+'|'+(await c.keys()).length;}})()
            """)).Should().Be("QuotaExceededError|0");
        await f.Page.EvaluateAsync("localStorage.removeItem('k')");
        (await sibling.EvaluateAndAwaitAsync<string>("write()")).Should().Be("complete");
        await sibling.EvaluateAndAwaitAsync("db.close();req(indexedDB.deleteDatabase('db'))");
        await sibling.EvaluateAndAwaitAsync("c.put('https://b.test/x',new Response('x'.repeat(700)))");
        await sibling.EvaluateAndAwaitAsync("caches.delete('c')");
        (await f.Page.EvaluateAsync<string>("try{localStorage.setItem('k','x'.repeat(700))}catch(e){e.name}")).Should().Be("QuotaExceededError");
        // Deleting the name cannot release a cache still held by script. Deleting its entry can.
        await sibling.EvaluateAndAwaitAsync("c.delete('https://b.test/x')");
        await f.Page.EvaluateAsync("localStorage.setItem('k','x'.repeat(700))");
        f.Page.Errors.Should().BeEmpty();
        sibling.Errors.Should().BeEmpty();
    }

    [Test]
    public async Task LocalStorageReplacementIsAtomicAndClearAndShrinkReleaseQuota()
    {
        await using var f = await LoopbackPage.CreateAsync(configureContext: o => o.MaxTotalStorageBytes = 100);
        await f.Page.SetContentAsync("<title>a</title>", "https://a.test/");
        await f.Page.EvaluateAsync("localStorage.setItem('k','x'.repeat(30))");
        var sibling = await f.NewPageAsync();
        await sibling.SetContentAsync("<title>b</title>", "https://b.test/");
        (await sibling.EvaluateAsync<string>("try{localStorage.setItem('k','y'.repeat(30))}catch(e){e.name+'|'+e.quota+'|'+localStorage.length}")).Should().Be("QuotaExceededError|100|0");
        (await f.Page.EvaluateAsync<string>("try{localStorage.setItem('k','z'.repeat(60))}catch(e){localStorage.getItem('k')}")).Should().Be(new string('x', 30));
        await f.Page.EvaluateAsync("localStorage.setItem('k','x')");
        await sibling.EvaluateAsync("localStorage.setItem('k','y'.repeat(30))");
        await sibling.EvaluateAsync("localStorage.clear()");
        await f.Page.EvaluateAsync("localStorage.setItem('k','z'.repeat(40))");
        var isolated = await f.NewIsolatedPageAsync();
        await isolated.SetContentAsync("<title>isolated</title>", "https://b.test/");
        await isolated.EvaluateAsync("localStorage.setItem('k','x'.repeat(40))");
    }

    [Test]
    public async Task ConcurrentPagesCannotOverbookTheContext()
    {
        await using var f = await LoopbackPage.CreateAsync(configureBrowser: o => o.MaxTotalStorageBytes = 2048);
        var pages = new List<Page> { f.Page };
        for (var i = 1; i < 8; i++) pages.Add(await f.NewPageAsync());
        for (var i = 0; i < pages.Count; i++)
            await pages[i].SetContentAsync("<title>race</title>", $"https://origin{i}.test/");
        var results = await Task.WhenAll(pages.Select(p => p.EvaluateAsync<string>("""
            (()=>{try{localStorage.setItem('k','x'.repeat(600));return 'stored';}catch(e){return e.name;}})()
            """)));
        results.Count(x => x == "stored").Should().Be(1);
        results.Count(x => x == "QuotaExceededError").Should().Be(7);
        foreach (var page in pages) page.Errors.Should().BeEmpty();
    }

    [Test]
    public async Task ClosingPageDoesNotReleaseContextRetainedStorage()
    {
        await using var f = await LoopbackPage.CreateAsync(configureBrowser: o => o.MaxTotalStorageBytes = 100);
        var page = await f.NewPageAsync();
        await page.SetContentAsync("<title>a</title>", "https://a.test/");
        await page.EvaluateAsync("localStorage.setItem('k','x'.repeat(40))");
        await page.CloseAsync();
        await f.Page.SetContentAsync("<title>b</title>", "https://b.test/");
        (await f.Page.EvaluateAsync<string>("try{localStorage.setItem('k','x'.repeat(40))}catch(e){e.name}")).Should().Be("QuotaExceededError");
        await f.Page.SetContentAsync("<title>return</title>", "https://a.test/");
        (await f.Page.EvaluateAsync<int>("localStorage.getItem('k').length")).Should().Be(40);
    }

    [Test]
    public async Task ZeroQuotaRejectsCreationAndWritesWithScriptErrors()
    {
        await using var f = await LoopbackPage.CreateAsync(configureBrowser: o => o.MaxTotalStorageBytes = 0);
        await f.Page.SetContentAsync("<title>zero</title>", "https://a.test/");
        (await f.Page.EvaluateAsync<string>("try{localStorage.setItem('k','v')}catch(e){e.name}")).Should().Be("QuotaExceededError");
        (await f.Page.EvaluateAndAwaitAsync<string>("caches.open('c').then(()=>'',e=>e.name)")).Should().Be("QuotaExceededError");
        await f.Page.EvaluateAsync(Helpers);
        (await f.Page.EvaluateAndAwaitAsync<string>("create().then(()=>'',e=>e.name)")).Should().Be("AbortError");
        (await f.Page.EvaluateAndAwaitAsync<int>("indexedDB.databases().then(x=>x.length)")).Should().Be(0);
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task ContextUnlimitedOverrideUsesFiniteBrowserQuotaOnlyUnderUntrustedContent(bool untrusted)
    {
        await using var f = await LoopbackPage.CreateAsync(
            configureBrowser: o => { o.MaxTotalStorageBytes = 100; if (untrusted) o.ForUntrustedContent(); },
            configureContext: o => o.MaxTotalStorageBytes = long.MaxValue);
        await f.Page.SetContentAsync("<title>quota</title>", "https://a.test/");
        (await f.Page.EvaluateAsync<string>("""
            (()=>{try{localStorage.setItem('k','x'.repeat(100));return 'stored';}catch(e){return e.name;}})()
            """)).Should().Be(untrusted ? "QuotaExceededError" : "stored");
    }

    [Test]
    public void CollectedDeletedCacheHandlesReleaseTheirAggregateCharge()
    {
        var quota = new StorageQuota(100);
        var provider = new BoundedCacheStorageProvider(1000, quota);
        var weak = OpenAndDelete(provider);
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        weak.TryGetTarget(out _).Should().BeFalse();
        // Each name costs 66 bytes. A stale aggregate charge would refuse the second origin.
        var other = new BoundedCacheStorageProvider(1000, quota);
        other.Open("b").Should().NotBeNull();
        GC.KeepAlive(provider);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference<CacheStore> OpenAndDelete(BoundedCacheStorageProvider provider)
    {
        var store = provider.Open("a");
        provider.Delete("a").Should().BeTrue();
        return new WeakReference<CacheStore>(store);
    }

    [Test]
    public void OptionsValidateAndKeepTheUntrustedProfileFinite()
    {
        var options = new BrowserOptions();
        options.MaxTotalStorageBytes.Should().Be(100 * 1024 * 1024);
        Caught.Exception(() => options.MaxTotalStorageBytes = -1).Should().BeOfType<ArgumentOutOfRangeException>();
        Caught.Exception(() => new BrowserContextOptions { MaxTotalStorageBytes = -1 }).Should().BeOfType<ArgumentOutOfRangeException>();
        options.MaxTotalStorageBytes = long.MaxValue;
        options.MaxTotalStorageBytes.Should().Be(long.MaxValue);
        options.ForUntrustedContent();
        options.MaxTotalStorageBytes.Should().Be(100 * 1024 * 1024);
    }
}
