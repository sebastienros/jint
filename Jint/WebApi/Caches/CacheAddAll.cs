#if NET8_0_OR_GREATER
using Jint.Native;
using Jint.Native.Promise;
using Jint.Runtime;
using Jint.WebApi.DomException;
using Jint.WebApi.Fetch;

namespace Jint.WebApi.Caches;

/// <summary>
/// <c>Cache.add</c> and <c>Cache.addAll</c>: fetch every request, and store the lot or store none of it.
/// <para>
/// https://w3c.github.io/ServiceWorker/#dom-cache-addall
/// </para>
/// </summary>
/// <remarks>
/// <para>
/// These are the only two <c>Cache</c> methods that reach the network, which is why they are the only two
/// that need <c>fetch</c> enabled. Every other method works on an engine that has the Cache API and nothing
/// else — a script can build its own <c>Request</c> and <c>Response</c> objects and populate the cache from
/// host data — so requiring the network feature for the whole surface would be far too coarse. The refusal
/// is a <c>TypeError</c> naming the flag rather than an absent method, because the method <i>is</i> there in
/// a browser and a script feature-detecting on <c>caches</c> alone would otherwise get no answer at all.
/// </para>
/// <para>
/// <b>All or nothing.</b> The standard's own atomicity comes from Batch Cache Operations' rollback; here it
/// comes from ordering: not one byte is written until every fetch has fulfilled and every response has
/// passed its checks, and the whole run is then committed as one <see cref="CacheStore.Write"/>. A failed
/// fetch, a 404, a <c>Vary: *</c> or a duplicate request therefore leaves the cache exactly as it was.
/// </para>
/// <para>
/// The fetches all start at once, so a list longer than
/// <c>Options.WebApi.Fetch.MaxConcurrentRequests</c> rejects on the requests that exceed it — and, being
/// all-or-nothing, stores nothing. That is the honest failure for a bound the host chose; a script wanting
/// more than the engine allows at once can <c>await</c> its way through <c>cache.add</c> instead.
/// </para>
/// </remarks>
internal static class CacheAddAll
{
    /// <summary>
    /// https://w3c.github.io/ServiceWorker/#dom-cache-add — "let requests be an array containing only
    /// request", then the algorithm below. The fulfillment value is already <c>undefined</c>, so the
    /// standard's mapping handler has nothing left to do.
    /// </summary>
    internal static JsValue Add(Engine engine, Realm realm, JsCache cache, JsValue request)
    {
        var info = CacheOperations.ResolveRequestInfo(request, optional: false)!;
        return Start(engine, realm, cache, [info], "add");
    }

    /// <summary>
    /// https://w3c.github.io/ServiceWorker/#dom-cache-addall
    /// </summary>
    internal static JsValue AddAll(Engine engine, Realm realm, JsCache cache, JsValue requests)
    {
        return Start(engine, realm, cache, ReadSequence(realm, requests), "addAll");
    }

    private static JsValue Start(Engine engine, Realm realm, JsCache cache, List<JsValue> infos, string operation)
    {
        var state = engine._webApi;
        if (state?.FetchOptions is null)
        {
            Throw.TypeError(
                realm,
                $"Failed to execute '{operation}' on 'Cache': this engine cannot fetch. Enable WebApiFeatures.Fetch (options.UseFetch()) to use Cache.add and Cache.addAll; every other Cache method works without it.");
        }

        // Step 3: a Request the script handed over is checked before anything is fetched — both its scheme
        // and its method. A string input cannot fail the method check, which is why the algorithm makes this
        // pass over the Request-typed elements alone.
        for (var i = 0; i < infos.Count; i++)
        {
            if (infos[i] is JsRequest request && (!CacheOperations.IsHttpScheme(request.Url) || !string.Equals(request.Method, "GET", StringComparison.Ordinal)))
            {
                Throw.TypeError(realm, $"Failed to execute '{operation}' on 'Cache': Request method '{request.Method}' is unsupported");
            }
        }

        // Step 5: every element becomes a request, and a scheme that is not HTTP(S) ends the whole call —
        // the standard aborts the fetches already started, which here means simply never starting one.
        var requests = new List<JsRequest>(infos.Count);
        for (var i = 0; i < infos.Count; i++)
        {
            var info = infos[i];
            var request = info as JsRequest ?? CacheOperations.ConstructRequest(realm, info);
            if (!CacheOperations.IsHttpScheme(request.Url))
            {
                Throw.TypeError(realm, $"Failed to execute '{operation}' on 'Cache': Add/AddAll does not support schemes other than \"http\" or \"https\"");
            }

            requests.Add(request);
        }

        var capability = PromiseConstructor.NewPromiseCapability(engine, realm.Intrinsics.Promise);
        new Join(engine, realm, cache, capability, requests, operation).Start(state!);
        return capability.PromiseInstance;
    }

    /// <summary>
    /// The <c>sequence&lt;RequestInfo&gt;</c> conversion, https://webidl.spec.whatwg.org/#es-sequence: the
    /// argument is iterated and every element is converted to the <c>RequestInfo</c> union as it arrives.
    /// </summary>
    private static List<JsValue> ReadSequence(Realm realm, JsValue requests)
    {
        var infos = new List<JsValue>();
        var iterator = requests.GetIterator(realm);

        try
        {
            while (iterator.TryIteratorStepValue(out var value))
            {
                infos.Add(CacheOperations.ResolveRequestInfo(value, optional: false)!);
            }
        }
        catch
        {
            iterator.CloseIfNotDone(CompletionType.Throw);
            throw;
        }

        return infos;
    }

