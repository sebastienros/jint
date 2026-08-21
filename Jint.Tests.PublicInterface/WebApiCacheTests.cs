#if NET8_0_OR_GREATER
#nullable enable

using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Jint;
using Jint.Runtime;
using Jint.WebApi;

namespace Jint.Tests.PublicInterface;

/// <summary>
/// The Cache API seen from outside the assembly: the host seam a third party implements to decide where a
/// script's cached data goes, and what the engine promises about how it is called.
/// </summary>
/// <remarks>
/// This project has no <c>InternalsVisibleTo</c>, so everything here — <see cref="CacheStorageProvider"/>,
/// <see cref="CacheStore"/>, the record types, <see cref="CacheQuotaExceededException"/> — is reachable by a
/// real embedder.
/// </remarks>
public class WebApiCacheTests
{
    /// <summary>
    /// A provider that records what the engine asked it for and lets a test decide how a write ends.
    /// </summary>
    private sealed class RecordingProvider : CacheStorageProvider
    {
        private readonly List<string> _names = new();
        private readonly Dictionary<string, RecordingStore> _stores = new(StringComparer.Ordinal);

        internal List<string> Calls { get; } = new();

        /// <summary>Returns an exception to fail the write with, or null to let it through.</summary>
        internal Func<CacheWrite, Exception?>? OnWrite { get; set; }

        public override IReadOnlyList<string> Names
        {
            get
            {
                Calls.Add("names");
                return _names;
            }
        }

        public override bool Contains(string cacheName)
        {
            Calls.Add("contains:" + cacheName);
            return _stores.ContainsKey(cacheName);
        }

        public override CacheStore Open(string cacheName)
        {
            Calls.Add("open:" + cacheName);

            if (_stores.TryGetValue(cacheName, out var existing))
            {
                return existing;
            }

            var store = new RecordingStore(this);
            _stores.Add(cacheName, store);
            _names.Add(cacheName);
            return store;
        }

        public override bool Delete(string cacheName)
        {
            Calls.Add("delete:" + cacheName);

            if (!_stores.Remove(cacheName))
            {
                return false;
            }

            _names.Remove(cacheName);
            return true;
        }

        internal RecordingStore Store(string cacheName) => _stores[cacheName];

        internal Exception? FailWrite(CacheWrite write) => OnWrite?.Invoke(write);
    }

    private sealed class RecordingStore : CacheStore
    {
        private readonly RecordingProvider _owner;
        private List<CacheEntry> _entries = new();

        internal RecordingStore(RecordingProvider owner)
        {
            _owner = owner;
        }

        internal List<CacheWrite> Writes { get; } = new();

        public override IReadOnlyList<CacheEntry> Entries => _entries;

        public override void Write(in CacheWrite write)
        {
            Writes.Add(write);

            if (_owner.FailWrite(write) is { } failure)
            {
                throw failure;
            }

            var next = new List<CacheEntry>();
            var removed = 0;
            for (var i = 0; i < _entries.Count; i++)
            {
                if (removed < write.RemovedIndexes.Count && write.RemovedIndexes[removed] == i)
                {
                    removed++;
                    continue;
                }

                next.Add(_entries[i]);
            }

            next.AddRange(write.Added);
            _entries = next;
        }

        /// <summary>Puts an entry there without the engine having asked, as a host restoring a cache does.</summary>
        internal void Seed(CacheEntry entry) => _entries.Add(entry);
    }

    private static Engine CacheEngine(CacheStorageProvider provider)
        => new(options => options.UseCacheApi(cache => cache.Provider = provider));

    private static string Run(Engine engine, string body)
        => engine.Evaluate("(async () => {" + body + "})()").UnwrapIfPromise().AsString();

    [Fact]
    public void ADefaultEngineHasNoCaches()
    {
        new Engine().Evaluate("typeof caches").AsString().Should().Be("undefined");

        // Not even the default set, which never carries a feature whose data outlives the evaluation.
        new Engine(options => options.UseWebApis()).Evaluate("typeof caches").AsString().Should().Be("undefined");
    }

