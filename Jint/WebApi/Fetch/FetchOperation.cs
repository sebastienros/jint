#if NET8_0_OR_GREATER
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Jint.Constraints;
using Jint.Native;
using Jint.Native.Promise;
using Jint.Runtime;
using Jint.WebApi.Abort;
using Jint.WebApi.DomException;

namespace Jint.WebApi.Fetch;

/// <summary>
/// One <c>fetch</c> call, from the synchronous checks on the engine thread to the promise settling on a later
/// event-loop turn.
/// <para>
/// https://fetch.spec.whatwg.org/#fetch-method
/// </para>
/// </summary>
/// <remarks>
/// <para>
/// Patterned on <c>ModuleLoadCompletion</c>, which solves the same problem for an asynchronous module load,
/// and obeys the same three rules. The <b>event-loop generation and the realm are captured at
/// registration</b>, not read at settle time: the two differ exactly when the engine's globals were restored
/// while the request was in flight, and that is the case the fence exists for. The outcome is <b>classified
/// off the engine thread into plain CLR data</b>, and only a generation-stamped job — which runs on the
/// engine thread — turns it into a <c>Response</c> or an error. And the settle is <b>once</b>, by
/// compare-and-swap, because a cancellation and a completion can race.
/// </para>
/// <para>
/// The one thing it does <i>not</i> copy is <c>ModuleLoadCompletion</c>'s inline-settle window. A module
/// loader may answer from a warm cache on the engine's own stack; a network request cannot, so there is never
/// a completion to run inline.
/// </para>
/// </remarks>
internal sealed class FetchOperation
{
    private readonly Engine _engine;

    /// <summary>
    /// The realm the fetch was started in, captured at registration. A settle arriving on a later event-loop
    /// turn runs under whatever realm is ambient then, and would otherwise build the <c>Response</c> — and
    /// mint the <c>TypeError</c> — against the wrong realm's intrinsics.
    /// </summary>
    private readonly Realm _realm;

    private readonly PromiseCapability _capability;

    /// <summary>
    /// The evaluation cycle this request was registered in. A settle carrying an earlier generation is
    /// discarded at dequeue, so a response that arrives after <c>RestoreGlobalSnapshot</c> never reaches the
    /// restored engine — the fence every other cross-thread completion in Jint sits behind.
    /// </summary>
    private readonly int _generation;

    /// <summary>
    /// The request's signal. Read only on the engine thread — the off-thread classification asks
    /// <see cref="_signalToken"/> instead, which is a thread-safe CLR object rather than engine state.
    /// </summary>
    private readonly JsAbortSignal _signal;

    private readonly CancellationToken _signalToken;

    /// <summary>
    /// The engine's own cancellation token, from <see cref="CancellationConstraint"/>. A request cancelled
    /// through it settles nothing at all: a constraint that became a promise rejection would no longer bound
    /// anything — the script would observe an ordinary failed fetch and carry on, in a loop if it liked.
    /// </summary>
    private readonly CancellationToken _engineToken;

    private readonly CancellationTokenSource _cancellation;
    private readonly TimeSpan _timeout;

    private int _settled;

    private FetchOperation(Engine engine, Realm realm, PromiseCapability capability, JsAbortSignal signal, TimeSpan timeout)
    {
        _engine = engine;
        _realm = realm;
        _capability = capability;
        _generation = engine.EventLoopGeneration;
        _timeout = timeout;
        _signal = signal;

        _signalToken = signal.CancellationToken;
        _engineToken = engine.Constraints.Find<CancellationConstraint>()?.Token ?? CancellationToken.None;
        _cancellation = CancellationTokenSource.CreateLinkedTokenSource(_signalToken, _engineToken);
    }

