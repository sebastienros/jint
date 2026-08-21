#if NET8_0_OR_GREATER
using Jint.Native;
using Jint.Native.Promise;
using Jint.Runtime;
using Jint.WebApi.Abort;
using Jint.WebApi.DomException;
using Jint.WebApi.Fetch;
using Jint.WebApi.Url;
using Jint.WebApi.Url.Parsing;

namespace Jint.WebApi.Caches;

/// <summary>
/// The bridge between the engine's <c>Request</c>/<c>Response</c> objects and the plain CLR records a
/// <see cref="CacheStorageProvider"/> stores, plus
/// <see href="https://w3c.github.io/ServiceWorker/#batch-cache-operations">Batch Cache Operations</see>.
/// </summary>
/// <remarks>
/// <para>
/// The standard models every mutation as a list of typed operations run atomically with a rollback if any of
/// them throws. Only three shapes of that list can occur — one <c>delete</c>, one <c>put</c>, and the run of
/// <c>put</c>s an <c>addAll</c> commits — so each is written out here instead, and each reads the store's
/// entries once and issues exactly one <see cref="CacheStore.Write"/>. The rollback then needs no backup
/// copy: nothing is written until every step has succeeded, which is a stronger guarantee than undoing
/// afterwards and is what makes a failed <c>addAll</c> leave the cache untouched.
/// </para>
/// <para>
/// Everything here runs on the engine's thread, inside the <c>Cache</c> method the script called.
/// </para>
/// </remarks>
internal static class CacheOperations
{
    /// <summary>
    /// Flattens a request/response pair into the record a provider stores.
    /// </summary>
    internal static CacheEntry Dehydrate(JsRequest request, JsResponse response, ReadOnlyMemory<byte>? body)
    {
        return new CacheEntry(
            new CachedRequest(request.Url.Serialize(), request.Method, Dehydrate(request.Headers.List)),
            new CachedResponse(
                response.Status,
                response.StatusText,
                Dehydrate(response.Headers.List),
                body,
                response.Url,
                response.Redirected));
    }

    private static CachedHeader[] Dehydrate(HeaderList headers)
    {
        var entries = headers.Entries;
        if (entries.Count == 0)
        {
            return [];
        }

        var flattened = new CachedHeader[entries.Count];
        for (var i = 0; i < entries.Count; i++)
        {
            flattened[i] = new CachedHeader(entries[i].LowerName, entries[i].Value);
        }

        return flattened;
    }

    /// <summary>
    /// The <c>Response</c> a <c>match</c> or a <c>matchAll</c> answers with. Its headers are immutable, which
    /// is what "a new Headers object whose guard is <c>immutable</c>" asks for — a cached response handed to
    /// a script must not be editable in place, because the edit would not reach the cache.
    /// </summary>
    internal static JsResponse HydrateResponse(Engine engine, Realm realm, CachedResponse cached)
    {
        var headers = realm.Intrinsics.Headers.CreateInstance(Hydrate(cached.Headers));
        headers.List.Guard = HeadersGuard.Immutable;

        var response = new JsResponse(engine, headers)
        {
            _prototype = realm.Intrinsics.Response.PrototypeObject,
            Status = cached.Status,
            StatusText = cached.StatusText,
            Url = cached.Url,
            Redirected = cached.Redirected,
        };

        if (cached.Body is { } body)
        {
            response.SetBufferedBody(body);
        }

        return response;
    }

    /// <summary>
    /// The <c>Request</c> a <c>keys</c> answers with, or <see langword="null"/> when the stored URL does not
    /// parse — an entry a provider hydrated badly is invisible rather than fatal, the same rule
    /// <see cref="CacheQuery.Matches"/> applies.
    /// </summary>
    internal static JsRequest? HydrateRequest(Engine engine, Realm realm, CachedRequest cached)
    {
        var url = UrlParser.Parse(cached.Url);
        if (url is null)
        {
            return null;
        }

        var headers = realm.Intrinsics.Headers.CreateInstance(Hydrate(cached.Headers));
        headers.List.Guard = HeadersGuard.Immutable;

        return new JsRequest(engine, headers)
        {
            _prototype = realm.Intrinsics.Request.PrototypeObject,
            Method = cached.Method,
            Url = url,

            // A request's signal is never null. A cached one was never in flight, so it gets a fresh signal
            // nothing can abort rather than the one the request that filled the cache carried.
            Signal = new JsAbortSignal(engine, realm) { _prototype = realm.Intrinsics.AbortSignal.PrototypeObject },
        };
    }

    /// <summary>
    /// Rebuilds a header list from a provider's records.
    /// </summary>
    /// <remarks>
    /// A header a provider hands back that is not a well-formed HTTP header — a name that is not a token, a
    /// value carrying NUL, CR or LF — is dropped rather than admitted, because everything downstream of a
    /// <c>Headers</c> object assumes those invariants. It is dropped only from the JavaScript object: the
    /// stored record is what the matching algorithm reads, so a dropped header still varies what it varies.
    /// </remarks>
    private static HeaderList Hydrate(IReadOnlyList<CachedHeader> headers)
    {
        var list = new HeaderList();
        for (var i = 0; i < headers.Count; i++)
        {
            var header = headers[i];
            if (HeaderList.IsName(header.Name) && HeaderList.IsValue(header.Value))
            {
                list.Append(header.Name, header.Value);
            }
        }

        return list;
    }

