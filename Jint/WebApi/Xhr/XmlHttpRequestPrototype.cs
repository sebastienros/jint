#if NET8_0_OR_GREATER
using Jint.Native;
using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Descriptors;
using Jint.WebApi.DomException;
using Jint.WebApi.Events;
using Jint.WebApi.Fetch;
using Jint.WebApi.Files;
using Jint.WebApi.Streams;
using Jint.WebApi.Url;
using Jint.WebApi.Url.Parsing;

namespace Jint.WebApi.Xhr;

/// <summary>
/// <c>XMLHttpRequest.prototype</c> — the interface prototype object.
/// <para>
/// https://xhr.spec.whatwg.org/#interface-xmlhttprequest
/// </para>
/// </summary>
/// <remarks>
/// <para>
/// Its <c>[[Prototype]]</c> is <c>XMLHttpRequestEventTarget.prototype</c>, which is where the seven progress
/// handler attributes live; <c>onreadystatechange</c> is this interface's own and is declared here. The five
/// readyState constants appear on the interface object as well, per
/// https://webidl.spec.whatwg.org/#es-constants, and <c>PreserveDeclarationOrder</c> keeps the generator from
/// sorting them by name because that order is observable.
/// </para>
/// <para>
/// <b>Two <c>InvalidAccessError</c> rules of the standard are deliberately absent</b>, and both are the same
/// rule: <c>open()</c> refuses a synchronous request that has a <c>timeout</c> or a <c>responseType</c>, and
/// the two setters refuse the same, only "if the current global object is a <c>Window</c> object". Jint's
/// global is not a <c>Window</c> — <c>Navigator</c> says so, and so does the absence of a document — so the
/// engine is in the position a worker is in, where the standard allows both. Every other
/// <c>InvalidStateError</c> and <c>InvalidAccessError</c> is enforced.
/// </para>
/// </remarks>
[JsObject(UseShape = true, PreserveDeclarationOrder = true)]
internal sealed partial class XmlHttpRequestPrototype : Prototype
{
    [JsProperty(Name = "constructor", Flags = PropertyFlag.NonEnumerable)]
    private readonly XmlHttpRequestConstructor _constructor;

    [JsSymbol("ToStringTag", Flags = PropertyFlag.Configurable)]
    private static readonly JsString XmlHttpRequestToStringTag = new("XMLHttpRequest");

    [JsProperty(Name = "UNSENT", Flags = PropertyFlag.OnlyEnumerable)] private static readonly JsNumber Unsent = JsNumber.Create(JsXmlHttpRequest.Unsent);
    [JsProperty(Name = "OPENED", Flags = PropertyFlag.OnlyEnumerable)] private static readonly JsNumber Opened = JsNumber.Create(JsXmlHttpRequest.Opened);
    [JsProperty(Name = "HEADERS_RECEIVED", Flags = PropertyFlag.OnlyEnumerable)] private static readonly JsNumber HeadersReceived = JsNumber.Create(JsXmlHttpRequest.HeadersReceived);
    [JsProperty(Name = "LOADING", Flags = PropertyFlag.OnlyEnumerable)] private static readonly JsNumber Loading = JsNumber.Create(JsXmlHttpRequest.Loading);
    [JsProperty(Name = "DONE", Flags = PropertyFlag.OnlyEnumerable)] private static readonly JsNumber Done = JsNumber.Create(JsXmlHttpRequest.Done);

    internal XmlHttpRequestPrototype(
        Engine engine,
        Realm realm,
        XmlHttpRequestConstructor constructor,
        ObjectInstance eventTargetPrototype) : base(engine, realm)
    {
        _prototype = eventTargetPrototype;
        _constructor = constructor;
    }

    protected override void Initialize()
    {
        CreateProperties_Generated();
        CreateSymbols_Generated();
    }

    // ---------------------------------------------------------------- the request

