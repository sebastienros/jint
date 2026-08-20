#if NET8_0_OR_GREATER
using Jint.Native;
using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Descriptors;

namespace Jint.WebApi.Fetch;

/// <summary>
/// <c>Response.prototype</c> — the interface prototype object.
/// <para>
/// https://fetch.spec.whatwg.org/#response-class
/// </para>
/// </summary>
/// <remarks>
/// <para>
/// The attributes are accessors here rather than own properties of the instance, as WebIDL specifies; each
/// brand-checks its receiver and raises a <c>TypeError</c> for anything that is not a <c>Response</c> —
/// including <c>Response.prototype</c> itself, which is not one.
/// </para>
/// <para>
/// The <c>Body</c> mixin's members are declared here as well as on <see cref="RequestPrototype"/>, because a
/// mixin's members are copied onto every interface that includes it: the two <c>text</c> functions are
/// different objects, exactly as in a browser. <c>formData()</c> is absent — see
/// <see cref="RequestPrototype"/>.
/// </para>
/// </remarks>
[JsObject(UseShape = true)]
internal sealed partial class ResponsePrototype : Prototype
{
    private static readonly JsString _typeDefault = new("default");
    private static readonly JsString _typeBasic = new("basic");
    private static readonly JsString _typeError = new("error");

    [JsProperty(Name = "constructor", Flags = PropertyFlag.NonEnumerable)]
    private readonly ResponseConstructor _constructor;

    [JsSymbol("ToStringTag", Flags = PropertyFlag.Configurable)]
    private static readonly JsString ResponseToStringTag = new("Response");

