#if NET8_0_OR_GREATER
namespace Jint.WebApi.Fetch;

/// <summary>
/// A response's type, https://fetch.spec.whatwg.org/#concept-response-type.
/// </summary>
/// <remarks>
/// Three of the standard's six can arise here. <c>cors</c>, <c>opaque</c> and <c>opaqueredirect</c> are what a
/// browser produces when a response crossed an origin boundary the page is not allowed to see through; Jint
/// has no origin and enforces no same-origin policy — a host that wants one writes a
/// <c>Options.FetchOptions.UrlFilter</c> — so no response is ever filtered and none of the three occurs.
/// </remarks>
internal enum ResponseType
{
    /// <summary>A response the script built itself with <c>new Response(...)</c>.</summary>
    Default,

    /// <summary>A response that came off the network.</summary>
    Basic,

    /// <summary>A network error, https://fetch.spec.whatwg.org/#concept-network-error.</summary>
    Error,
}

/// <summary>
/// A <c>Response</c> instance.
/// <para>
/// https://fetch.spec.whatwg.org/#response-class
/// </para>
/// </summary>
/// <remarks>
/// Every attribute is an accessor on <see cref="ResponsePrototype"/> reading this state through a brand check,
/// so an instance has no own property — which is what a browser reports for
/// <c>Object.getOwnPropertyNames(new Response())</c>.
/// </remarks>
internal sealed class JsResponse : FetchBodyObject
{
    internal JsResponse(Engine engine, JsHeaders headers) : base(engine, headers)
    {
    }

    /// <summary>https://fetch.spec.whatwg.org/#concept-response-status</summary>
    internal int Status { get; set; } = 200;

    /// <summary>https://fetch.spec.whatwg.org/#concept-response-status-message</summary>
    internal string StatusText { get; set; } = string.Empty;

    /// <summary>
    /// https://fetch.spec.whatwg.org/#concept-response-type — what the <c>type</c> attribute answers.
    /// Named <c>Kind</c> because <c>JsValue.Type</c> already means the JavaScript type of the object.
    /// </summary>
    internal ResponseType Kind { get; set; } = ResponseType.Default;

    /// <summary>
    /// The serialized last URL of https://fetch.spec.whatwg.org/#concept-response-url-list, excluding its
    /// fragment — the empty string when the list is empty, which is every response a script built itself.
    /// </summary>
    internal string Url { get; set; } = string.Empty;

    /// <summary>
    /// https://fetch.spec.whatwg.org/#dom-response-redirected — whether the URL list has more than one entry,
    /// i.e. whether a redirect was followed to reach this response.
    /// </summary>
    internal bool Redirected { get; set; }

    /// <summary>https://fetch.spec.whatwg.org/#ok-status</summary>
    internal bool Ok => Status is >= 200 and <= 299;
}
#endif
