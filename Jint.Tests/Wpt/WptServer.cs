#if NET8_0_OR_GREATER
#nullable enable

using System.Collections.Concurrent;
using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace Jint.Tests.Wpt;

/// <summary>
/// The driver's stand-in for <c>wptserve</c>: an in-process HTTP/1.1 origin on the loopback interface,
/// serving the vendored tree and a hand-written C# port of the handful of <c>.py</c> handlers the
/// server-backed suites ask for.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why there is a server at all.</b> Thirteen rows of <c>WptTestRunner._notVendored</c> read "needs a wpt
/// server", and a file that needs one usually cannot produce a test report at all — so it lands in the
/// not-vendored table rather than the exclusion table, and the per-test <c>NeedsWptServer</c> category badly
/// understates the cost. What those files want is narrow: a static file, a header echo, a status code, a
/// redirect, a trickled body. Not a browser, not a DOM.
/// </para>
/// <para>
/// <b>Why it is not <c>wptserve</c> itself.</b> Deno's runner drives the real Python server against an
/// upstream checkout; that would put a Python dependency in a .NET test suite's CI and give up the
/// vendored-and-byte-verified model that <c>Vendor/README.md</c> depends on. This serves the <i>vendored</i>
/// corpus — <see cref="WptCorpus"/>, the same bytes every other suite reads — so nothing about provenance
/// changes, and the only divergence is the handlers, which are documented one by one on the methods below.
/// </para>
/// <para>
/// <b>Why a raw <see cref="TcpListener"/> rather than <c>HttpListener</c> or Kestrel.</b> Three reasons, and
/// each of them is a property the suites actually need. It binds port <b>0</b> and is told which port it got,
/// so a run can never collide with anything else on the machine — <c>HttpListener</c> takes a URL prefix and
/// has no ephemeral form, so it would need a port picked in advance and a race to go with it. It needs no
/// URL ACL, which <c>HttpListener</c> does for anything but <c>localhost</c> on Windows, and no
/// <c>Microsoft.AspNetCore.App</c> framework reference, which Kestrel does. And it owns the bytes on the
/// wire, which is what lets <c>trickle.py</c> dribble a body out over a real socket.
/// </para>
/// <para>
/// <b>Every response closes its connection.</b> <c>Connection: close</c> throughout: it makes the body's end
/// unambiguous without a chunked encoder, and it is what lets <see cref="Trickle"/> stream by simply writing
/// and flushing. The corpus makes a few hundred requests in total, so the connection churn does not matter.
/// </para>
/// <para>
/// <b>One server for the whole test run.</b> <see cref="Instance"/> is created on first use and lives until
/// the process exits — starting one per theory case would pay an accept loop per file for nothing. It is
/// therefore reached from several test threads at once and everything it holds is either immutable or a
/// <see cref="ConcurrentDictionary{TKey,TValue}"/>; the one piece of cross-request state, wptserve's
/// <i>stash</i>, is keyed by a token the corpus generates per test.
/// </para>
/// </remarks>
internal sealed class WptServer : IDisposable
{
    private static readonly Lazy<WptServer> _instance = new(static () => new WptServer(), LazyThreadSafetyMode.ExecutionAndPublication);

    /// <summary>The server every server-backed suite runs against, started on first use.</summary>
    internal static WptServer Instance => _instance.Value;

    private readonly TcpListener _listener;
    private readonly CancellationTokenSource _stopping = new();

    /// <summary>
    /// wptserve's <c>request.server.stash</c>: a per-token dictionary that outlives one request, which is how
    /// <c>redirect.py</c> counts the hops of a redirect chain and how <c>clean-stash.py</c> resets it.
    /// </summary>
    private readonly ConcurrentDictionary<string, int> _stash = new(StringComparer.Ordinal);

    private WptServer()
    {
        _listener = new TcpListener(IPAddress.Loopback, 0);
        _listener.Start();
        Port = ((IPEndPoint) _listener.LocalEndpoint).Port;
        _ = Task.Run(AcceptAsync);
    }

    internal int Port { get; }

