using System.Net.Http;
using System.Text;
using Jint.WebApi.Fetch;
using Jint.WebApi.Url.Parsing;

namespace Jint.Browser.Runtime;

/// <summary>What one document fetch produced: the markup to parse, and the response it came from.</summary>
/// <param name="Html">The markup, already decoded and — for a plain-text answer — wrapped.</param>
/// <param name="Url">The URL the document ends up with: the last hop of the redirect chain.</param>
/// <param name="Response">The response, for <see cref="Page.Response"/>.</param>
internal sealed record FetchedDocument(string Html, string Url, PageResponse Response);

/// <summary>
/// A navigation's document fetch: Jint's own engine-free fetch pipeline, driven by the page rather than by
/// a script.
/// </summary>
/// <remarks>
/// <para>
/// <b>It runs off the page loop, on whichever thread awaits it.</b> That is the whole reason it goes through
/// <c>FetchTransport</c> — the layer built to hold no engine state — rather than through a <c>fetch()</c>
/// call inside the outgoing document: the page's thread goes on pumping timers, answering requests and
/// obeying a close while the response is on its way, and the engine the new document will run in does not
/// exist yet.
/// </para>
/// <para>
/// <b>The policy is the context's, and it is re-checked per hop.</b> A redirect is followed here rather than
/// by <see cref="HttpClient"/> precisely so that <c>BrowserContextOptions.UrlFilter</c> and the scheme list
/// see every hop; a server answering <c>302 Location: http://169.254.169.254/</c> is refused at the second
/// hop and not at the socket. The first hop's filter is run by the caller, because the transport
/// deliberately does not run it twice.
/// </para>
/// <para>
/// <b>What a document may be.</b> <c>text/html</c> is parsed as markup and <c>text/plain</c> is wrapped in
/// HTML's own plain-text document — a <c>&lt;pre&gt;</c> holding the text, which is what makes
/// <c>document.body.textContent</c> the file. Anything else is refused with a
/// <see cref="NavigationFailedException"/> naming the type, because a page showing a PDF or an image as if
/// it were markup would be worse than a page saying it cannot.
/// </para>
/// </remarks>
internal static class DocumentFetch
{
#pragma warning disable JINT0002 // The observation types are the engine's preview network seam; a document fetch is a host fetch.
    internal static async Task<FetchedDocument> LoadAsync(
        PageNetwork network,
        HttpClient client,
        DocumentRequest request,
        PageNetworkRecorder? recorder,
        string loaderId,
        CancellationToken cancellationToken)
    {
        // The document's own request is addressed by the loaderId, which is what Chrome does and what every
        // client reads as "this request is the navigation": Puppeteer decides a request is a navigation by
        // comparing exactly those two strings.
        var observation = recorder?.Observe(RequestInitiator.Document, PageRequestKind.Document, loaderId);

        var headers = new List<HeaderEntry>();
        if (request.ContentType is { } contentType)
        {
            headers.Add(new HeaderEntry("content-type", contentType));
        }

        var snapshot = new FetchRequestSnapshot
        {
            Method = request.Method,
            Url = request.Url,
            Headers = headers,
            Body = request.Body,
            BodyContent = null,
            Redirect = "follow",

            // A top-level navigation always carries its cookies: it is the document's own request, and the
            // same-origin rule exists for a subresource asking on someone else's behalf.
            Credentials = JsRequest.CredentialsInclude,
            Referrer = request.Referrer,
            ReferrerPolicy = ReferrerPolicy.StrictOriginWhenCrossOrigin,
        };

        var policy = new FetchPolicy
        {
            AllowedSchemes = ["https", "http"],
            UrlFilter = network.UrlFilter,
            MaxResponseBytes = request.MaxResponseBytes,
            MaxRedirects = request.MaxRedirects,
            Origin = request.Origin,
            SameOriginReference = request.Origin,
            CookieJar = network.CookieJar,
        };

        try
        {
            using var exchange = await FetchTransport
                .SendForStreamAsync(client, snapshot, policy, cancellationToken, observation)
                .ConfigureAwait(false);

            var response = exchange.Response;
            var headerList = Collect(response);

            // The debt every SendForStreamAsync caller owes its observer; see FetchObservation.FinalResponse.
            observation?.FinalResponse(exchange, ToFetchHeaders(headerList));

            var bytes = await ReadBoundedAsync(response, request.MaxResponseBytes, cancellationToken).ConfigureAwait(false);

            // The body half of the debt FetchObservation.FinalResponse names: this method reads the bytes
            // itself, so it is the only place the observer can be handed them.
            observation?.Data(bytes);
            observation?.Completed(bytes.Length);

            var url = exchange.Url.Serialize(excludeFragment: true);
            var pageResponse = new PageResponse(
                url,
                (int) response.StatusCode,
                response.ReasonPhrase ?? "",
                headerList,
                exchange.Redirected);

            var html = Decode(bytes, pageResponse, url);
            return new FetchedDocument(html, url, pageResponse);
        }
        catch (OperationCanceledException)
        {
            observation?.Failed("The navigation was cancelled.", null);
            throw;
        }
        catch (NavigationFailedException failure)
        {
            observation?.Failed(failure.Message, failure);
            throw;
        }
        catch (Exception exception)
        {
            observation?.Failed(exception.Message, exception);
            throw new NavigationFailedException(
                request.Url.Serialize(),
                "Navigation to '" + request.Url.Serialize() + "' failed: " + exception.Message,
                exception);
        }
    }
#pragma warning restore JINT0002