    /// <summary>
    /// https://fetch.spec.whatwg.org/#dom-global-fetch — the whole of the method's synchronous half.
    /// </summary>
    /// <remarks>
    /// <b>It never throws.</b> Every failure the standard names, and every policy failure this implementation
    /// adds, becomes a rejection of the promise it returns — which is what lets a fetch chain be written
    /// without a <c>try</c>. The only things that escape are the failures that must: a constraint firing, an
    /// engine cancellation, an out-of-memory.
    /// </remarks>
    internal static JsValue Start(Engine engine, Realm realm, WebApiEngineState state, JsCallArguments arguments)
    {
        // Step 1: the promise exists before anything can fail, so that everything below is a rejection.
        var capability = PromiseConstructor.NewPromiseCapability(engine, realm.Intrinsics.Promise);

        JsRequest request;
        try
        {
            // Step 2: "invoke the initial value of Request as constructor" — so a bad method, an unparsable
            // URL, a FormData body and a GET with a body are all rejections rather than throws.
            request = (JsRequest) realm.Intrinsics.Request.Construct(arguments, realm.Intrinsics.Request);
        }
        catch (JavaScriptException ex)
        {
            capability.Reject(ex.Error);
            return capability.PromiseInstance;
        }
        catch (Exception ex) when (!ConstraintFailure.MustPropagate(ex))
        {
            // A CLR failure raised by host code the conversion reached — a projected object's ToString, an
            // interop getter. The promise is what fetch answers with, so this is a rejection too; only the
            // failures that bound execution are allowed to escape.
            capability.Reject(NetworkError(realm, ex));
            return capability.PromiseInstance;
        }

        // Step 6: an already-aborted signal ends the fetch before anything is sent, with the signal's own
        // reason — https://fetch.spec.whatwg.org/#abort-fetch.
        if (request.Signal.Aborted)
        {
            capability.Reject(request.Signal.Reason);
            return capability.PromiseInstance;
        }

        var options = state.FetchOptions!;

        var urlFilter = options.UrlFilter;
        if (urlFilter is null)
        {
            urlFilter = static _ => true;
        }

        var policy = new FetchPolicy
        {
            AllowedSchemes = [.. options.AllowedSchemes],
            UrlFilter = urlFilter,
            MaxResponseBytes = options.MaxResponseBytes,
            MaxRedirects = options.MaxRedirects,
        };

        // The first hop's policy check runs here, on the engine thread, so a refused URL never reaches a
        // socket and never costs a task. Every redirect hop is re-checked inside the transport.
        if (!policy.Allows(request.Url, out _))
        {
            var denial = new FetchFailureException(FetchFailureKind.PolicyDenied, $"The fetch policy refused '{request.Url.Serialize()}'.");
            capability.Reject(NetworkError(realm, denial));
            return capability.PromiseInstance;
        }

        if (state.ActiveFetchCount >= options.MaxConcurrentRequests)
        {
            // Not a specified failure mode — the standard assumes a browser's connection manager — but an
            // engine embedded in a server cannot let a script hold sockets without bound. Rejecting rather
            // than queueing is the honest answer: a queue would turn a burst into an unbounded backlog.
            capability.Reject(realm.Intrinsics.TypeError.Construct(
                $"Failed to fetch: the engine already has {options.MaxConcurrentRequests} requests in flight, which is its Options.WebApi.Fetch.MaxConcurrentRequests limit."));
            return capability.PromiseInstance;
        }

        HttpClient client;
        try
        {
            client = ResolveClient(engine, realm, options);
        }
        catch (JavaScriptException ex)
        {
            capability.Reject(ex.Error);
            return capability.PromiseInstance;
        }
        catch (Exception ex) when (!ConstraintFailure.MustPropagate(ex))
        {
            // A host HttpClientFactory that threw. Its failure belongs to the fetch, not to the statement
            // that happened to call it.
            capability.Reject(NetworkError(realm, ex));
            return capability.PromiseInstance;
        }

        var operation = new FetchOperation(engine, realm, capability, request.Signal, options.Timeout);
        var snapshot = new FetchRequestSnapshot
        {
            Method = request.Method,
            Url = request.Url,
            Headers = new List<HeaderEntry>(request.Headers.List.Entries),
            Body = request.Body,
            Redirect = request.Redirect,
        };

        state.RegisterFetch(operation);
        operation.Run(client, snapshot, policy);
        return capability.PromiseInstance;
    }

    private static HttpClient ResolveClient(Engine engine, Realm realm, Options.FetchOptions options)
    {
        if (options.HttpClientFactory is { } factory)
        {
            // Called on the engine thread, once per fetch, so it may read per-request host state through
            // engine.Advanced.HostDefined.
            var client = factory(engine);
            if (client is null)
            {
                Throw.TypeError(realm, "Failed to fetch: Options.WebApi.Fetch.HttpClientFactory returned null.");
            }

            return client;
        }

        return options.HttpClient ?? FetchTransport.SharedClient;
    }

