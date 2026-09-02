#if NET8_0_OR_GREATER
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Jint.Constraints;
using Jint.Runtime;
using Jint.WebApi.DomException;
using Jint.WebApi.Fetch;
using Jint.WebApi.Files;
using Jint.WebApi.Url.Parsing;

namespace Jint.WebApi.Xhr;

/// <summary>
/// How one <c>XMLHttpRequest</c> ended, as the four answers
/// https://xhr.spec.whatwg.org/#handle-errors chooses between.
/// </summary>
internal enum XhrOutcome
{
    /// <summary>The response arrived whole.</summary>
    Success,

    /// <summary>A network error — DNS, TLS, a refused policy, a body past the cap.</summary>
    NetworkError,

    /// <summary>A deadline: the object's own <c>timeout</c>, or the host's.</summary>
    Timeout,

    /// <summary><c>abort()</c>, which settles nothing because it has already settled itself.</summary>
    Aborted,

    /// <summary>The engine was cancelled or restored; nothing is settled at all.</summary>
    Abandoned,
}

/// <summary>
/// One <c>send()</c>, from the synchronous checks on the engine thread to the events fired on a later pump —
/// or, for a synchronous request, to the blocking wait that finishes before <c>send()</c> returns.
/// <para>
/// https://xhr.spec.whatwg.org/#dom-xmlhttprequest-send
/// </para>
/// </summary>
/// <remarks>
/// <para>
/// Patterned on <c>FetchOperation</c> and obeying the same three rules: the <b>event-loop generation and the
/// realm are captured at registration</b> so a response arriving after <c>RestoreGlobalSnapshot</c> never
/// reaches the restored engine; the outcome is <b>classified off the engine thread into plain CLR data</b>;
/// and the engine-thread half runs from a generation-stamped job.
/// </para>
/// <para>
/// <b>The synchronous flag is the one thing it does differently, and it is why the design works.</b>
/// <c>FetchTransport</c> mentions no <c>Engine</c>, no <c>Realm</c> and no <c>JsValue</c> — its own class
/// remarks say so, because the HTTP half of <c>fetch</c> already runs on a thread pool thread while script
/// goes on running. A synchronous request therefore <i>blocks on that task directly</i> rather than pumping
/// the engine, so it can never deadlock with a host's own loop: the work it waits for needs no engine turn to
/// make progress, and no job it queued is waiting behind it. Every event a synchronous request fires is fired
/// after the wait, on the caller's own stack.
/// </para>
/// <para>
/// <b>Both deadlines are CLR-side</b>, on cancellation token sources rather than on the timer queue, so an
/// engine nobody pumps still lets go of its socket. They are two sources rather than one because the
/// <c>timeout</c> attribute may be re-armed mid-flight and the host's own
/// <c>Options.WebApi.Fetch.Timeout</c> may not.
/// </para>
/// </remarks>
internal sealed class XhrOperation : IDisposable
{
    /// <summary>The read size the response-body loop uses.</summary>
    private const int ChunkSize = 16 * 1024;

    private readonly JsXmlHttpRequest _xhr;
    private readonly Engine _engine;
    private readonly Realm _realm;
    private readonly WebApiEngineState _state;
    private readonly EventLoopRegistration _registration;

    private readonly string _method;
    private readonly UrlRecord _url;
    private readonly ReadOnlyMemory<byte>? _body;

    /// <summary>
    /// The <c>Blob</c> a <c>blob:</c> URL named when <c>open()</c> ran, or <see langword="null"/> for every
    /// other URL — see <see cref="DeliverBlobUrl"/>.
    /// </summary>
    private readonly JsBlob? _blobUrlEntry;

    /// <summary>
    /// The engine's own cancellation token, from <see cref="CancellationConstraint"/>. A request cancelled
    /// through it settles nothing at all: a constraint that became an <c>error</c> event would no longer
    /// bound anything.
    /// </summary>
    private readonly CancellationToken _engineToken;

    private readonly CancellationTokenSource _abortSource = new();
    private readonly CancellationTokenSource _hostDeadline = new();
    private readonly CancellationTokenSource _scriptDeadline = new();
    private readonly CancellationTokenSource _cancellation;

