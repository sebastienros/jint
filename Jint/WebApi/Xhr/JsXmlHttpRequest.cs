#if NET8_0_OR_GREATER
using Jint.Native;
using Jint.Native.ArrayBuffer;
using Jint.Native.Json;
using Jint.Runtime;
using Jint.WebApi.DomException;
using Jint.WebApi.Encoding;
using Jint.WebApi.Fetch;
using Jint.WebApi.Files;
using Jint.WebApi.Url.Parsing;

namespace Jint.WebApi.Xhr;

/// <summary>
/// An <c>XMLHttpRequest</c> instance: the whole of the object's state machine, and an
/// <see cref="JsXmlHttpRequestEventTarget"/> so that a script can listen for the eight events it fires.
/// <para>
/// https://xhr.spec.whatwg.org/#interface-xmlhttprequest
/// </para>
/// </summary>
/// <remarks>
/// <para>
/// <b>Everything here runs on the engine's thread.</b> The readyState transitions, the event dispatches and
/// the response decoding all happen inside the pump for an asynchronous request, and inside the
/// <c>send()</c> call itself for a synchronous one; the transport's own thread only ever hands over plain CLR
/// data through a generation-stamped job. That is what makes the state readable from script without a lock.
/// </para>
/// <para>
/// <b>The deadlines are CLR-side, the events are not.</b> Both <c>timeout</c> and
/// <c>Options.WebApi.Fetch.Timeout</c> cancel the transport from a timer thread, so an abandoned request lets
/// go of its socket even in an engine nobody pumps; the <c>timeout</c> event that follows still waits for a
/// pump, because firing it means running script.
/// </para>
/// <para>
/// <b>The forbidden request-header list is deliberately not enforced</b>, which is the choice
/// <see cref="HeadersGuard"/> already documents for <c>fetch</c>: those names protect headers a
/// <i>browser</i> alone controls, and server-side they are exactly what a script legitimately sets. A
/// <c>setRequestHeader</c> that a browser would silently drop is honoured here, and the two interfaces agree.
/// </para>
/// </remarks>
internal sealed class JsXmlHttpRequest : JsXmlHttpRequestEventTarget
{
    /// <summary>https://xhr.spec.whatwg.org/#dom-xmlhttprequest-unsent.</summary>
    internal const int Unsent = 0;

    /// <summary>https://xhr.spec.whatwg.org/#dom-xmlhttprequest-opened.</summary>
    internal const int Opened = 1;

    /// <summary>https://xhr.spec.whatwg.org/#dom-xmlhttprequest-headers_received.</summary>
    internal const int HeadersReceived = 2;

    /// <summary>https://xhr.spec.whatwg.org/#dom-xmlhttprequest-loading.</summary>
    internal const int Loading = 3;

    /// <summary>https://xhr.spec.whatwg.org/#dom-xmlhttprequest-done.</summary>
    internal const int Done = 4;

    /// <summary>https://xhr.spec.whatwg.org/#event-xhr-readystatechange.</summary>
    internal const string ReadyStateChangeEventType = "readystatechange";

    internal const string ResponseTypeText = "";
    internal const string ResponseTypeArrayBuffer = "arraybuffer";
    internal const string ResponseTypeBlob = "blob";
    internal const string ResponseTypeDocument = "document";
    internal const string ResponseTypeJson = "json";
    internal const string ResponseTypeTextExplicit = "text";

    private static readonly JsString _readyStateChangeEventName = new(ReadyStateChangeEventType);
    private static readonly JsString _loadStartEventName = new(LoadStartEventType);
    private static readonly JsString _progressEventName = new(ProgressEventType);
    private static readonly JsString _loadEventName = new(LoadEventType);
    private static readonly JsString _loadEndEventName = new(LoadEndEventType);

    /// <summary>
    /// How often the response's <c>progress</c> event may fire while the body streams —
    /// https://xhr.spec.whatwg.org/#handle-response-body, "if not roughly 50ms have passed".
    /// </summary>
    private static readonly TimeSpan _progressInterval = TimeSpan.FromMilliseconds(50);

    private readonly WebApiEngineState _state;

