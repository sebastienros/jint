#if NET8_0_OR_GREATER
#nullable enable

using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Jint.Native;

namespace Jint.Tests.Runtime.WebApi;

/// <summary>
/// The Cache API as https://w3c.github.io/ServiceWorker/#cache-interface specifies it — the object model, the
/// request-matching algorithm and the batch semantics, over the in-box in-memory provider.
/// </summary>
public class CacheTests
{
    private static Engine CacheEngine() => new(options => options.UseCacheApi());

    private static JsValue Run(Engine engine, string source) => engine.Evaluate(source).UnwrapIfPromise();

    private static JsValue Run(string source) => Run(CacheEngine(), source);

    /// <summary>
    /// <see cref="Run(Engine, string)"/> for a cache that is filled over the network. It is a separate helper
    /// because the promise it waits for settles only once <c>FetchBodyStream</c>'s <c>Task.Run</c> read loop
    /// has delivered the whole body, so the wait depends on a thread-pool continuation where every other test
    /// in this class settles on the engine's own queue.
    /// </summary>
    /// <remarks>
    /// Hence <see cref="TransportSignalCeiling"/> rather than the ten-second default: what is asserted is what
    /// the cache ended up holding, never how long the transport took, so the bound must be one only a genuine
    /// failure to deliver can reach. Every test that reaches the transport also hands its body to
    /// <see cref="DedicatedThread.RunAsync"/>, so this wait is not holding the worker the read loop needs —
    /// which is the inversion that dropped <see cref="AddAllRefusesTwoRequestsThatMatchEachOther"/> on a
    /// loaded machine (sebastienros/jint#3213).
    /// <para>
    /// It is what every test built on <see cref="FetchingCacheEngine"/> uses, including the several that
    /// refuse before a socket is opened and pin that with <c>Requests.Should().BeEmpty()</c>: which of them
    /// reaches the transport is a property of the case, not of the harness, and one helper for the group
    /// keeps a test that starts fetching from silently inheriting the ten-second default.
    /// </para>
    /// </remarks>
    private static JsValue RunFetching(Engine engine, string source) => engine.Evaluate(source).UnwrapIfPromise(TransportSignalCeiling);

    /// <summary>
    /// How long a network-backed cache operation may take to settle. Not a measurement and not a budget.
    /// </summary>
    private static readonly TimeSpan TransportSignalCeiling = TimeSpan.FromMinutes(2);

    /// <summary>
    /// Wraps a body in an immediately-invoked async function, which is how every one of these tests reads the
    /// promises the whole interface is made of.
    /// </summary>
    private static string Async(string body) => "(async () => {" + body + "})()";

    [Test]
    public void PutAndMatchRoundTripTheWholeResponse()
    {
        var result = Run(Async("""
            const cache = await caches.open('v1');
            await cache.put('https://example.org/a', new Response('hello', {
                status: 201,
                statusText: 'Created',
                headers: { 'x-a': '1', 'content-type': 'text/plain' },
            }));

            const r = await cache.match('https://example.org/a');
            return [r.status, r.statusText, r.ok, r.headers.get('x-a'), r.headers.get('content-type'), await r.text()].join('|');
            """));

        result.AsString().Should().Be("201|Created|true|1|text/plain|hello");
    }

    [Test]
    public void MatchAnswersUndefinedWhenNothingMatches()
    {
        Run(Async("""
            const cache = await caches.open('v1');
            await cache.put('https://example.org/a', new Response('hello'));
            return typeof (await cache.match('https://example.org/b'));
            """)).AsString().Should().Be("undefined");
    }

    [Test]
    public void AMatchedResponseCarriesImmutableHeaders()
    {
        // "Add a new Response object associated with response and a new Headers object whose guard is
        // 'immutable'" — a script must not be able to edit the answer and mistake it for editing the cache.
        Run(Async("""
            const cache = await caches.open('v1');
            await cache.put('https://example.org/a', new Response('hello', { headers: { 'x-a': '1' } }));

            const r = await cache.match('https://example.org/a');
            try {
                r.headers.set('x-a', '2');
                return 'mutated';
            } catch (e) {
                return e.constructor.name;
            }
            """)).AsString().Should().Be("TypeError");
    }