    private void Run(HttpClient client, FetchRequestSnapshot snapshot, FetchPolicy policy)
    {
        // The deadline is CLR-side rather than on the timer queue, deliberately: it must fire even for an
        // engine nobody is pumping, so an abandoned request cannot hold a socket open forever.
        if (_timeout > TimeSpan.Zero && _timeout != Timeout.InfiniteTimeSpan)
        {
            _cancellation.CancelAfter(_timeout);
        }

        Task<FetchResponseSnapshot> task;
        try
        {
            task = FetchTransport.SendAsync(client, snapshot, policy, _cancellation.Token);
        }
        catch (Exception ex)
        {
            // A client whose handler throws synchronously — a host DelegatingHandler, a disposed client.
            Complete(Task.FromException<FetchResponseSnapshot>(ex));
            return;
        }

        _ = task.ContinueWith(
            static (t, state) => ((FetchOperation) state!).Complete(t),
            this,
            CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);
    }

    /// <summary>
    /// Off the engine thread: classify the outcome into plain CLR data and queue the engine-thread half.
    /// Nothing here may touch the engine, which is why the classification asks the tokens rather than the
    /// signal, and mints no JavaScript value.
    /// </summary>
    private void Complete(Task<FetchResponseSnapshot> task)
    {
        if (Interlocked.CompareExchange(ref _settled, 1, 0) != 0)
        {
            return;
        }

        if (task.IsCanceled || task.Exception?.InnerException is OperationCanceledException)
        {
            // The engine's own cancellation is the one outcome with no settlement at all — see _engineToken.
            // Checked first, because a request that is also past its deadline is still bounded by the
            // constraint, and the constraint is what must not become a rejection.
            if (_engineToken.IsCancellationRequested)
            {
                Enqueue(null);
                return;
            }

            if (_signalToken.IsCancellationRequested)
            {
                Enqueue(RejectWithAbortReason);
                return;
            }

            // Neither token fired, so either the deadline did or the engine abandoned the request at a
            // global-snapshot restore — and the restore's own generation fence discards the job below.
            Enqueue(RejectWithTimeout);
            return;
        }

        if (task.IsFaulted)
        {
            var failure = Unwrap(task.Exception);
            Enqueue(() => RejectWithNetworkError(failure));
            return;
        }

        var response = task.Result;
        Enqueue(() => ResolveWithResponse(response));
    }

    private static Exception Unwrap(AggregateException? exception)
    {
        if (exception is null)
        {
            return new FetchFailureException(FetchFailureKind.Network, "The request failed.");
        }

        // The AggregateException wrapper says nothing a host can use; the transport failure itself is the
        // single inner exception in every ordinary case.
        return exception.InnerExceptions.Count == 1 ? exception.InnerExceptions[0] : exception;
    }

    /// <summary>
    /// Queues the engine-thread half, carrying the generation the fetch was registered in. A
    /// <see langword="null"/> settle deregisters and settles nothing, which is what an engine cancellation
    /// asks for — the bookkeeping still has to happen on the engine thread.
    /// </summary>
    private void Enqueue(Action? settle)
    {
        _engine.AddToEventLoop(() => RunSettle(settle), _generation);
    }

    /// <summary>
    /// On the engine thread, in the realm the fetch started in.
    /// </summary>
    private void RunSettle(Action? settle)
    {
        var entered = EnterFetchRealm();
        try
        {
            Release();
            settle?.Invoke();
        }
        catch (JavaScriptException ex)
        {
            _capability.Reject(ex.Error);
        }
        catch (Exception ex) when (_engine.EventLoop.IsRunningJob && !ConstraintFailure.MustPropagate(ex))
        {
            // On a queued event-loop turn there is no caller left to throw to: escaping would erupt out of
            // whatever is pumping, with the promise permanently pending. The failure becomes the fetch's
            // failure instead — and carries the original exception on the error value, for the host.
            _capability.Reject(NetworkError(_realm, ex));
        }
        finally
        {
            LeaveFetchRealm(entered);
        }
    }