    /// <summary>
    /// https://xhr.spec.whatwg.org/#dom-xmlhttprequest-open — one operation with two declared arities, which
    /// WebIDL overload resolution reduces to "<c>async</c> defaults to true when there is no third argument".
    /// </summary>
    /// <remarks>
    /// <c>username</c> and <c>password</c> are converted and dropped; <see cref="JsXmlHttpRequest.Open"/> says
    /// why. The URL is parsed against <c>Options.WebApi.Fetch.BaseUrl</c>, which is what a document's URL is
    /// to a browser and which an engine has only when the host gave it one.
    /// </remarks>
    [JsFunction(Name = "open", Length = 2, Flags = PropertyFlag.ConfigurableEnumerableWritable)]
    private JsValue Open(JsValue thisObject, JsCallArguments arguments)
    {
        var xhr = Brand(thisObject);

        if (arguments.Length < 2)
        {
            Throw.TypeError(_realm, $"Failed to execute 'open' on 'XMLHttpRequest': 2 arguments required, but only {arguments.Length} present.");
        }

        var method = FetchValues.ToByteString(_realm, arguments[0]);
        var href = UrlValues.ToUsvString(arguments[1]);

        // Step 2/3: "If method is not a method, then throw a SyntaxError DOMException. If method is a
        // forbidden method, then throw a SecurityError DOMException." The two are different failures on
        // purpose — one is a grammar error and the other is a refusal.
        if (!FetchValues.IsMethod(method))
        {
            ThrowDomException(DomExceptionNames.Syntax, $"Failed to execute 'open' on 'XMLHttpRequest': '{method}' is not a valid HTTP method.");
        }

        if (FetchValues.IsForbiddenMethod(method))
        {
            ThrowDomException(DomExceptionNames.Security, $"Failed to execute 'open' on 'XMLHttpRequest': '{method}' is a forbidden method.");
        }

        // Step 5: "Let parsedURL be the result of encoding-parsing a URL given url, relative to this's
        // relevant settings object." Step 6: "If parsedURL is failure, then throw a SyntaxError DOMException."
        var url = UrlParser.Parse(href, _engine._webApi?.FetchNetwork.BaseUrl);
        if (url is null)
        {
            ThrowDomException(DomExceptionNames.Syntax, $"Failed to execute 'open' on 'XMLHttpRequest': Invalid URL '{href}'.");
        }

        // "If the async argument is omitted, set async to true" — and only then, so an explicit undefined is
        // converted like any other boolean and means false.
        var async = arguments.Length <= 2 || TypeConverter.ToBoolean(arguments[2]);

        xhr.Open(FetchValues.NormalizeMethod(method), url!, async);
        return Undefined;
    }

    /// <summary>
    /// https://xhr.spec.whatwg.org/#dom-xmlhttprequest-setrequestheader
    /// </summary>
    /// <remarks>
    /// Step 5 — "If (name, value) is a forbidden request-header, then return" — is deliberately not
    /// implemented; <see cref="JsXmlHttpRequest"/> says why, and it is the choice <c>fetch</c> already made.
    /// </remarks>
    [JsFunction(Name = "setRequestHeader", Length = 2, Flags = PropertyFlag.ConfigurableEnumerableWritable)]
    private JsValue SetRequestHeader(JsValue thisObject, JsValue name, JsValue value)
    {
        var xhr = Brand(thisObject);

        if (xhr.ReadyState != JsXmlHttpRequest.Opened)
        {
            ThrowDomException(DomExceptionNames.InvalidState, "Failed to execute 'setRequestHeader' on 'XMLHttpRequest': The object's state must be OPENED.");
        }

        if (xhr.SendFlag)
        {
            ThrowDomException(DomExceptionNames.InvalidState, "Failed to execute 'setRequestHeader' on 'XMLHttpRequest': The request has already been sent.");
        }

        var headerName = FetchValues.ToByteString(_realm, name);
        var headerValue = HeaderList.Normalize(FetchValues.ToByteString(_realm, value));

        if (!HeaderList.IsName(headerName) || !HeaderList.IsValue(headerValue))
        {
            ThrowDomException(DomExceptionNames.Syntax, $"Failed to execute 'setRequestHeader' on 'XMLHttpRequest': '{headerName}' is not a valid HTTP header field name/value pair.");
        }

        xhr.CombineRequestHeader(headerName, headerValue);
        return Undefined;
    }

