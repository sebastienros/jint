#if NET8_0_OR_GREATER
#nullable enable

using Jint.Native;
using Jint.Runtime;
using Jint.WebApi.IndexedDb;

namespace Jint.Tests.Runtime.WebApi;

/// <summary>
/// The web APIs that hand script a list of what script stored, a <c>Set-Cookie</c> value, a cache name, a database,
/// a record or an array key, build that array under <c>Options.Constraints.MaxArraySize</c> like the arrays a
/// built-in makes from a count (issue #4505). Each one is pinned past the limit and, as the control, at exactly it.
/// </summary>
public class WebApiListMaxArraySizeTests
{
    private const uint Limit = 4;

    private const string IndexedDbHelpers = """
        function request(r) {
            return new Promise((resolve, reject) => { r.onsuccess = () => resolve(r.result); r.onerror = () => reject(r.error); });
        }
        function completed(t) {
            return new Promise((resolve, reject) => { t.oncomplete = () => resolve(); t.onabort = () => reject(t.error); });
        }
        function open(name, upgrade = db => db.createObjectStore('s')) {
            const r = indexedDB.open(name, 1);
            r.onupgradeneeded = () => upgrade(r.result);
            return request(r);
        }
        async function fill(count) {
            const db = await open('db');
            const t = db.transaction('s', 'readwrite'), done = completed(t);
            for (let i = 0; i < count; i++) t.objectStore('s').put(i, i);
            await done;
            return db;
        }
        """;

    private static JsValue Run(Engine engine, string body)
        => engine.Evaluate("(async () => {" + body + "})()").UnwrapIfPromise();

    private static void ShouldBeRefused(Func<JsValue> action, int reportedSize)
    {
        var thrown = Caught.Exception(() => action());

        thrown.Should().BeOfType<MemoryLimitExceededException>();
        thrown!.Message.Should().Be($"The array size {reportedSize} is larger than maximum allowed ({Limit})");
    }

    private static Engine FetchEngine() => new(options =>
    {
        options.UseFetch();
        options.Constraints.MaxArraySize = Limit;
    });

    [TestCase(5)]
    [TestCase(4)]
    public void GetSetCookieIsBoundedByTheLimit(int cookies)
    {
        using var engine = FetchEngine();
        engine.Execute($"var h = new Headers(); for (let i = 0; i < {cookies}; i++) h.append('Set-Cookie', 'c' + i);");

        if (cookies > Limit)
        {
            ShouldBeRefused(() => engine.Evaluate("h.getSetCookie()"), cookies);
        }
        else
        {
            engine.Evaluate("h.getSetCookie().length").AsNumber().Should().Be(cookies);
        }
    }

    private static Engine CacheEngine() => new(options =>
    {
        options.UseCacheApi();
        options.Constraints.MaxArraySize = Limit;
    });

    [TestCase(5)]
    [TestCase(4)]
    public void CacheStorageKeysIsBoundedByTheLimit(int caches)
    {
        using var engine = CacheEngine();
        Run(engine, $"for (let i = 0; i < {caches}; i++) await caches.open('c' + i);");

        if (caches > Limit)
        {
            ShouldBeRefused(() => Run(engine, "return (await caches.keys()).length;"), caches);
        }
        else
        {
            Run(engine, "return (await caches.keys()).length;").AsNumber().Should().Be(caches);
        }
    }

    private static Engine IndexedDbEngine(uint maxArraySize = Limit, IndexedDbStore? store = null)
    {
        var engine = new Engine(options =>
        {
            options.UseWebApis(WebApiFeatures.IndexedDb);
            options.Constraints.MaxArraySize = maxArraySize;
        });
        if (store is not null)
        {
            engine._webApi!.IndexedDb.Configure(store, opaqueOrigin: false);
        }

        engine.Execute(IndexedDbHelpers);
        return engine;
    }

    [TestCase(5)]
    [TestCase(4)]
    public void DatabasesIsBoundedByTheLimit(int databases)
    {
        using var engine = IndexedDbEngine();
        Run(engine, $"for (let i = 0; i < {databases}; i++) (await open('db' + i)).close();");

        if (databases > Limit)
        {
            ShouldBeRefused(() => Run(engine, "return (await indexedDB.databases()).length;"), databases);
        }
        else
        {
            Run(engine, "return (await indexedDB.databases()).length;").AsNumber().Should().Be(databases);
        }
    }

    [TestCase("getAll", 5)]
    [TestCase("getAllKeys", 5)]
    [TestCase("getAll", 4)]
    [TestCase("getAllKeys", 4)]
    public void GetAllIsBoundedByTheLimit(string operation, int records)
    {
        using var engine = IndexedDbEngine();
        Run(engine, $"globalThis.db = await fill({records});");
        var read = $"return (await request(db.transaction('s').objectStore('s').{operation}())).length;";

        if (records > Limit)
        {
            ShouldBeRefused(() => Run(engine, read), records);
        }
        else
        {
            Run(engine, read).AsNumber().Should().Be(records);
        }
    }

    [Test]
    public void GetAllWithACountWithinTheLimitReadsThatMany()
    {
        using var engine = IndexedDbEngine();
        Run(engine, "globalThis.db = await fill(10);");

        Run(engine, "return (await request(db.transaction('s').objectStore('s').getAll(undefined, 4))).length;")
            .AsNumber().Should().Be(4);
    }

    /// <summary>
    /// A key array and a key path are as long as the array the writing engine was given, and an origin's store is
    /// shared by every engine of that origin, so the engine reading one back bounds it by its own limit.
    /// </summary>
    [TestCase(5)]
    [TestCase(4)]
    public void AKeyArrayWrittenByAnotherEngineIsBoundedByTheReadersLimit(int length)
    {
        var store = new IndexedDbStore();
        using var writer = IndexedDbEngine(maxArraySize: uint.MaxValue, store);
        Run(writer, $$"""
            const key = Array.from({ length: {{length}} }, (_, i) => i);
            const db = await open('db');
            const t = db.transaction('s', 'readwrite'), done = completed(t);
            t.objectStore('s').put('value', key);
            await done;
            db.close();
            """);

        using var reader = IndexedDbEngine(store: store);
        const string Read = """
            const db = await open('db');
            const keys = await request(db.transaction('s').objectStore('s').getAllKeys());
            return keys[0].length;
            """;

        if (length > Limit)
        {
            ShouldBeRefused(() => Run(reader, Read), length);
        }
        else
        {
            Run(reader, Read).AsNumber().Should().Be(length);
        }
    }

    [TestCase(5)]
    [TestCase(4)]
    public void AKeyPathWrittenByAnotherEngineIsBoundedByTheReadersLimit(int length)
    {
        var store = new IndexedDbStore();
        using var writer = IndexedDbEngine(maxArraySize: uint.MaxValue, store);
        Run(writer, $$"""
            const keyPath = Array.from({ length: {{length}} }, (_, i) => 'p' + i);
            (await open('db', db => db.createObjectStore('s', { keyPath }))).close();
            """);

        using var reader = IndexedDbEngine(store: store);
        const string Read = "const db = await open('db'); return db.transaction('s').objectStore('s').keyPath.length;";

        if (length > Limit)
        {
            ShouldBeRefused(() => Run(reader, Read), length);
        }
        else
        {
            Run(reader, Read).AsNumber().Should().Be(length);
        }
    }
}
#endif
