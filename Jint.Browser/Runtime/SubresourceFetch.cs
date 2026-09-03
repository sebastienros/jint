using System.Net.Http;
using System.Text;
using Jint.WebApi.Fetch;
using Jint.WebApi.Url.Parsing;

namespace Jint.Browser.Runtime;

/// <summary>What one subresource fetch produced: the bytes, what they claim to be, and where they came from.</summary>
/// <param name="Bytes">The body, exactly as it arrived.</param>
/// <param name="ContentType">The response's <c>Content-Type</c>, or <see langword="null"/> when it had none.</param>
/// <param name="Url">The URL the resource ended up at: the last hop of the redirect chain.</param>
/// <param name="Status">The status of the final response.</param>
internal sealed record FetchedSubresource(byte[] Bytes, string? ContentType, string Url, int Status)
{
    /// <summary>
    /// The body decoded as text, with the charset the response declared, then the caller's hint, then UTF-8.
    /// </summary>
    /// <remarks>
    /// https://html.spec.whatwg.org/multipage/scripting.html#fetch-a-classic-script — a classic script is
    /// decoded with the response's charset, falling back to the element's <c>charset</c> attribute and then
    /// to the document's encoding. A leading byte-order mark is dropped whatever said so, because it is a
    /// mark and not content.
    /// </remarks>
    internal string Text(string? fallbackCharset = null)
    {
        var declared = ContentType is null ? null : MimeType.Parse(ContentType)?.GetParameter("charset");
        var encoding = Resolve(declared) ?? Resolve(fallbackCharset) ?? Encoding.UTF8;
        var text = encoding.GetString(Bytes);
        return text.Length != 0 && text[0] == '﻿' ? text[1..] : text;
    }

    private static Encoding? Resolve(string? label)
    {
        if (string.IsNullOrEmpty(label))
        {
            return null;
        }

        try
        {
            return Encoding.GetEncoding(label);
        }
        catch (ArgumentException)
        {
            // An unknown label is ignored and the next candidate is tried, which is what the standard says.
            return null;
        }
    }
}

/// <summary>One subresource fetch, as the thing that wants the bytes describes it.</summary>
/// <param name="Url">The absolute URL to ask for.</param>
/// <param name="Referrer">The document the reference was found in, or <see langword="null"/>.</param>
/// <param name="Origin">
/// The document URL, kept for its origin alone, or <see langword="null"/> for an opaque one.
/// </param>
/// <param name="MaxResponseBytes">The ceiling on the body.</param>
/// <param name="MaxRedirects">How many hops the chain may follow.</param>
/// <param name="Initiator">What the page's network log should call this request.</param>
/// <param name="Kind">What the resource is for, which is what a protocol client filters requests on.</param>
internal sealed record SubresourceRequest(
    UrlRecord Url,
    UrlRecord? Referrer,
    UrlRecord? Origin,
    long MaxResponseBytes,
    int MaxRedirects,
    RequestInitiator Initiator,
    PageRequestKind Kind = PageRequestKind.Other);

/// <summary>Raised when a subresource could not be obtained, with the sentence a page error should carry.</summary>
internal sealed class SubresourceFetchException : Exception
{
    internal SubresourceFetchException(string url, string message, Exception? inner = null)
        : base(message, inner)
    {
        Url = url;
    }

    /// <summary>The URL that could not be loaded.</summary>
    internal string Url { get; }
}