    [Test]
    public void PutReplacesAnEntryWithTheSameUrl()
    {
        // Batch Cache Operations' put arm: "set requestResponses to the result of running Query Cache with
        // operation's request … remove the item … append", so a second put for one URL leaves one entry.
        Run(Async("""
            const cache = await caches.open('v1');
            await cache.put('https://example.org/a', new Response('first'));
            await cache.put('https://example.org/a', new Response('second'));

            const all = await cache.matchAll('https://example.org/a');
            return all.length + ':' + (await all[0].text());
            """)).AsString().Should().Be("1:second");
    }

    [Test]
    public void PutConsumesTheResponseItWasGiven()
    {
        // "Let bodyReadPromise be the result of reading all bytes from reader" — the original is disturbed,
        // and the copy the cache keeps is not.
        Run(Async("""
            const cache = await caches.open('v1');
            const response = new Response('hello');
            await cache.put('https://example.org/a', response);

            let second = 'read';
            try { await response.text(); } catch (e) { second = e.constructor.name; }

            const cached = await cache.match('https://example.org/a');
            return response.bodyUsed + '|' + second + '|' + (await cached.text());
            """)).AsString().Should().Be("true|TypeError|hello");
    }

    [Test]
    public void MatchAllAnswersEveryEntryInCacheOrder()
    {
        Run(Async("""
            const cache = await caches.open('v1');
            await cache.put('https://example.org/a', new Response('a'));
            await cache.put('https://example.org/b', new Response('b'));
            await cache.put('https://example.org/c', new Response('c'));

            const all = await cache.matchAll();
            const bodies = [];
            for (const r of all) { bodies.push(await r.text()); }
            return bodies.join(',');
            """)).AsString().Should().Be("a,b,c");
    }

    [Test]
    public void MatchAllKeepsVaryingEntriesApartAndInOrder()
    {
        // Two entries may share a URL when the cached responses vary by a header the two requests differ in;
        // ignoreVary then collapses them, and the order is the order they were put in.
        var engine = CacheEngine();
        engine.Execute("""
            var setup = (async () => {
                const cache = await caches.open('v1');
                await cache.put(new Request('https://example.org/a', { headers: { accept: 'x' } }),
                    new Response('first', { headers: { vary: 'accept' } }));
                await cache.put(new Request('https://example.org/a', { headers: { accept: 'y' } }),
                    new Response('second', { headers: { vary: 'accept' } }));
                return cache;
            })();
            """);

        Run(engine, Async("""
            const cache = await setup;
            const all = await cache.matchAll('https://example.org/a', { ignoreVary: true });
            const bodies = [];
            for (const r of all) { bodies.push(await r.text()); }
            return bodies.join(',');
            """)).AsString().Should().Be("first,second");

        // Without ignoreVary each request finds only its own entry, and a query carrying no `accept` at all
        // finds neither: an absent value does not match a present one.
        Run(engine, Async("""
            const cache = await setup;
            const x = await cache.match(new Request('https://example.org/a', { headers: { accept: 'x' } }));
            const y = await cache.match(new Request('https://example.org/a', { headers: { accept: 'y' } }));
            const none = await cache.match('https://example.org/a');
            return (await x.text()) + '|' + (await y.text()) + '|' + typeof none;
            """)).AsString().Should().Be("first|second|undefined");
    }

    [Test]
    public void MatchAllAndKeysAnswerFrozenArrays()
    {
        Run(Async("""
            const cache = await caches.open('v1');
            await cache.put('https://example.org/a', new Response('a'));
            return Object.isFrozen(await cache.matchAll()) + ':' + Object.isFrozen(await cache.keys());
            """)).AsString().Should().Be("true:true");
    }

