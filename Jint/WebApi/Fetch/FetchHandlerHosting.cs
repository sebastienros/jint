#if NET8_0_OR_GREATER
using System.Net;
using System.Net.Http;
using Jint.Native;
using Jint.Runtime;
using Jint.WebApi.Abort;
using Jint.WebApi.Url.Parsing;

namespace Jint.WebApi.Fetch;

/// <summary>
/// The inbound half of the fetch object model: an <see cref="HttpRequestMessage"/> becomes the
/// <c>Request</c> a handler is called with, and the <c>Response</c> it answers becomes an
/// <see cref="HttpResponseMessage"/> the host can send.
/// </summary>
/// <remarks>
/// <para>
/// This is the mirror image of <see cref="FetchTransport"/>, over the same objects, and the differences are
/// all in who is being protected. Outbound, a malformed value comes from a script and becomes a
/// <c>TypeError</c> it can catch; inbound, a malformed value comes from the <b>host</b> and becomes an
/// <see cref="InvalidOperationException"/> naming what to fix, because a host bug that quietly produced a
/// half-built <c>Request</c> would surface as a script bug somewhere else entirely.
/// </para>
/// <para>
/// <b>Everything here runs on the engine's thread</b>, and none of it runs script — the objects are built and
/// read through their internal state rather than through their prototypes' accessors, so a script that
/// replaced <c>Response.prototype.status</c> changes what other script sees and not what the host sends.
/// </para>
/// </remarks>
internal static class FetchHandlerHosting
{
    /// <summary>
    /// The headers the host's own HTTP stack owns rather than the handler. A framing header copied out of a
    /// <c>Response</c> would describe the body the script <i>said</i> it was sending rather than the one it
    /// actually is, which is a response-splitting primitive when the two disagree; the
    /// <see cref="HttpContent"/> computes its own length, and the transfer coding belongs to the connection.
    /// </summary>
    private static readonly string[] _hostOwnedResponseHeaders = ["content-length", "transfer-encoding"];

    /// <summary>
    /// Builds the <c>Request</c> the handler is called with.
    /// </summary>
    /// <remarks>
    /// The URL is required to be absolute, because the Fetch Standard's request URL is absolute and this
    /// engine has no document to resolve a relative one against — the same reason
    /// <c>new Request('/a')</c> is a <c>TypeError</c> in script.
    /// </remarks>
    /// <param name="engine">The engine the request is built for.</param>
    /// <param name="realm">The principal realm, whose intrinsics the request's prototypes come from.</param>
    /// <param name="message">The host's request.</param>
    /// <param name="body">The request body, already read; <see langword="null"/> when there is no content.</param>
    /// <exception cref="InvalidOperationException">The message cannot be expressed as a <c>Request</c>.</exception>
    internal static JsRequest CreateRequest(Engine engine, Realm realm, HttpRequestMessage message, byte[]? body)
    {
        var method = message.Method.Method;
        if (!FetchValues.IsMethod(method))
        {
            Throw.InvalidOperationException($"The request method '{method}' is not an HTTP token, so it cannot be given to a fetch handler.");
        }

        // Normalized exactly as the Request constructor normalizes it, so a handler comparing
        // request.method against 'GET' sees the same value however the host spelled it.
        method = FetchValues.NormalizeMethod(method);

        var uri = message.RequestUri;
        if (uri is null)
        {
            Throw.InvalidOperationException("The request has no RequestUri. A fetch handler is called with a Request, whose URL is absolute — set HttpRequestMessage.RequestUri to the absolute URL the client asked for.");
        }

        if (!uri.IsAbsoluteUri)
        {
            Throw.InvalidOperationException($"The request URI '{uri.OriginalString}' is relative. A fetch handler is called with a Request, whose URL is absolute — build the absolute URL from your server's scheme, host and path first.");
        }

        var url = UrlParser.Parse(uri.AbsoluteUri);
        if (url is null)
        {
            Throw.InvalidOperationException($"The request URI '{uri.AbsoluteUri}' is not a URL the WHATWG URL parser accepts, so it cannot become a Request's URL.");
        }

        if (url.IncludesCredentials)
        {
            // The same refusal the Request constructor makes, and for the same reason: a URL carrying a
            // username and password hands the script a credential it can echo into a log or a Referer.
            Throw.InvalidOperationException($"The request URI '{uri.AbsoluteUri}' carries credentials, which a Request may not. Move them into an Authorization header.");
        }

        var list = new HeaderList();
        Collect(list, message.Headers);
        if (message.Content is not null)
        {
            // Content headers are part of the one header list the Fetch Standard has — the split into request
            // headers and content headers is System.Net.Http's, not the standard's, so a handler reading
            // request.headers.get('content-type') has to find it.
            Collect(list, message.Content.Headers);
        }

        var headers = realm.Intrinsics.Headers.CreateInstance(list);
        headers.List.Guard = HeadersGuard.Request;

        var request = new JsRequest(engine, headers)
        {
            _prototype = realm.Intrinsics.Request.PrototypeObject,
            Method = method,
            Url = url,
            Redirect = JsRequest.RedirectFollow,

            // A Request's signal is never null. Nothing aborts this one: an inbound request has no
            // client-disconnect channel here, so it exists to be forwarded to an outbound fetch and to be
            // listened to, not to fire.
            Signal = new JsAbortSignal(engine, realm) { _prototype = realm.Intrinsics.AbortSignal.PrototypeObject },
        };

        // A zero-byte content is a null body rather than an empty one, which is what makes a GET carrying an
        // empty stream — the ordinary shape an ASP.NET Core host produces — behave like a GET with no body at
        // all: consumable any number of times, and bodyUsed never flipping.
        if (body is { Length: > 0 })
        {
            request.Body = new ReadOnlyMemory<byte>(body);
        }

        return request;
    }

