#if NET8_0_OR_GREATER
using Jint.Native;
using Jint.Native.Array;
using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Descriptors;
using Jint.WebApi.Fetch;

namespace Jint.WebApi.Caches;

/// <summary>
/// <c>CacheStorage.prototype</c> — the interface prototype object.
/// <para>
/// https://w3c.github.io/ServiceWorker/#cachestorage-interface
/// </para>
/// </summary>
/// <remarks>
/// <para>
/// The standard's own note describes this interface as "largely conform[ing] to ECMAScript 6 Map objects but
/// entirely async": <c>has</c>, <c>delete</c> and <c>keys</c> mean what they do on a map, <c>open</c> is the
/// get-or-create, and <c>match</c> is the convenience that searches every cache.
/// </para>
/// <para>
/// Every operation returns a promise and therefore never throws — see
/// <see cref="CacheOperations.Promised"/>.
/// </para>
/// </remarks>
[JsObject(UseShape = true)]
internal sealed partial class CacheStoragePrototype : Prototype
{
    [JsProperty(Name = "constructor", Flags = PropertyFlag.NonEnumerable)]
    private readonly CacheStorageConstructor _constructor;

    [JsSymbol("ToStringTag", Flags = PropertyFlag.Configurable)]
    private static readonly JsString CacheStorageToStringTag = new("CacheStorage");

    internal CacheStoragePrototype(
        Engine engine,
        Realm realm,
        CacheStorageConstructor constructor,
        ObjectPrototype objectPrototype) : base(engine, realm)
    {
        _prototype = objectPrototype;
        _constructor = constructor;
    }

    protected override void Initialize()
    {
        CreateProperties_Generated();
        CreateSymbols_Generated();
    }

    /// <summary>
    /// https://w3c.github.io/ServiceWorker/#cache-storage-match — the first response any cache can answer
    /// with, or <c>undefined</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The caches are searched <b>in creation order</b>, which is the order the provider's
    /// <see cref="CacheStorageProvider.Names"/> reports; the first cache with a match wins, and the rest are
    /// not consulted. An <c>options.cacheName</c> narrows the search to that one cache, and naming a cache
    /// that does not exist answers <c>undefined</c> rather than creating it.
    /// </para>
    /// <para>
    /// The standard chains one promise per cache, so a browser takes a microtask turn per cache searched;
    /// the search here is synchronous. The answer is identical — only the interleaving with other pending
    /// jobs differs.
    /// </para>
    /// </remarks>
    [JsFunction(Name = "match", Length = 1, Flags = PropertyFlag.ConfigurableEnumerableWritable)]
    private JsValue Match(JsValue thisObject, JsValue request, JsValue options)
        => CacheOperations.Promised(_engine, _realm, () => MatchCore(thisObject, request, options));

    /// <summary>
    /// https://w3c.github.io/ServiceWorker/#cache-storage-has
    /// </summary>
    [JsFunction(Name = "has", Length = 1, Flags = PropertyFlag.ConfigurableEnumerableWritable)]
    private JsValue Has(JsValue thisObject, JsValue cacheName, [ArgCount] int argumentCount)
        => CacheOperations.Promised(_engine, _realm, () =>
        {
            var storage = Brand(thisObject);
            var name = ToCacheName(cacheName, argumentCount, "has");
            return JsBoolean.Create(storage.Provider.Contains(name));
        });

    /// <summary>
    /// https://w3c.github.io/ServiceWorker/#cache-storage-open — the cache with that name, created if it did
    /// not exist. A new <c>Cache</c> object every time, over the same storage.
    /// </summary>
    [JsFunction(Name = "open", Length = 1, Flags = PropertyFlag.ConfigurableEnumerableWritable)]
    private JsValue Open(JsValue thisObject, JsValue cacheName, [ArgCount] int argumentCount)
        => CacheOperations.Promised(_engine, _realm, () =>
        {
            var storage = Brand(thisObject);
            var name = ToCacheName(cacheName, argumentCount, "open");
            return _realm.Intrinsics.Cache.CreateInstance(name, storage.Provider.Open(name));
        });