    [Test]
    public void IgnoreSearchComparesUrlsWithoutTheirQuery()
    {
        var engine = CacheEngine();
        engine.Execute("""
            var setup = (async () => {
                const cache = await caches.open('v1');
                await cache.put('https://example.org/a?q=1', new Response('stored'));
                return cache;
            })();
            """);

        // Exact by default …
        Run(engine, Async("const c = await setup; return typeof (await c.match('https://example.org/a'));"))
            .AsString().Should().Be("undefined");

        // … and with the queries removed from both sides, a bare URL and a different query both match.
        Run(engine, Async("""
            const c = await setup;
            const bare = await c.match('https://example.org/a', { ignoreSearch: true });
            const other = await c.match('https://example.org/a?q=2', { ignoreSearch: true });
            return (await bare.text()) + '|' + (await other.text());
            """)).AsString().Should().Be("stored|stored");

        // A different path still does not match, so ignoreSearch is not simply ignoring the tail.
        Run(engine, Async("const c = await setup; return typeof (await c.match('https://example.org/b', { ignoreSearch: true }));"))
            .AsString().Should().Be("undefined");
    }

    [Test]
    public void AFragmentIsExcludedFromTheComparisonButKeptOnTheStoredRequest()
    {
        // "If queryURL does not equal cachedURL with the exclude fragment flag set" — matching ignores the
        // fragment, while keys() reports the request's URL as the script wrote it.
        Run(Async("""
            const cache = await caches.open('v1');
            await cache.put('https://example.org/a#one', new Response('stored'));

            const matched = await cache.match('https://example.org/a#two');
            const keys = await cache.keys();
            return (await matched.text()) + '|' + keys[0].url;
            """)).AsString().Should().Be("stored|https://example.org/a#one");
    }

    [Test]
    public void IgnoreMethodDecidesWhetherANonGetQueryMatchesAnything()
    {
        var engine = CacheEngine();
        engine.Execute("""
            var setup = (async () => {
                const cache = await caches.open('v1');
                await cache.put('https://example.org/a', new Response('stored'));
                return cache;
            })();
            var post = () => new Request('https://example.org/a', { method: 'POST', body: 'x' });
            """);

        // "If r's method is not `GET` and options.ignoreMethod is false, return a promise resolved with an
        // empty array" — a successful empty answer, not a rejection.
        Run(engine, Async("""
            const c = await setup;
            const all = await c.matchAll(post());
            const one = await c.match(post());
            const keys = await c.keys(post());
            return all.length + '|' + typeof one + '|' + keys.length;
            """)).AsString().Should().Be("0|undefined|0");

        Run(engine, Async("""
            const c = await setup;
            const one = await c.match(post(), { ignoreMethod: true });
            const keys = await c.keys(post(), { ignoreMethod: true });
            return (await one.text()) + '|' + keys.length;
            """)).AsString().Should().Be("stored|1");
    }

    [Test]
    public void DeleteHonoursIgnoreMethodAndReportsWhetherAnythingWent()
    {
        var engine = CacheEngine();
        engine.Execute("""
            var open = () => caches.open('v1');
            var post = () => new Request('https://example.org/a', { method: 'POST', body: 'x' });
            """);

        Run(engine, Async("""
            const cache = await open();
            await cache.put('https://example.org/a', new Response('stored'));

            // A non-GET query deletes nothing and says so …
            const refused = await cache.delete(post());
            const stillThere = (await cache.keys()).length;

            // … until ignoreMethod lets it through.
            const deleted = await cache.delete(post(), { ignoreMethod: true });
            const afterwards = (await cache.keys()).length;

            // A second delete finds nothing left.
            const again = await cache.delete('https://example.org/a');

            return [refused, stillThere, deleted, afterwards, again].join('|');
            """)).AsString().Should().Be("false|1|true|0|false");
    }

    [Test]
    public void PutRefusesANonGetRequest()
    {
        // "If innerRequest's url's scheme is not one of `http` and `https`, or innerRequest's method is not
        // `GET`, return a promise rejected with a TypeError."
        Run(Async("""
            const cache = await caches.open('v1');
            try {
                await cache.put(new Request('https://example.org/a', { method: 'POST', body: 'x' }), new Response('y'));
                return 'stored';
            } catch (e) {
                return e.constructor.name + ':' + (await cache.keys()).length;
            }
            """)).AsString().Should().Be("TypeError:0");
    }