    /// <summary>
    /// Turns the <c>Response</c> a handler answered with into the message the host sends.
    /// </summary>
    /// <remarks>
    /// The body is taken without consuming it, so the script's own <c>bodyUsed</c> is untouched: the bytes are
    /// immutable and buffered, and flipping a flag the host's read cannot be observed through would only
    /// surprise a handler that logs its own response afterwards.
    /// </remarks>
    /// <exception cref="InvalidOperationException">
    /// The value is not a <c>Response</c>, or is one that has no wire representation.
    /// </exception>
    internal static HttpResponseMessage CreateResponse(JsValue result)
    {
        if (result is not JsResponse response)
        {
            Throw.InvalidOperationException($"The fetch handler must answer with a Response or a promise of one, but it produced {Throw.SafeToDisplayString(result)}.");
            return null!;
        }

        if (response.Kind == ResponseType.Error)
        {
            // https://fetch.spec.whatwg.org/#dom-response-error — a network error, whose status is 0. It is
            // what fetch produces when a request could not be made at all, and there is no status line that
            // means that.
            Throw.InvalidOperationException("The fetch handler answered with Response.error(), which represents a network error and has no HTTP representation. Answer with a real response — new Response(null, { status: 502 }) — and let the host decide what a failure looks like on the wire.");
        }

        var message = new HttpResponseMessage((HttpStatusCode) response.Status);

        // Left null for an empty statusText so that the framework's own reason phrase for the status is used;
        // an empty string would send an empty reason phrase, which is legal but is not what the script asked
        // for by leaving it out.
        if (response.StatusText.Length > 0)
        {
            message.ReasonPhrase = response.StatusText;
        }

        HttpContent? content = response.Body is { } body ? new ByteArrayContent(body.ToArray()) : null;

        foreach (var header in response.Headers.List.Entries)
        {
            if (IsHostOwned(header.LowerName))
            {
                continue;
            }

            // The same split FetchTransport performs in the other direction: the response-header collection
            // refuses a content header, and that refusal is how a content header is recognized. Both calls
            // are TryAddWithoutValidation, because the header list has already been validated against the
            // Fetch Standard's own grammar — which is stricter than System.Net.Http's about CR and LF and is
            // what the script was measured against.
            if (message.Headers.TryAddWithoutValidation(header.LowerName, header.Value))
            {
                continue;
            }

            // A content header on a response with no body still has to go somewhere, so an empty content is
            // created to carry it rather than the header being dropped.
            content ??= new ByteArrayContent([]);
            content.Headers.TryAddWithoutValidation(header.LowerName, header.Value);
        }

        if (content is not null)
        {
            message.Content = content;
        }

        return message;
    }

    private static bool IsHostOwned(string lowerName)
    {
        foreach (var name in _hostOwnedResponseHeaders)
        {
            if (string.Equals(lowerName, name, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Every value of every header becomes its own entry, so that a request carrying a header twice is not
    /// folded into one — the same collection <see cref="FetchTransport"/> performs for a response.
    /// </summary>
    /// <remarks>
    /// A name or value the Fetch Standard's grammar refuses is a host bug rather than something to sanitize:
    /// a value carrying CR or LF is exactly the request-splitting shape the grammar exists to stop, and
    /// silently trimming it would hand the handler a header that is not the one that arrived.
    /// </remarks>
    private static void Collect(HeaderList list, System.Net.Http.Headers.HttpHeaders source)
    {
        foreach (var header in source.NonValidated)
        {
            if (!HeaderList.IsName(header.Key))
            {
                Throw.InvalidOperationException($"The request header name '{header.Key}' is not an HTTP token, so it cannot become a Request header.");
            }

            foreach (var value in header.Value)
            {
                var normalized = HeaderList.Normalize(value);
                if (!HeaderList.IsValue(normalized))
                {
                    Throw.InvalidOperationException($"The value of request header '{header.Key}' contains a NUL, CR or LF, so it cannot become a Request header.");
                }

                list.Append(header.Key, normalized);
            }
        }
    }
}
#endif