    /// <summary>
    /// https://xhr.spec.whatwg.org/#received-bytes, owned by the engine thread alone. A growable byte list
    /// rather than a <c>MemoryStream</c>: nothing here needs a stream, and an <c>ObjectInstance</c> cannot
    /// be <see cref="IDisposable"/>.
    /// </summary>
    private readonly List<byte> _receivedBytes = new();

    private long _lastProgressTimestamp;

    /// <summary>
    /// Whether a chunk has been announced yet, which is what makes the <i>first</i> one announce however
    /// quickly it arrived: https://xhr.spec.whatwg.org/#handle-response-body step 2 skips a report when
    /// 50 ms have not passed "since the last invocation of this step", and on the first chunk there has
    /// been no last invocation. Without it a response small enough to arrive in one read would go from
    /// <c>HEADERS_RECEIVED</c> straight to <c>DONE</c> and never enter <c>LOADING</c> at all.
    /// </summary>
    private bool _announcedAChunk;

    /// <summary>
    /// The cached <c>response</c> object, and whether building it failed. Cleared by <c>open()</c>, which is
    /// what makes a reused object answer about its new response rather than its old one.
    /// </summary>
    private JsValue? _responseObject;

    private bool _responseObjectFailed;

    internal JsXmlHttpRequest(Engine engine, Realm realm, WebApiEngineState state) : base(engine, realm)
    {
        _state = state;

        // Eagerly, because whether it carries a listener is what the upload listener flag reads at send()
        // time; a lazily created one would have to exist by then anyway.
        Upload = new JsXmlHttpRequestUpload(engine, realm) { _prototype = realm.Intrinsics.XmlHttpRequestUpload.PrototypeObject };
    }

    /// <summary>https://xhr.spec.whatwg.org/#dom-xmlhttprequest-upload.</summary>
    internal JsXmlHttpRequestUpload Upload { get; }

    /// <summary>https://xhr.spec.whatwg.org/#dom-xmlhttprequest-readystate.</summary>
    internal int ReadyState { get; private set; } = Unsent;

    /// <summary>https://xhr.spec.whatwg.org/#send()-flag.</summary>
    internal bool SendFlag { get; private set; }

    /// <summary>https://xhr.spec.whatwg.org/#synchronous-flag.</summary>
    internal bool SynchronousFlag { get; private set; }

    /// <summary>https://xhr.spec.whatwg.org/#upload-complete-flag.</summary>
    private bool UploadCompleteFlag { get; set; } = true;

    /// <summary>https://xhr.spec.whatwg.org/#upload-listener-flag.</summary>
    private bool UploadListenerFlag { get; set; }

    /// <summary>https://xhr.spec.whatwg.org/#dom-xmlhttprequest-timeout, in milliseconds; 0 means none.</summary>
    internal uint TimeoutMilliseconds { get; private set; }

    /// <summary>https://xhr.spec.whatwg.org/#dom-xmlhttprequest-withcredentials.</summary>
    internal bool WithCredentials { get; private set; }

    /// <summary>https://xhr.spec.whatwg.org/#dom-xmlhttprequest-responsetype.</summary>
    internal string ResponseType { get; private set; } = ResponseTypeText;

    /// <summary>https://xhr.spec.whatwg.org/#override-mime-type, or <see langword="null"/> for none.</summary>
    private MimeType? OverrideMimeType { get; set; }

    /// <summary>https://xhr.spec.whatwg.org/#author-request-headers.</summary>
    internal HeaderList AuthorRequestHeaders { get; private set; } = new();

    /// <summary>The request's method, already normalized.</summary>
    private string RequestMethod { get; set; } = "GET";

    /// <summary>The request's URL, always absolute by the time it is stored.</summary>
    private UrlRecord? RequestUrl { get; set; }

    /// <summary>
    /// Whether the request's method is one <c>send()</c> drops a body for —
    /// https://xhr.spec.whatwg.org/#dom-xmlhttprequest-send step 3, "If this's request method is `GET` or
    /// `HEAD`, then set body to null".
    /// </summary>
    internal bool RequestMethodIsBodiless => RequestMethod is "GET" or "HEAD";

