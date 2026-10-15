#if NET8_0_OR_GREATER
using System.Diagnostics.CodeAnalysis;
using Jint.WebApi.Url.Parsing;

namespace Jint.WebApi.Fetch;

/// <summary>
/// The <c>data</c> arm of <i>scheme fetch</i>: how a <c>data:</c> URL becomes a response without a socket.
/// <para>
/// https://fetch.spec.whatwg.org/#scheme-fetch
/// </para>
/// </summary>
/// <remarks>
/// <para>
/// <b>It is answered before every network check, as the <c>blob</c> arm is, and for the same reason.</b> The
/// bytes are in the URL itself, so there is nothing for <c>Options.WebApi.Fetch.AllowedSchemes</c>, the host's
/// <c>UrlFilter</c>, the concurrency cap, an <c>HttpClient</c> or a <c>FetchObserver</c> to decide or to see.
/// Both <c>fetch</c> and <c>XMLHttpRequest</c> take this path, and only for the request's own URL: a redirect
/// to a <c>data:</c> URL is still the network error
/// https://fetch.spec.whatwg.org/#http-redirect-fetch makes of a location that is not an HTTP(S) URL.
/// </para>
/// <para>
/// <b>What it does owe is <c>Options.WebApi.Fetch.MaxResponseBytes</c></b>, on the decoded body, with the
/// failure and the message a body over the limit gets from the transport — a script may not escape a size
/// ceiling by inlining. <see cref="DataUrl.TryProcess(UrlRecord, long, out DataUrlContent, out bool)"/>
/// refuses such a body from its length before decoding it where the length is enough to tell.
/// </para>
/// <para>
/// <b>Any method, unlike the <c>blob</c> arm.</b> The arm names no method, so a <c>POST</c> gets the body (its
/// own request body goes nowhere), and a <c>HEAD</c> gets the headers and no body — main fetch's rule for
/// every response, applied here because a response built in memory never reaches the transport that applies
/// it — and is therefore never refused for its length.
/// </para>
/// </remarks>
internal static class DataUrlFetch
{
    /// <summary>
    /// Builds the response, or returns <see langword="false"/> with the failure: a URL the processor refuses,
    /// or a body longer than <paramref name="maxResponseBytes"/>.
    /// </summary>
    /// <param name="url">The request's URL, whose scheme is <c>data</c>.</param>
    /// <param name="method">The request's method, already normalized.</param>
    /// <param name="maxResponseBytes"><c>Options.WebApi.Fetch.MaxResponseBytes</c>.</param>
    /// <param name="response">The response to answer with.</param>
    /// <param name="failure">Why there is none — the host's to read, never the script's.</param>
    internal static bool TryBuild(
        UrlRecord url,
        string method,
        long maxResponseBytes,
        out LocalSchemeResponse response,
        [NotNullWhen(false)] out FetchFailureException? failure)
    {
        response = default;
        failure = null;

        var hasBody = !string.Equals(method, "HEAD", StringComparison.Ordinal);

        // Steps 1 and 2: the processor's failure is a network error. A HEAD still runs it — a URL that is not
        // a data: URL at all fails whatever was asked of it — but asks for no ceiling, since nothing is read.
        if (!DataUrl.TryProcess(url, hasBody ? maxResponseBytes : long.MaxValue, out var content, out var tooLarge))
        {
            failure = tooLarge
                ? new FetchFailureException(
                    FetchFailureKind.ResponseTooLarge,
                    $"The response body exceeded the {maxResponseBytes} byte limit set by Options.WebApi.Fetch.MaxResponseBytes.")
                : new FetchFailureException(FetchFailureKind.Network, "The data: URL could not be processed.");
            return false;
        }

        // Steps 3 and 4: 200 `OK`, one header — the serialized MIME type — and the bytes. No Content-Length,
        // which is the difference from the blob arm: this one names a single header.
        response = new LocalSchemeResponse(
            200,
            "OK",
            [new HeaderEntry("content-type", content.MimeType.Serialize())],
            hasBody ? content.Body : (ReadOnlyMemory<byte>?) null);

        return true;
    }
}
#endif