    /// <summary>
    /// https://xhr.spec.whatwg.org/#dom-xmlhttprequest-send — steps 1 to 5, which read the body and settle
    /// the <c>Content-Type</c>; everything from step 6 is <see cref="JsXmlHttpRequest.Send"/>.
    /// </summary>
    /// <remarks>
    /// The <c>Document</c> arm of the union is absent because there is no <c>Document</c> interface to hand
    /// it; every other <c>XMLHttpRequestBodyInit</c> arm — <c>Blob</c>, a buffer source, <c>FormData</c>,
    /// <c>URLSearchParams</c> and a string — goes through the fetch object model's own extract-a-body, which
    /// is what makes a <c>FormData</c> body multipart here exactly as it is there.
    /// </remarks>
    [JsFunction(Name = "send", Length = 0, Flags = PropertyFlag.ConfigurableEnumerableWritable)]
    private JsValue Send(JsValue thisObject, JsValue body)
    {
        var xhr = Brand(thisObject);

        if (xhr.ReadyState != JsXmlHttpRequest.Opened)
        {
            ThrowDomException(DomExceptionNames.InvalidState, "Failed to execute 'send' on 'XMLHttpRequest': The object's state must be OPENED.");
        }

        if (xhr.SendFlag)
        {
            ThrowDomException(DomExceptionNames.InvalidState, "Failed to execute 'send' on 'XMLHttpRequest': The request has already been sent.");
        }

        // Step 3: "If this's request method is GET or HEAD, then set body to null." A body a script passed is
        // dropped rather than refused, which is the difference from the Request constructor.
        var hasBody = !body.IsNullOrUndefined() && !xhr.RequestMethodIsBodiless;

        ReadOnlyMemory<byte>? bytes = null;
        if (hasBody)
        {
            // https://xhr.spec.whatwg.org/#typedefdef-xmlhttprequestbodyinit has no ReadableStream arm,
            // unlike fetch's BodyInit — so a stream falls through to the USVString arm and is sent as
            // "[object ReadableStream]". Taking it out of the union before extract-a-body sees it is what
            // keeps a stream the script still owns from being disturbed.
            var source = body is JsReadableStream ? JsString.Create(body.ToString()) : body;

            var extracted = FetchBody.Extract(_realm, source);
            bytes = extracted.Bytes;
            ApplyContentType(xhr, source, extracted.Type);
        }

        xhr.Send(bytes);
        return Undefined;
    }