    /// <summary>The operation currently in flight, or <see langword="null"/>.</summary>
    internal XhrOperation? CurrentOperation { get; private set; }

    // ---------------------------------------------------------------- the response

    /// <summary>Whether this's response is a network error — https://xhr.spec.whatwg.org/#response.</summary>
    private bool NetworkErrorResponse { get; set; } = true;

    internal int ResponseStatus { get; private set; }

    internal string ResponseStatusText { get; private set; } = string.Empty;

    internal HeaderList ResponseHeaders { get; private set; } = new();

    /// <summary>https://xhr.spec.whatwg.org/#dom-xmlhttprequest-responseurl, without its fragment.</summary>
    internal string ResponseUrl { get; private set; } = string.Empty;

    /// <summary>Whether the response declared a length, and what it was — for the progress events.</summary>
    private double ResponseBodyLength { get; set; }

    // ---------------------------------------------------------------- open()

    /// <summary>
    /// https://xhr.spec.whatwg.org/#dom-xmlhttprequest-open — both arities, since the three-argument form is
    /// the four- and five-argument form with its credentials defaulted.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>A relative URL needs <c>Options.WebApi.Fetch.BaseUrl</c>.</b> The specification parses against "the
    /// entry settings object's API base URL", which is a document's URL; an engine has one only when the host
    /// gave it one, and without it a relative URL is the specification's parse failure — a <c>SyntaxError</c>.
    /// </para>
    /// <para>
    /// <b>The <c>username</c> and <c>password</c> arguments are read and dropped.</b> They set the request
    /// URL's username and password, which the standard then feeds to HTTP authentication; there is no
    /// credential store here and the URL is never re-serialized with them, so honouring them would mean
    /// putting a password on the wire in a header the caller did not ask for. A script that wants
    /// authentication sets <c>Authorization</c> with <c>setRequestHeader</c>.
    /// </para>
    /// </remarks>
    internal void Open(string method, UrlRecord url, bool async)
    {
        // Step 11: "terminate this's fetch controller" — a second open() abandons whatever was in flight,
        // silently, because the object is being reused rather than failed.
        AbandonOperation();

        SendFlag = false;
        UploadListenerFlag = false;
        RequestMethod = method;
        RequestUrl = url;
        SynchronousFlag = !async;
        AuthorRequestHeaders = new HeaderList();
        ClearResponse();

        // Step 11.9: "If this's state is not opened, then set this's state to opened and fire an event named
        // readystatechange" — so open() on an already-opened object fires nothing.
        if (ReadyState != Opened)
        {
            ReadyState = Opened;
            FireReadyStateChange();
        }
    }

    /// <summary>
    /// https://xhr.spec.whatwg.org/#response — "set this's response to a network error", plus the received
    /// bytes and the cached response object the standard resets beside it.
    /// </summary>
    private void ClearResponse()
    {
        NetworkErrorResponse = true;
        ResponseStatus = 0;
        ResponseStatusText = string.Empty;
        ResponseHeaders = new HeaderList();
        ResponseUrl = string.Empty;
        ResponseBodyLength = 0;
        _receivedBytes.Clear();
        _announcedAChunk = false;
        _responseObject = null;
        _responseObjectFailed = false;
    }

    // ---------------------------------------------------------------- setRequestHeader()

    /// <summary>
    /// https://xhr.spec.whatwg.org/#dom-xmlhttprequest-setrequestheader step 6: "combine (name, value) in
    /// this's author request headers", which appends to an existing value with <c>", "</c> between.
    /// </summary>
    internal void CombineRequestHeader(string name, string value) => AuthorRequestHeaders.Combine(name, value);

    // ---------------------------------------------------------------- send()