    /// <summary>
    /// Reads the body, refusing one that grows past the cap rather than buffering it and then complaining.
    /// </summary>
    private static async Task<byte[]> ReadBoundedAsync(HttpResponseMessage response, long maxBytes, CancellationToken cancellationToken)
    {
        if (response.Content.Headers.ContentLength is { } declared && declared > maxBytes)
        {
            throw new NavigationFailedException(
                "",
                "The document is " + declared.ToString(System.Globalization.CultureInfo.InvariantCulture)
                + " bytes, which is more than the " + maxBytes.ToString(System.Globalization.CultureInfo.InvariantCulture)
                + " a page may load.");
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
                throw new NavigationFailedException(
                    "",
                    "The document exceeded the " + maxBytes.ToString(System.Globalization.CultureInfo.InvariantCulture)
                    + " bytes a page may load.");
            }

            buffer.Write(chunk, 0, read);
        }

        return buffer.ToArray();
    }

    /// <summary>
    /// https://html.spec.whatwg.org/multipage/browsing-the-web.html#navigate — what kind of document the
    /// response's <c>Content-Type</c> asks for, and the encoding to read it in.
    /// </summary>
    private static string Decode(byte[] bytes, PageResponse response, string url)
    {
        var declared = response.Header("content-type");
        var mime = declared is null ? null : MimeType.Parse(declared);
        var essence = mime?.Essence;

        var encoding = EncodingFor(mime, bytes);
        var text = Read(bytes, encoding);

        if (essence is null)
        {
            // No Content-Type at all. Browsers sniff; the one distinction that matters here is markup versus
            // text, and a body whose first non-space character opens a tag is markup.
            essence = LooksLikeMarkup(text) ? "text/html" : "text/plain";
        }

        if (string.Equals(essence, "text/html", StringComparison.OrdinalIgnoreCase))
        {
            return text;
        }

        if (string.Equals(essence, "text/plain", StringComparison.OrdinalIgnoreCase))
        {
            return PlainTextDocument(text);
        }

        throw new NavigationFailedException(
            url,
            "Navigation to '" + url + "' produced a '" + essence + "' response, and a page can render only "
            + "text/html and text/plain in this version. Fetch it with fetch() or XMLHttpRequest instead.");
    }

    /// <summary>
    /// HTML's plain-text document: the text inside a <c>&lt;pre&gt;</c>, so that
    /// <c>document.body.textContent</c> is the file and the DOM is a real one.
    /// </summary>
    private static string PlainTextDocument(string text)
    {
        var escaped = text
            .Replace("&", "&amp;", StringComparison.Ordinal)
            .Replace("<", "&lt;", StringComparison.Ordinal)
            .Replace(">", "&gt;", StringComparison.Ordinal);

        return "<html><head></head><body><pre>" + escaped + "</pre></body></html>";
    }

    private static bool LooksLikeMarkup(string text)
    {
        foreach (var c in text)
        {
            if (char.IsWhiteSpace(c))
            {
                continue;
            }

            return c == '<';
        }

        return false;
    }

    /// <summary>
    /// The encoding: the <c>charset</c> parameter, then a byte-order mark, then UTF-8 — which is what the
    /// encoding sniffing algorithm reduces to for a document the network answered with a type.
    /// </summary>
    private static Encoding EncodingFor(MimeType? mime, byte[] bytes)
    {
        if (mime?.GetParameter("charset") is { Length: > 0 } charset)
        {
            try
            {
                return Encoding.GetEncoding(charset);
            }
            catch (ArgumentException)
            {
                // An unknown label is ignored and the sniffing goes on, which is what the standard says.
            }
        }

        if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
        {
            return Encoding.UTF8;
        }

        if (bytes.Length >= 2 && bytes[0] == 0xFF && bytes[1] == 0xFE)
        {
            return Encoding.Unicode;
        }

        if (bytes.Length >= 2 && bytes[0] == 0xFE && bytes[1] == 0xFF)
        {
            return Encoding.BigEndianUnicode;
        }

        return Encoding.UTF8;
    }

    private static string Read(byte[] bytes, Encoding encoding)
    {
        var text = encoding.GetString(bytes);

        // A UTF-8 BOM decodes to U+FEFF, which would become a text node before <html> and push the parser
        // into a shape no page intends.
        return text.Length != 0 && text[0] == '﻿' ? text[1..] : text;
    }

    private static List<PageHeader> Collect(HttpResponseMessage response)
    {
        var headers = new List<PageHeader>();
        Collect(headers, response.Headers);
        Collect(headers, response.Content.Headers);
        return headers;
    }

    private static void Collect(List<PageHeader> headers, System.Net.Http.Headers.HttpHeaders source)
    {
        foreach (var header in source.NonValidated)
        {
            foreach (var value in header.Value)
            {
                headers.Add(new PageHeader(HeaderList.Lowercase(header.Key), value));
            }
        }
    }