    /// <summary>The origin the suites fetch from, e.g. <c>http://127.0.0.1:51234</c>.</summary>
    internal string Origin => "http://127.0.0.1:" + Port.ToString(CultureInfo.InvariantCulture);

    /// <summary>
    /// The absolute URL a vendored path is served at. This is also what a file's <c>location.href</c> is set
    /// to, so <c>fetch(location.href)</c> reaches the file's own bytes exactly as it does upstream.
    /// </summary>
    internal string UrlFor(string path) => Origin + "/" + path;

    /// <summary>
    /// Whether a URL is one this server would answer. The driver installs it as
    /// <c>Options.WebApi.Fetch.UrlFilter</c>, which is what keeps the harness's oldest promise true: a suite
    /// still cannot open a socket to anything but this loopback port, on the first hop and on every redirect.
    /// </summary>
    internal bool Owns(Uri uri)
        => uri.IsLoopback
            && uri.Port == Port
            && string.Equals(uri.Scheme, "http", StringComparison.Ordinal);

    public void Dispose()
    {
        _stopping.Cancel();
        _listener.Stop();
        _stopping.Dispose();
    }

    private async Task AcceptAsync()
    {
        while (!_stopping.IsCancellationRequested)
        {
            TcpClient client;
            try
            {
                client = await _listener.AcceptTcpClientAsync(_stopping.Token).ConfigureAwait(false);
            }
            catch (Exception)
            {
                return;
            }

            _ = Task.Run(() => ServeAsync(client));
        }
    }

    private async Task ServeAsync(TcpClient client)
    {
        using (client)
        {
            try
            {
                client.NoDelay = true;
                using var stream = client.GetStream();
                var request = await WptServerRequest.ReadAsync(stream, _stopping.Token).ConfigureAwait(false);
                if (request is not null)
                {
                    await RespondAsync(stream, request, _stopping.Token).ConfigureAwait(false);
                }
            }
            catch (Exception)
            {
                // A client that hung up mid-request — which is exactly what `response-cancel-stream.any.js`
                // and the abort suites do on purpose — is not a server error.
            }
        }
    }

    private Task RespondAsync(Stream stream, WptServerRequest request, CancellationToken token) => request.Path switch
    {
        "" => WriteAsync(stream, new WptServerResponse(200, "OK", [("content-type", "text/plain")], []), request, token),
        "fetch/api/resources/inspect-headers.py" => WriteAsync(stream, InspectHeaders(request), request, token),
        "fetch/api/resources/status.py" => WriteAsync(stream, Status(request), request, token),
        "fetch/api/resources/method.py" => WriteAsync(stream, Method(request), request, token),
        "fetch/api/resources/redirect.py" => WriteAsync(stream, Redirect(request), request, token),
        "fetch/api/resources/redirect-empty-location.py" => WriteAsync(stream, RedirectEmptyLocation(), request, token),
        "fetch/api/resources/clean-stash.py" => WriteAsync(stream, CleanStash(request), request, token),
        "fetch/api/resources/trickle.py" => Trickle(stream, request, token),
        _ => WriteAsync(stream, StaticFile(request), request, token),
    };

    // ------------------------------------------------------------------ handlers

    /// <summary>
    /// <c>fetch/api/resources/inspect-headers.py</c>: echoes the named request headers back as
    /// <c>x-request-&lt;name&gt;</c> response headers.
    /// </summary>
    /// <remarks>
    /// The <c>cors</c> branch of the upstream handler is deliberately not ported: Jint has no CORS model, so
    /// every file that passes <c>?cors</c> is parked for that reason rather than for want of a server, and a
    /// half-implemented <c>Access-Control-Allow-Origin</c> would only make such a file look runnable.
    /// </remarks>
    private static WptServerResponse InspectHeaders(WptServerRequest request)
    {
        var headers = new List<(string Name, string Value)>();

        if (request.Query.TryGetValue("headers", out var requested))
        {
            foreach (var name in requested.Split('|'))
            {
                if (name.Length > 0 && request.TryGetHeader(name, out var value))
                {
                    headers.Add(("x-request-" + name.ToLowerInvariant(), value));
                }
            }
        }

        headers.Add(("content-type", "text/plain"));
        return new WptServerResponse(200, "OK", headers, []);
    }