    [Test]
    public void PutRefusesANonHttpScheme()
    {
        Run(Async("""
            const cache = await caches.open('v1');
            try {
                await cache.put('data:,hello', new Response('y'));
                return 'stored';
            } catch (e) {
                return e.constructor.name;
            }
            """)).AsString().Should().Be("TypeError");
    }

    [Test]
    public void PutRefusesAPartialResponseAndAVaryWildcard()
    {
        // Both refusals exist so that what a cache answers with later can be trusted: a 206 is a fragment of
        // a body, and a response varying by everything could never be matched again.
        Run(Async("""
            const cache = await caches.open('v1');
            const failures = [];

            try { await cache.put('https://example.org/a', new Response('x', { status: 206 })); failures.push('206 stored'); }
            catch (e) { failures.push(e.constructor.name); }

            try { await cache.put('https://example.org/a', new Response('x', { headers: { vary: '*' } })); failures.push('vary stored'); }
            catch (e) { failures.push(e.constructor.name); }

            try { await cache.put('https://example.org/a', new Response('x', { headers: { vary: 'accept, *' } })); failures.push('list stored'); }
            catch (e) { failures.push(e.constructor.name); }

            return failures.join(',') + ':' + (await cache.keys()).length;
            """)).AsString().Should().Be("TypeError,TypeError,TypeError:0");
    }

    [Test]
    public void PutRefusesAnAlreadyConsumedBody()
    {
        Run(Async("""
            const cache = await caches.open('v1');
            const response = new Response('hello');
            await response.text();

            try {
                await cache.put('https://example.org/a', response);
                return 'stored';
            } catch (e) {
                return e.constructor.name;
            }
            """)).AsString().Should().Be("TypeError");
    }

    [Test]
    public void PutRefusesASecondArgumentThatIsNotAResponse()
    {
        Run(Async("""
            const cache = await caches.open('v1');
            try { await cache.put('https://example.org/a', 'not a response'); return 'stored'; }
            catch (e) { return e.constructor.name; }
            """)).AsString().Should().Be("TypeError");
    }

    [Test]
    public void KeysAnswersTheStoredRequestsInCacheOrder()
    {
        Run(Async("""
            const cache = await caches.open('v1');
            await cache.put('https://example.org/b', new Response('b'));
            await cache.put('https://example.org/a', new Response('a'));
            await cache.put('https://example.org/c', new Response('c'));

            const keys = await cache.keys();
            return keys.map(r => r.url + ':' + r.method).join(',');
            """)).AsString().Should().Be("https://example.org/b:GET,https://example.org/a:GET,https://example.org/c:GET");
    }

    [Test]
    public void KeysNarrowsToOneRequestWhenGivenOne()
    {
        Run(Async("""
            const cache = await caches.open('v1');
            await cache.put('https://example.org/a', new Response('a'));
            await cache.put('https://example.org/b', new Response('b'));

            const keys = await cache.keys('https://example.org/b');
            return keys.length + ':' + keys[0].url;
            """)).AsString().Should().Be("1:https://example.org/b");
    }

    [Test]
    public void ARequestsHeadersSurviveTheRoundTrip()
    {
        Run(Async("""
            const cache = await caches.open('v1');
            await cache.put(new Request('https://example.org/a', { headers: { 'x-a': '1', 'x-b': '2' } }), new Response('a'));

            const keys = await cache.keys();
            return keys[0].headers.get('x-a') + '|' + keys[0].headers.get('x-b');
            """)).AsString().Should().Be("1|2");
    }

