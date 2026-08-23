#if NET8_0_OR_GREATER
using Jint.Native;
using Jint.Native.Array;
using Jint.Native.Object;
using Jint.Native.Promise;
using Jint.Runtime;
using Jint.Runtime.Descriptors;
using Jint.WebApi.Fetch;

namespace Jint.WebApi.Caches;

/// <summary>
/// <c>Cache.prototype</c> — the interface prototype object.
/// <para>
/// https://w3c.github.io/ServiceWorker/#cache-interface
/// </para>
/// </summary>
/// <remarks>
/// <para>
/// Every operation returns a promise, so none of them throws: a brand-check failure, an argument conversion
/// failure and a failing step all reject instead — see <see cref="CacheOperations.Promised"/>.
/// </para>
/// <para>
/// The standard runs the storage half "in parallel" and queues a task to settle. The host storage here is
/// synchronous, so the work is done on the calling turn and only the settle is deferred, which the promise
/// does anyway: a <c>.then</c> never runs before the current job finishes. What that reduction costs is the
/// number of microtask turns between the call and the settle — nothing a script can observe about
/// <i>this</i> promise, only about how it interleaves with others.
/// </para>
/// </remarks>
[JsObject(UseShape = true)]
internal sealed partial class CachePrototype : Prototype
{
    [JsProperty(Name = "constructor", Flags = PropertyFlag.NonEnumerable)]
    private readonly CacheConstructor _constructor;

    [JsSymbol("ToStringTag", Flags = PropertyFlag.Configurable)]
    private static readonly JsString CacheToStringTag = new("Cache");