/// <summary>
/// A subresource load — a script, a stylesheet — over the same engine-free pipeline a navigation uses.
/// </summary>
/// <remarks>
/// <para>
/// <b>It is <see cref="DocumentFetch"/>'s sibling and shares everything but the rules.</b> Same transport,
/// same per-hop policy re-check, same cookie jar, same observation; what differs is that a subresource is
/// fetched on someone else's behalf, so its credentials mode is <c>same-origin</c> rather than the
/// <c>include</c> a top-level navigation gets, and a status the server calls an error is a failure rather
/// than a document to show.
/// </para>
/// <para>
/// <b>It runs off the page loop, on whichever thread awaits it</b> — which is what lets the parser driver
/// pump timers and tasks while a parser-blocking <c>&lt;script src&gt;</c> is on its way.
/// </para>
/// </remarks>
internal static class SubresourceFetch
{
#pragma warning disable JINT0002 // The observation types are the engine's preview network seam; a subresource is a host fetch.
    internal static async Task<FetchedSubresource> LoadAsync(
        PageNetwork network,
        HttpClient client,
        SubresourceRequest request,
        PageNetworkRecorder? recorder,
        CancellationToken cancellationToken)
    {
        var url = request.Url.Serialize();

        // The first hop's filter is run here rather than in the transport, which deliberately does not run it
        // twice: a host filter being called once per request is observable to the host.
        var uri = PageUrl.ToUri(request.Url);
        if (uri is null || !network.UrlFilter(uri))
        {
            recorder?.RecordNotFetched(url, request.Initiator, request.Kind, "the browser context's URL filter refused it");
            throw new SubresourceFetchException(url, "'" + url + "' was refused by the browser context's URL filter.");
        }

        var observation = recorder?.Observe(request.Initiator, request.Kind);

        var snapshot = new FetchRequestSnapshot
        {
            Method = "GET",
            Url = request.Url,
            Headers = [],
            Body = null,
            BodyContent = null,
            Redirect = "follow",

            // https://html.spec.whatwg.org/multipage/webappapis.html#fetch-a-classic-script: a subresource is
            // asked for on the document's behalf, so it carries cookies only to its own origin unless the
            // element opted in — and `crossorigin` is accepted and ignored here, so it never does.
            Credentials = JsRequest.CredentialsSameOrigin,
            Referrer = request.Referrer,
            ReferrerPolicy = ReferrerPolicy.StrictOriginWhenCrossOrigin,
        };

        var policy = new FetchPolicy
        {
            AllowedSchemes = ["https", "http"],
            UrlFilter = network.UrlFilter,
            MaxResponseBytes = request.MaxResponseBytes,
            MaxRedirects = request.MaxRedirects,
            // Both are URL records the transport reads through SerializeOrigin(): one decides the Origin
            // header, the other is what a same-origin credentials mode compares a hop against. Neither
            // compares a path, which is why the document's URL is the right thing to pass.
            Origin = request.Origin,
            SameOriginReference = request.Origin,
            CookieJar = network.CookieJar,
        };

        try
        {
            using var exchange = await FetchTransport
                .SendForStreamAsync(client, snapshot, policy, cancellationToken, observation)
                .ConfigureAwait(false);

            // The debt every SendForStreamAsync caller owes its observer; see FetchObservation.FinalResponse.
            observation?.FinalResponse(exchange);

            var response = exchange.Response;
            var bytes = await ReadBoundedAsync(response, request.MaxResponseBytes, cancellationToken).ConfigureAwait(false);

            // The body half of that same debt: a subresource reads its own bytes, so nothing else can hand
            // them to the observer.
            observation?.Data(bytes);
            observation?.Completed(bytes.Length);

            var status = (int) response.StatusCode;
            var final = exchange.Url.Serialize(excludeFragment: true);

            if (!response.IsSuccessStatusCode)
            {
                // https://html.spec.whatwg.org/multipage/webappapis.html#fetch-a-classic-script step 5: an
                // "ok" status is what makes the fetch a success; anything else is a network error to the
                // element that asked, whatever bytes came with it.
                throw new SubresourceFetchException(
                    final,
                    "'" + final + "' answered " + status.ToString(System.Globalization.CultureInfo.InvariantCulture) + ".");
            }

            return new FetchedSubresource(bytes, ContentTypeOf(response), final, status);
        }
        catch (SubresourceFetchException)
        {
            throw;
        }
        catch (OperationCanceledException)
        {
            observation?.Failed("The load was cancelled.", null);
            throw;
        }
        catch (Exception exception)
        {
            observation?.Failed(exception.Message, exception);
            throw new SubresourceFetchException(url, "'" + url + "' could not be loaded: " + exception.Message, exception);
        }
    }
#pragma warning restore JINT0002

    private static string? ContentTypeOf(HttpResponseMessage response)
        => response.Content.Headers.TryGetValues("Content-Type", out var values)
            ? string.Join(", ", values)
            : null;

    /// <summary>Reads the body, refusing one that grows past the cap rather than buffering it first.</summary>
    private static async Task<byte[]> ReadBoundedAsync(HttpResponseMessage response, long maxBytes, CancellationToken cancellationToken)
    {
        if (response.Content.Headers.ContentLength is { } declared && declared > maxBytes)
        {
            throw new SubresourceFetchException("", "the resource is larger than the " + Describe(maxBytes) + " a page may load.");
        }

#pragma warning disable CA2007 // The await is on a stream this method owns; there is no context to capture.
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
#pragma warning restore CA2007

        var buffer = new MemoryStream();
        var chunk = new byte[16 * 1024];

        while (true)
        {
            var read = await stream.ReadAsync(chunk, cancellationToken).ConfigureAwait(false);
            if (read == 0)
            {
                break;
            }

            if (buffer.Length + read > maxBytes)
            {
                throw new SubresourceFetchException("", "the resource exceeded the " + Describe(maxBytes) + " a page may load.");
            }

            buffer.Write(chunk, 0, read);
        }

        return buffer.ToArray();
    }

    private static string Describe(long bytes)
        => bytes.ToString(System.Globalization.CultureInfo.InvariantCulture) + " bytes";
}