    [Test]
    public void CachesAreListedInCreationOrderAndDeletedByName()
    {
        Run(Async("""
            const before = await caches.keys();
            await caches.open('v2');
            await caches.open('v1');

            const listed = await caches.keys();
            const has = await caches.has('v1');
            const missing = await caches.has('nope');
            const deleted = await caches.delete('v2');
            const again = await caches.delete('v2');

            return before.length + '|' + listed.join(',') + '|' + has + '|' + missing + '|' + deleted + '|' + again + '|' + (await caches.keys()).join(',');
            """)).AsString().Should().Be("0|v2,v1|true|false|true|false|v1");
    }

    [Test]
    public void OpeningTheSameNameTwiceGivesTwoObjectsOverOneCache()
    {
        // "Resolve promise with a new Cache object that represents value" — a new object every time, over the
        // storage the name already had.
        Run(Async("""
            const first = await caches.open('v1');
            await first.put('https://example.org/a', new Response('stored'));

            const second = await caches.open('v1');
            return (first === second) + ':' + (await (await second.match('https://example.org/a')).text());
            """)).AsString().Should().Be("false:stored");
    }

    [Test]
    public void ACacheKeepsWorkingAfterItsNameIsDeleted()
    {
        // The standard's own note: "the existing DOM objects … should remain functional".
        Run(Async("""
            const cache = await caches.open('v1');
            await cache.put('https://example.org/a', new Response('stored'));
            await caches.delete('v1');

            const matched = await cache.match('https://example.org/a');
            return (await caches.has('v1')) + ':' + (await matched.text());
            """)).AsString().Should().Be("false:stored");
    }

    [Test]
    public void CachesMatchSearchesEveryCacheInCreationOrder()
    {
        Run(Async("""
            const v1 = await caches.open('v1');
            const v2 = await caches.open('v2');
            await v2.put('https://example.org/a', new Response('from v2'));
            await v2.put('https://example.org/b', new Response('only in v2'));
            await v1.put('https://example.org/a', new Response('from v1'));

            // v1 was opened first, so it answers first even though v2 was filled first.
            const first = await caches.match('https://example.org/a');

            // A cache further down the list still answers for a URL nothing above it holds.
            const later = await caches.match('https://example.org/b');

            return (await first.text()) + '|' + (await later.text());
            """)).AsString().Should().Be("from v1|only in v2");
    }

    [Test]
    public void CachesMatchNarrowsToOneCacheByName()
    {
        Run(Async("""
            const v1 = await caches.open('v1');
            const v2 = await caches.open('v2');
            await v1.put('https://example.org/a', new Response('from v1'));
            await v2.put('https://example.org/a', new Response('from v2'));

            const named = await caches.match('https://example.org/a', { cacheName: 'v2' });
            const missing = await caches.match('https://example.org/a', { cacheName: 'nope' });

            // Naming a cache that does not exist answers undefined rather than creating it.
            return (await named.text()) + '|' + typeof missing + '|' + (await caches.keys()).join(',');
            """)).AsString().Should().Be("from v2|undefined|v1,v2");
    }

    [Test]
    public void CachesMatchAnswersUndefinedWhenNoCacheHoldsIt()
    {
        Run(Async("""
            await caches.open('v1');
            return typeof (await caches.match('https://example.org/nothing'));
            """)).AsString().Should().Be("undefined");
    }

    [Test]
    public void EveryOperationRejectsRatherThanThrows()
    {
        // Both interfaces are promise-returning throughout, so a brand-check failure and an argument that
        // cannot be converted are rejections — which is what lets a caches chain be written without a try.
        Run(Async("""
            const cache = await caches.open('v1');
            const results = [];

            try { results.push(await Cache.prototype.match.call({}, 'https://example.org/a')); }
            catch (e) { results.push('brand:' + e.constructor.name); }

            try { results.push(await CacheStorage.prototype.keys.call({})); }
            catch (e) { results.push('storageBrand:' + e.constructor.name); }

            try { results.push(await cache.match('not a url')); }
            catch (e) { results.push('url:' + e.constructor.name); }

            try { results.push(await cache.match('https://example.org/a', 5)); }
            catch (e) { results.push('options:' + e.constructor.name); }

            try { results.push(await caches.open()); }
            catch (e) { results.push('missing:' + e.constructor.name); }

            return results.join(',');
            """)).AsString().Should().Be("brand:TypeError,storageBrand:TypeError,url:TypeError,options:TypeError,missing:TypeError");
    }