    /// <summary>
    /// This request's side of the host's <c>FetchObserver</c>, kept so that the failure path — which is not
    /// inside the send — can report the one terminal call the observer is owed.
    /// </summary>
    private FetchObservation? _observation;

    /// <summary>When the request was sent, so a re-armed <c>timeout</c> measures from the right instant.</summary>
    private long _sentAt;

    private int _released;

    internal XhrOperation(
        JsXmlHttpRequest xhr,
        Engine engine,
        Realm realm,
        WebApiEngineState state,
        string method,
        UrlRecord url,
        ReadOnlyMemory<byte>? body,
        JsBlob? blobUrlEntry = null)
    {
        _xhr = xhr;
        _engine = engine;
        _realm = realm;
        _state = state;
        _method = method;
        _url = url;
        _body = body;
        _blobUrlEntry = blobUrlEntry;
        _registration = engine.CaptureEventLoopRegistration();
        _engineToken = engine.Constraints.Find<CancellationConstraint>()?.Token ?? CancellationToken.None;
        _cancellation = CancellationTokenSource.CreateLinkedTokenSource(
            _engineToken,
            _abortSource.Token,
            _hostDeadline.Token,
            _scriptDeadline.Token);
    }

    /// <summary>
    /// Whether this engine may open a socket at all — <see cref="WebApiFeatures.Fetch"/>, or an
    /// <c>HttpClient</c> the host named, which is the same decision said a different way.
    /// </summary>
    /// <remarks>
    /// Installing <c>XMLHttpRequest</c> is installing an interface; granting the network is a separate act, so
    /// an engine that named only the one flag gets an object whose <c>send()</c> fails exactly as a
    /// <c>fetch</c> refused by the policy does.
    /// </remarks>
    private static bool HasNetworkGrant(Engine engine, Options.FetchOptions options)
        => (engine._webApiFeatures & WebApiFeatures.Fetch) != WebApiFeatures.None
        || options.HttpClient is not null
        || options.HttpClientFactory is not null;

    /// <summary>
    /// The asynchronous half of <c>send()</c>: everything that can be checked on the engine thread, then the
    /// task and its continuation.
    /// </summary>
    internal void Start()
    {
        if (IsBlobUrl)
        {
            // Scheme fetch's `blob` arm. Queued rather than delivered here, because an asynchronous
            // XMLHttpRequest fires every one of its events from a task and a script that has not returned
            // from send() has had no chance to register a handler.
            Post(() => DeliverBlobUrl(reportProgress: true));
            return;
        }

        if (!TryBegin(out var client, out var policy, out var failure))
        {
            // Every one of these failures is the standard's network error, and for an asynchronous request a
            // network error is reported from a task rather than from send()'s own stack.
            Post(() =>
            {
                Release();
                Settle(XhrOutcome.NetworkError);
            });
            return;
        }

        _state.RegisterXhr(this);
        Arm();

        Task<XhrResponseData> task;
        try
        {
            task = SendAsync(client!, policy!, reportProgress: true);
        }
        catch (Exception ex)
        {
            // A client whose handler throws synchronously — a host DelegatingHandler, a disposed client.
            task = Task.FromException<XhrResponseData>(ex);
        }

        _ = task.ContinueWith(
            static (t, state) => ((XhrOperation) state!).CompleteAsynchronously(t),
            this,
            CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);
    }