    /// <summary>
    /// https://xhr.spec.whatwg.org/#dom-xmlhttprequest-send, from step 6 — the earlier steps, which read the
    /// body and settle the <c>Content-Type</c>, are the prototype's because they convert JavaScript values.
    /// </summary>
    internal void Send(ReadOnlyMemory<byte>? body)
    {
        // Step 6: "If this's upload object has any registered event listener, then set this's upload
        // listener flag." Every one of the eight types counts, which is what makes an upload object with a
        // single `loadend` listener enough to turn upload reporting on.
        UploadListenerFlag = Upload.HasAnyListener;

        // Step 9: "If req's body is null, then set this's upload complete flag."
        UploadCompleteFlag = body is null;
        SendFlag = true;

        var operation = new XhrOperation(this, _engine, _realm, _state, RequestMethod, RequestUrl!, body);
        CurrentOperation = operation;

        if (SynchronousFlag)
        {
            operation.RunSynchronously();
            return;
        }

        // Step 10.1/10.2: the two loadstart events, before anything is sent.
        FireProgressEvent(_loadStartEventName, 0, 0);
        if (!UploadCompleteFlag && UploadListenerFlag)
        {
            Upload.FireProgressEvent(_loadStartEventName, 0, body!.Value.Length);
        }

        // Step 10.3: "If this's state is not opened or this's send() flag is unset, then return" — a
        // loadstart listener may have called abort() or open().
        if (ReadyState != Opened || !SendFlag || !ReferenceEquals(CurrentOperation, operation))
        {
            return;
        }

        operation.Start();
    }

    // ---------------------------------------------------------------- abort()

    /// <summary>
    /// https://xhr.spec.whatwg.org/#dom-xmlhttprequest-abort — terminate, then the request error steps,
    /// synchronously in the caller's own stack rather than from a queued task.
    /// </summary>
    internal void Abort()
    {
        AbandonOperation();

        if ((ReadyState == Opened && SendFlag) || ReadyState is HeadersReceived or Loading)
        {
            RunRequestErrorSteps(AbortEventType, DomExceptionNames.Abort, "The operation was aborted.");
        }

        // Step 3: "If this's state is done, then set this's state to unsent and this's response to a network
        // error." No event is fired, and the transition is deliberately invisible to readystatechange.
        if (ReadyState == Done)
        {
            ReadyState = Unsent;
            ClearResponse();
        }
    }

    /// <summary>
    /// Terminates whatever is in flight without settling it. The completion job the transport may already
    /// have queued finds itself superseded and does nothing.
    /// </summary>
    private void AbandonOperation()
    {
        var operation = CurrentOperation;
        if (operation is null)
        {
            return;
        }

        CurrentOperation = null;
        operation.Abandon();
    }

    /// <summary>
    /// The fence a <c>RestoreGlobalSnapshot</c> puts up, reached from the engine state. The socket is let go
    /// and the object is left <c>DONE</c> with no event at all — the evaluation cycle those listeners
    /// belonged to has ended, so there is nobody left to tell.
    /// </summary>
    internal void AbandonForRestore()
    {
        CurrentOperation = null;
        SendFlag = false;
        ReadyState = Done;
    }

    // ---------------------------------------------------------------- the transport's callbacks

    /// <summary>
    /// https://xhr.spec.whatwg.org/#request-error-steps, the shared tail of <c>abort</c>, <c>timeout</c> and
    /// <c>error</c>.
    /// </summary>
    /// <remarks>
    /// For a synchronous request the steps stop at step 4 and throw, so a failed synchronous request fires no
    /// event whatever — which is exactly what makes <c>send()</c> answer with an exception rather than with a
    /// state a caller would have to inspect.
    /// </remarks>
    internal void RunRequestErrorSteps(string eventType, string exceptionName, string message)
    {
        ReadyState = Done;
        SendFlag = false;
        ClearResponse();

        if (SynchronousFlag)
        {
            ThrowDomException(exceptionName, message);
        }

        FireReadyStateChange();

        if (!UploadCompleteFlag)
        {
            UploadCompleteFlag = true;

            if (UploadListenerFlag)
            {
                Upload.FireProgressEvent(JsString.Create(eventType), 0, 0);
                Upload.FireProgressEvent(_loadEndEventName, 0, 0);
            }
        }

        FireProgressEvent(JsString.Create(eventType), 0, 0);
        FireProgressEvent(_loadEndEventName, 0, 0);
    }