    /// <summary>
    /// Waits for every fetch, checks each response, and commits once — the "get a promise to wait for all"
    /// step and the fulfillment handler after it.
    /// </summary>
    /// <remarks>
    /// The reactions are engine-internal continuations rather than JavaScript functions, so no function
    /// object is materialized per request and nothing a script could reach observes the join. Everything
    /// here runs on the engine's thread, inside a promise reaction job — which is why every failure has to
    /// become a rejection: an exception escaping a queued job erupts out of whatever is pumping and leaves
    /// the script's promise pending forever.
    /// </remarks>
    private sealed class Join
    {
        private readonly Engine _engine;
        private readonly Realm _realm;
        private readonly JsCache _cache;
        private readonly PromiseCapability _capability;
        private readonly List<JsRequest> _requests;
        private readonly JsResponse?[] _responses;
        private readonly ReadOnlyMemory<byte>?[] _bodies;
        private readonly string _operation;

        private int _pending;
        private bool _settled;

        internal Join(Engine engine, Realm realm, JsCache cache, PromiseCapability capability, List<JsRequest> requests, string operation)
        {
            _engine = engine;
            _realm = realm;
            _cache = cache;
            _capability = capability;
            _requests = requests;
            _responses = new JsResponse?[requests.Count];
            _bodies = new ReadOnlyMemory<byte>?[requests.Count];
            _operation = operation;
        }

        internal void Start(WebApiEngineState state)
        {
            _pending = _requests.Count;

            // `addAll([])` waits for nothing and has nothing to store, so the commit below reaches the
            // provider with an empty batch — which CacheOperations.Put declines to write at all.
            if (_pending == 0)
            {
                Guarded(Commit);
                return;
            }

            for (var i = 0; i < _requests.Count; i++)
            {
                // Through the engine's own fetch, so the host's scheme list, URL filter, size cap, deadline
                // and concurrency bound all apply exactly as they do to a fetch the script wrote itself.
                var promise = FetchOperation.Start(_engine, _realm, state, [_requests[i]]);
                PromiseOperations.PerformPromiseThen(_engine, (JsPromise) promise, new Reaction(this, i));
            }
        }

        internal void Settle(int index, JsValue value, ReactionType type)
        {
            if (_settled)
            {
                return;
            }

            if (type == ReactionType.Reject)
            {
                // A fetch that failed — a network error, a refused URL, a blown deadline — is the whole
                // call's failure, with the reason the script would have seen from the fetch itself.
                _settled = true;
                _capability.Reject(value);
                return;
            }

            Guarded(() => Fulfilled(index, value));
        }

        private void Fulfilled(int index, JsValue value)
        {
            var response = (JsResponse) value;

            // processResponse: "if response's type is error, or response's status is not an ok status or is
            // 206, reject with a TypeError". A network error is already a rejection here, so what is left is
            // the status — and 206, which is an ok status but a partial one, so caching it would answer a
            // later request with a fragment of a body.
            if (!response.Ok || response.Status == 206)
            {
                Fail(_realm.Intrinsics.TypeError.Construct(
                    $"Failed to execute '{_operation}' on 'Cache': Request failed with status {response.Status}"));
                return;
            }

            if (CacheQuery.VaryContainsWildcard(response.Headers.List.Get("vary")))
            {
                Fail(_realm.Intrinsics.TypeError.Construct(
                    $"Failed to execute '{_operation}' on 'Cache': Response with Vary: * cannot be cached"));
                return;
            }

            // A network response's body is a stream now, so the standard's read-all-bytes is a second
            // asynchronous stage per slot: the slot only counts as arrived once its bytes have too, and a
            // body that errors mid-read fails the whole call exactly as the fetch itself failing would.
            FetchBody.ReadBodyBytes(
                _engine,
                _realm,
                response,
                bytes => Guarded(() => BodyRead(index, response, bytes)),
                reason =>
                {
                    if (!_settled)
                    {
                        Fail(reason);
                    }
                });
        }

        private void BodyRead(int index, JsResponse response, ReadOnlyMemory<byte>? bytes)
        {
            if (_settled)
            {
                return;
            }

            _responses[index] = response;
            _bodies[index] = bytes;

            if (--_pending == 0)
            {
                Commit();
            }
        }

        private void Commit()
        {
            _settled = true;

            var responses = new List<JsResponse>(_responses.Length);
            var bodies = new List<ReadOnlyMemory<byte>?>(_responses.Length);
            for (var i = 0; i < _responses.Length; i++)
            {
                responses.Add(_responses[i]!);
                bodies.Add(_bodies[i]);
            }

            CacheOperations.Put(_realm, _cache.Store, _requests, responses, bodies);
            _capability.Resolve(JsValue.Undefined);
        }

        private void Fail(JsValue reason)
        {
            _settled = true;
            _capability.Reject(reason);
        }

        /// <summary>
        /// Runs one step of the join on an event-loop turn, where nothing may escape.
        /// </summary>
        private void Guarded(Action step)
        {
            try
            {
                step();
            }
            catch (JavaScriptException ex)
            {
                Fail(ex.Error);
            }
            catch (CacheQuotaExceededException ex)
            {
                Fail(_realm.Intrinsics.DomException.CreateException(DomExceptionNames.QuotaExceeded, ex.Message));
            }
            catch (Exception ex) when (!ConstraintFailure.MustPropagate(ex))
            {
                Fail(CacheOperations.ProviderFailure(_realm, ex));
            }
        }

        /// <summary>
        /// One request's reaction, carrying the index the response belongs at.
        /// </summary>
        private sealed class Reaction : IPromiseContinuation
        {
            private readonly Join _join;
            private readonly int _index;

            internal Reaction(Join join, int index)
            {
                _join = join;
                _index = index;
            }

            public void Invoke(Engine engine, JsValue value, ReactionType type) => _join.Settle(_index, value, type);
        }
    }
}
#endif