    /// <summary>
    /// The synchronous half of <c>send()</c>: the same request, waited for on the calling thread, with every
    /// event fired afterwards on that same stack.
    /// </summary>
    /// <remarks>
    /// The wait is <see cref="Task{TResult}.GetAwaiter"/> rather than a pump, deliberately — see the class
    /// remarks for why that is the design and not a shortcut.
    /// </remarks>
    internal void RunSynchronously()
    {
        if (IsBlobUrl)
        {
            // No task and no wait: the bytes are already in hand, and a synchronous request fires its events
            // on the caller's own stack anyway.
            DeliverBlobUrl(reportProgress: false);
            return;
        }

        if (!TryBegin(out var client, out var policy, out var failure))
        {
            Release();

            // The request error steps throw for a synchronous request, which is the whole of how send()
            // reports a failure the caller can catch.
            _xhr.RunRequestErrorSteps(
                JsXmlHttpRequestEventTarget.ErrorEventType,
                DomExceptionNames.Network,
                failure ?? "The request failed.");
            return;
        }

        _state.RegisterXhr(this);
        Arm();

        XhrOutcome outcome;
        XhrResponseData? data = null;

        try
        {
            // The blocking wait the synchronous flag is: see the class remarks for why it cannot deadlock.
            data = SendAsync(client!, policy!, reportProgress: false).GetAwaiter().GetResult();
            outcome = XhrOutcome.Success;
        }
        catch (Exception ex) when (!ConstraintFailure.MustPropagate(ex))
        {
            outcome = Classify(ex);
        }

        Release();

        if (outcome == XhrOutcome.Abandoned || !ReferenceEquals(_xhr.CurrentOperation, this))
        {
            return;
        }

        if (outcome == XhrOutcome.Success)
        {
            var head = data!;
            _xhr.ReceiveResponseHead(head.Status, head.StatusText, head.Headers, head.Url, head.DeclaredLength);
            if (head.Body is { Length: > 0 })
            {
                _xhr.ReceiveBodyChunk(head.Body);
            }

            _xhr.HandleResponseEndOfBody();
            return;
        }

        Settle(outcome);
    }

    /// <summary>
    /// Whether this request names a <c>blob:</c> URL, which scheme fetch answers without a transport.
    /// </summary>
    private bool IsBlobUrl => string.Equals(_url.Scheme, "blob", StringComparison.Ordinal);

    /// <summary>
    /// https://fetch.spec.whatwg.org/#scheme-fetch, the <c>blob</c> arm, delivered through the same three
    /// calls a real response takes.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Every network check is skipped, which is the algorithm's own order rather than a shortcut: a blob URL
    /// names bytes this engine's own script created, so there is no socket for
    /// <c>Options.WebApi.Fetch.AllowedSchemes</c>, the host's <c>UrlFilter</c>, the concurrency cap or the
    /// network grant to bound. That is what makes <c>xhr.open('GET', URL.createObjectURL(blob))</c> work on
    /// an engine that named <c>WebApiFeatures.XmlHttpRequest</c> and nothing else — see
    /// <see cref="BlobUrlFetch"/>.
    /// </para>
    /// <para>
    /// The request's own <c>Range</c> header is honoured, so a 206 with a <c>Content-Range</c> is what a
    /// script asking for part of a blob gets, exactly as it would from a server.
    /// </para>
    /// </remarks>
    private void DeliverBlobUrl(bool reportProgress)
    {
        if (_blobUrlEntry is null
            || !BlobUrlFetch.TryBuild(_blobUrlEntry, _method, _xhr.AuthorRequestHeaders.Get("Range"), out var built))
        {
            if (reportProgress)
            {
                Settle(XhrOutcome.NetworkError);
            }
            else
            {
                _xhr.RunRequestErrorSteps(
                    JsXmlHttpRequestEventTarget.ErrorEventType,
                    DomExceptionNames.Network,
                    "The request failed.");
            }

            return;
        }

        var headers = new HeaderList();
        headers.Entries.AddRange(built.Headers);

        _xhr.ReceiveResponseHead(
            built.Status,
            built.StatusText,
            headers,
            _url.Serialize(excludeFragment: true),
            built.Body.Length);

        if (built.Body.Length > 0)
        {
            _xhr.ReceiveBodyChunk(built.Body.ToArray());
        }

        _xhr.HandleResponseEndOfBody();
    }