    [Fact]
    public void UseCacheApiRecordsOnlyItsOwnFlag()
    {
        var options = new Options().UseCacheApi();

        // The features its model needs are added when the engine is built, so the option still reads back
        // exactly what the host asked for.
        options.WebApi.Features.Should().Be(WebApiFeatures.CacheApi);
        options.WebApi.Cache.Provider.Should().BeNull();

        var engine = new Engine(options);
        engine.Evaluate("typeof caches").AsString().Should().Be("object");
        engine.Evaluate("typeof Response").AsString().Should().Be("function");

        // ... and it is not a way to get network access.
        engine.Evaluate("typeof fetch").AsString().Should().Be("undefined");
    }

    [Fact]
    public void PutIssuesOneWriteCarryingTheFlattenedPair()
    {
        var provider = new RecordingProvider();
        var engine = CacheEngine(provider);

        Run(engine, """
            const cache = await caches.open('v1');
            await cache.put(new Request('https://example.org/a?q=1', { headers: { 'x-req': 'r' } }),
                new Response('hi', { status: 201, statusText: 'Created', headers: { 'x-res': '1' } }));
            return 'done';
            """);

        var store = provider.Store("v1");
        var write = store.Writes.Should().ContainSingle().Subject;

        write.RemovedIndexes.Should().BeEmpty();
        var entry = write.Added.Should().ContainSingle().Subject;

        entry.Request.Url.Should().Be("https://example.org/a?q=1");
        entry.Request.Method.Should().Be("GET");
        entry.Request.Headers.Should().Contain(header => header.Name == "x-req" && header.Value == "r");

        entry.Response.Status.Should().Be(201);
        entry.Response.StatusText.Should().Be("Created");
        entry.Response.Url.Should().BeEmpty();
        entry.Response.Redirected.Should().BeFalse();
        entry.Response.Headers.Should().Contain(header => header.Name == "x-res" && header.Value == "1");

        entry.Response.Body.Should().NotBeNull();
        Encoding.UTF8.GetString(entry.Response.Body!.Value.ToArray()).Should().Be("hi");
    }

    [Fact]
    public void ASecondPutForOneUrlRemovesTheEntryItReplaces()
    {
        var provider = new RecordingProvider();
        var engine = CacheEngine(provider);

        Run(engine, """
            const cache = await caches.open('v1');
            await cache.put('https://example.org/a', new Response('first'));
            await cache.put('https://example.org/a', new Response('second'));
            return 'done';
            """);

        var store = provider.Store("v1");
        store.Writes.Should().HaveCount(2);

        // The second write names the entry it evicts by its index in the list the store had just handed out.
        store.Writes[1].RemovedIndexes.Should().Equal(0);
        store.Writes[1].Added.Should().ContainSingle();
        store.Entries.Should().ContainSingle();
    }

    [Fact]
    public void ADeleteThatMatchesNothingWritesNothing()
    {
        var provider = new RecordingProvider();
        var engine = CacheEngine(provider);

        Run(engine, """
            const cache = await caches.open('v1');
            await cache.put('https://example.org/a', new Response('a'));
            const missing = await cache.delete('https://example.org/b');
            const hit = await cache.delete('https://example.org/a');
            return missing + ':' + hit;
            """).Should().Be("false:true");

        var store = provider.Store("v1");

        // One write for the put and one for the delete that matched; the delete that matched nothing does
        // not open a transaction only to be told there was nothing to do.
        store.Writes.Should().HaveCount(2);
        store.Writes[1].Added.Should().BeEmpty();
        store.Writes[1].RemovedIndexes.Should().Equal(0);
    }

    [Fact]
    public void AHostCanSeedACacheAndTheScriptReadsItBack()
    {
        var provider = new RecordingProvider();
        var store = (RecordingStore) provider.Open("v1");

        store.Seed(new CacheEntry(
            new CachedRequest("https://example.org/seeded", "GET", [new CachedHeader("x-req", "r")]),
            new CachedResponse(
                Status: 203,
                StatusText: "Non-Authoritative Information",
                Headers: [new CachedHeader("x-res", "s")],
                Body: Encoding.UTF8.GetBytes("from the host"),
                Url: "https://example.org/seeded",
                Redirected: true)));

        var engine = CacheEngine(provider);

        Run(engine, """
            const cache = await caches.open('v1');
            const r = await cache.match('https://example.org/seeded');
            const keys = await cache.keys();
            return [r.status, r.statusText, r.url, r.redirected, r.headers.get('x-res'), await r.text(), keys[0].url, keys[0].headers.get('x-req')].join('|');
            """).Should().Be("203|Non-Authoritative Information|https://example.org/seeded|true|s|from the host|https://example.org/seeded|r");
    }