    /// <summary>
    /// <c>fetch/api/resources/status.py</c>: the status code, status text, content type and body all come
    /// from the query string.
    /// </summary>
    private static WptServerResponse Status(WptServerRequest request)
    {
        var code = request.QueryInt("code", 200);
        var text = request.Query.TryGetValue("text", out var t) ? t : "OMG";
        var content = request.Query.TryGetValue("content", out var c) ? c : "";
        var type = request.Query.TryGetValue("type", out var ty) ? ty : "";

        // The content comes out of the query string, where wptserve percent-decodes it to *bytes* and writes
        // those. `text-utf8.any.js` passes `%fe%ff…` precisely to see a body that is not valid UTF-8, so the
        // round trip has to be byte-exact — which is what WptServerRequest's Latin-1 decoding preserves.
        return new WptServerResponse(code, text,
            [("content-type", type), ("x-request-method", request.Method)], Bytes(content));
    }

    /// <summary>
    /// <c>fetch/api/resources/method.py</c>: echoes the request's method, its four body-ish headers and its
    /// body, which is what lets <c>redirect-method.any.js</c> see what survived a redirect.
    /// </summary>
    /// <remarks>The <c>cors</c> branch is not ported, for the reason given on <see cref="InspectHeaders"/>.</remarks>
    private static WptServerResponse Method(WptServerRequest request)
    {
        var headers = new List<(string, string)>
        {
            ("x-request-method", request.Method),
            ("x-request-content-type", request.HeaderOr("content-type", "NO")),
            ("x-request-content-length", request.HeaderOr("content-length", "NO")),
            ("x-request-content-encoding", request.HeaderOr("content-encoding", "NO")),
            ("x-request-content-language", request.HeaderOr("content-language", "NO")),
            ("x-request-content-location", request.HeaderOr("content-location", "NO")),
        };

        return new WptServerResponse(200, "OK", headers, Bytes(request.Body));
    }

    /// <summary>
    /// <c>fetch/api/resources/redirect.py</c>, reduced to the half that does not involve CORS.
    /// </summary>
    /// <remarks>
    /// <para>
    /// What is ported: <c>redirect_status</c>, <c>location</c>, the <c>simple</c> flag, the query-preserving
    /// rewrite of a relative or <c>http(s)</c> location — including the <c>&amp;count=</c> suffix that makes
    /// the URL change on every hop, without which a browser's own redirect loop would short-circuit — and the
    /// <c>token</c>/<c>max_count</c> stash pair that <c>redirect-count.any.js</c> counts hops with.
    /// </para>
    /// <para>
    /// What is not: the <c>Access-Control-*</c> headers, the <c>OPTIONS</c> preflight branch, and
    /// <c>redirect_referrerpolicy</c>. All three exist for the CORS and referrer suites, which are parked for
    /// missing engine features rather than for want of a server; a <c>delay</c> is not ported because no
    /// vendored file passes one.
    /// </para>
    /// </remarks>
    private WptServerResponse Redirect(WptServerRequest request)
    {
        var status = request.QueryInt("redirect_status", 302);
        var headers = new List<(string, string)>
        {
            ("content-type", "text/plain"),
            ("cache-control", "no-cache"),
            ("pragma", "no-cache"),
        };

        var count = 0;
        request.Query.TryGetValue("token", out var token);
        if (token is not null)
        {
            count = _stash.TryGetValue(token, out var stashed) ? stashed : 0;
        }

        count++;

        if (request.Query.TryGetValue("location", out var location))
        {
            if (!request.Query.ContainsKey("simple"))
            {
                var scheme = SchemeOf(location);
                if (scheme is "" or "http" or "https")
                {
                    location += location.Contains('?') ? "&" : "?";
                    location += request.RawQueryPairs(count);
                }
            }

            headers.Add(("location", location));
        }

        if (token is not null)
        {
            _stash[token] = count;

            if (request.Query.TryGetValue("max_count", out var max)
                && int.TryParse(max, NumberStyles.Integer, CultureInfo.InvariantCulture, out var maxCount)
                && count > maxCount)
            {
                // Upstream returns a bare string, which wptserve turns into a 200 with that body. The "-1" is
                // its own: the hop that reports the count is not itself a redirection.
                return new WptServerResponse(200, "OK", [("content-type", "text/plain")],
                    Bytes((count - 1).ToString(CultureInfo.InvariantCulture)));
            }
        }

        return new WptServerResponse(status, StatusTextFor(status), headers, []);
    }

