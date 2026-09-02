#if NET8_0_OR_GREATER
using Jint.Native;
using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Descriptors;
using Jint.WebApi.Abort;

namespace Jint.WebApi.Fetch;

/// <summary>
/// <c>Request.prototype</c> — the interface prototype object.
/// <para>
/// https://fetch.spec.whatwg.org/#request-class
/// </para>
/// </summary>
/// <remarks>
/// <para>
/// The attributes are accessors here rather than own properties of the instance, as WebIDL specifies; each
/// brand-checks its receiver and raises a <c>TypeError</c> for anything that is not a <c>Request</c> —
/// including <c>Request.prototype</c> itself, which is not one.
/// </para>
/// <para>
/// The <c>Body</c> mixin's members are declared here too, because a mixin's members are copied onto every
/// interface that includes it — <c>Request.prototype.text</c> and <c>Response.prototype.text</c> are two
/// different function objects, exactly as in a browser.
/// </para>
/// </remarks>
[JsObject(UseShape = true)]
internal sealed partial class RequestPrototype : Prototype
{
    [JsProperty(Name = "constructor", Flags = PropertyFlag.NonEnumerable)]
    private readonly RequestConstructor _constructor;

    [JsSymbol("ToStringTag", Flags = PropertyFlag.Configurable)]
    private static readonly JsString RequestToStringTag = new("Request");