    /// <summary>
    /// The <c>put</c> half of Batch Cache Operations for one or more pairs: each new request evicts whatever
    /// it matches, then is appended, and the whole run is committed as one write.
    /// </summary>
    /// <remarks>
    /// The duplicate check is the algorithm's step 4.3 — "if the result of running Query Cache with
    /// operation's request, operation's options, and addedItems is not empty, throw an InvalidStateError" —
    /// and it is the reason <c>cache.addAll(['/a', '/a'])</c> rejects and stores nothing rather than quietly
    /// keeping one of the two. Matching is run without options, exactly as a <c>put</c> operation carries
    /// none, so a cached response's <c>Vary</c> still decides what a new one evicts.
    /// </remarks>
    internal static void Put(Realm realm, CacheStore store, List<JsRequest> requests, List<JsResponse> responses, List<ReadOnlyMemory<byte>?> bodies)
    {
        var snapshot = store.Entries;
        var evicted = new bool[snapshot.Count];
        var added = new List<CacheEntry>(requests.Count);

        for (var i = 0; i < requests.Count; i++)
        {
            var request = requests[i];

            for (var j = 0; j < added.Count; j++)
            {
                if (CacheQuery.Matches(request, added[j], default))
                {
                    throw new JavaScriptException(realm.Intrinsics.DomException.CreateException(
                        DomExceptionNames.InvalidState,
                        "Failed to execute 'addAll' on 'Cache': duplicate requests"));
                }
            }

            for (var k = 0; k < snapshot.Count; k++)
            {
                if (!evicted[k] && CacheQuery.Matches(request, snapshot[k], default))
                {
                    evicted[k] = true;
                }
            }

            added.Add(Dehydrate(request, responses[i], bodies[i]));
        }

        var removed = new List<int>();
        for (var k = 0; k < evicted.Length; k++)
        {
            if (evicted[k])
            {
                removed.Add(k);
            }
        }

        if (removed.Count == 0 && added.Count == 0)
        {
            // `addAll([])` is the only way here: nothing was fetched and nothing is evicted, so there is
            // nothing for a provider to open a transaction for. Same rule as a delete that matched nothing.
            return;
        }

        store.Write(new CacheWrite(removed, added));
    }

    /// <summary>
    /// The <c>delete</c> half of Batch Cache Operations: whether anything matched, and it is gone if so.
    /// </summary>
    /// <remarks>
    /// A delete that matches nothing issues no write at all, which is the rule both operations follow: the
    /// standard's batch still "runs", but a batch that changes nothing is not worth a provider's
    /// transaction.
    /// </remarks>
    internal static bool Delete(CacheStore store, JsRequest request, CacheQueryOptions options)
    {
        var snapshot = store.Entries;
        var matches = CacheQuery.Run(snapshot, request, options);
        if (matches.Count == 0)
        {
            return false;
        }

        store.Write(new CacheWrite(matches, []));
        return true;
    }

    /// <summary>
    /// The responses a query selects, hydrated in list order —
    /// https://w3c.github.io/ServiceWorker/#dom-cache-matchall steps 5.2 and 5.3.
    /// </summary>
    /// <param name="engine">The engine the responses are built in.</param>
    /// <param name="realm">The realm whose <c>Response</c> prototype they get.</param>
    /// <param name="store">The cache to search.</param>
    /// <param name="query">
    /// The request to match, or <see langword="null"/> for the omitted-argument case, which selects every
    /// entry the cache holds.
    /// </param>
    /// <param name="options">The query options the matching algorithm honours.</param>
    /// <param name="stopAtFirst">
    /// Whether to stop once one response has been produced — what <c>match</c> is defined as, and what
    /// keeps <c>caches.match</c> from hydrating a whole cache to discard all but its first entry.
    /// </param>
    internal static List<JsValue> MatchAll(
        Engine engine,
        Realm realm,
        CacheStore store,
        JsRequest? query,
        CacheQueryOptions options,
        bool stopAtFirst)
    {
        var entries = store.Entries;
        var responses = new List<JsValue>();

        for (var i = 0; i < entries.Count; i++)
        {
            var entry = entries[i];
            var selected = query is null ? CacheQuery.IsAddressable(entry) : CacheQuery.Matches(query, entry, options);
            if (!selected)
            {
                continue;
            }

            responses.Add(HydrateResponse(engine, realm, entry.Response));
            if (stopAtFirst)
            {
                break;
            }
        }

        return responses;
    }