    /// <summary>
    /// https://xhr.spec.whatwg.org/#dom-xmlhttprequest-send steps 5.5 and 5.6: an author
    /// <c>Content-Type</c> keeps its essence but is forced to UTF-8 for a string body, and one the author did
    /// not set is taken from the body.
    /// </summary>
    private static void ApplyContentType(JsXmlHttpRequest xhr, JsValue body, string? extracted)
    {
        var author = xhr.AuthorRequestHeaders.Get("content-type");

        if (author is null)
        {
            if (extracted is not null)
            {
                xhr.AuthorRequestHeaders.Set("content-type", extracted);
            }

            return;
        }

        // "If body is a Document or a USVString" — the Document arm does not exist here, so the standard's
        // own rule is the string arm alone. A <c>URLSearchParams</c> body is included anyway, because
        // every engine does and because such a body really is always UTF-8: xhr/send-usp.any.js says so
        // in a comment beside the row that asserts it. Every other body type keeps the charset the author
        // wrote, since only the author knows what those bytes are.
        if (body is JsBlob or JsFormData || FileApi.TryGetBufferSourceBytes(body, out _))
        {
            return;
        }

        var parsed = MimeType.Parse(author);
        if (parsed is null)
        {
            return;
        }

        var charset = parsed.GetParameter("charset");
        if (charset is null || string.Equals(charset, "UTF-8", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        parsed.SetParameter("charset", "UTF-8");
        xhr.AuthorRequestHeaders.Set("content-type", parsed.Serialize());
    }

    /// <summary>
    /// https://xhr.spec.whatwg.org/#dom-xmlhttprequest-abort
    /// </summary>
    [JsFunction(Name = "abort", Length = 0, Flags = PropertyFlag.ConfigurableEnumerableWritable)]
    private JsValue Abort(JsValue thisObject)
    {
        Brand(thisObject).Abort();
        return Undefined;
    }

    // ---------------------------------------------------------------- the response

    /// <summary>
    /// https://xhr.spec.whatwg.org/#dom-xmlhttprequest-getresponseheader — the combined value, or <c>null</c>.
    /// </summary>
    [JsFunction(Name = "getResponseHeader", Length = 1, Flags = PropertyFlag.ConfigurableEnumerableWritable)]
    private JsValue GetResponseHeader(JsValue thisObject, JsValue name)
    {
        var xhr = Brand(thisObject);
        var headerName = FetchValues.ToByteString(_realm, name);

        if (!HeaderList.IsName(headerName))
        {
            return Null;
        }

        var value = xhr.ResponseHeaders.Get(headerName);
        return value is null ? Null : JsString.Create(value);
    }

    /// <summary>
    /// https://xhr.spec.whatwg.org/#dom-xmlhttprequest-getallresponseheaders — the sorted, combined header
    /// list, one <c>name: value</c> per line, each terminated by CRLF.
    /// </summary>
    /// <remarks>
    /// The list every value comes from is the response's own, so a <c>Set-Cookie</c> contributes one line per
    /// value rather than being combined. The forbidden response-header names a browser filters here are not
    /// filtered, for the reason the request half is not: there is no user agent whose headers they would be.
    /// </remarks>
    [JsFunction(Name = "getAllResponseHeaders", Length = 0, Flags = PropertyFlag.ConfigurableEnumerableWritable)]
    private JsString GetAllResponseHeaders(JsValue thisObject)
    {
        var xhr = Brand(thisObject);
        var combined = xhr.ResponseHeaders.SortAndCombine();
        if (combined.Count == 0)
        {
            return JsString.Empty;
        }

        var builder = new System.Text.StringBuilder();
        foreach (var header in combined)
        {
            builder.Append(header.LowerName).Append(": ").Append(header.Value).Append("\r\n");
        }

        return JsString.Create(builder.ToString());
    }

    /// <summary>
    /// https://xhr.spec.whatwg.org/#dom-xmlhttprequest-overridemimetype
    /// </summary>
    [JsFunction(Name = "overrideMimeType", Length = 1, Flags = PropertyFlag.ConfigurableEnumerableWritable)]
    private JsValue OverrideMimeType(JsValue thisObject, JsValue mime)
    {
        var xhr = Brand(thisObject);

        if (xhr.ReadyState is JsXmlHttpRequest.Loading or JsXmlHttpRequest.Done)
        {
            ThrowDomException(DomExceptionNames.InvalidState, "Failed to execute 'overrideMimeType' on 'XMLHttpRequest': MimeType cannot be overridden when the state is LOADING or DONE.");
        }

        xhr.SetOverrideMimeType(FetchValues.ToByteString(_realm, mime));
        return Undefined;
    }

    // ---------------------------------------------------------------- attributes

    /// <summary>https://xhr.spec.whatwg.org/#handler-xhr-onreadystatechange.</summary>
    [JsAccessor("onreadystatechange", Flags = PropertyFlag.Configurable | PropertyFlag.Enumerable)]
    private JsValue OnReadyStateChangeGet(JsValue thisObject)
        => EventHandlerAttributes.Get(Brand(thisObject), JsXmlHttpRequest.ReadyStateChangeEventType);

    /// <summary>https://xhr.spec.whatwg.org/#handler-xhr-onreadystatechange, setter half.</summary>
    [JsAccessor("onreadystatechange", AccessorKind.Set, Flags = PropertyFlag.Configurable | PropertyFlag.Enumerable)]
    private JsValue OnReadyStateChangeSet(JsValue thisObject, JsValue value)
        => EventHandlerAttributes.Set(Brand(thisObject), JsXmlHttpRequest.ReadyStateChangeEventType, value);

    /// <summary>https://xhr.spec.whatwg.org/#dom-xmlhttprequest-readystate.</summary>
    [JsAccessor("readyState", Flags = PropertyFlag.Configurable | PropertyFlag.Enumerable)]
    private JsNumber ReadyStateGet(JsValue thisObject) => JsNumber.Create(Brand(thisObject).ReadyState);

    /// <summary>https://xhr.spec.whatwg.org/#dom-xmlhttprequest-timeout.</summary>
    [JsAccessor("timeout", Flags = PropertyFlag.Configurable | PropertyFlag.Enumerable)]
    private JsNumber TimeoutGet(JsValue thisObject) => JsNumber.Create(Brand(thisObject).TimeoutMilliseconds);

    /// <summary>
    /// https://xhr.spec.whatwg.org/#dom-xmlhttprequest-timeout, setter half — the deadline in milliseconds,
    /// re-armed while the request is in flight so that lowering it really does shorten it.
    /// </summary>
    [JsAccessor("timeout", AccessorKind.Set, Flags = PropertyFlag.Configurable | PropertyFlag.Enumerable)]
    private JsValue TimeoutSet(JsValue thisObject, JsValue value)
    {
        Brand(thisObject).SetTimeout(ToUnsignedLong(value));
        return Undefined;
    }

    /// <summary>https://xhr.spec.whatwg.org/#dom-xmlhttprequest-withcredentials.</summary>
    [JsAccessor("withCredentials", Flags = PropertyFlag.Configurable | PropertyFlag.Enumerable)]
    private JsBoolean WithCredentialsGet(JsValue thisObject) => JsBoolean.Create(Brand(thisObject).WithCredentials);

    /// <summary>
    /// https://xhr.spec.whatwg.org/#dom-xmlhttprequest-withcredentials, setter half.
    /// </summary>
    /// <remarks>
    /// <c>true</c> sends the request with the <c>include</c> credentials mode, which is what makes the host's
    /// <c>Options.WebApi.Fetch.CookieJar</c> travel to another origin; without a jar there is nothing to send
    /// and the member changes nothing. There is no HTTP authentication store either, so the half of the
    /// member that is about <c>WWW-Authenticate</c> has nothing to act on.
    /// </remarks>
    [JsAccessor("withCredentials", AccessorKind.Set, Flags = PropertyFlag.Configurable | PropertyFlag.Enumerable)]
    private JsValue WithCredentialsSet(JsValue thisObject, JsValue value)
    {
        var xhr = Brand(thisObject);

        if (xhr.ReadyState is not (JsXmlHttpRequest.Unsent or JsXmlHttpRequest.Opened))
        {
            ThrowDomException(DomExceptionNames.InvalidState, "Failed to set the 'withCredentials' property on 'XMLHttpRequest': The value may only be set if the object's state is UNSENT or OPENED.");
        }

        if (xhr.SendFlag)
        {
            ThrowDomException(DomExceptionNames.InvalidState, "Failed to set the 'withCredentials' property on 'XMLHttpRequest': The value may not be set if the object's send() method has already been invoked.");
        }

        xhr.SetWithCredentials(TypeConverter.ToBoolean(value));
        return Undefined;
    }

    /// <summary>https://xhr.spec.whatwg.org/#dom-xmlhttprequest-upload.</summary>
    [JsAccessor("upload", Flags = PropertyFlag.Configurable | PropertyFlag.Enumerable)]
    private JsXmlHttpRequestUpload UploadGet(JsValue thisObject) => Brand(thisObject).Upload;

    /// <summary>https://xhr.spec.whatwg.org/#dom-xmlhttprequest-status.</summary>
    [JsAccessor("status", Flags = PropertyFlag.Configurable | PropertyFlag.Enumerable)]
    private JsNumber StatusGet(JsValue thisObject) => JsNumber.Create(Brand(thisObject).ResponseStatus);

    /// <summary>https://xhr.spec.whatwg.org/#dom-xmlhttprequest-statustext.</summary>
    [JsAccessor("statusText", Flags = PropertyFlag.Configurable | PropertyFlag.Enumerable)]
    private JsString StatusTextGet(JsValue thisObject) => JsString.Create(Brand(thisObject).ResponseStatusText);

    /// <summary>https://xhr.spec.whatwg.org/#dom-xmlhttprequest-responseurl.</summary>
    [JsAccessor("responseURL", Flags = PropertyFlag.Configurable | PropertyFlag.Enumerable)]
    private JsString ResponseUrlGet(JsValue thisObject) => JsString.Create(Brand(thisObject).ResponseUrl);

    /// <summary>https://xhr.spec.whatwg.org/#dom-xmlhttprequest-responsetype.</summary>
    [JsAccessor("responseType", Flags = PropertyFlag.Configurable | PropertyFlag.Enumerable)]
    private JsString ResponseTypeGet(JsValue thisObject) => JsString.Create(Brand(thisObject).ResponseType);

    /// <summary>
    /// https://xhr.spec.whatwg.org/#dom-xmlhttprequest-responsetype, setter half.
    /// </summary>
    /// <remarks>
    /// <b>The enumeration is converted before the setter runs at all</b>, which is WebIDL's rule for an
    /// attribute whose type is an enumeration (https://webidl.spec.whatwg.org/#es-enumeration): a value the
    /// enumeration does not name is ignored, and ignored <i>instead of</i> reaching the state check. So
    /// <c>xhr.responseType = "nosuchtype"</c> is silent even in <c>DONE</c>, where a real member is an
    /// <c>InvalidStateError</c> — three rows of <c>xhr/responsetype.any.js</c> assert exactly that.
    /// </remarks>
    [JsAccessor("responseType", AccessorKind.Set, Flags = PropertyFlag.Configurable | PropertyFlag.Enumerable)]
    private JsValue ResponseTypeSet(JsValue thisObject, JsValue value)
    {
        var xhr = Brand(thisObject);

        var requested = TypeConverter.ToString(value);
        if (requested is not (JsXmlHttpRequest.ResponseTypeText
            or JsXmlHttpRequest.ResponseTypeArrayBuffer
            or JsXmlHttpRequest.ResponseTypeBlob
            or JsXmlHttpRequest.ResponseTypeDocument
            or JsXmlHttpRequest.ResponseTypeJson
            or JsXmlHttpRequest.ResponseTypeTextExplicit))
        {
            return Undefined;
        }

        if (xhr.ReadyState is JsXmlHttpRequest.Loading or JsXmlHttpRequest.Done)
        {
            ThrowDomException(DomExceptionNames.InvalidState, "Failed to set the 'responseType' property on 'XMLHttpRequest': The response type cannot be set if the object's state is LOADING or DONE.");
        }

        xhr.SetResponseType(requested);
        return Undefined;
    }

    /// <summary>
    /// https://xhr.spec.whatwg.org/#dom-xmlhttprequest-response
    /// </summary>
    [JsAccessor("response", Flags = PropertyFlag.Configurable | PropertyFlag.Enumerable)]
    private JsValue ResponseGet(JsValue thisObject)
    {
        var xhr = Brand(thisObject);

        if (xhr.ResponseType is JsXmlHttpRequest.ResponseTypeText or JsXmlHttpRequest.ResponseTypeTextExplicit)
        {
            return xhr.ReadyState is JsXmlHttpRequest.Loading or JsXmlHttpRequest.Done
                ? xhr.TextResponse()
                : JsString.Empty;
        }

        return xhr.ReadyState == JsXmlHttpRequest.Done ? xhr.ResponseObject() : Null;
    }

    /// <summary>
    /// https://xhr.spec.whatwg.org/#dom-xmlhttprequest-responsetext
    /// </summary>
    [JsAccessor("responseText", Flags = PropertyFlag.Configurable | PropertyFlag.Enumerable)]
    private JsString ResponseTextGet(JsValue thisObject)
    {
        var xhr = Brand(thisObject);

        if (xhr.ResponseType is not (JsXmlHttpRequest.ResponseTypeText or JsXmlHttpRequest.ResponseTypeTextExplicit))
        {
            ThrowDomException(DomExceptionNames.InvalidState, "Failed to read the 'responseText' property from 'XMLHttpRequest': The value is only accessible if the object's 'responseType' is '' or 'text'.");
        }

        return xhr.ReadyState is JsXmlHttpRequest.Loading or JsXmlHttpRequest.Done ? xhr.TextResponse() : JsString.Empty;
    }

    /// <summary>
    /// https://xhr.spec.whatwg.org/#dom-xmlhttprequest-responsexml — the document response, which answers
    /// <c>null</c> unless the host set <c>Options.WebApi.Xhr.DocumentParser</c>.
    /// </summary>
    [JsAccessor("responseXML", Flags = PropertyFlag.Configurable | PropertyFlag.Enumerable)]
    private JsValue ResponseXmlGet(JsValue thisObject)
    {
        var xhr = Brand(thisObject);

        if (xhr.ResponseType is not (JsXmlHttpRequest.ResponseTypeText or JsXmlHttpRequest.ResponseTypeDocument))
        {
            ThrowDomException(DomExceptionNames.InvalidState, "Failed to read the 'responseXML' property from 'XMLHttpRequest': The value is only accessible if the object's 'responseType' is '' or 'document'.");
        }

        return xhr.ReadyState == JsXmlHttpRequest.Done ? xhr.DocumentResponseValue() : Null;
    }

    /// <summary>
    /// WebIDL's <c>unsigned long</c> conversion, https://webidl.spec.whatwg.org/#idl-unsigned-long: truncate
    /// towards zero and wrap modulo 2^32, which is what <c>timeout</c> is declared as.
    /// </summary>
    private static uint ToUnsignedLong(JsValue value)
    {
        var number = TypeConverter.ToNumber(value);
        if (!double.IsFinite(number))
        {
            return 0;
        }

        var truncated = System.Math.Truncate(number) % 4294967296.0;
        if (truncated < 0)
        {
            truncated += 4294967296.0;
        }

        return (uint) truncated;
    }

    /// <summary>
    /// Raises a <c>DOMException</c>, which is how every failure this interface reports differs from the
    /// <c>TypeError</c> the fetch interfaces raise for the same mistakes.
    /// </summary>
    private void ThrowDomException(string name, string message)
    {
        var exception = _realm.Intrinsics.DomException.CreateException(name, message);
        var location = _engine._lastSyntaxElement?.Location ?? default;
        Throw.JavaScriptException(_engine, exception, in location);
    }

    /// <summary>
    /// The WebIDL brand check every member performs: a receiver that is not an <c>XMLHttpRequest</c> raises a
    /// <c>TypeError</c>.
    /// </summary>
    private JsXmlHttpRequest Brand(JsValue thisObject)
    {
        if (thisObject is JsXmlHttpRequest xhr)
        {
            return xhr;
        }

        Throw.TypeError(_realm, "Illegal invocation: receiver is not an XMLHttpRequest");
        return null!;
    }
}
#endif