    internal CachePrototype(
        Engine engine,
        Realm realm,
        CacheConstructor constructor,
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
    /// https://w3c.github.io/ServiceWorker/#dom-cache-match — <c>matchAll</c>, then its first element or
    /// <c>undefined</c>.
    /// </summary>
    [JsFunction(Name = "match", Length = 1, Flags = PropertyFlag.ConfigurableEnumerableWritable)]
    private JsValue Match(JsValue thisObject, JsValue request, JsValue options)
        => CacheOperations.Promised(_engine, _realm, () => MatchAllCore(thisObject, request, options, "match", single: true));

    /// <summary>
    /// https://w3c.github.io/ServiceWorker/#dom-cache-matchall — a frozen array, in cache order, of every
    /// response the query selects. With the request omitted that is every response the cache holds.
    /// </summary>
    [JsFunction(Name = "matchAll", Length = 0, Flags = PropertyFlag.ConfigurableEnumerableWritable)]
    private JsValue MatchAll(JsValue thisObject, JsValue request, JsValue options)
        => CacheOperations.Promised(_engine, _realm, () => MatchAllCore(thisObject, request, options, "matchAll", single: false));

    /// <summary>
    /// https://w3c.github.io/ServiceWorker/#dom-cache-add — fetch one request and store what comes back.
    /// <b>Needs <c>fetch</c> enabled</b>; see <see cref="CacheAddAll"/>.
    /// </summary>
    [JsFunction(Name = "add", Length = 1, Flags = PropertyFlag.ConfigurableEnumerableWritable)]
    private JsValue Add(JsValue thisObject, JsValue request)
        => CacheOperations.Promised(_engine, _realm, () => CacheAddAll.Add(_engine, _realm, Brand(thisObject), request));

    /// <summary>
    /// https://w3c.github.io/ServiceWorker/#dom-cache-addall — fetch every request and store all of them or
    /// none. <b>Needs <c>fetch</c> enabled</b>; see <see cref="CacheAddAll"/>.
    /// </summary>
    [JsFunction(Name = "addAll", Length = 1, Flags = PropertyFlag.ConfigurableEnumerableWritable)]
    private JsValue AddAll(JsValue thisObject, JsValue requests)
        => CacheOperations.Promised(_engine, _realm, () => CacheAddAll.AddAll(_engine, _realm, Brand(thisObject), requests));

    /// <summary>
    /// https://w3c.github.io/ServiceWorker/#dom-cache-put — store a pair the script already has, with no
    /// network involved.
    /// </summary>
    [JsFunction(Name = "put", Length = 2, Flags = PropertyFlag.ConfigurableEnumerableWritable)]
    private JsValue Put(JsValue thisObject, JsValue request, JsValue response)
        => CacheOperations.Promised(_engine, _realm, () => PutCore(thisObject, request, response));

    /// <summary>
    /// https://w3c.github.io/ServiceWorker/#dom-cache-delete — whether anything matched, and it is gone if
    /// so.
    /// </summary>
    [JsFunction(Name = "delete", Length = 1, Flags = PropertyFlag.ConfigurableEnumerableWritable)]
    private JsValue Delete(JsValue thisObject, JsValue request, JsValue options)
        => CacheOperations.Promised(_engine, _realm, () => DeleteCore(thisObject, request, options));

    /// <summary>
    /// https://w3c.github.io/ServiceWorker/#dom-cache-keys — a frozen array, in cache order, of the requests
    /// the query selects. With the request omitted that is every request the cache holds.
    /// </summary>
    [JsFunction(Name = "keys", Length = 0, Flags = PropertyFlag.ConfigurableEnumerableWritable)]
    private JsValue Keys(JsValue thisObject, JsValue request, JsValue options)
        => CacheOperations.Promised(_engine, _realm, () => KeysCore(thisObject, request, options));

    private JsValue MatchAllCore(JsValue thisObject, JsValue request, JsValue options, string operation, bool single)
    {
        var cache = Brand(thisObject);

        // The union conversion first, then the dictionary's members, then the steps — the order WebIDL
        // performs them in, which an argument whose toString runs script can observe.
        var info = CacheOperations.ResolveRequestInfo(request, optional: !single);
        var query = CacheQuery.ReadOptions(_realm, options, operation, "Cache");

        if (!CacheOperations.TryQueryRequest(_realm, info, query, out var target))
        {
            // A non-GET Request without ignoreMethod selects nothing, which is a successful empty answer.
            return single ? Undefined : Frozen([]);
        }

        var responses = CacheOperations.MatchAll(_engine, _realm, cache.Store, target, query, stopAtFirst: single);

        if (!single)
        {
            return Frozen(responses);
        }

        return responses.Count == 0 ? Undefined : responses[0];
    }

    private JsArray KeysCore(JsValue thisObject, JsValue request, JsValue options)
    {
        var cache = Brand(thisObject);

        var info = CacheOperations.ResolveRequestInfo(request, optional: true);
        var query = CacheQuery.ReadOptions(_realm, options, "keys", "Cache");

        var requests = new List<JsValue>();
        if (CacheOperations.TryQueryRequest(_realm, info, query, out var target))
        {
            var entries = cache.Store.Entries;
            for (var i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                if (target is not null && !CacheQuery.Matches(target, entry, query))
                {
                    continue;
                }

                // An entry whose stored URL does not parse is invisible rather than fatal — the same rule
                // the matching algorithm applies, so keys() and match() agree about which entries exist.
                var hydrated = CacheOperations.HydrateRequest(_engine, _realm, entry.Request);
                if (hydrated is not null)
                {
                    requests.Add(hydrated);
                }
            }
        }

        return Frozen(requests);
    }

    private JsBoolean DeleteCore(JsValue thisObject, JsValue request, JsValue options)
    {
        var cache = Brand(thisObject);

        var info = CacheOperations.ResolveRequestInfo(request, optional: false);
        var query = CacheQuery.ReadOptions(_realm, options, "delete", "Cache");

        if (!CacheOperations.TryQueryRequest(_realm, info, query, out var target))
        {
            return JsBoolean.False;
        }

        return JsBoolean.Create(CacheOperations.Delete(cache.Store, target!, query));
    }

    /// <summary>
    /// https://w3c.github.io/ServiceWorker/#dom-cache-put, whose refusals are the whole of what makes a cache
    /// answerable later.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Only <c>GET</c>, only HTTP(S), never a 206 and never <c>Vary: *</c>.</b> A non-GET has a method the
    /// matching algorithm would refuse to match on the way out; a 206 is a fragment of a body, so serving it
    /// as a whole one would corrupt the answer; and a response varying by everything can never be matched
    /// again, so storing it only wastes room.
    /// </para>
    /// <para>
    /// The response's body is <b>consumed</b> here, as it is in a browser: the standard reads all its bytes
    /// while keeping a clone for the cache, so <c>bodyUsed</c> flips and a later <c>text()</c> on the very
    /// object that was cached rejects. The cached copy carries its own flag and reads fine.
    /// </para>
    /// </remarks>
    private JsValue PutCore(JsValue thisObject, JsValue requestValue, JsValue responseValue)
    {
        var cache = Brand(thisObject);

        var info = CacheOperations.ResolveRequestInfo(requestValue, optional: false)!;
        if (responseValue is not JsResponse response)
        {
            Throw.TypeError(_realm, "Failed to execute 'put' on 'Cache': parameter 2 is not of type 'Response'.");
            return Undefined;
        }

        var request = info as JsRequest ?? CacheOperations.ConstructRequest(_realm, info);

        if (!CacheOperations.IsHttpScheme(request.Url) || !string.Equals(request.Method, "GET", StringComparison.Ordinal))
        {
            Throw.TypeError(_realm, "Failed to execute 'put' on 'Cache': Request scheme must be 'http' or 'https' and its method must be 'GET'");
        }

        if (response.Status == 206)
        {
            Throw.TypeError(_realm, "Failed to execute 'put' on 'Cache': Partial response (status code 206) is unsupported");
        }

        if (CacheQuery.VaryContainsWildcard(response.Headers.List.Get("vary")))
        {
            Throw.TypeError(_realm, "Failed to execute 'put' on 'Cache': Vary header contains *");
        }

        if (response.IsUnusable)
        {
            Throw.TypeError(_realm, "Failed to execute 'put' on 'Cache': Response body is already used");
        }

        // "Read all bytes from reader" — the clone the cache keeps is the record built below, and the
        // original is disturbed by having been read. A buffered body answers synchronously, so the common
        // put settles on this very turn; a stream-backed body — a network response — settles when its last
        // chunk has arrived, exactly as the standard's read-all-bytes does. The outer promise adopts the
        // capability either way.
        var capability = PromiseConstructor.NewPromiseCapability(_engine, _realm.Intrinsics.Promise);
        var store = cache.Store;
        FetchBody.ReadBodyBytes(
            _engine,
            _realm,
            response,
            bytes =>
            {
                try
                {
                    CacheOperations.Put(_realm, store, [request], [response], [bytes]);
                    capability.Resolve(Undefined);
                }
                catch (JavaScriptException ex)
                {
                    capability.Reject(ex.Error);
                }
                catch (CacheQuotaExceededException ex)
                {
                    capability.Reject(_realm.Intrinsics.QuotaExceededError.CreateException(ex.Message, ex.Quota, ex.Requested));
                }
            },
            capability.Reject);
        return capability.PromiseInstance;
    }

    /// <summary>
    /// The <c>FrozenArray</c> both list-returning operations answer with,
    /// https://webidl.spec.whatwg.org/#dfn-create-frozen-array — an ordinary array that has been frozen, so a
    /// script cannot edit the answer it was handed and mistake it for having edited the cache.
    /// </summary>
    private JsArray Frozen(List<JsValue> items)
    {
        var array = new JsArray(_engine, items.ToArray());
        array.SetIntegrityLevel(IntegrityLevel.Frozen);
        return array;
    }

    /// <summary>
    /// The WebIDL brand check every member performs.
    /// </summary>
    private JsCache Brand(JsValue thisObject)
    {
        if (thisObject is JsCache cache)
        {
            return cache;
        }

        Throw.TypeError(_realm, "Illegal invocation: receiver is not a Cache");
        return null!;
    }
}
#endif