    /// <summary>
    /// The checks <c>fetch</c> makes on the engine thread before a socket is opened: the network grant, the
    /// destination policy, the concurrency cap and the host's client.
    /// </summary>
    /// <remarks>
    /// A refusal carries a message for the host's benefit and never reaches script: what the script sees is
    /// an <c>error</c> event or a <c>NetworkError</c> <c>DOMException</c> whose text says only that the
    /// request failed — a message naming the rule would let a script map the host's internal network by
    /// probing it, which is the reasoning <c>FetchOperation.NetworkError</c> already sets out.
    /// </remarks>
    private bool TryBegin(out HttpClient? client, out FetchPolicy? policy, out string? failure)
    {
        client = null;
        policy = null;
        failure = null;

        var options = _state.FetchOptions;
        if (options is null)
        {
            failure = "The request failed.";
            return false;
        }

        if (!HasNetworkGrant(_engine, options))
        {
            failure = "The request failed: this engine has no network grant. Enable WebApiFeatures.Fetch, or set Options.WebApi.Fetch.HttpClient.";
            return false;
        }

        var network = _state.FetchNetwork;

        policy = new FetchPolicy
        {
            AllowedSchemes = [.. options.AllowedSchemes],
            UrlFilter = options.UrlFilter ?? (static _ => true),
            MaxResponseBytes = options.MaxResponseBytes,
            MaxRedirects = options.MaxRedirects,
            Origin = network.Origin,
            SameOriginReference = network.SameOriginReference,
            CookieJar = network.CookieJar,
        };

        // The first hop's policy check runs here, on the engine thread, so a refused URL never reaches a
        // socket and never costs a task. Every redirect hop is re-checked inside the transport.
        if (!policy.Allows(_url, out _))
        {
            failure = "The request failed.";
            return false;
        }

        if (_state.ActiveXhrCount >= options.MaxConcurrentRequests)
        {
            failure = $"The request failed: the engine already has {options.MaxConcurrentRequests} XMLHttpRequests in flight, which is its Options.WebApi.Fetch.MaxConcurrentRequests limit.";
            return false;
        }

        try
        {
            client = FetchTransport.ResolveClient(_engine, options);
        }
        catch (Exception ex) when (!ConstraintFailure.MustPropagate(ex))
        {
            // A host HttpClientFactory that threw. Unlike fetch, where the failure becomes the rejection of
            // the promise the call returned, there is no promise here — it becomes the request's failure.
            failure = "The request failed.";
            return false;
        }

        if (client is null)
        {
            failure = "The request failed: Options.WebApi.Fetch.HttpClientFactory returned null.";
            return false;
        }

        return true;
    }

    /// <summary>Arms both deadlines and records when the request was sent.</summary>
    private void Arm()
    {
        _sentAt = System.Diagnostics.Stopwatch.GetTimestamp();

        var hostTimeout = _state.FetchOptions!.Timeout;
        if (hostTimeout > TimeSpan.Zero && hostTimeout != Timeout.InfiniteTimeSpan)
        {
            _hostDeadline.CancelAfter(hostTimeout);
        }

        RearmScriptDeadline(_xhr.TimeoutMilliseconds);
    }

    /// <summary>
    /// https://xhr.spec.whatwg.org/#timeout-timer — the deadline measured from when the request was sent, so
    /// that assigning <c>timeout</c> mid-flight really does shorten (or remove) it.
    /// </summary>
    internal void RearmScriptDeadline(uint milliseconds)
    {
        try
        {
            if (milliseconds == 0)
            {
                // "or xhr's timeout becomes 0 (whichever comes first)" — the wait is abandoned rather than
                // left to fire against a request that no longer has a deadline.
                _scriptDeadline.CancelAfter(Timeout.InfiniteTimeSpan);
                return;
            }

            var remaining = TimeSpan.FromMilliseconds(milliseconds) - System.Diagnostics.Stopwatch.GetElapsedTime(_sentAt);
            _scriptDeadline.CancelAfter(remaining > TimeSpan.Zero ? remaining : TimeSpan.Zero);
        }
        catch (ObjectDisposedException)
        {
            // Raced with a settle that already released the sources; the request is over either way.
        }
    }