    [Fact]
    public void AnEntryWhoseStoredUrlDoesNotParseIsInvisible()
    {
        var provider = new RecordingProvider();
        var store = (RecordingStore) provider.Open("v1");

        store.Seed(new CacheEntry(
            new CachedRequest("not a url", "GET", []),
            new CachedResponse(200, string.Empty, [], Encoding.UTF8.GetBytes("x"), string.Empty, false)));
        store.Seed(new CacheEntry(
            new CachedRequest("https://example.org/ok", "GET", []),
            new CachedResponse(200, string.Empty, [], Encoding.UTF8.GetBytes("y"), string.Empty, false)));

        var engine = CacheEngine(provider);

        // A row a provider hydrated badly can never match and never appears among the keys, rather than
        // failing every read of the cache it is in.
        Run(engine, """
            const cache = await caches.open('v1');
            const keys = await cache.keys();
            const all = await cache.matchAll();
            return keys.length + '|' + keys[0].url + '|' + all.length;
            """).Should().Be("1|https://example.org/ok|1");
    }

    [Fact]
    public void AQuotaExceededExceptionBecomesTheErrorABrowserRaises()
    {
        var provider = new RecordingProvider { OnWrite = _ => new CacheQuotaExceededException("no room left") };
        var engine = CacheEngine(provider);

        // https://webidl.spec.whatwg.org/#quotaexceedederror — the interface, with both members null because
        // this provider named no figures.
        Run(engine, """
            const cache = await caches.open('v1');
            try {
                await cache.put('https://example.org/a', new Response('x'));
                return 'stored';
            } catch (e) {
                return [e.name, e.code, e.message, e instanceof QuotaExceededError, e instanceof DOMException, String(e.quota)].join('|');
            }
            """).Should().Be("QuotaExceededError|22|no room left|true|true|null");
    }

    [Fact]
    public void AProviderCanReportHowMuchRoomThereWasAndHowMuchWasWanted()
    {
        var provider = new RecordingProvider
        {
            OnWrite = _ => new CacheQuotaExceededException("no room left", quota: 1024, requested: 4096),
        };
        var engine = CacheEngine(provider);

        Run(engine, """
            const cache = await caches.open('v1');
            try {
                await cache.put('https://example.org/a', new Response('x'));
                return 'stored';
            } catch (e) {
                return [e.name, e.quota, e.requested].join('|');
            }
            """).Should().Be("QuotaExceededError|1024|4096");
    }