    /// <summary>
    /// https://w3c.github.io/ServiceWorker/#cache-storage-delete — whether the cache existed.
    /// </summary>
    /// <remarks>
    /// A <c>Cache</c> object a script is still holding goes on working afterwards, which is what the
    /// standard's own note asks for: what is deleted is the name, not the storage behind an object somebody
    /// already has.
    /// </remarks>
    [JsFunction(Name = "delete", Length = 1, Flags = PropertyFlag.ConfigurableEnumerableWritable)]
    private JsValue Delete(JsValue thisObject, JsValue cacheName, [ArgCount] int argumentCount)
        => CacheOperations.Promised(_engine, _realm, () =>
        {
            var storage = Brand(thisObject);
            var name = ToCacheName(cacheName, argumentCount, "delete");
            return JsBoolean.Create(storage.Provider.Delete(name));
        });

    /// <summary>
    /// https://w3c.github.io/ServiceWorker/#cache-storage-keys — the cache names, in the order they were
    /// first opened.
    /// </summary>
    [JsFunction(Name = "keys", Length = 0, Flags = PropertyFlag.ConfigurableEnumerableWritable)]
    private JsValue Keys(JsValue thisObject)
        => CacheOperations.Promised(_engine, _realm, () =>
        {
            var storage = Brand(thisObject);
            var names = storage.Provider.Names;

            var items = new JsValue[names.Count];
            for (var i = 0; i < names.Count; i++)
            {
                items[i] = JsString.Create(names[i]);
            }

            return new JsArray(_engine, items);
        });

    private JsValue MatchCore(JsValue thisObject, JsValue request, JsValue options)
    {
        var storage = Brand(thisObject);

        var info = CacheOperations.ResolveRequestInfo(request, optional: false);
        var query = CacheQuery.ReadMultiOptions(_realm, options, "match", "CacheStorage", out var cacheName);

        if (!CacheOperations.TryQueryRequest(_realm, info, query, out var target))
        {
            return Undefined;
        }

        if (cacheName is not null)
        {
            return storage.Provider.Contains(cacheName)
                ? MatchIn(storage.Provider.Open(cacheName), target, query)
                : Undefined;
        }

        var names = storage.Provider.Names;
        for (var i = 0; i < names.Count; i++)
        {
            var response = MatchIn(storage.Provider.Open(names[i]), target, query);
            if (!response.IsUndefined())
            {
                return response;
            }
        }

        return Undefined;
    }

    private JsValue MatchIn(CacheStore store, JsRequest? target, CacheQueryOptions query)
    {
        var responses = CacheOperations.MatchAll(_engine, _realm, store, target, query, stopAtFirst: true);
        return responses.Count == 0 ? Undefined : responses[0];
    }

    /// <summary>
    /// The <c>DOMString</c> argument, https://webidl.spec.whatwg.org/#es-DOMString — with the missing-argument
    /// check WebIDL performs before any conversion, so <c>caches.open()</c> is a <c>TypeError</c> rather than
    /// a cache named <c>"undefined"</c>.
    /// </summary>
    private string ToCacheName(JsValue value, int argumentCount, string operation)
    {
        if (argumentCount < 1)
        {
            Throw.TypeError(_realm, $"Failed to execute '{operation}' on 'CacheStorage': 1 argument required, but only 0 present.");
        }

        return TypeConverter.ToString(value);
    }

    /// <summary>
    /// The WebIDL brand check every member performs.
    /// </summary>
    private JsCacheStorage Brand(JsValue thisObject)
    {
        if (thisObject is JsCacheStorage storage)
        {
            return storage;
        }

        Throw.TypeError(_realm, "Illegal invocation: receiver is not a CacheStorage");
        return null!;
    }
}
#endif