    /// <summary>
    /// The <c>RequestInfo</c> union conversion, https://webidl.spec.whatwg.org/#es-union: a <c>Request</c>
    /// stays itself and everything else becomes a <c>USVString</c>.
    /// </summary>
    /// <remarks>
    /// Performed where WebIDL performs it — before the method's own steps — so an argument whose
    /// <c>toString</c> runs script does so before an options bag's getters, exactly as in a browser. The
    /// <c>Request</c> constructor is <i>not</i> invoked here: that is a method step, and it runs after the
    /// options have been read.
    /// </remarks>
    /// <param name="value">The argument as the script passed it.</param>
    /// <param name="optional">
    /// Whether an omitted argument is meaningful. For <c>matchAll</c> and <c>keys</c> it is, and answers
    /// <see langword="null"/>; for the required arguments <c>undefined</c> is stringified like anything else,
    /// which ends in the <c>TypeError</c> a missing argument owes the script.
    /// </param>
    internal static JsValue? ResolveRequestInfo(JsValue value, bool optional)
    {
        if (optional && value.IsUndefined())
        {
            return null;
        }

        return value is JsRequest ? value : JsString.Create(UrlValues.ToUsvString(value));
    }

    /// <summary>
    /// The "let r be …" prologue <c>matchAll</c>, <c>delete</c> and <c>keys</c> share.
    /// </summary>
    /// <returns>
    /// <see langword="false"/> when the query is a <c>Request</c> whose method is not <c>GET</c> and
    /// <c>ignoreMethod</c> was not asked for — the callers' "return a promise resolved with an empty array"
    /// and "…with false" step, which is a successful answer rather than a failure.
    /// </returns>
    internal static bool TryQueryRequest(Realm realm, JsValue? info, CacheQueryOptions options, out JsRequest? query)
    {
        query = null;

        if (info is null)
        {
            return true;
        }

        if (info is JsRequest request)
        {
            if (!options.IgnoreMethod && !string.Equals(request.Method, "GET", StringComparison.Ordinal))
            {
                return false;
            }

            query = request;
            return true;
        }

        query = ConstructRequest(realm, info);
        return true;
    }

    /// <summary>
    /// "The result of invoking the initial value of <c>Request</c> as constructor" — so a URL that does not
    /// parse fails here, with the constructor's own <c>TypeError</c>.
    /// </summary>
    internal static JsRequest ConstructRequest(Realm realm, JsValue info)
        => (JsRequest) realm.Intrinsics.Request.Construct([info], realm.Intrinsics.Request);

    /// <summary>
    /// The scheme restriction <c>put</c> and <c>addAll</c> impose: a cache holds HTTP(S) responses and
    /// nothing else.
    /// </summary>
    internal static bool IsHttpScheme(UrlRecord url)
        => string.Equals(url.Scheme, "http", StringComparison.Ordinal)
        || string.Equals(url.Scheme, "https", StringComparison.Ordinal);

    /// <summary>
    /// Runs one <c>Cache</c> or <c>CacheStorage</c> method's steps and turns the outcome into the promise the
    /// method returns.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Every operation on both interfaces returns a promise, which under WebIDL means <b>none of them ever
    /// throws</b>: an argument conversion failure, a brand-check failure and a step that raises are all
    /// rejections of the promise the script is holding —
    /// https://webidl.spec.whatwg.org/#dfn-create-operation-function.
    /// </para>
    /// <para>
    /// A body that answers with a promise of its own — which is what <c>add</c> and <c>addAll</c> do — has it
    /// adopted, so the script sees one promise settling once.
    /// </para>
    /// <para>
    /// The two provider failures are told apart here, and only here:
    /// <see cref="CacheQuotaExceededException"/> is the storage steps' <c>QuotaExceededError</c>, and every
    /// other CLR exception becomes a <c>TypeError</c> carrying the original on the error value for
    /// <c>JintException.TryGetClrException</c>. The failures that bound execution still escape — a constraint
    /// that became a promise rejection would no longer bound anything.
    /// </para>
    /// </remarks>
    internal static JsValue Promised(Engine engine, Realm realm, Func<JsValue> body)
    {
        var capability = PromiseConstructor.NewPromiseCapability(engine, realm.Intrinsics.Promise);

        try
        {
            capability.Resolve(body());
        }
        catch (JavaScriptException ex)
        {
            capability.Reject(ex.Error);
        }
        catch (CacheQuotaExceededException ex)
        {
            capability.Reject(realm.Intrinsics.QuotaExceededError.CreateException(ex.Message, ex.Quota, ex.Requested));
        }
        catch (Exception ex) when (!ConstraintFailure.MustPropagate(ex))
        {
            capability.Reject(ProviderFailure(realm, ex));
        }

        return capability.PromiseInstance;
    }

    /// <summary>
    /// The error value a provider's own failure becomes: a <c>TypeError</c>, with the CLR exception on the
    /// error value where the host can read it and the script cannot.
    /// </summary>
    internal static JsValue ProviderFailure(Realm realm, Exception failure)
        => new JavaScriptException(realm.Intrinsics.TypeError, "The cache storage provider failed", failure).Error;
}
#endif