    /// <summary>
    /// The request, the redirect loop and the bounded body read — every line of it off the engine thread.
    /// </summary>
    private async Task<XhrResponseData> SendAsync(HttpClient client, FetchPolicy policy, bool reportProgress)
    {
        try
        {
            return await SendCoreAsync(client, policy, reportProgress).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            // The observer is owed exactly one terminal call, and every failure of this request arrives here:
            // a refused hop, a blown cap, an abort, a timeout, a transport error. Reporting it is what keeps a
            // host network log from showing an XMLHttpRequest as sent and never answered.
            _observation?.Failed(exception.Message, exception);
            throw;
        }
    }

    private async Task<XhrResponseData> SendCoreAsync(HttpClient client, FetchPolicy policy, bool reportProgress)
    {
        var network = _state.FetchNetwork;
        var uploadLength = _body?.Length ?? 0;

        var request = new FetchRequestSnapshot
        {
            Method = _method,
            Url = _url,
            Headers = new List<HeaderEntry>(_xhr.AuthorRequestHeaders.Entries),
            Body = _body,
            Redirect = JsRequest.RedirectFollow,

            // https://xhr.spec.whatwg.org/#dom-xmlhttprequest-withcredentials: the member selects the request's
            // credentials mode, and "same-origin" is what an XMLHttpRequest starts with.
            Credentials = _xhr.WithCredentials ? JsRequest.CredentialsInclude : JsRequest.CredentialsSameOrigin,
            Referrer = network.Referrer,
            ReferrerPolicy = network.ReferrerPolicy,
        };

        var observation = FetchObservation.Create(network.Observer, FetchInitiator.Script);
        _observation = observation;

        using var exchange = await FetchTransport
            .SendForStreamAsync(client, request, policy, _cancellation.Token, observation)
            .ConfigureAwait(false);

        var response = exchange.Response;
        var status = (int) response.StatusCode;

        // Read before the headers are collected, and not only for the cap below: a response built in memory
        // computes its length on first access and only then carries the header.
        var declaredLength = response.Content.Headers.ContentLength;

        var headers = new HeaderList();
        Collect(headers, response.Headers);
        Collect(headers, response.Content.Headers);

        // The same rule main fetch applies: a HEAD carries the headers a GET would have had and none of the
        // bytes they describe, and a null body status transfers nothing either.
        var hasBody = !FetchValues.IsNullBodyStatus(status)
            && !string.Equals(exchange.Method, "HEAD", StringComparison.Ordinal);

        if (hasBody && declaredLength is { } declared && declared > policy.MaxResponseBytes)
        {
            throw TooLarge(policy);
        }

        var head = new XhrResponseData
        {
            Status = status,
            StatusText = response.ReasonPhrase ?? string.Empty,
            Headers = headers,
            Url = exchange.Url.Serialize(excludeFragment: true),
            DeclaredLength = hasBody ? declaredLength ?? 0 : 0,
        };

        // The final response, reported here rather than by the redirect loop, which only reports the hops it
        // walks past — the debt FetchObservation.FinalResponse names, paid with the headers already collected
        // above rather than by reading them off the response a second time.
        Observe(observation, head, exchange);

        if (!hasBody)
        {
            observation?.Completed(0);
        }

        if (reportProgress)
        {
            // The upload is over by the time a response has a status line, whatever the content reported.
            Post(() =>
            {
                _xhr.UploadedLength = uploadLength;
                _xhr.ReceiveResponseHead(head.Status, head.StatusText, head.Headers, head.Url, head.DeclaredLength);
            });
        }

        if (!hasBody)
        {
            return head;
        }

        System.IO.Stream body;
        try
        {
            body = await response.Content.ReadAsStreamAsync(_cancellation.Token).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException and not FetchFailureException)
        {
            throw new FetchFailureException(FetchFailureKind.Network, $"Reading the response body of '{head.Url}' failed: {ex.Message}", ex);
        }

        var buffer = new byte[ChunkSize];
        var collected = reportProgress ? null : new System.IO.MemoryStream();
        long total = 0;

        while (true)
        {
            var read = await body.ReadAsync(buffer.AsMemory(), _cancellation.Token).ConfigureAwait(false);
            if (read == 0)
            {
                break;
            }

            total += read;
            if (total > policy.MaxResponseBytes)
            {
                throw TooLarge(policy);
            }

            // A copy per chunk, because the buffer is about to be reused and the engine thread has not read
            // it yet — for the synchronous path the copy goes straight into the collector instead.
            if (collected is not null)
            {
                collected.Write(buffer, 0, read);
                continue;
            }

            var chunk = buffer.AsSpan(0, read).ToArray();
            Post(() => _xhr.ReceiveBodyChunk(chunk));
        }

        observation?.Completed(total);

        return collected is null
            ? head
            : head with { Body = collected.ToArray() };
    }