    [Test]
    public void NeitherInterfaceIsConstructible()
    {
        var engine = CacheEngine();

        // WebIDL: an interface with no constructor operation has an interface object that refuses to build
        // anything — https://webidl.spec.whatwg.org/#es-interface-call.
        engine.Evaluate("(() => { try { new Cache(); return 'built'; } catch (e) { return e.constructor.name; } })()")
            .AsString().Should().Be("TypeError");
        engine.Evaluate("(() => { try { new CacheStorage(); return 'built'; } catch (e) { return e.constructor.name; } })()")
            .AsString().Should().Be("TypeError");

        engine.Evaluate("caches instanceof CacheStorage").AsBoolean().Should().BeTrue();
        engine.Evaluate("Object.prototype.toString.call(caches)").AsString().Should().Be("[object CacheStorage]");
    }

    [Test]
    public void ACacheObjectHasTheShapeWebIdlDescribes()
    {
        var engine = CacheEngine();

        Run(engine, Async("globalThis.cache = await caches.open('v1'); return 0;"));

        engine.Evaluate("Object.getOwnPropertyNames(cache).length").AsNumber().Should().Be(0);
        engine.Evaluate("Object.getPrototypeOf(cache) === Cache.prototype").AsBoolean().Should().BeTrue();
        engine.Evaluate("Object.prototype.toString.call(cache)").AsString().Should().Be("[object Cache]");
        engine.Evaluate("[Cache.prototype.match.length, Cache.prototype.matchAll.length, Cache.prototype.add.length, Cache.prototype.addAll.length, Cache.prototype.put.length, Cache.prototype.delete.length, Cache.prototype.keys.length].join(',')")
            .AsString().Should().Be("1,0,1,1,2,1,0");
        engine.Evaluate("[CacheStorage.prototype.match.length, CacheStorage.prototype.has.length, CacheStorage.prototype.open.length, CacheStorage.prototype.delete.length, CacheStorage.prototype.keys.length].join(',')")
            .AsString().Should().Be("1,1,1,1,0");
    }

    [Test]
    public void InstallsTheCacheGlobalsBehindTheirOwnFlagOnly()
    {
        var engine = CacheEngine();

        foreach (var name in new[] { "Cache", "CacheStorage" })
        {
            engine.Evaluate($"typeof {name}").AsString().Should().Be("function");

            // Interface objects: writable and configurable, but not enumerable.
            var descriptor = engine.Realm.GlobalObject.GetOwnProperty(name);
            descriptor.Enumerable.Should().BeFalse();
            descriptor.Writable.Should().BeTrue();
            descriptor.Configurable.Should().BeTrue();
        }

        engine.Evaluate("typeof caches").AsString().Should().Be("object");
        engine.Realm.GlobalObject.GetOwnProperty("caches").Enumerable.Should().BeTrue();

        // The default set never carries it, and neither does a default engine.
        new Engine(options => options.UseWebApis()).Evaluate("typeof caches").AsString().Should().Be("undefined");
        new Engine().Evaluate("typeof caches").AsString().Should().Be("undefined");

        // ... and never inside a shadow realm.
        engine.Evaluate("new ShadowRealm().evaluate('typeof caches')").AsString().Should().Be("undefined");
    }

    [Test]
    public void BringsTheObjectModelACacheIsMadeOfButNotFetch()
    {
        var engine = CacheEngine();

        foreach (var name in new[] { "Headers", "Request", "Response", "AbortController", "URL", "Blob" })
        {
            engine.Evaluate($"typeof {name}").AsString().Should().Be("function");
        }

        // Caching is not network access: the flag brings the classes and deliberately not the function.
        engine.Evaluate("typeof fetch").AsString().Should().Be("undefined");
    }