    /// <summary>
    /// https://xhr.spec.whatwg.org/#dom-xmlhttprequest-send step 10.4, <i>processRequestEndOfBody</i>: the
    /// three events an upload object gets once the request body has gone.
    /// </summary>
    /// <remarks>
    /// <b>This is the only upload progress there is</b>, and <i>processRequestBodyChunkLength</i> — the
    /// per-chunk <c>progress</c> beside it — is deliberately not implemented. The body is handed to the
    /// transport whole, so there are no chunk lengths to report; a browser sending a body small enough
    /// to go out in one write reports exactly this much too, which is what
    /// <c>xhr/event-timeout-order.any.js</c> pins by asserting the event order of a twelve-byte upload.
    /// </remarks>
    internal void ReportUploadComplete(double length)
    {
        if (UploadCompleteFlag)
        {
            return;
        }

        UploadCompleteFlag = true;

        if (!UploadListenerFlag)
        {
            return;
        }

        Upload.FireProgressEvent(_progressEventName, length, length);
        Upload.FireProgressEvent(_loadEventName, length, length);
        Upload.FireProgressEvent(_loadEndEventName, length, length);
    }

    /// <summary>
    /// https://xhr.spec.whatwg.org/#dom-xmlhttprequest-send, <i>processResponse</i>: the response's head
    /// is in, so the object leaves <c>OPENED</c>.
    /// </summary>
    /// <remarks>
    /// <b>A synchronous request never reaches <c>HEADERS_RECEIVED</c>.</b> Step 11 fetches with
    /// <i>processResponseConsumeBody</i> alone — there is no <i>processResponse</i> to run, so the state
    /// goes from <c>OPENED</c> straight to <c>DONE</c> and the intermediate
    /// <c>readystatechange</c> never fires. The response itself is still recorded here, because
    /// <c>status</c> and the header list have to be readable by the time <c>send()</c> returns.
    /// </remarks>
    internal void ReceiveResponseHead(int status, string statusText, HeaderList headers, string url, double bodyLength)
    {
        NetworkErrorResponse = false;
        ResponseStatus = status;
        ResponseStatusText = statusText;
        ResponseHeaders = headers;
        ResponseUrl = url;
        ResponseBodyLength = bodyLength;

        if (SynchronousFlag)
        {
            return;
        }

        // "Set this's state to headers received" — and the upload is over by definition, since the
        // response cannot start before the request finished being sent.
        ReportUploadComplete(UploadedLength);

        if (ReadyState == Opened)
        {
            ReadyState = HeadersReceived;
            FireReadyStateChange();
        }

        _lastProgressTimestamp = System.Diagnostics.Stopwatch.GetTimestamp();
    }

    /// <summary>How many bytes the request body had, for the upload events that report a total.</summary>
    internal double UploadedLength { get; set; }

    /// <summary>
    /// https://xhr.spec.whatwg.org/#handle-response-body, <i>processBodyChunk</i>: append the bytes, and —
    /// no more often than every 50 ms — announce them.
    /// </summary>
    internal void ReceiveBodyChunk(byte[] chunk)
    {
        _receivedBytes.AddRange(chunk);

        if (SynchronousFlag)
        {
            // The synchronous flag has no processBodyChunk at all: step 11 consumes the whole body and
            // hands it over once, so there is nothing to announce and no LOADING state to enter.
            return;
        }

        // Step 2: "If not roughly 50ms have passed since the last invocation of this step, return."
        if (_announcedAChunk && System.Diagnostics.Stopwatch.GetElapsedTime(_lastProgressTimestamp) < _progressInterval)
        {
            return;
        }

        _announcedAChunk = true;
        _lastProgressTimestamp = System.Diagnostics.Stopwatch.GetTimestamp();
        AnnounceLoading();
    }

    /// <summary>
    /// Steps 3 to 5 of <i>processBodyChunk</i>: enter <c>LOADING</c>, fire <c>readystatechange</c> — which
    /// fires more often than the state changes, deliberately and for web compatibility — and report progress.
    /// </summary>
    private void AnnounceLoading()
    {
        if (ReadyState == HeadersReceived)
        {
            ReadyState = Loading;
        }

        FireReadyStateChange();
        FireProgressEvent(_progressEventName, _receivedBytes.Count, ResponseBodyLength);
    }