    /// <summary>
    /// Hands the final response to the observer, with the headers this request already collected.
    /// </summary>
    private static void Observe(FetchObservation? observation, XhrResponseData head, FetchExchange exchange)
    {
        if (observation is null)
        {
            return;
        }

        var entries = head.Headers.Entries;
        var reported = new FetchHeader[entries.Count];
        for (var i = 0; i < entries.Count; i++)
        {
            reported[i] = new FetchHeader(entries[i].LowerName, entries[i].Value);
        }

        observation.FinalResponse(exchange, reported);
    }

    private static FetchFailureException TooLarge(FetchPolicy policy)
        => new(FetchFailureKind.ResponseTooLarge, $"The response body exceeded the {policy.MaxResponseBytes} byte limit set by Options.WebApi.Fetch.MaxResponseBytes.");

    /// <summary>
    /// Every value of every header becomes its own entry, so a response carrying several <c>Set-Cookie</c>
    /// headers is not silently folded into one.
    /// </summary>
    private static void Collect(HeaderList headers, System.Net.Http.Headers.HttpHeaders source)
    {
        foreach (var header in source.NonValidated)
        {
            foreach (var value in header.Value)
            {
                headers.Append(header.Key, value);
            }
        }
    }

    /// <summary>
    /// Off the engine thread: classify the outcome and queue the engine-thread half behind whatever chunk
    /// jobs are already in the queue.
    /// </summary>
    private void CompleteAsynchronously(Task<XhrResponseData> task)
    {
        var outcome = task.IsCompletedSuccessfully
            ? XhrOutcome.Success
            : Classify(Unwrap(task.Exception));

        if (outcome == XhrOutcome.Abandoned)
        {
            // Nothing is settled, but the bookkeeping still has to happen on the engine thread.
            Post(Release, evenIfSuperseded: true);
            return;
        }

        Post(() =>
        {
            Release();

            if (outcome == XhrOutcome.Success)
            {
                _xhr.HandleResponseEndOfBody();
                return;
            }

            Settle(outcome);
        });
    }

    /// <summary>
    /// The transport failure itself rather than the <see cref="AggregateException"/> wrapper, which says
    /// nothing a classification can use.
    /// </summary>
    private static Exception? Unwrap(AggregateException? exception)
        => exception is { InnerExceptions.Count: 1 } ? exception.InnerExceptions[0] : exception;

    /// <summary>
    /// https://xhr.spec.whatwg.org/#handle-errors, in the order it names: timed out, then aborted, then a
    /// network error. The engine's own cancellation comes first of all and settles nothing.
    /// </summary>
    private XhrOutcome Classify(Exception? exception)
    {
        if (exception is OperationCanceledException || _cancellation.IsCancellationRequested)
        {
            if (_engineToken.IsCancellationRequested)
            {
                return XhrOutcome.Abandoned;
            }

            if (_abortSource.IsCancellationRequested)
            {
                return XhrOutcome.Aborted;
            }

            if (_scriptDeadline.IsCancellationRequested || _hostDeadline.IsCancellationRequested)
            {
                return XhrOutcome.Timeout;
            }

            // Neither source fired, so the engine abandoned the request at a global-snapshot restore — and
            // the restore's own generation fence discards the job below.
            return XhrOutcome.Abandoned;
        }

        return XhrOutcome.NetworkError;
    }