    internal RequestPrototype(
        Engine engine,
        Realm realm,
        RequestConstructor constructor,
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
    /// https://fetch.spec.whatwg.org/#dom-request-method
    /// </summary>
    [JsAccessor("method", Flags = PropertyFlag.Configurable | PropertyFlag.Enumerable)]
    private JsString MethodGet(JsValue thisObject) => JsString.Create(Brand(thisObject).Method);

    /// <summary>
    /// https://fetch.spec.whatwg.org/#dom-request-url
    /// </summary>
    [JsAccessor("url", Flags = PropertyFlag.Configurable | PropertyFlag.Enumerable)]
    private JsString UrlGet(JsValue thisObject) => JsString.Create(Brand(thisObject).Url.Serialize());

    /// <summary>
    /// https://fetch.spec.whatwg.org/#dom-request-headers — <c>[SameObject]</c>, so the very same
    /// <c>Headers</c> object every time.
    /// </summary>
    [JsAccessor("headers", Flags = PropertyFlag.Configurable | PropertyFlag.Enumerable)]
    private JsHeaders HeadersGet(JsValue thisObject) => Brand(thisObject).Headers;

    /// <summary>
    /// https://fetch.spec.whatwg.org/#dom-request-redirect
    /// </summary>
    [JsAccessor("redirect", Flags = PropertyFlag.Configurable | PropertyFlag.Enumerable)]
    private JsString RedirectGet(JsValue thisObject) => JsString.Create(Brand(thisObject).Redirect);

    /// <summary>
    /// https://fetch.spec.whatwg.org/#dom-request-referrer — the empty string for "no referrer",
    /// <c>about:client</c> for a request that takes <c>Options.WebApi.Fetch.Referrer</c>, and otherwise the
    /// URL the <c>referrer</c> member named.
    /// </summary>
    [JsAccessor("referrer", Flags = PropertyFlag.Configurable | PropertyFlag.Enumerable)]
    private JsString ReferrerGet(JsValue thisObject)
    {
        var request = Brand(thisObject);
        return request.ReferrerSource switch
        {
            FetchReferrerSource.NoReferrer => JsString.Empty,
            FetchReferrerSource.Url => JsString.Create(request.ReferrerUrl!.Serialize()),
            _ => JsString.Create(JsRequest.ReferrerClient),
        };
    }

    /// <summary>
    /// https://fetch.spec.whatwg.org/#dom-request-referrerpolicy — the empty string means the request named
    /// none and takes <c>Options.WebApi.Fetch.ReferrerPolicy</c>.
    /// </summary>
    [JsAccessor("referrerPolicy", Flags = PropertyFlag.Configurable | PropertyFlag.Enumerable)]
    private JsString ReferrerPolicyGet(JsValue thisObject)
    {
        var policy = Brand(thisObject).ReferrerPolicy;
        return policy is null ? JsString.Empty : JsString.Create(FetchReferrer.ToWireValue(policy.Value));
    }

    /// <summary>
    /// https://fetch.spec.whatwg.org/#dom-request-credentials — whether
    /// <c>Options.WebApi.Fetch.CookieJar</c> is consulted for this request, and when.
    /// </summary>
    [JsAccessor("credentials", Flags = PropertyFlag.Configurable | PropertyFlag.Enumerable)]
    private JsString CredentialsGet(JsValue thisObject) => JsString.Create(Brand(thisObject).Credentials);

    /// <summary>
    /// https://fetch.spec.whatwg.org/#dom-request-signal — never null, and the handle
    /// <c>fetch</c> links its HTTP request against.
    /// </summary>
    [JsAccessor("signal", Flags = PropertyFlag.Configurable | PropertyFlag.Enumerable)]
    private JsAbortSignal SignalGet(JsValue thisObject) => Brand(thisObject).Signal;

    /// <summary>
    /// https://fetch.spec.whatwg.org/#dom-request-duplex — "the getter steps are to return
    /// <c>"half"</c>", which is what every request is: the whole request body reaches the wire before the
    /// response body is read. <c>"full"</c> is reserved and nothing implements it.
    /// </summary>
    [JsAccessor("duplex", Flags = PropertyFlag.Configurable | PropertyFlag.Enumerable)]
    private JsString DuplexGet(JsValue thisObject)
    {
        Brand(thisObject);
        return JsString.Create(JsRequest.DuplexHalf);
    }

    /// <summary>
    /// https://fetch.spec.whatwg.org/#dom-body-body — the body's <c>ReadableStream</c>, or <c>null</c> when
    /// the request has no body, which every <c>GET</c> and <c>HEAD</c> necessarily has not.
    /// </summary>
    /// <remarks>
    /// A body given as bytes materializes its stream here, on first ask, as a readable <i>byte</i> stream —
    /// so a BYOB reader works on it, exactly as https://fetch.spec.whatwg.org/#concept-body requires. A body
    /// given as a <c>ReadableStream</c> is that stream, whatever kind it is, and <c>fetch</c> streams it to
    /// the wire rather than collecting it first: see <see cref="FetchRequestBodyStream"/>.
    /// </remarks>
    [JsAccessor("body", Flags = PropertyFlag.Configurable | PropertyFlag.Enumerable)]
    private JsValue BodyGet(JsValue thisObject)
        => Brand(thisObject).GetOrCreateStream(_realm) ?? (JsValue) Null;

    /// <summary>
    /// https://fetch.spec.whatwg.org/#dom-body-bodyused
    /// </summary>
    [JsAccessor("bodyUsed", Flags = PropertyFlag.Configurable | PropertyFlag.Enumerable)]
    private JsBoolean BodyUsedGet(JsValue thisObject) => JsBoolean.Create(Brand(thisObject).BodyUsed);

    /// <summary>
    /// https://fetch.spec.whatwg.org/#dom-body-arraybuffer
    /// </summary>
    [JsFunction(Name = "arrayBuffer", Length = 0, Flags = PropertyFlag.ConfigurableEnumerableWritable)]
    private JsValue ArrayBuffer(JsValue thisObject) => Consume(thisObject, BodyConsumeKind.ArrayBuffer);

    /// <summary>
    /// https://fetch.spec.whatwg.org/#dom-body-bytes
    /// </summary>
    [JsFunction(Name = "bytes", Length = 0, Flags = PropertyFlag.ConfigurableEnumerableWritable)]
    private JsValue Bytes(JsValue thisObject) => Consume(thisObject, BodyConsumeKind.Bytes);

    /// <summary>
    /// https://fetch.spec.whatwg.org/#dom-body-blob
    /// </summary>
    [JsFunction(Name = "blob", Length = 0, Flags = PropertyFlag.ConfigurableEnumerableWritable)]
    private JsValue Blob(JsValue thisObject) => Consume(thisObject, BodyConsumeKind.Blob);

    /// <summary>
    /// https://fetch.spec.whatwg.org/#dom-body-json
    /// </summary>
    [JsFunction(Name = "json", Length = 0, Flags = PropertyFlag.ConfigurableEnumerableWritable)]
    private JsValue Json(JsValue thisObject) => Consume(thisObject, BodyConsumeKind.Json);

    /// <summary>
    /// https://fetch.spec.whatwg.org/#dom-body-text
    /// </summary>
    [JsFunction(Name = "text", Length = 0, Flags = PropertyFlag.ConfigurableEnumerableWritable)]
    private JsValue Text(JsValue thisObject) => Consume(thisObject, BodyConsumeKind.Text);

    /// <summary>
    /// https://fetch.spec.whatwg.org/#dom-body-formdata
    /// </summary>
    [JsFunction(Name = "formData", Length = 0, Flags = PropertyFlag.ConfigurableEnumerableWritable)]
    private JsValue FormData(JsValue thisObject) => Consume(thisObject, BodyConsumeKind.FormData);

    /// <summary>
    /// https://fetch.spec.whatwg.org/#dom-request-clone — the one member of the pair that throws
    /// synchronously rather than rejecting, because it is not defined to return a promise.
    /// </summary>
    [JsFunction(Name = "clone", Length = 0, Flags = PropertyFlag.ConfigurableEnumerableWritable)]
    private JsRequest Clone(JsValue thisObject)
    {
        var request = Brand(thisObject);
        if (request.IsUnusable)
        {
            Throw.TypeError(_realm, "Failed to execute 'clone' on 'Request': Request body is already used");
        }

        var headers = _realm.Intrinsics.Headers.CreateInstance(request.Headers.List.Clone());
        headers.List.Guard = request.Headers.List.Guard;

        var clone = new JsRequest(_engine, headers)
        {
            _prototype = _realm.Intrinsics.Request.PrototypeObject,
            Method = request.Method,
            Url = request.Url,
            Redirect = request.Redirect,

            // Step 4: "set clonedRequestObject's signal to the result of creating a dependent abort signal
            // given « this's signal »" — the clone aborts with the original, never the other way round.
            Signal = JsAbortSignal.CreateDependent(_engine, _realm, [request.Signal]),
        };

        // A streaming body is teed — with the chunks structurally cloned for the second branch, which is
        // what https://fetch.spec.whatwg.org/#concept-body-clone asks for — and a buffered one shares its
        // bytes; only the used flag is the clone's own, which is the whole point of cloning before reading
        // twice.
        FetchBody.CloneBody(request, clone);
        return clone;
    }

    private JsValue Consume(JsValue thisObject, BodyConsumeKind kind)
        => FetchBody.Consume(_engine, _realm, Brand(thisObject), kind);

    /// <summary>
    /// The WebIDL brand check every member performs.
    /// </summary>
    private JsRequest Brand(JsValue thisObject)
    {
        if (thisObject is JsRequest request)
        {
            return request;
        }

        Throw.TypeError(_realm, "Illegal invocation: receiver is not a Request");
        return null!;
    }
}
#endif