    private void ResolveWithResponse(FetchResponseSnapshot snapshot)
    {
        var list = new HeaderList();
        list.Entries.AddRange(snapshot.Headers);

        var response = _realm.Intrinsics.Response.CreateInstance(list);
        response.Status = snapshot.Status;
        response.StatusText = snapshot.StatusText;
        response.Url = snapshot.Url;
        response.Redirected = snapshot.Redirected;

        // A null body status carries no body, and the transport's zero-byte read is not the same thing: a
        // 204's bodyUsed must stay false however often the script reads it.
        if (!FetchValues.IsNullBodyStatus(snapshot.Status))
        {
            response.Body = snapshot.Body;
        }

        _capability.Resolve(response);
    }

    /// <summary>
    /// https://fetch.spec.whatwg.org/#abort-fetch — the rejection value is the signal's own abort reason,
    /// which for a bare <c>controller.abort()</c> is an <c>AbortError</c> <c>DOMException</c>.
    /// </summary>
    private void RejectWithAbortReason()
    {
        var reason = _signal.Aborted
            ? _signal.Reason
            : _realm.Intrinsics.DomException.CreateException(DomExceptionNames.Abort, "The operation was aborted.");

        _capability.Reject(reason);
    }

    /// <summary>
    /// The failure a blown <c>Options.WebApi.Fetch.Timeout</c> produces — the same one
    /// <c>AbortSignal.timeout()</c> raises, so a script can handle a deadline uniformly however it was set.
    /// </summary>
    private void RejectWithTimeout()
    {
        _capability.Reject(_realm.Intrinsics.DomException.CreateException(
            DomExceptionNames.Timeout,
            $"The request exceeded the {_timeout} timeout set by Options.WebApi.Fetch.Timeout."));
    }

    private void RejectWithNetworkError(Exception failure)
    {
        _capability.Reject(NetworkError(_realm, failure));
    }

    /// <summary>
    /// The one error value every network-class failure produces —
    /// https://fetch.spec.whatwg.org/#concept-network-error, whose own answer is a <c>TypeError</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The message is deliberately uninformative</b>, which is what a browser does too: a message naming
    /// the DNS failure, the refused connection or the policy rule would let a script map the host's internal
    /// network by probing it and reading the answers apart. Two failures do carry a number — the redirect and
    /// size caps — because those are the host's own limits and say nothing about the network.
    /// </para>
    /// <para>
    /// The originating CLR exception rides the error <i>value</i>, so the host can read it with
    /// <c>JintException.TryGetClrException</c> while the script cannot see it at all.
    /// </para>
    /// </remarks>
    private static JsValue NetworkError(Realm realm, Exception failure)
    {
        var message = failure is FetchFailureException { Kind: FetchFailureKind.RedirectLimit or FetchFailureKind.ResponseTooLarge } bounded
            ? "Failed to fetch: " + bounded.Message
            : "Failed to fetch";

        return new JavaScriptException(realm.Intrinsics.TypeError, message, failure).Error;
    }

    /// <summary>
    /// Leaves the engine's in-flight set and frees the request's cancellation source. Runs on the engine
    /// thread, from the settle job.
    /// </summary>
    private void Release()
    {
        _engine._webApi?.UnregisterFetch(this);
        _cancellation.Dispose();
    }

    /// <summary>
    /// Abandons the request because the evaluation cycle it belongs to has ended. Cancels the transport so
    /// the socket is freed at once; any settle already on its way is discarded by the generation fence rather
    /// than applied to the restored engine.
    /// </summary>
    internal void Abandon()
    {
        Interlocked.Exchange(ref _settled, 1);

        try
        {
            _cancellation.Cancel();
        }
        catch (ObjectDisposedException)
        {
            // Raced with a settle that already released it; there is nothing left to cancel.
        }

        _cancellation.Dispose();
    }

    private bool EnterFetchRealm()
    {
        if (ReferenceEquals(_engine.Realm, _realm))
        {
            return false;
        }

        _engine.EnterExecutionContext(_realm.GlobalEnv, _realm.GlobalEnv, _realm, privateEnvironment: null, strict: _engine.Options.Strict);
        return true;
    }

    private void LeaveFetchRealm(bool entered)
    {
        if (entered)
        {
            _engine.LeaveExecutionContext();
        }
    }
}
#endif