    /// <summary>
    /// The engine-thread half of a failure: the request error steps for whichever event the outcome names.
    /// </summary>
    private void Settle(XhrOutcome outcome)
    {
        if (!ReferenceEquals(_xhr.CurrentOperation, this))
        {
            // Superseded by an abort(), an open() or a restore, each of which has already done whatever it
            // owed the object.
            return;
        }

        switch (outcome)
        {
            case XhrOutcome.Timeout:
                _xhr.RunRequestErrorSteps(
                    JsXmlHttpRequestEventTarget.TimeoutEventType,
                    DomExceptionNames.Timeout,
                    "The request timed out.");
                break;

            case XhrOutcome.NetworkError:
                _xhr.RunRequestErrorSteps(
                    JsXmlHttpRequestEventTarget.ErrorEventType,
                    DomExceptionNames.Network,
                    "The request failed.");
                break;

            default:
                // Aborted and Abandoned both settle nothing: abort() already ran the steps on its own stack,
                // and an abandoned request has nobody left to tell.
                break;
        }
    }

    /// <summary>
    /// Queues one engine-thread job carrying the generation this request was registered in, and entering the
    /// realm it started in.
    /// </summary>
    private void Post(Action action, bool evenIfSuperseded = false)
    {
        _engine.AddToEventLoop(() => RunOnEngineThread(action, evenIfSuperseded), _registration);
    }

    /// <summary>
    /// On the engine thread, in the realm the request started in — and only while this operation is
    /// still the one its object names, so an <c>abort()</c> or a second <c>open()</c> silences whatever
    /// the transport had already queued.
    /// </summary>
    private void RunOnEngineThread(Action action, bool evenIfSuperseded)
    {
        if (!evenIfSuperseded && !ReferenceEquals(_xhr.CurrentOperation, this))
        {
            return;
        }

        var entered = false;
        if (!ReferenceEquals(_engine.Realm, _realm))
        {
            _engine.EnterExecutionContext(_realm.GlobalEnv, _realm.GlobalEnv, _realm, privateEnvironment: null, strict: _engine.Options.Strict);
            entered = true;
        }

        try
        {
            action();
        }
        finally
        {
            if (entered)
            {
                _engine.LeaveExecutionContext();
            }
        }
    }

    private void Release() => Dispose();

    /// <summary>
    /// Leaves the in-flight set and frees the four cancellation sources, exactly once. Every call site is
    /// on the engine thread; the interlock is there because a cancelled request can reach it from both the
    /// settle job and <c>abort()</c>.
    /// </summary>
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _released, 1) != 0)
        {
            return;
        }

        _state.UnregisterXhr(this);
        _cancellation.Dispose();
        _abortSource.Dispose();
        _hostDeadline.Dispose();
        _scriptDeadline.Dispose();
    }

    /// <summary>
    /// <c>abort()</c> and <c>open()</c>: cancel the transport so the socket goes at once. Whatever the
    /// transport does next finds itself superseded, because the object no longer names this operation.
    /// </summary>
    internal void Abandon()
    {
        try
        {
            _abortSource.Cancel();
        }
        catch (ObjectDisposedException)
        {
            // Raced with a settle that already released it; there is nothing left to cancel.
        }

        Release();
    }

    /// <summary>
    /// A <c>RestoreGlobalSnapshot</c>: the same cancellation, plus the object's own half of it, because there
    /// will be no job to run it.
    /// </summary>
    internal void AbandonForRestore()
    {
        Abandon();
        _xhr.AbandonForRestore();
    }

    /// <summary>
    /// One <c>send()</c>'s response as plain CLR data, classified off the engine thread.
    /// </summary>
    /// <remarks>
    /// <see cref="Body"/> is filled only for a synchronous request, where nothing was reported chunk by
    /// chunk; the asynchronous path hands its bytes over as it reads them and leaves this null.
    /// </remarks>
    private sealed record XhrResponseData
    {
        internal required int Status { get; init; }

        internal required string StatusText { get; init; }

        internal required HeaderList Headers { get; init; }

        internal required string Url { get; init; }

        /// <summary>What <c>Content-Length</c> declared, or 0 for a response with no body to read.</summary>
        internal required long DeclaredLength { get; init; }

        internal byte[]? Body { get; init; }
    }

}
#endif