    [Test]
    public void AddAndAddAllRejectWithoutTheFetchFeature()
    {
        var result = Run(Async("""
            const cache = await caches.open('v1');
            const messages = [];

            try { await cache.add('https://example.org/a'); messages.push('added'); }
            catch (e) { messages.push(e.constructor.name + ':' + e.message); }

            try { await cache.addAll(['https://example.org/a']); messages.push('added'); }
            catch (e) { messages.push(e.constructor.name); }

            return messages.join('|');
            """)).AsString();

        // The message has to name what to turn on — an absent capability that says nothing is a support
        // ticket rather than a diagnosis.
        result.Should().StartWith("TypeError:");
        result.Should().Contain("WebApiFeatures.Fetch");
        result.Should().Contain("UseFetch");
        result.Should().EndWith("|TypeError");
    }

    private sealed class StubHandler : HttpMessageHandler
    {
        internal List<string> Requests { get; } = new();

        internal Func<string, HttpResponseMessage>? Responder { get; set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var url = request.RequestUri!.ToString();
            Requests.Add(url);

            var response = Responder is { } responder
                ? responder(url)
                : new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("body of " + url) };

            return Task.FromResult(response);
        }
    }

    private static Engine FetchingCacheEngine(HttpMessageHandler handler, Action<Options.FetchOptions>? configure = null)
    {
        return new Engine(options => options
            .UseCacheApi()
            .UseFetch(fetch =>
            {
                fetch.HttpClient = new HttpClient(handler);
                configure?.Invoke(fetch);
            }));
    }

    [Test]
    public Task AddAllFetchesEveryRequestAndStoresThemAll() => DedicatedThread.RunAsync(() =>
    {
        var handler = new StubHandler();

        RunFetching(FetchingCacheEngine(handler), Async("""
            const cache = await caches.open('v1');
            const result = await cache.addAll(['https://example.org/a', 'https://example.org/b']);

            const keys = await cache.keys();
            const first = await cache.match('https://example.org/a');
            return typeof result + '|' + keys.map(r => r.url).join(',') + '|' + (await first.text());
            """)).AsString().Should().Be("undefined|https://example.org/a,https://example.org/b|body of https://example.org/a");

        handler.Requests.Should().BeEquivalentTo(["https://example.org/a", "https://example.org/b"]);
    });

    [Test]
    public Task AddStoresTheOneResponseItFetched() => DedicatedThread.RunAsync(() =>
    {
        var handler = new StubHandler();

        RunFetching(FetchingCacheEngine(handler), Async("""
            const cache = await caches.open('v1');
            await cache.add('https://example.org/a');
            return await (await cache.match('https://example.org/a')).text();
            """)).AsString().Should().Be("body of https://example.org/a");
    });

    [Test]
    public Task AddAllStoresNothingWhenAnyResponseIsNotOk() => DedicatedThread.RunAsync(() =>
    {
        var handler = new StubHandler
        {
            Responder = url => url.EndsWith("/b", StringComparison.Ordinal)
                ? new HttpResponseMessage(HttpStatusCode.NotFound) { Content = new StringContent("nope") }
                : new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("ok") },
        };

        // "If response's … status is not an ok status … reject responsePromise with a TypeError" — and
        // because nothing is committed until every response has passed, the first one is not stored either.
        RunFetching(FetchingCacheEngine(handler), Async("""
            const cache = await caches.open('v1');
            let outcome = 'stored';
            try { await cache.addAll(['https://example.org/a', 'https://example.org/b']); }
            catch (e) { outcome = e.constructor.name; }

            return outcome + ':' + (await cache.keys()).length;
            """)).AsString().Should().Be("TypeError:0");
    });

    [Test]
    public Task AddAllStoresNothingWhenAResponseVariesByEverything() => DedicatedThread.RunAsync(() =>
    {
        var handler = new StubHandler
        {
            Responder = _ =>
            {
                var response = new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("ok") };
                response.Headers.Add("vary", "*");
                return response;
            },
        };

        RunFetching(FetchingCacheEngine(handler), Async("""
            const cache = await caches.open('v1');
            let outcome = 'stored';
            try { await cache.addAll(['https://example.org/a']); }
            catch (e) { outcome = e.constructor.name; }

            return outcome + ':' + (await cache.keys()).length;
            """)).AsString().Should().Be("TypeError:0");
    });

    [Test]
    public Task AddAllRefusesTwoRequestsThatMatchEachOther() => DedicatedThread.RunAsync(() =>
    {
        var handler = new StubHandler();

        // Batch Cache Operations step 4.3: "if the result of running Query Cache with operation's request …
        // and addedItems is not empty, throw an InvalidStateError" — and the rollback leaves nothing behind.
        RunFetching(FetchingCacheEngine(handler), Async("""
            const cache = await caches.open('v1');
            let outcome = 'stored';
            try { await cache.addAll(['https://example.org/a', 'https://example.org/a']); }
            catch (e) { outcome = e.name; }

            return outcome + ':' + (await cache.keys()).length;
            """)).AsString().Should().Be("InvalidStateError:0");
    });

    [Test]
    public void AddAllRefusesANonHttpSchemeBeforeFetchingAnything()
    {
        var handler = new StubHandler();

        RunFetching(FetchingCacheEngine(handler), Async("""
            const cache = await caches.open('v1');
            let outcome = 'stored';
            try { await cache.addAll(['https://example.org/a', 'data:,hello']); }
            catch (e) { outcome = e.constructor.name; }

            return outcome + ':' + (await cache.keys()).length;
            """)).AsString().Should().Be("TypeError:0");

        // The refusal is made before a socket is opened, so the sibling request is never sent either.
        handler.Requests.Should().BeEmpty();
    }

    [Test]
    public void AddAllRefusesANonGetRequestObject()
    {
        var handler = new StubHandler();

        RunFetching(FetchingCacheEngine(handler), Async("""
            const cache = await caches.open('v1');
            try { await cache.addAll([new Request('https://example.org/a', { method: 'POST', body: 'x' })]); return 'stored'; }
            catch (e) { return e.constructor.name; }
            """)).AsString().Should().Be("TypeError");

        handler.Requests.Should().BeEmpty();
    }

    [Test]
    public void AddGoesThroughTheEnginesOwnFetchPolicy()
    {
        var handler = new StubHandler();

        // The whole point of routing add/addAll through the engine's fetch: the host's URL filter, scheme
        // list, size cap and deadline bound them exactly as they bound a fetch the script wrote itself.
        var engine = FetchingCacheEngine(handler, fetch => fetch.UrlFilter = uri => uri.Host.Equals("allowed.example", StringComparison.Ordinal));

        RunFetching(engine, Async("""
            const cache = await caches.open('v1');
            let outcome = 'stored';
            try { await cache.add('https://denied.example/a'); }
            catch (e) { outcome = e.constructor.name; }

            return outcome + ':' + (await cache.keys()).length;
            """)).AsString().Should().Be("TypeError:0");

        handler.Requests.Should().BeEmpty();
    }

    [Test]
    public void AddAllWithNoRequestsResolvesWithoutFetchingAnything()
    {
        var handler = new StubHandler();

        RunFetching(FetchingCacheEngine(handler), Async("""
            const cache = await caches.open('v1');
            const result = await cache.addAll([]);
            return typeof result + ':' + (await cache.keys()).length;
            """)).AsString().Should().Be("undefined:0");

        handler.Requests.Should().BeEmpty();
    }

    [Test]
    public void AddAllRejectsForSomethingThatIsNotIterable()
    {
        var handler = new StubHandler();

        // The sequence<RequestInfo> conversion is an argument conversion, so its failure is a rejection like
        // every other — https://webidl.spec.whatwg.org/#es-sequence.
        RunFetching(FetchingCacheEngine(handler), Async("""
            const cache = await caches.open('v1');
            try { await cache.addAll(5); return 'accepted'; }
            catch (e) { return e.constructor.name; }
            """)).AsString().Should().Be("TypeError");
    }
}
#endif