    /// <summary>
    /// https://xhr.spec.whatwg.org/#handle-response-end-of-body — the successful end of a request, for both
    /// the asynchronous and the synchronous flag.
    /// </summary>
    internal void HandleResponseEndOfBody()
    {
        if (NetworkErrorResponse)
        {
            return;
        }

        var transmitted = (double) _receivedBytes.Count;

        if (!SynchronousFlag)
        {
            FireProgressEvent(_progressEventName, transmitted, ResponseBodyLength);
        }

        ReadyState = Done;
        SendFlag = false;
        FireReadyStateChange();
        FireProgressEvent(_loadEventName, transmitted, ResponseBodyLength);
        FireProgressEvent(_loadEndEventName, transmitted, ResponseBodyLength);
    }

    /// <summary>
    /// https://xhr.spec.whatwg.org/#concept-event-fire for the one event that is not a
    /// <c>ProgressEvent</c> — <c>readystatechange</c> is a plain <c>Event</c>.
    /// </summary>
    private void FireReadyStateChange() => FireEvent(_readyStateChangeEventName);

    // ---------------------------------------------------------------- the response attributes

    /// <summary>
    /// https://xhr.spec.whatwg.org/#final-mime-type — the override MIME type when the script set one, and the
    /// response's own <c>Content-Type</c> otherwise.
    /// </summary>
    private MimeType? FinalMimeType()
    {
        if (OverrideMimeType is { } overridden)
        {
            return overridden;
        }

        var contentType = ResponseHeaders.Get("content-type");
        return contentType is null ? null : MimeType.Parse(contentType);
    }

    /// <summary>
    /// https://xhr.spec.whatwg.org/#dom-xmlhttprequest-overridemimetype — the parsed value, or
    /// <c>application/octet-stream</c> when the string is not a MIME type at all.
    /// </summary>
    internal void SetOverrideMimeType(string mime)
    {
        OverrideMimeType = MimeType.Parse(mime) ?? MimeType.Parse("application/octet-stream");
    }

    /// <summary>
    /// The label the response is decoded with — https://xhr.spec.whatwg.org/#final-charset. UTF-8 is the
    /// fallback the standard names, and it is also what an unrecognized label falls back to.
    /// </summary>
    private string FinalCharset() => FinalMimeType()?.GetParameter("charset") ?? "UTF-8";

    /// <summary>
    /// https://xhr.spec.whatwg.org/#text-response — the received bytes decoded with the final charset.
    /// </summary>
    /// <remarks>
    /// The XML encoding-sniffing arm of step 3 is not reached, because it applies only where a document could
    /// be produced and this engine has no XML parser. A <c>charset</c> the encoding registry does not know
    /// falls back to UTF-8, which is what the standard's <i>get an encoding</i> failure does.
    /// </remarks>
    internal JsString TextResponse()
    {
        if (_receivedBytes.Count == 0)
        {
            return JsString.Empty;
        }

        var decoder = EncodingLabels.TryLookup(FinalCharset(), out var entry)
            ? new TextDecoderCommon(in entry, fatal: false, ignoreBom: false)
            : TextDecoderCommon.Utf8();

        return decoder.Decode(_realm, System.Runtime.InteropServices.CollectionsMarshal.AsSpan(_receivedBytes), stream: false);
    }

    /// <summary>
    /// https://xhr.spec.whatwg.org/#dom-xmlhttprequest-response, steps 3 to 6 — the cached response object,
    /// built once per response and per <c>responseType</c>.
    /// </summary>
    internal JsValue ResponseObject()
    {
        if (_responseObjectFailed)
        {
            return Null;
        }

        if (_responseObject is { } cached)
        {
            return cached;
        }

        var built = BuildResponseObject();
        if (built is null)
        {
            _responseObjectFailed = true;
            return Null;
        }

        _responseObject = built;
        return built;
    }