    [Theory]
    [InlineData(-1d, 5d)]
    [InlineData(5d, -1d)]
    [InlineData(9d, 8d)]
    [InlineData(double.NaN, 1d)]
    public void AnIllFormedQuotaPairIsRefusedAtTheExceptionsOwnConstructor(double quota, double requested)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new CacheQuotaExceededException("x", quota, requested));
    }

    [Fact]
    public void AnyOtherProviderFailureBecomesATypeErrorTheHostCanReadTheCauseOf()
    {
        var provider = new RecordingProvider { OnWrite = _ => new InvalidOperationException("the disk is on fire") };
        var engine = CacheEngine(provider);

        // The script sees a plain TypeError …
        Run(engine, """
            const cache = await caches.open('v1');
            try { await cache.put('https://example.org/a', new Response('x')); return 'stored'; }
            catch (e) { return e.constructor.name + '|' + (e.message.indexOf('disk') >= 0); }
            """).Should().Be("TypeError|false");

        // … and the host gets the original exception off the error value, which the script cannot reach.
        var rejected = Assert.Throws<PromiseRejectedException>(() => engine
            .Evaluate("(async () => { const c = await caches.open('v1'); await c.put('https://example.org/a', new Response('x')); })()")
            .UnwrapIfPromise());

        JintException.TryGetClrException(rejected, out var clrException).Should().BeTrue();
        clrException.Should().BeOfType<InvalidOperationException>().Which.Message.Should().Be("the disk is on fire");
    }

    [Fact]
    public void TheDefaultProviderIsPrivateToEachEngineAndAnAssignedOneIsNot()
    {
        var shared = new Options().UseCacheApi();

        var writer = new Engine(shared);
        var reader = new Engine(shared);

        Run(writer, "const c = await caches.open('v1'); await c.put('https://example.org/a', new Response('x')); return 'done';");

        // Two engines built from one Options do not share a cache unless the host said so.
        Run(reader, "const c = await caches.open('v1'); return typeof (await c.match('https://example.org/a'));")
            .Should().Be("undefined");

        var provider = new RecordingProvider();
        var explicitly = new Options().UseCacheApi(cache => cache.Provider = provider);

        Run(new Engine(explicitly), "const c = await caches.open('v1'); await c.put('https://example.org/a', new Response('x')); return 'done';");
        Run(new Engine(explicitly), "const c = await caches.open('v1'); return await (await c.match('https://example.org/a')).text();")
            .Should().Be("x");
    }

    [Fact]
    public void CachedDataSurvivesAGlobalSnapshotRestore()
    {
        var engine = new Engine(options => options.UseCacheApi());
        var snapshot = engine.Advanced.CaptureGlobalSnapshot();

        Run(engine, "const c = await caches.open('v1'); await c.put('https://example.org/a', new Response('x')); return 'done';");

        engine.Advanced.RestoreGlobalSnapshot(snapshot);

        // A restore reverts the engine's global bindings, not host storage — the same answer the module
        // registry gets. A host that wants each cycle to start empty gives the engine a fresh provider.
        Run(engine, "const c = await caches.open('v1'); return await (await c.match('https://example.org/a')).text();")
            .Should().Be("x");
    }

    private sealed class StubHandler : HttpMessageHandler
    {
        internal int Requests { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests++;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("body of " + request.RequestUri),
            });
        }
    }

    [Fact]
    public void AddAllCommitsTheWholeBatchAsOneWrite()
    {
        var provider = new RecordingProvider();
        var handler = new StubHandler();

        var engine = new Engine(options => options
            .UseCacheApi(cache => cache.Provider = provider)
            .UseFetch(fetch => fetch.HttpClient = new HttpClient(handler)));

        Run(engine, """
            const cache = await caches.open('v1');
            await cache.addAll(['https://example.org/a', 'https://example.org/b']);
            return 'done';
            """);

        handler.Requests.Should().Be(2);

        // One write for two requests: what makes the standard's all-or-nothing true here is that nothing is
        // written until every fetch has come back, so a provider only ever has one transaction to honour.
        var store = provider.Store("v1");
        store.Writes.Should().ContainSingle();
        store.Writes[0].Added.Should().HaveCount(2);
        store.Entries.Should().HaveCount(2);
    }

    [Fact]
    public void AFailedAddAllNeverReachesTheProvider()
    {
        var provider = new RecordingProvider();
        var handler = new StubHandler();

        var engine = new Engine(options => options
            .UseCacheApi(cache => cache.Provider = provider)
            .UseFetch(fetch =>
            {
                fetch.UrlFilter = uri => uri.Host.Equals("allowed.example", StringComparison.Ordinal);
                fetch.HttpClient = new HttpClient(handler);
            }));

        Run(engine, """
            const cache = await caches.open('v1');
            try { await cache.addAll(['https://example.org/a', 'https://example.org/b']); return 'stored'; }
            catch (e) { return e.constructor.name; }
            """).Should().Be("TypeError");

        handler.Requests.Should().Be(0);
        provider.Store("v1").Writes.Should().BeEmpty();
    }

    [Fact]
    public void TheEngineAsksTheProviderForNamesInTheOrderItSearchesThem()
    {
        var provider = new RecordingProvider();
        var engine = CacheEngine(provider);

        Run(engine, """
            await caches.open('v1');
            await caches.open('v2');
            return (await caches.keys()).join(',');
            """).Should().Be("v1,v2");

        provider.Calls.Should().Contain("open:v1");
        provider.Calls.Should().Contain("open:v2");
        provider.Calls.Should().Contain("names");

        // caches.match with a cacheName asks whether it exists rather than opening it, so a name nobody
        // created is not created by looking for it.
        Run(engine, "return typeof (await caches.match('https://example.org/a', { cacheName: 'v3' }));")
            .Should().Be("undefined");

        provider.Calls.Should().Contain("contains:v3");
        provider.Calls.Should().NotContain("open:v3");
    }
}
#endif
