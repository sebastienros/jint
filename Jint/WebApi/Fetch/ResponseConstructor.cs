#if NET8_0_OR_GREATER
using System.Buffers;
using Jint.Native;
using Jint.Native.Function;
using Jint.Native.Json;
using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Descriptors;
using Jint.WebApi.Url;
using Jint.WebApi.Url.Parsing;

namespace Jint.WebApi.Fetch;

/// <summary>
/// The <c>Response</c> interface object, and its three statics <c>error</c>, <c>redirect</c> and <c>json</c>.
/// <para>
/// https://fetch.spec.whatwg.org/#response-class
/// </para>
/// </summary>
/// <remarks>
/// <c>constructor(optional BodyInit? body = null, optional ResponseInit init = {})</c>. As a WebIDL interface
/// object its <c>[[Prototype]]</c> is <c>%Function.prototype%</c> and calling it without <c>new</c> raises a
/// <c>TypeError</c>, which <see cref="Constructor"/> already does.
/// </remarks>
[JsObject(UseShape = true)]
internal sealed partial class ResponseConstructor : Constructor
{
    private static readonly JsString _functionName = new("Response");

    internal ResponseConstructor(
        Engine engine,
        Realm realm,
        FunctionPrototype functionPrototype,
        ObjectPrototype objectPrototype)
        : base(engine, realm, _functionName)
    {
        _prototype = functionPrototype;
        PrototypeObject = new ResponsePrototype(engine, realm, this, objectPrototype);
        _length = new PropertyDescriptor(JsNumber.PositiveZero, PropertyFlag.Configurable);
        _prototypeDescriptor = new PropertyDescriptor(PrototypeObject, PropertyFlag.AllForbidden);
    }

    internal ResponsePrototype PrototypeObject { get; }

    protected override void Initialize() => CreateProperties_Generated();

    /// <summary>
    /// https://fetch.spec.whatwg.org/#dom-response
    /// </summary>
    public override ObjectInstance Construct(JsCallArguments arguments, JsValue newTarget)
    {
        var body = arguments.At(0);
        var init = arguments.At(1);

        var response = OrdinaryCreateFromConstructor(
            newTarget,
            static intrinsics => intrinsics.Response.PrototypeObject,
            static (Engine engine, Realm _, JsHeaders? state) => new JsResponse(engine, state!),
            NewResponseHeaders());

        // Steps 3 and 4: the body is extracted *before* initialize runs its range checks, so
        // `new Response(new FormData(), { status: 999 })` reports the unsupported body rather than the
        // status. body is optional and nullable, so an omitted argument and an explicit null both mean none.
        (ReadOnlyMemory<byte> Bytes, string? Type)? bodyWithType = null;
        if (!body.IsNullOrUndefined())
        {
            bodyWithType = FetchBody.Extract(_realm, body);
        }

        Initialize(response, init, bodyWithType);
        return response;
    }

    /// <summary>
    /// https://fetch.spec.whatwg.org/#dom-response-error — a response representing a network error. Its
    /// headers are immutable and its status is 0, so <c>ok</c> is false and nothing about it can be edited.
    /// </summary>
    [JsFunction(Name = "error", Length = 0)]
    private JsResponse Error(JsValue thisObject)
    {
        var response = NewResponse();
        response.Status = 0;
        response.Kind = ResponseType.Error;
        response.Headers.List.Guard = HeadersGuard.Immutable;
        return response;
    }

    /// <summary>
    /// https://fetch.spec.whatwg.org/#dom-response-redirect
    /// </summary>
    /// <remarks>
    /// The URL is parsed without a base, for the reason <c>Request</c>'s constructor documents: an embedded
    /// engine has no document to be relative to.
    /// </remarks>
    [JsFunction(Name = "redirect", Length = 1)]
    private JsResponse Redirect(JsValue thisObject, JsValue url, JsValue status)
    {
        var href = UrlValues.ToUsvString(url);
        var parsed = UrlParser.Parse(href);
        if (parsed is null)
        {
            Throw.TypeError(_realm, $"Failed to execute 'redirect' on 'Response': Failed to parse URL from {href}");
        }

        // The IDL default is 302, taken by an omitted argument and by an explicit undefined alike.
        var code = status.IsUndefined() ? 302 : FetchValues.ToUnsignedShort(status);
        if (!FetchValues.IsRedirectStatus(code))
        {
            Throw.RangeError(_realm, $"Failed to execute 'redirect' on 'Response': Invalid status code {code}");
        }

        var response = NewResponse();
        response.Status = code;
        response.Headers.List.Guard = HeadersGuard.Immutable;

        // Appended past the guard deliberately: the guard is what the *script* may not edit, and step 5 of
        // the algorithm appends this after setting it.
        response.Headers.List.Append("location", parsed.Serialize());
        return response;
    }

