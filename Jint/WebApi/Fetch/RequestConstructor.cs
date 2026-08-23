#if NET8_0_OR_GREATER
using Jint.Native;
using Jint.Native.Function;
using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Descriptors;
using Jint.WebApi.Abort;
using Jint.WebApi.Url;
using Jint.WebApi.Url.Parsing;

namespace Jint.WebApi.Fetch;

/// <summary>
/// The <c>Request</c> interface object.
/// <para>
/// https://fetch.spec.whatwg.org/#request-class
/// </para>
/// </summary>
/// <remarks>
/// <c>constructor(RequestInfo input, optional RequestInit init = {})</c>. As a WebIDL interface object its
/// <c>[[Prototype]]</c> is <c>%Function.prototype%</c> and calling it without <c>new</c> raises a
/// <c>TypeError</c>, which <see cref="Constructor"/> already does.
/// </remarks>
internal sealed class RequestConstructor : Constructor
{
    private static readonly JsString _functionName = new("Request");

    internal RequestConstructor(
        Engine engine,
        Realm realm,
        FunctionPrototype functionPrototype,
        ObjectPrototype objectPrototype)
        : base(engine, realm, _functionName)
    {
        _prototype = functionPrototype;
        PrototypeObject = new RequestPrototype(engine, realm, this, objectPrototype);
        _length = new PropertyDescriptor(JsNumber.PositiveOne, PropertyFlag.Configurable);
        _prototypeDescriptor = new PropertyDescriptor(PrototypeObject, PropertyFlag.AllForbidden);
    }

    internal RequestPrototype PrototypeObject { get; }

