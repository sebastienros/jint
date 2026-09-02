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
    private readonly EventLoopRegistration _registration;

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

    /// <summary>
    /// The host's observer for this request, or <see langword="null"/> when it set none. Held here as well
    /// as passed to the transport, because the failures this class classifies — an abort, a blown deadline,
    /// an abandoned evaluation cycle — are ones the transport never sees as failures of its own.
    /// </summary>
    private readonly FetchObservation? _observation;

    /// <summary>
    /// When the deadline started, so that the body half of the request can be given what is left of it. The
    /// documented contract is that <c>Options.WebApi.Fetch.Timeout</c> bounds the whole call "from the call
    /// to the last byte of the body", and the body no longer shares the header phase's token source.
    /// </summary>
    private readonly long _startedAt = System.Diagnostics.Stopwatch.GetTimestamp();

    private int _settled;

    /// <summary>
    /// The response body the transport handed back, between the pool thread that produced it and the engine
    /// thread that turns it into a stream. Taken by whichever of <see cref="ResolveWithResponse"/> and
    /// <see cref="Abandon"/> gets there first, so the connection is let go exactly once.
    /// </summary>
    private FetchBodyStream? _pendingBody;

    /// <summary>
    /// The engine half of a streaming request body, or <see langword="null"/> for every other body shape.
    /// Touched only on the engine thread: the transport sees its <see cref="HttpContent"/> and nothing else.
    /// </summary>
    private FetchRequestBodyStream? _requestBody;

    private FetchOperation(Engine engine, Realm realm, PromiseCapability capability, JsAbortSignal signal, TimeSpan timeout, FetchObservation? observation)
    {
        _engine = engine;
        _realm = realm;
        _capability = capability;
        _registration = engine.CaptureEventLoopRegistration();
        _timeout = timeout;
        _signal = signal;
        _observation = observation;

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

        var options = state.FetchOptions!;
        var network = state.FetchNetwork;

        // Created before the first thing that can refuse the request, so that every refusal is reported — a
        // request the transport never sees still reaches OnFailed, with no OnRequest before it.
        var observation = FetchObservation.Create(network.Observer, FetchInitiator.Script);

        // Step 6: an already-aborted signal ends the fetch before anything is sent, with the signal's own
        // reason — https://fetch.spec.whatwg.org/#abort-fetch.
        if (request.Signal.Aborted)
        {
            observation?.Failed("The request was aborted before it was sent.", null);
            capability.Reject(request.Signal.Reason);
            return capability.PromiseInstance;
        }

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
            Origin = network.Origin,
            SameOriginReference = network.SameOriginReference,
            CookieJar = network.CookieJar,
        };

        // The first hop's policy check runs here, on the engine thread, so a refused URL never reaches a
        // socket and never costs a task. Every redirect hop is re-checked inside the transport.
        if (!policy.Allows(request.Url, out _))
        {
            var denial = new FetchFailureException(FetchFailureKind.PolicyDenied, $"The fetch policy refused '{request.Url.Serialize()}'.");
            observation?.Failed(denial.Message, denial);
            capability.Reject(NetworkError(realm, denial));
            return capability.PromiseInstance;
        }

        if (state.ActiveFetchCount >= options.MaxConcurrentRequests)
        {
            observation?.Failed($"The engine already has {options.MaxConcurrentRequests} requests in flight.", null);
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
            observation?.Failed("The HttpClient factory failed: " + ex.Message, ex);
            capability.Reject(NetworkError(realm, ex));
            return capability.PromiseInstance;
        }

        var operation = new FetchOperation(engine, realm, capability, request.Signal, options.Timeout, observation);
        state.RegisterFetch(operation);

        // https://fetch.spec.whatwg.org/#concept-request-referrer: "client" is resolved here, on the engine
        // thread, because it names a setting the transport is not allowed to read.
        var referrer = request.ReferrerSource switch
        {
            FetchReferrerSource.NoReferrer => null,
            FetchReferrerSource.Url => request.ReferrerUrl,
            _ => network.Referrer,
        };

        FetchRequestSnapshot Snapshot(ReadOnlyMemory<byte>? body, HttpContent? content = null) => new()
        {
            Method = request.Method,
            Url = request.Url,
            Headers = new List<HeaderEntry>(request.Headers.List.Entries),
            Body = body,
            BodyContent = content,
            Redirect = request.Redirect,
            Credentials = request.Credentials,
            Referrer = referrer,
            ReferrerPolicy = request.ReferrerPolicy ?? network.ReferrerPolicy,
        };

        // A request body given as a ReadableStream has no bytes to snapshot: it is streamed to the wire as
        // the socket drains it, which is the standard's `duplex: "half"` and is why the Request constructor
        // makes that member compulsory for such a body. Everything else already holds its bytes.
        if (request is { HasBody: true, Source: null })
        {
            if (request.IsUnusable)
            {
                state.UnregisterFetch(operation);
                observation?.Failed("The request body has already been consumed.", null);
                operation.RejectBeforeSending(realm.Intrinsics.TypeError.Construct("Body has already been consumed"));
                return capability.PromiseInstance;
            }

            var requestBody = new FetchRequestBodyStream(engine, request.Stream!);
            operation._requestBody = requestBody;

            // Started before the transport, so the first chunk is usually already waiting when the socket
            // asks for it — and so that a stream which errors immediately does so before a byte is sent.
            requestBody.Start();

            operation.Run(client, Snapshot(null, requestBody.Content), policy);
            return capability.PromiseInstance;
        }

        operation.Run(client, Snapshot(request.Source), policy);
        return capability.PromiseInstance;
    }

    /// <summary>
    /// Fails the fetch before a socket was ever opened — a request whose body turned out to be unusable.
    /// The token source has nothing to cancel and is released here rather than by a settle job that will
    /// never run.
    /// </summary>
    private void RejectBeforeSending(JsValue error)
    {
        if (Interlocked.Exchange(ref _settled, 1) != 0)
        {
            // Already abandoned, which the restore's generation fence now stands behind: settling this
            // promise is exactly what that forbids.
            return;
        }

        _cancellation.Dispose();
        _capability.Reject(error);
    }

    private static HttpClient ResolveClient(Engine engine, Realm realm, Options.FetchOptions options)
    {
        // Called on the engine thread, once per fetch, so a host factory may read per-request state through
        // engine.HostDefined.
        var client = FetchTransport.ResolveClient(engine, options);
        if (client is null)
        {
            Throw.TypeError(realm, "Failed to fetch: Options.WebApi.Fetch.HttpClientFactory returned null.");
        }

        return client;
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
            task = FetchTransport.SendAsync(client, snapshot, policy, _cancellation.Token, _observation);
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
        var body = task.Status == TaskStatus.RanToCompletion ? task.Result.Body : null;

        if (Interlocked.CompareExchange(ref _settled, 1, 0) != 0)
        {
            // Abandoned while the headers were in flight. The connection has nobody left to read it, and the
            // settle job that would have taken ownership of it will never run.
            body?.Dispose();
            return;
        }

        if (body is not null)
        {
            Interlocked.Exchange(ref _pendingBody, body);
        }

        if (task.IsCanceled || task.Exception?.InnerException is OperationCanceledException)
        {
            // The engine's own cancellation is the one outcome with no settlement at all — see _engineToken.
            // Checked first, because a request that is also past its deadline is still bounded by the
            // constraint, and the constraint is what must not become a rejection.
            if (_engineToken.IsCancellationRequested)
            {
                _observation?.Failed("The engine's execution was cancelled.", null);
                Enqueue(null);
                return;
            }

            if (_signalToken.IsCancellationRequested)
            {
                _observation?.Failed("The request was aborted.", null);
                Enqueue(RejectWithAbortReason);
                return;
            }

            // Neither token fired, so either the deadline did or the engine abandoned the request at a
            // global-snapshot restore — and the restore's own generation fence discards the job below.
            _observation?.Failed($"The request exceeded the {_timeout} timeout set by Options.WebApi.Fetch.Timeout.", null);
            Enqueue(RejectWithTimeout);
            return;
        }

        if (task.IsFaulted)
        {
            var failure = Unwrap(task.Exception);
            _observation?.Failed(failure.Message, failure);
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
        _engine.AddToEventLoop(() => RunSettle(settle), _registration);
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

        // "Set responseObject to the result of creating a Response object, given response, "immutable", and
        // relevantRealm" — https://fetch.spec.whatwg.org/#dom-global-fetch. What the server said is not the
        // script's to rewrite: a header edited in place here would look like an edit of the response while
        // reaching nothing that already read it, and a cache put or a handler's answer would carry the
        // script's version of what the origin sent. A script that does need to add one builds its own
        // Headers from these — filling copies the headers and not the guard.
        var response = _realm.Intrinsics.Response.CreateInstance(list, HeadersGuard.Immutable);
        response.Status = snapshot.Status;
        response.StatusText = snapshot.StatusText;
        response.Url = snapshot.Url;
        response.Redirected = snapshot.Redirected;

        // The transport gives no body at all for a null body status, and a zero-byte read would not be the
        // same thing: a 204's bodyUsed must stay false however often the script reads it.
        if (Interlocked.Exchange(ref _pendingBody, null) is { } body)
        {
            // The body gets a token source of its own rather than sharing the header phase's, which this
            // settle job is about to dispose: an abort or an engine cancellation still reaches it, and the
            // rest of the deadline is re-armed on it.
            var cancellation = CancellationTokenSource.CreateLinkedTokenSource(_signalToken, _engineToken);
            response.SetStreamBody(body.Attach(_engine, _realm, cancellation, RemainingTimeout()));
        }

        _capability.Resolve(response);
    }

    /// <summary>
    /// What is left of <c>Options.WebApi.Fetch.Timeout</c> once the headers are in, or <see langword="null"/>
    /// when the host asked for no deadline at all.
    /// </summary>
    private TimeSpan? RemainingTimeout()
    {
        if (_timeout <= TimeSpan.Zero || _timeout == Timeout.InfiniteTimeSpan)
        {
            return null;
        }

        return _timeout - System.Diagnostics.Stopwatch.GetElapsedTime(_startedAt);
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
    internal static JsValue NetworkError(Realm realm, Exception failure)
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

        // Whatever the outcome, nothing will consume another byte of a streaming request body: the transport
        // disposes its content on the way out of a successful send, and a failed or cancelled one has
        // nothing left to send it to.
        _requestBody?.StopProducing();
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

        _observation?.Failed("The engine's globals were restored while the request was in flight.", null);

        try
        {
            _cancellation.Cancel();
        }
        catch (ObjectDisposedException)
        {
            // Raced with a settle that already released it; there is nothing left to cancel.
        }

        _cancellation.Dispose();

        // A response whose headers arrived but whose settle job the fence will discard: nothing will ever
        // read the body, so the connection is let go here rather than waiting for a finalizer.
        Interlocked.Exchange(ref _pendingBody, null)?.Dispose();

        // Runs on the engine thread, from the restore, so the engine half of a streaming request body can be
        // stopped here directly rather than through a job the fence would discard.
        _requestBody?.Abandon();
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