    private JsValue? BuildResponseObject()
    {
        var bytes = System.Runtime.InteropServices.CollectionsMarshal.AsSpan(_receivedBytes);

        switch (ResponseType)
        {
            case ResponseTypeArrayBuffer:
                // "Set this's response object to a new ArrayBuffer ... If this throws an exception, then set
                // this's response object to failure and return null" — an allocation this large failing is
                // the only way that happens.
                return new JsArrayBuffer(_engine, bytes.ToArray())
                {
                    _prototype = _realm.Intrinsics.ArrayBuffer.PrototypeObject,
                };

            case ResponseTypeBlob:
                return new JsBlob(_engine, bytes.ToArray(), FinalMimeType()?.Serialize() ?? string.Empty)
                {
                    _prototype = _realm.Intrinsics.Blob.PrototypeObject,
                };

            case ResponseTypeDocument:
                return DocumentResponse();

            case ResponseTypeJson:
                return JsonResponse(bytes);

            default:
                return null;
        }
    }

    /// <summary>
    /// https://xhr.spec.whatwg.org/#json-response — the received bytes parsed as JSON, or the specification's
    /// failure when they are not JSON.
    /// </summary>
    private JsValue? JsonResponse(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length == 0)
        {
            return null;
        }

        try
        {
            // The byte overload transcodes and strips one leading BOM, which is the UTF-8 decode the standard
            // runs before parsing.
            return new JsonParser(_engine).Parse(bytes);
        }
        catch (JavaScriptException)
        {
            // "If that threw an exception, then return null" — the standard's own answer, and the reason a
            // malformed JSON body is `null` rather than a throw out of the getter.
            return null;
        }
    }

    /// <summary>
    /// https://xhr.spec.whatwg.org/#document-response, delegated whole to
    /// <c>Options.WebApi.Xhr.DocumentParser</c>: Jint parses no markup, so a host that wants a document
    /// supplies one and an engine without one answers <c>null</c>.
    /// </summary>
    /// <summary>
    /// https://xhr.spec.whatwg.org/#dom-xmlhttprequest-responsexml steps 4 to 6 — the same cached
    /// response object <c>response</c> answers with, built here for the one <c>responseType</c> whose
    /// getter is a second name for it.
    /// </summary>
    internal JsValue DocumentResponseValue()
    {
        if (_responseObjectFailed)
        {
            return Null;
        }

        if (_responseObject is { } cached)
        {
            return cached;
        }

        var document = DocumentResponse();
        if (document is null)
        {
            _responseObjectFailed = true;
            return Null;
        }

        _responseObject = document;
        return document;
    }

    private JsValue? DocumentResponse()
    {
        var parser = _state.XhrDocumentParser;
        if (parser is null)
        {
            return null;
        }

        var essence = FinalMimeType()?.Essence;
        if (essence is null)
        {
            return null;
        }

        var document = parser(_engine, TextResponse().ToString(), essence);
        return document is null || document.IsNull() || document.IsUndefined() ? null : document;
    }

    /// <summary>
    /// The setter half of <c>timeout</c> — https://xhr.spec.whatwg.org/#dom-xmlhttprequest-timeout, whose
    /// step 3 re-runs the timer so that lowering the value mid-flight really does shorten the deadline.
    /// </summary>
    internal void SetTimeout(uint milliseconds)
    {
        TimeoutMilliseconds = milliseconds;

        if (ReadyState == Opened && SendFlag)
        {
            CurrentOperation?.RearmScriptDeadline(milliseconds);
        }
    }

    internal void SetWithCredentials(bool value) => WithCredentials = value;

    internal void SetResponseType(string value) => ResponseType = value;

    /// <summary>
    /// Raises a <c>DOMException</c> from the engine's own algorithms, which is how every failure this
    /// interface reports differs from <c>fetch</c>'s <c>TypeError</c>.
    /// </summary>
    internal void ThrowDomException(string name, string message)
    {
        var exception = _realm.Intrinsics.DomException.CreateException(name, message);
        var location = _engine._lastSyntaxElement?.Location ?? default;
        Throw.JavaScriptException(_engine, exception, in location);
    }
}
#endif