    /// <summary>
    /// https://fetch.spec.whatwg.org/#dom-request, reduced to the members this implementation has —
    /// see <see cref="JsRequest"/> for which those are and why.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>There is no base URL.</b> The specification parses a string input against "the entry settings
    /// object's API base URL", which is a document's URL; an embedded engine has no document, so a relative
    /// URL simply does not parse and is a <c>TypeError</c>. Pass an absolute URL, or resolve it yourself with
    /// <c>new URL(relative, base).href</c>.
    /// </para>
    /// <para>
    /// The <c>RequestInit</c> members this implementation does not act on — <c>cache</c>, <c>credentials</c>,
    /// <c>integrity</c>, <c>keepalive</c>, <c>mode</c>, <c>priority</c>, <c>referrer</c>,
    /// <c>referrerPolicy</c>, <c>window</c> — are neither read nor validated, so a getter among them is not
    /// invoked and a misspelled enumeration value is not refused. Accepting and ignoring them is the Node and
    /// workerd convention: a script written for a browser keeps working, and nothing here pretends to honour
    /// a same-origin policy or an HTTP cache that does not exist.
    /// </para>
    /// </remarks>
    public override ObjectInstance Construct(JsCallArguments arguments, JsValue newTarget)
    {
        var input = arguments.At(0);
        var init = arguments.At(1);

        // Step 5/6: the input is either a Request to copy or a URL string to parse.
        var inputRequest = input as JsRequest;
        UrlRecord url;
        var method = "GET";
        var redirect = JsRequest.RedirectFollow;
        HeaderList headerList;
        JsAbortSignal? signal = null;

        if (inputRequest is not null)
        {
            url = inputRequest.Url;
            method = inputRequest.Method;
            redirect = inputRequest.Redirect;
            headerList = inputRequest.Headers.List.Clone();
            signal = inputRequest.Signal;
        }
        else
        {
            var href = UrlValues.ToUsvString(input);
            var parsed = UrlParser.Parse(href);
            if (parsed is null)
            {
                Throw.TypeError(_realm, $"Failed to construct 'Request': Failed to parse URL from {href}");
            }

            // Step 5.3. Credentials in the URL are a phishing and credential-leak shape a browser refuses
            // outright, and there is no reason for an embedded engine to be more permissive: an Authorization
            // header says the same thing without travelling through logs and Referer headers.
            if (parsed.IncludesCredentials)
            {
                Throw.TypeError(_realm, "Failed to construct 'Request': Request cannot be constructed from a URL that includes credentials");
            }

            url = parsed;
            headerList = new HeaderList();
        }

        // WebIDL converts a dictionary's members in lexicographical order of their identifiers, so a bag whose
        // members are getters observes body, duplex, headers, method, redirect, signal in that order.
        var initObject = ToInit(init);
        var bodyInit = Member(initObject, "body");
        var duplexInit = Member(initObject, "duplex");
        var headersInit = Member(initObject, "headers");
        var methodInit = Member(initObject, "method");
        var redirectInit = Member(initObject, "redirect");
        var signalInit = Member(initObject, "signal");

        if (methodInit is not null)
        {
            var requested = FetchValues.ToByteString(_realm, methodInit);
            if (!FetchValues.IsMethod(requested) || FetchValues.IsForbiddenMethod(requested))
            {
                Throw.TypeError(_realm, $"Failed to construct 'Request': '{requested}' is not a valid HTTP method.");
            }

            method = FetchValues.NormalizeMethod(requested);
        }

        if (redirectInit is not null)
        {
            redirect = FetchValues.ToByteString(_realm, redirectInit);
            if (redirect is not (JsRequest.RedirectFollow or JsRequest.RedirectError or JsRequest.RedirectManual))
            {
                Throw.TypeError(_realm, $"Failed to construct 'Request': The provided value '{redirect}' is not a valid enum value of type RequestRedirect.");
            }
        }

        // `enum RequestDuplex { "half" };` — "half" is the only value, and "full" is reserved for a
        // full-duplex fetch nobody has specified yet. WebIDL performs this conversion while the dictionary
        // is being read rather than as a step of the algorithm; doing it here instead only changes which
        // TypeError a bag carrying two invalid members reports, and keeps it beside the other enum member.
        if (duplexInit is not null && !string.Equals(FetchValues.ToByteString(_realm, duplexInit), JsRequest.DuplexHalf, StringComparison.Ordinal))
        {
            Throw.TypeError(_realm, "Failed to construct 'Request': The provided value is not a valid enum value of type RequestDuplex.");
        }

        if (signalInit is not null && !signalInit.IsNull())
        {
            signal = signalInit as JsAbortSignal;
            if (signal is null)
            {
                Throw.TypeError(_realm, "Failed to construct 'Request': The provided value is not of type 'AbortSignal'");
            }
        }

        var headers = _realm.Intrinsics.Headers.CreateInstance(headerList);
        headers.List.Guard = HeadersGuard.Request;

        var request = OrdinaryCreateFromConstructor(
            newTarget,
            static intrinsics => intrinsics.Request.PrototypeObject,
            static (Engine engine, Realm _, JsHeaders? state) => new JsRequest(engine, state!),
            headers);

        request.Method = method;
        request.Url = url;
        request.Redirect = redirect;

        // Step 35/36: the request's own signal always exists, and follows the one the initializer named.
        request.Signal = signal is null
            ? new JsAbortSignal(_engine, _realm) { _prototype = _realm.Intrinsics.AbortSignal.PrototypeObject }
            : JsAbortSignal.CreateDependent(_engine, _realm, [signal]);

        // Step 33: an explicit headers member replaces the copied list wholesale, rather than adding to it.
        if (headersInit is not null)
        {
            headerList.Entries.Clear();
            headers.Fill(_realm, headersInit);
        }

        var hasInitBody = bodyInit is not null && !bodyInit.IsNull();
        var hasInputBody = inputRequest is { HasBody: true };

        // Step 37: a body — from either source — is refused for the two methods that cannot carry one.
        if ((hasInitBody || hasInputBody) && method is "GET" or "HEAD")
        {
            Throw.TypeError(_realm, "Failed to construct 'Request': Request with GET/HEAD method cannot have body.");
        }

        if (hasInitBody)
        {
            // Steps 38-40: extract, then append the implied Content-Type unless the header list — which the
            // step above has already filled — carries one of its own.
            var extracted = FetchBody.Extract(_realm, bodyInit!);

            // Step 41: "If initBody is non-null and init["duplex"] does not exist, then throw a TypeError."
            // Only a body whose source is null — the ReadableStream arm — reaches it, which is why a string
            // or a Blob body may carry duplex or not as it likes and neither is refused. The member is what
            // makes a script say out loud that it knows the request will be sent before the response is
            // read; there is no "full" to ask for.
            if (extracted.Stream is not null && duplexInit is null)
            {
                Throw.TypeError(_realm, "Failed to construct 'Request': The `duplex` member must be set to 'half' for a request with a ReadableStream body.");
            }

            FetchBody.SetBody(request, in extracted);
        }
        else if (hasInputBody)
        {
            // Step 42: "create a proxy for inputBody" — a tee when the input has a stream, and a shared
            // source when it does not. Pointedly NOT the clone-a-body algorithm: a proxy is an identity
            // transform, so the chunks pass through unchanged rather than being structured-cloned.
            if (inputRequest!.IsUnusable)
            {
                Throw.TypeError(_realm, "Failed to construct 'Request': Request body is already used");
            }

            FetchBody.ProxyBody(inputRequest, request);
        }

        return request;
    }

    /// <summary>
    /// WebIDL dictionary conversion: <see langword="null"/> and <c>undefined</c> mean "every member
    /// defaulted", and anything that is not an object is a <c>TypeError</c> —
    /// https://webidl.spec.whatwg.org/#es-dictionary.
    /// </summary>
    private ObjectInstance? ToInit(JsValue init)
    {
        if (init.IsNullOrUndefined())
        {
            return null;
        }

        if (init is not ObjectInstance initObject)
        {
            Throw.TypeError(_realm, "Failed to construct 'Request': The provided value is not of type 'RequestInit'");
            return null;
        }

        return initObject;
    }

    /// <summary>
    /// Reads one dictionary member. Every member of <c>RequestInit</c> is optional with no default value, so
    /// an explicitly passed <c>undefined</c> means "not present" — <c>{ method: undefined }</c> keeps the
    /// method the input had.
    /// </summary>
    internal static JsValue? Member(ObjectInstance? dictionary, string name)
    {
        if (dictionary is null)
        {
            return null;
        }

        var value = dictionary.Get(name);
        return value.IsUndefined() ? null : value;
    }
}
#endif