#pragma warning disable JINT0002 // The observation types are the engine's preview network seam.
    private static FetchHeader[] ToFetchHeaders(List<PageHeader> headers)
    {
        var converted = new FetchHeader[headers.Count];
        for (var i = 0; i < headers.Count; i++)
        {
            converted[i] = new FetchHeader(headers[i].Name, headers[i].Value);
        }

        return converted;
    }
#pragma warning restore JINT0002
}

/// <summary>One document fetch, as the navigator describes it.</summary>
/// <param name="Url">The URL to ask for.</param>
/// <param name="Method">The HTTP method — <c>GET</c>, or a form's <c>POST</c>.</param>
/// <param name="Body">The request body, for a form submission.</param>
/// <param name="ContentType">The body's <c>Content-Type</c>, when there is a body.</param>
/// <param name="Referrer">The document this navigation started from, or <see langword="null"/>.</param>
/// <param name="Origin">The origin the request reports, or <see langword="null"/> for an opaque one.</param>
/// <param name="MaxResponseBytes">The ceiling on the document's size.</param>
/// <param name="MaxRedirects">How many hops the chain may follow.</param>
internal sealed record DocumentRequest(
    UrlRecord Url,
    string Method,
    ReadOnlyMemory<byte>? Body,
    string? ContentType,
    UrlRecord? Referrer,
    UrlRecord? Origin,
    long MaxResponseBytes,
    int MaxRedirects);