    /// <summary><c>fetch/api/resources/redirect-empty-location.py</c>: a 302 whose <c>Location</c> is empty.</summary>
    private static WptServerResponse RedirectEmptyLocation()
        => new(302, "Found", [("location", "")], []);

    /// <summary><c>fetch/api/resources/clean-stash.py</c>: drops one token's stash entry and answers 200.</summary>
    private WptServerResponse CleanStash(WptServerRequest request)
    {
        if (request.Query.TryGetValue("token", out var token))
        {
            _stash.TryRemove(token, out _);
        }

        return new WptServerResponse(200, "OK", [("content-type", "text/plain")], Bytes("1"));
    }

    /// <summary>
    /// <c>fetch/api/resources/trickle.py</c>: <c>count</c> lines of <c>TEST_TRICKLE</c>, one every <c>ms</c>
    /// milliseconds, with the same delay before the headers and before the first line.
    /// </summary>
    /// <remarks>
    /// This is the handler that decides the shape of the whole server. It has to reach the socket between
    /// chunks, which is why every response here ends its connection instead of framing itself: the body is
    /// simply what was written before the close, so a partial read is a partial read and cancelling one — the
    /// point of <c>response-cancel-stream.any.js</c> — really does hang up on a server mid-write.
    /// </remarks>
    private static async Task Trickle(Stream stream, WptServerRequest request, CancellationToken token)
    {
        var delay = TimeSpan.FromMilliseconds(request.QueryInt("ms", 500));
        var count = request.QueryInt("count", 50);

        await Task.Delay(delay, token).ConfigureAwait(false);

        var head = new StringBuilder("HTTP/1.1 200 OK\r\nconnection: close\r\n");
        if (!request.Query.ContainsKey("notype"))
        {
            head.Append("content-type: text/plain\r\n");
        }

        head.Append("\r\n");
        await WriteRawAsync(stream, head.ToString(), token).ConfigureAwait(false);
        await Task.Delay(delay, token).ConfigureAwait(false);

        for (var i = 0; i < count; i++)
        {
            await WriteRawAsync(stream, "TEST_TRICKLE\n", token).ConfigureAwait(false);
            await Task.Delay(delay, token).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Anything else is a file out of the vendored corpus, served with a content type derived from its
    /// extension. A path the corpus does not hold is a 404 — deliberately a real HTTP failure rather than a
    /// CLR exception, because unlike the shim's resource reader this one is answering a request the corpus
    /// itself composed, and several suites fetch a URL precisely to see it fail.
    /// </summary>
    private static WptServerResponse StaticFile(WptServerRequest request)
    {
        if (!WptCorpus.Contains(request.Path))
        {
            return new WptServerResponse(404, "Not Found", [("content-type", "text/plain")], []);
        }

        var extension = request.Path.AsSpan(request.Path.LastIndexOf('.') + 1);
        var type = extension switch
        {
            "js" => "text/javascript",
            "json" => "application/json",
            "html" => "text/html",
            _ => "text/plain",
        };

        // The corpus is held as text, so its bytes are the UTF-8 of that text — which is how it was vendored.
        return new WptServerResponse(200, "OK", [("content-type", type)],
            Encoding.UTF8.GetBytes(WptCorpus.Read(request.Path)));
    }

    // ------------------------------------------------------------------ wire

    private static async Task WriteAsync(Stream stream, WptServerResponse response, WptServerRequest request, CancellationToken token)
    {
        var body = response.Body;

        // A HEAD response carries the headers a GET would and no body — which is what
        // `response-null-body.any.js` asks about — and 204/205/304 are bodiless by RFC 9110. Content-Length is
        // still what a GET would have said, exactly as wptserve reports it.
        var bodiless = string.Equals(request.Method, "HEAD", StringComparison.Ordinal)
            || response.Status is 204 or 205 or 304;

        var head = new StringBuilder()
            .Append("HTTP/1.1 ").Append(response.Status.ToString(CultureInfo.InvariantCulture)).Append(' ')
            .Append(response.StatusText).Append("\r\n");

        foreach (var (name, value) in response.Headers)
        {
            head.Append(name).Append(": ").Append(value).Append("\r\n");
        }

        head.Append("content-length: ").Append(body.Length.ToString(CultureInfo.InvariantCulture)).Append("\r\n");
        head.Append("connection: close\r\n\r\n");

        await WriteRawAsync(stream, head.ToString(), token).ConfigureAwait(false);

        if (!bodiless && body.Length > 0)
        {
            await stream.WriteAsync(body, token).ConfigureAwait(false);
            await stream.FlushAsync(token).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// One char to one byte, which is what a header value and a query-decoded body both are on the wire — see
    /// <see cref="WptServerRequest"/> on why the whole server holds wire bytes as Latin-1 text.
    /// </summary>
    private static byte[] Bytes(string wireText) => Encoding.Latin1.GetBytes(wireText);

    private static async Task WriteRawAsync(Stream stream, string text, CancellationToken token)
    {
        var bytes = Encoding.Latin1.GetBytes(text);
        await stream.WriteAsync(bytes, token).ConfigureAwait(false);
        await stream.FlushAsync(token).ConfigureAwait(false);
    }

    private static string SchemeOf(string url)
    {
        var colon = url.IndexOf(':');
        if (colon <= 0)
        {
            return "";
        }

        for (var i = 0; i < colon; i++)
        {
            var c = url[i];
            if (!char.IsAsciiLetterOrDigit(c) && c is not ('+' or '-' or '.'))
            {
                return "";
            }
        }

        return url.Substring(0, colon).ToLowerInvariant();
    }

    private static string StatusTextFor(int status) => status switch
    {
        301 => "Moved Permanently",
        302 => "Found",
        303 => "See Other",
        307 => "Temporary Redirect",
        308 => "Permanent Redirect",
        _ => "OK",
    };
}

/// <summary>
/// One request off the wire: the method, the path, the decoded query, the headers and the body.
/// </summary>
/// <remarks>
/// <b>Everything here is Latin-1 text, one char to one byte, and that is not a shortcut.</b> wptserve works in
/// bytes: <c>request.GET.first(b"content")</c> is a percent-decoded <i>byte</i> string that its handlers write
/// straight back out. <c>fetch/api/basic/text-utf8.any.js</c> depends on it — it asks for
/// <c>content=%fe%ff%4e%09…</c>, a UTF-16BE body served as <c>text/plain;charset=UTF-8</c>, to see what
/// <c>Response.text()</c> makes of bytes that are not valid UTF-8. Decoding the query as UTF-8 would replace
/// those bytes with U+FFFD before the engine ever saw them, and the file would assert against the server's
/// mistake instead of the engine's decoder.
/// </remarks>
internal sealed class WptServerRequest
{
    private readonly List<(string Name, string Value)> _headers;
    private readonly List<(string Name, string Value)> _queryPairs;

    private WptServerRequest(
        string method,
        string path,
        List<(string, string)> queryPairs,
        List<(string, string)> headers,
        string body)
    {
        Method = method;
        Path = path;
        _queryPairs = queryPairs;
        _headers = headers;
        Body = body;

        var query = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var (name, value) in queryPairs)
        {
            // wptserve's `request.GET.first(...)`: the first occurrence wins.
            query.TryAdd(name, value);
        }

        Query = query;
    }

    internal string Method { get; }

    /// <summary>The request target with its leading slash and its query removed: <c>fetch/api/resources/top.txt</c>.</summary>
    internal string Path { get; }

    internal IReadOnlyDictionary<string, string> Query { get; }

    internal string Body { get; }

    internal bool TryGetHeader(string name, out string value)
    {
        foreach (var (candidate, candidateValue) in _headers)
        {
            if (string.Equals(candidate, name, StringComparison.OrdinalIgnoreCase))
            {
                value = candidateValue;
                return true;
            }
        }

        value = "";
        return false;
    }

    internal string HeaderOr(string name, string fallback) => TryGetHeader(name, out var value) ? value : fallback;

    internal int QueryInt(string name, int fallback)
        => Query.TryGetValue(name, out var raw)
            && int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
                ? value
                : fallback;

    /// <summary>
    /// The query re-encoded for <c>redirect.py</c>'s query-preserving rewrite, with the hop counter appended —
    /// upstream's <c>urlencode(url_parameters) + "&amp;count=" + count</c>.
    /// </summary>
    internal string RawQueryPairs(int count)
    {
        var builder = new StringBuilder();
        foreach (var (name, value) in _queryPairs)
        {
            if (builder.Length > 0)
            {
                builder.Append('&');
            }

            Escape(builder, name);
            builder.Append('=');
            Escape(builder, value);
        }

        if (builder.Length > 0)
        {
            builder.Append('&');
        }

        return builder.Append("count=").Append(count.ToString(CultureInfo.InvariantCulture)).ToString();

        // Per byte, which is what Python's urlencode does over the byte strings it holds. Uri.EscapeDataString
        // would UTF-8-encode first, which for the Latin-1 text this class holds is a different byte string.
        static void Escape(StringBuilder into, string wireText)
        {
            foreach (var c in wireText)
            {
                if (char.IsAsciiLetterOrDigit(c) || c is '-' or '_' or '.' or '~')
                {
                    into.Append(c);
                }
                else
                {
                    into.Append('%').Append(((int) c).ToString("X2", CultureInfo.InvariantCulture));
                }
            }
        }
    }

    /// <summary>
    /// Reads one HTTP/1.1 request. Answers <see langword="null"/> for anything that is not one — a client that
    /// connected and hung up, which is what an aborted fetch looks like from here.
    /// </summary>
    internal static async Task<WptServerRequest?> ReadAsync(Stream stream, CancellationToken token)
    {
        var head = new List<byte>(1024);
        var buffer = new byte[1];

        while (head.Count < 64 * 1024)
        {
            var read = await stream.ReadAsync(buffer, token).ConfigureAwait(false);
            if (read == 0)
            {
                return null;
            }

            head.Add(buffer[0]);
            if (head.Count >= 4
                && head[head.Count - 4] == (byte) '\r' && head[head.Count - 3] == (byte) '\n'
                && head[head.Count - 2] == (byte) '\r' && head[head.Count - 1] == (byte) '\n')
            {
                break;
            }
        }

        var text = Encoding.Latin1.GetString(head.ToArray());
        var lines = text.Split("\r\n");
        var requestLine = lines[0].Split(' ');
        if (requestLine.Length < 2)
        {
            return null;
        }

        var method = requestLine[0];
        var target = requestLine[1];

        var headers = new List<(string, string)>();
        for (var i = 1; i < lines.Length; i++)
        {
            var line = lines[i];
            if (line.Length == 0)
            {
                break;
            }

            var colon = line.IndexOf(':');
            if (colon > 0)
            {
                headers.Add((line.Substring(0, colon).Trim(), line.Substring(colon + 1).Trim()));
            }
        }

        var body = await ReadBodyAsync(stream, headers, token).ConfigureAwait(false);

        var question = target.IndexOf('?');
        var path = (question < 0 ? target : target.Substring(0, question)).TrimStart('/');
        var queryPairs = ParseQuery(question < 0 ? "" : target.Substring(question + 1));

        return new WptServerRequest(method, path, queryPairs, headers, body);
    }

    private static async Task<string> ReadBodyAsync(Stream stream, List<(string Name, string Value)> headers, CancellationToken token)
    {
        var chunked = false;
        var length = 0;

        foreach (var (name, value) in headers)
        {
            if (string.Equals(name, "content-length", StringComparison.OrdinalIgnoreCase))
            {
                int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out length);
            }
            else if (string.Equals(name, "transfer-encoding", StringComparison.OrdinalIgnoreCase)
                && value.Contains("chunked", StringComparison.OrdinalIgnoreCase))
            {
                chunked = true;
            }
        }

        if (chunked)
        {
            return await ReadChunkedAsync(stream, token).ConfigureAwait(false);
        }

        if (length <= 0)
        {
            return "";
        }

        var body = new byte[length];
        var read = 0;
        while (read < length)
        {
            var got = await stream.ReadAsync(body.AsMemory(read, length - read), token).ConfigureAwait(false);
            if (got == 0)
            {
                break;
            }

            read += got;
        }

        return Encoding.Latin1.GetString(body, 0, read);
    }

    private static async Task<string> ReadChunkedAsync(Stream stream, CancellationToken token)
    {
        var body = new List<byte>();
        var single = new byte[1];

        while (true)
        {
            var size = new StringBuilder();
            while (true)
            {
                if (await stream.ReadAsync(single, token).ConfigureAwait(false) == 0)
                {
                    return Encoding.Latin1.GetString(body.ToArray());
                }

                if (single[0] == (byte) '\n')
                {
                    break;
                }

                if (single[0] != (byte) '\r')
                {
                    size.Append((char) single[0]);
                }
            }

            var semicolon = size.ToString().IndexOf(';');
            var digits = semicolon < 0 ? size.ToString() : size.ToString().Substring(0, semicolon);
            if (!int.TryParse(digits.Trim(), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var chunk) || chunk == 0)
            {
                return Encoding.Latin1.GetString(body.ToArray());
            }

            var data = new byte[chunk];
            var read = 0;
            while (read < chunk)
            {
                var got = await stream.ReadAsync(data.AsMemory(read, chunk - read), token).ConfigureAwait(false);
                if (got == 0)
                {
                    break;
                }

                read += got;
            }

            body.AddRange(data.AsSpan(0, read).ToArray());

            // The CRLF that terminates the chunk.
            await stream.ReadAsync(single, token).ConfigureAwait(false);
            await stream.ReadAsync(single, token).ConfigureAwait(false);
        }
    }

    private static List<(string, string)> ParseQuery(string query)
    {
        var pairs = new List<(string, string)>();
        if (query.Length == 0)
        {
            return pairs;
        }

        foreach (var pair in query.Split('&'))
        {
            if (pair.Length == 0)
            {
                continue;
            }

            var equals = pair.IndexOf('=');
            var name = equals < 0 ? pair : pair.Substring(0, equals);
            var value = equals < 0 ? "" : pair.Substring(equals + 1);
            pairs.Add((Decode(name), Decode(value)));
        }

        return pairs;
    }

    /// <summary>
    /// Percent-decoding to bytes, held one byte to one char. Deliberately not
    /// <see cref="Uri.UnescapeDataString(string)"/>, which decodes to UTF-8 text and would turn the
    /// deliberately-invalid sequences <c>text-utf8.any.js</c> asks for into replacement characters — see the
    /// class remarks.
    /// </summary>
    private static string Decode(string value)
    {
        var decoded = new StringBuilder(value.Length);

        for (var i = 0; i < value.Length; i++)
        {
            var c = value[i];

            if (c == '+')
            {
                decoded.Append(' ');
            }
            else if (c == '%' && i + 2 < value.Length
                && Uri.IsHexDigit(value[i + 1]) && Uri.IsHexDigit(value[i + 2]))
            {
                decoded.Append((char) int.Parse(value.AsSpan(i + 1, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture));
                i += 2;
            }
            else
            {
                decoded.Append(c);
            }
        }

        return decoded.ToString();
    }
}

/// <summary>A response the server is about to write: status, headers and a body it frames itself.</summary>
internal readonly record struct WptServerResponse(
    int Status,
    string StatusText,
    IReadOnlyList<(string Name, string Value)> Headers,
    byte[] Body);
#endif