    /// <summary>
    /// https://fetch.spec.whatwg.org/#dom-response-json
    /// </summary>
    [JsFunction(Name = "json", Length = 1)]
    private JsResponse Json(JsValue thisObject, JsValue data, JsValue init)
    {
        // "Serialize a JavaScript value to JSON bytes", https://infra.spec.whatwg.org/#serialize-a-javascript-value-to-json-bytes:
        // JSON.stringify, and a TypeError when it answers undefined — which is what `undefined`, a function
        // and a symbol all do.
        var writer = new ArrayBufferWriter<byte>();
        if (!new JsonSerializer(_engine).Serialize(data, writer))
        {
            Throw.TypeError(_realm, "Failed to execute 'json' on 'Response': The data is not JSON serializable");
        }

        var response = NewResponse();
        Initialize(response, init, (writer.WrittenMemory, FetchBody.JsonContentType));
        return response;
    }

    /// <summary>
    /// "Initialize a response", https://fetch.spec.whatwg.org/#initialize-a-response — the status, status
    /// message, headers and body a <c>ResponseInit</c> carries, shared by the constructor and by
    /// <c>Response.json</c>.
    /// </summary>
    private void Initialize(JsResponse response, JsValue init, (ReadOnlyMemory<byte> Bytes, string? Type)? body)
    {
        var initObject = ToInit(init);

        // Lexicographical order of the dictionary's members: headers, status, statusText. The two range
        // checks come after every member has been read, which is what the algorithm's own step order says.
        var headersInit = RequestConstructor.Member(initObject, "headers");
        var statusInit = RequestConstructor.Member(initObject, "status");
        var statusTextInit = RequestConstructor.Member(initObject, "statusText");

        var status = statusInit is null ? 200 : FetchValues.ToUnsignedShort(statusInit);
        if (status is < 200 or > 599)
        {
            Throw.RangeError(_realm, $"Failed to construct 'Response': The status provided ({status}) is outside the range [200, 599].");
        }

        var statusText = statusTextInit is null ? string.Empty : FetchValues.ToByteString(_realm, statusTextInit);
        if (!FetchValues.IsReasonPhrase(statusText))
        {
            Throw.TypeError(_realm, $"Failed to construct 'Response': Invalid status text: '{statusText}'");
        }

        response.Status = status;
        response.StatusText = statusText;

        if (headersInit is not null)
        {
            response.Headers.Fill(_realm, headersInit);
        }

        if (body is not { } bodyWithType)
        {
            return;
        }

        // Step 6.1 — and it applies to Response.json too, so `Response.json(null, { status: 204 })` is a
        // TypeError rather than a 204 carrying "null".
        if (FetchValues.IsNullBodyStatus(response.Status))
        {
            Throw.TypeError(_realm, "Failed to construct 'Response': Response with null body status cannot have body");
        }

        FetchBody.SetBody(response, bodyWithType.Bytes, bodyWithType.Type);
    }

    private ObjectInstance? ToInit(JsValue init)
    {
        if (init.IsNullOrUndefined())
        {
            return null;
        }

        if (init is not ObjectInstance initObject)
        {
            Throw.TypeError(_realm, "Failed to construct 'Response': The provided value is not of type 'ResponseInit'");
            return null;
        }

        return initObject;
    }

    /// <summary>
    /// A fresh response object, for the three statics — none of which goes through
    /// <c>OrdinaryCreateFromConstructor</c>, because none of them takes a <c>newTarget</c>.
    /// </summary>
    private JsResponse NewResponse()
        => new(_engine, NewResponseHeaders()) { _prototype = PrototypeObject };

    /// <summary>
    /// An empty header list under the <c>response</c> guard, which every response starts with.
    /// </summary>
    private JsHeaders NewResponseHeaders()
    {
        var headers = _realm.Intrinsics.Headers.CreateInstance(new HeaderList());
        headers.List.Guard = HeadersGuard.Response;
        return headers;
    }

    /// <summary>
    /// Builds a <c>Response</c> for a response the engine produced itself, which is what <c>fetch</c> settles
    /// its promise with.
    /// </summary>
    internal JsResponse CreateInstance(HeaderList headerList)
    {
        var headers = _realm.Intrinsics.Headers.CreateInstance(headerList);
        headers.List.Guard = HeadersGuard.Response;

        return new JsResponse(_engine, headers)
        {
            _prototype = PrototypeObject,
            Kind = ResponseType.Basic,
        };
    }
}
#endif