    internal ResponsePrototype(
        Engine engine,
        Realm realm,
        ResponseConstructor constructor,
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
    /// https://fetch.spec.whatwg.org/#dom-response-type
    /// </summary>
    [JsAccessor("type", Flags = PropertyFlag.Configurable | PropertyFlag.Enumerable)]
    private JsString TypeGet(JsValue thisObject) => Brand(thisObject).Kind switch
    {
        ResponseType.Basic => _typeBasic,
        ResponseType.Error => _typeError,
        _ => _typeDefault,
    };

    /// <summary>
    /// https://fetch.spec.whatwg.org/#dom-response-url — the empty string for a response a script built
    /// itself, which has no URL list at all.
    /// </summary>
    [JsAccessor("url", Flags = PropertyFlag.Configurable | PropertyFlag.Enumerable)]
    private JsString UrlGet(JsValue thisObject) => JsString.Create(Brand(thisObject).Url);

    /// <summary>
    /// https://fetch.spec.whatwg.org/#dom-response-redirected
    /// </summary>
    [JsAccessor("redirected", Flags = PropertyFlag.Configurable | PropertyFlag.Enumerable)]
    private JsBoolean RedirectedGet(JsValue thisObject) => JsBoolean.Create(Brand(thisObject).Redirected);

    /// <summary>
    /// https://fetch.spec.whatwg.org/#dom-response-status
    /// </summary>
    [JsAccessor("status", Flags = PropertyFlag.Configurable | PropertyFlag.Enumerable)]
    private JsNumber StatusGet(JsValue thisObject) => JsNumber.Create(Brand(thisObject).Status);

    /// <summary>
    /// https://fetch.spec.whatwg.org/#dom-response-ok
    /// </summary>
    [JsAccessor("ok", Flags = PropertyFlag.Configurable | PropertyFlag.Enumerable)]
    private JsBoolean OkGet(JsValue thisObject) => JsBoolean.Create(Brand(thisObject).Ok);

    /// <summary>
    /// https://fetch.spec.whatwg.org/#dom-response-statustext
    /// </summary>
    [JsAccessor("statusText", Flags = PropertyFlag.Configurable | PropertyFlag.Enumerable)]
    private JsString StatusTextGet(JsValue thisObject) => JsString.Create(Brand(thisObject).StatusText);

    /// <summary>
    /// https://fetch.spec.whatwg.org/#dom-response-headers — <c>[SameObject]</c>, so the very same
    /// <c>Headers</c> object every time.
    /// </summary>
    [JsAccessor("headers", Flags = PropertyFlag.Configurable | PropertyFlag.Enumerable)]
    private JsHeaders HeadersGet(JsValue thisObject) => Brand(thisObject).Headers;

    /// <summary>
    /// https://fetch.spec.whatwg.org/#dom-body-body — <b>always null in this version</b>; see
    /// <see cref="RequestPrototype"/> for why it is present rather than absent.
    /// </summary>
    [JsAccessor("body", Flags = PropertyFlag.Configurable | PropertyFlag.Enumerable)]
    private JsValue BodyGet(JsValue thisObject)
    {
        Brand(thisObject);
        return Null;
    }

    /// <summary>
    /// https://fetch.spec.whatwg.org/#dom-body-bodyused
    /// </summary>
    [JsAccessor("bodyUsed", Flags = PropertyFlag.Configurable | PropertyFlag.Enumerable)]
    private JsBoolean BodyUsedGet(JsValue thisObject) => JsBoolean.Create(Brand(thisObject).BodyUsed);

    /// <summary>
    /// https://fetch.spec.whatwg.org/#dom-body-arraybuffer
    /// </summary>
    [JsFunction(Name = "arrayBuffer", Length = 0)]
    private JsValue ArrayBuffer(JsValue thisObject) => Consume(thisObject, BodyConsumeKind.ArrayBuffer);

    /// <summary>
    /// https://fetch.spec.whatwg.org/#dom-body-bytes
    /// </summary>
    [JsFunction(Name = "bytes", Length = 0)]
    private JsValue Bytes(JsValue thisObject) => Consume(thisObject, BodyConsumeKind.Bytes);

    /// <summary>
    /// https://fetch.spec.whatwg.org/#dom-body-blob
    /// </summary>
    [JsFunction(Name = "blob", Length = 0)]
    private JsValue Blob(JsValue thisObject) => Consume(thisObject, BodyConsumeKind.Blob);

    /// <summary>
    /// https://fetch.spec.whatwg.org/#dom-body-json
    /// </summary>
    [JsFunction(Name = "json", Length = 0)]
    private JsValue Json(JsValue thisObject) => Consume(thisObject, BodyConsumeKind.Json);

    /// <summary>
    /// https://fetch.spec.whatwg.org/#dom-body-text
    /// </summary>
    [JsFunction(Name = "text", Length = 0)]
    private JsValue Text(JsValue thisObject) => Consume(thisObject, BodyConsumeKind.Text);

    /// <summary>
    /// https://fetch.spec.whatwg.org/#dom-response-clone — a synchronous <c>TypeError</c> for an already-read
    /// body, because <c>clone</c> does not return a promise to reject.
    /// </summary>
    /// <remarks>
    /// The clone shares the original's bytes and carries its own used flag, which is what makes the
    /// <c>const copy = response.clone(); await response.json(); await copy.text();</c> pattern work.
    /// </remarks>
    [JsFunction(Name = "clone", Length = 0)]
    private JsResponse Clone(JsValue thisObject)
    {
        var response = Brand(thisObject);
        if (response.IsUnusable)
        {
            Throw.TypeError(_realm, "Failed to execute 'clone' on 'Response': Response body is already used");
        }

        var headers = _realm.Intrinsics.Headers.CreateInstance(response.Headers.List.Clone());
        headers.List.Guard = response.Headers.List.Guard;

        return new JsResponse(_engine, headers)
        {
            _prototype = _realm.Intrinsics.Response.PrototypeObject,
            Status = response.Status,
            StatusText = response.StatusText,
            Kind = response.Kind,
            Url = response.Url,
            Redirected = response.Redirected,
            Body = response.Body,
        };
    }

    private JsValue Consume(JsValue thisObject, BodyConsumeKind kind)
        => FetchBody.Consume(_engine, _realm, Brand(thisObject), kind);

    /// <summary>
    /// The WebIDL brand check every member performs.
    /// </summary>
    private JsResponse Brand(JsValue thisObject)
    {
        if (thisObject is JsResponse response)
        {
            return response;
        }

        Throw.TypeError(_realm, "Illegal invocation: receiver is not a Response");
        return null!;
    }
}
#endif
