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
/// <b>There are two halves, and this file is one of them.</b> Everything here answers a named <c>.py</c>
/// file; <see cref="WptServerFiles"/> is what applies to <i>every</i> file — the content-type table, the
/// <c>.headers</c> sidecars, the <c>.sub.</c> template language. The split is what the two lanes need: an
/// <c>.any.js</c> file is handed to an engine directly and only ever asks this server for a <c>fetch</c>,
/// while a <c>.html</c> test is <i>loaded</i> from it and so meets all of the second half at once.
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

    /// <summary>
    /// What <c>/resources/testharnessreport.js</c> answers with. It is Jint's own file rather than a
    /// vendored one — see <c>Prelude/testharnessreport.js</c> for why — and it is a constructor parameter so
    /// that the browser lane can overlay it per server without a vendored file changing.
    /// </summary>
    private readonly string _harnessReport;

    /// <summary>
    /// What <c>/resources/testdriver-vendor.js</c> answers with, or <see langword="null"/> for the vendored
    /// file — which is upstream's, and is empty.
    /// </summary>
    /// <remarks>
    /// The second slot upstream ships for a vendor to fill: <c>testdriver.js</c> declares every automation
    /// call as a member of <c>window.test_driver_internal</c> that throws "not implemented by
    /// testdriver-vendor.js", and this file is where an implementation replaces them. Unlike
    /// <c>testharnessreport.js</c> the stub <i>is</i> vendored, because it is a real (empty) file in the tree
    /// rather than one whose whole content is a placeholder, so the default here is to serve it.
    /// </remarks>
    private readonly string? _testDriverVendor;

    /// <summary>
    /// Starts a server on an ephemeral loopback port.
    /// </summary>
    /// <param name="harnessReportOverlay">
    /// What to answer <c>/resources/testharnessreport.js</c> with, or <see langword="null"/> for the stub in
    /// <c>Prelude/</c>. The browser lane passes the script that posts a page's results back to its driver;
    /// upstream's own file exists to be replaced exactly this way.
    /// </param>
    /// <param name="testDriverVendorOverlay">
    /// What to answer <c>/resources/testdriver-vendor.js</c> with, or <see langword="null"/> for the vendored
    /// empty file. The browser lane passes the script that maps <c>test_driver</c> onto the same dispatcher
    /// the <c>Input</c> domain reaches.
    /// </param>
    internal WptServer(string? harnessReportOverlay = null, string? testDriverVendorOverlay = null)
    {
        _harnessReport = harnessReportOverlay ?? WptCorpus.HarnessReport;
        _testDriverVendor = testDriverVendorOverlay;
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

                    // A graceful close, in two steps, because a response whose length is delimited by the
                    // close itself — which every .asis file is, having neither Content-Length nor a chunked
                    // encoding — is the one shape an abrupt one corrupts. Disposing the socket while bytes
                    // are still unacknowledged can end the connection with a reset rather than a FIN, and
                    // the client then reports a failed read where the body should have ended. It showed up
                    // as one row of xhr/getresponseheader.any.js failing about one run in three, a different
                    // row each time.
                    //
                    // So: FIN first, then wait for the peer to close its own half, bounded so that a client
                    // which never does costs one connection and not the run.
                    client.Client.Shutdown(SocketShutdown.Send);
                    await DrainUntilPeerClosesAsync(stream).ConfigureAwait(false);
                }
            }
            catch (Exception)
            {
                // A client that hung up mid-request — which is exactly what `response-cancel-stream.any.js`
                // and the abort suites do on purpose — is not a server error.
            }
        }
    }

    /// <summary>
    /// Reads until the peer closes its half of the connection, or until a short grace period elapses.
    /// </summary>
    /// <remarks>
    /// The wait is what makes the close graceful; the bound is what keeps a client that never closes from
    /// holding a task. Whatever arrives is discarded — the request has been answered, and a pipelined
    /// second request on a connection this server has already said it is closing is not one it owes a
    /// reply to.
    /// </remarks>
    private static async Task DrainUntilPeerClosesAsync(Stream stream)
    {
        using var grace = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var buffer = new byte[256];

        try
        {
            while (await stream.ReadAsync(buffer, grace.Token).ConfigureAwait(false) > 0)
            {
            }
        }
        catch (Exception)
        {
            // The grace period, or a reset from the other side. Either way the response is out.
        }
    }

    private Task RespondAsync(Stream stream, WptServerRequest request, CancellationToken token) => request.Path switch
    {
        // wptserve answers the root with a directory listing. Nothing reads what is in it; what
        // xhr/responsetype.any.js reads is that a GET of "/" has a body at all, which is what its
        // `assert_not_equals(xhr.responseText, "")` checks before it gets to the responseType rules it is
        // really about.
        "" => WriteAsync(stream, new WptServerResponse(200, "OK", [("content-type", "text/plain")], Bytes("wpt")), request, token),
        "fetch/api/resources/inspect-headers.py" => WriteAsync(stream, InspectHeaders(request), request, token),
        "fetch/api/resources/status.py" => WriteAsync(stream, Status(request), request, token),
        "fetch/api/resources/method.py" => WriteAsync(stream, Method(request), request, token),
        "fetch/api/resources/redirect.py" => WriteAsync(stream, Redirect(request), request, token),
        "fetch/api/resources/redirect-empty-location.py" => WriteAsync(stream, RedirectEmptyLocation(), request, token),
        "fetch/api/resources/clean-stash.py" => WriteAsync(stream, CleanStash(request), request, token),
        "fetch/api/resources/trickle.py" => Trickle(stream, request, token),

        // The xhr corpus. Six of these are the same handler under a second path — upstream keeps a copy
        // of status.py and trickle.py in each suite's own resources/ directory — and the rest are its
        // own. Every one of them is documented against its source below.
        "xhr/resources/content.py" => WriteAsync(stream, Content(request), request, token),
        "xhr/resources/delay.py" => DelayAsync(stream, request, token),
        "xhr/resources/echo-content-type.py" => WriteAsync(stream, EchoContentType(request), request, token),
        "xhr/resources/echo-headers.py" => WriteAsync(stream, EchoHeaders(request), request, token),
        "xhr/resources/form.py" => WriteAsync(stream, Form(request), request, token),
        "xhr/resources/status.py" => WriteAsync(stream, Status(request), request, token),
        "xhr/resources/trickle.py" => Trickle(stream, request, token),

        // Under fetch/, not xhr/: the one file that asks for it names the fetch suite's copy by an
        // absolute path, and upstream keeps the same handler in both places.
        "fetch/api/resources/bad-chunk-encoding.py" => BadChunkEncoding(stream, request, token),

        // The one harness file that does not come out of Vendor/. Upstream's is a stub whose whole purpose
        // is to be replaced by whoever is running the tests, so vendoring it would put bytes in the tree
        // that the server never sends; Jint's copy lives in Prelude/ beside the shim, and a caller can pass
        // an overlay per server. Answered here rather than in the static path because there is no corpus
        // entry to find.
        "resources/testharnessreport.js" => WriteAsync(stream, HarnessReport(), request, token),

        // The other vendor slot. Upstream's file is vendored and is empty, so unlike the one above this
        // falls through to the corpus when no overlay was passed — a caller that supplies none gets
        // testdriver.js's own "not implemented by testdriver-vendor.js" rejections, which is what a driver
        // with no automation should get.
        "resources/testdriver-vendor.js" when _testDriverVendor is not null
            => WriteAsync(stream, Script(_testDriverVendor), request, token),

        _ => StaticAsync(stream, request, token),
    };

    /// <summary>
    /// <c>resources/testharnessreport.js</c>, with the content type upstream's own
    /// <c>testharnessreport.js.headers</c> sidecar gives it.
    /// </summary>
    private WptServerResponse HarnessReport() => Script(_harnessReport);

    /// <summary>One overlay script, with the content type upstream's <c>.headers</c> sidecars give it.</summary>
    private static WptServerResponse Script(string source)
        => new(200, "OK", [("Content-Type", "text/javascript; charset=utf-8")], Encoding.UTF8.GetBytes(source));

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
    /// <summary>
    /// <c>xhr/resources/content.py</c>: answers with the request body — or with the <c>content</c> query
    /// parameter when there is one — and echoes four facts about the request as headers.
    /// </summary>
    /// <remarks>
    /// The three <c>X-Request-*</c> headers fall back to the literal string <c>NO</c> rather than being
    /// omitted, which is what the suites assert against: <c>send()</c> with no body has to answer
    /// <c>NO</c> for the content type, not an absent header.
    /// </remarks>
    private static WptServerResponse Content(WptServerRequest request)
    {
        var type = request.Query.TryGetValue("response_charset_label", out var charset)
            ? "text/plain;charset=" + charset
            : "text/plain";

        var headers = new List<(string, string)>
        {
            ("content-type", type),
            ("x-request-method", request.Method),
            ("x-request-query", request.RawQuery.Length == 0 ? "NO" : request.RawQuery),
            ("x-request-content-length", request.HeaderOr("content-length", "NO")),
            ("x-request-content-type", request.HeaderOr("content-type", "NO")),
        };

        var body = request.Query.TryGetValue("content", out var content) ? content : request.Body;
        return new WptServerResponse(200, "OK", headers, Bytes(body));
    }

    /// <summary>
    /// <c>xhr/resources/delay.py</c>: sleeps for <c>ms</c> milliseconds (500 by default) and then answers
    /// <c>TEST_DELAY</c>. Method-agnostic, which is what the upload suites need of it.
    /// </summary>
    /// <remarks>
    /// The two <c>Access-Control-*</c> headers upstream sets are not written: nothing here has a CORS
    /// model, and a half-implemented one would make a cross-origin file look runnable.
    /// </remarks>
    private static async Task DelayAsync(Stream stream, WptServerRequest request, CancellationToken token)
    {
        await Task.Delay(TimeSpan.FromMilliseconds(request.QueryInt("ms", 500)), token).ConfigureAwait(false);
        await WriteAsync(
            stream,
            new WptServerResponse(200, "OK", [("content-type", "text/plain")], Bytes("TEST_DELAY")),
            request,
            token).ConfigureAwait(false);
    }

    /// <summary>
    /// <c>xhr/resources/echo-content-type.py</c>: the request's own <c>Content-Type</c> as the body.
    /// </summary>
    private static WptServerResponse EchoContentType(WptServerRequest request)
        => new(200, "OK", [("content-type", "text/plain")], Bytes(request.HeaderOr("content-type", "")));

    /// <summary>
    /// <c>xhr/resources/echo-headers.py</c>: the request's raw header block as the body.
    /// </summary>
    /// <remarks>
    /// Upstream writes <c>str(request.raw_headers)</c>, which is Python's rendering of the block it read:
    /// one <c>Name: value</c> per line. The one file that reads it asks whether
    /// <c>Content-Length: 22</c> is in there, so what matters is the shape of a line and the casing the
    /// header arrived with.
    /// </remarks>
    private static WptServerResponse EchoHeaders(WptServerRequest request)
    {
        var builder = new StringBuilder();
        foreach (var (name, value) in request.RawHeaders)
        {
            builder.Append(name).Append(':').Append(' ').Append(value).Append('\n');
        }

        return new WptServerResponse(200, "OK", [("content-type", "text/plain")], Bytes(builder.ToString()));
    }

    /// <summary>
    /// <c>xhr/resources/form.py</c>: <c>id:&lt;id&gt;;value:&lt;value&gt;;</c> from the posted form.
    /// </summary>
    /// <remarks>
    /// wptserve's <c>request.POST</c> parses both <c>application/x-www-form-urlencoded</c> and
    /// <c>multipart/form-data</c>; the one file that reads this posts a <c>FormData</c>, so it is the
    /// multipart form that has to work.
    /// </remarks>
    private static WptServerResponse Form(WptServerRequest request)
    {
        var fields = request.PostFields();
        var id = fields.TryGetValue("id", out var idValue) ? idValue : "";
        var value = fields.TryGetValue("value", out var v) ? v : "";

        return new WptServerResponse(200, "OK", [("content-type", "text/plain")],
            Bytes("id:" + id + ";value:" + value + ";"));
    }

    /// <summary>
    /// <c>fetch/api/resources/bad-chunk-encoding.py</c>: <c>count</c> well-formed chunks and then the
    /// literal bytes <c>garbage</c> where a chunk header should be, with no terminating chunk.
    /// </summary>
    /// <remarks>
    /// The framing is written by hand, because the point is a chunked body that is <i>not</i> valid: a
    /// client has to report the stream as failed part-way through rather than as a short read. The delay
    /// between chunks is upstream's, so the failure arrives after the response has been handed to script
    /// rather than with its headers.
    /// </remarks>
    private static async Task BadChunkEncoding(Stream stream, WptServerRequest request, CancellationToken token)
    {
        var delay = TimeSpan.FromMilliseconds(request.QueryInt("ms", 1000));
        var count = request.QueryInt("count", 50);

        await Task.Delay(delay, token).ConfigureAwait(false);
        await WriteRawAsync(stream, "HTTP/1.1 200 OK\r\ntransfer-encoding: chunked\r\n\r\n", token).ConfigureAwait(false);
        await Task.Delay(delay, token).ConfigureAwait(false);

        for (var i = 0; i < count; i++)
        {
            await WriteRawAsync(stream, "a\r\nTEST_CHUNK\r\n", token).ConfigureAwait(false);
            await Task.Delay(delay, token).ConfigureAwait(false);
        }

        await WriteRawAsync(stream, "garbage", token).ConfigureAwait(false);
    }

    /// <summary>
    /// A file out of the vendored tree — or, for an <c>.asis</c> file, the whole response verbatim.
    /// </summary>
    /// <remarks>
    /// wptserve writes an <c>.asis</c> file to the socket as it stands, status line and all, which is the
    /// point of the six the xhr corpus has: a 280 with a made-up reason phrase, an <c>HTTP/1.0</c>
    /// response, a repeated <c>Content-Length</c>, header values that are empty or hold a vertical tab.
    /// Nothing here may reconstruct one from a parsed form, so it does not go through
    /// <see cref="WriteAsync"/> at all — that method frames a response, and this one <i>is</i> the
    /// framing.
    /// </remarks>
    private Task StaticAsync(Stream stream, WptServerRequest request, CancellationToken token)
    {
        if (request.Path.EndsWith(".asis", StringComparison.Ordinal) && WptCorpus.Contains(request.Path))
        {
            // Upstream stores these with LF line endings and wptserve writes them as they are; a browser
            // accepts that framing and System.Net.Http does not, so the terminators — and only those — are
            // normalized on the way out. Everything a test reads is in a header value or the body, neither
            // of which this touches: the vertical tab and form feed headers-some-are-empty.asis carries
            // survive it.
            var raw = WptCorpus.Read(request.Path).Replace("\r\n", "\n", StringComparison.Ordinal);

            // None of the six ends in a blank line: upstream lets the connection close stand for the end of
            // the header block, which a browser accepts and System.Net.Http does not. The terminator is
            // added rather than assumed, so a file that ever grows a body keeps it.
            if (!raw.EndsWith("\n\n", StringComparison.Ordinal))
            {
                raw += raw.EndsWith("\n", StringComparison.Ordinal) ? "\n" : "\n\n";
            }

            return WriteRawAsync(stream, raw.Replace("\n", "\r\n", StringComparison.Ordinal), token);
        }

        WptServerResponse response;
        try
        {
            response = StaticFile(request, Port);
        }
        catch (WptServerFileException e)
        {
            // wptserve turns a raise inside a pipe into a 500, and so does this. The alternative — serving
            // the file with its placeholders intact, or with a pipe silently skipped — fails a test
            // somewhere far away from the cause, which is the failure mode this whole port exists to avoid.
            response = new WptServerResponse(500, "Internal Server Error",
                [("content-type", "text/plain")], Bytes(e.Message));
        }

        return WriteAsync(stream, response, request, token);
    }

    /// <summary>
    /// wptserve's <c>FileHandler</c>: the file's bytes, the headers its <c>.headers</c> sidecars ask for,
    /// and the <c>sub</c> pipe where the name or the query calls for it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The query and the fragment take no part in finding the file — <c>filesystem_path</c> reads
    /// <c>url_parts.path</c> alone — which is what lets <c>?pipe=</c> and <c>{{GET[…]}}</c> exist at all,
    /// and it is why <see cref="WptServerRequest.Path"/> is already stripped of both.
    /// </para>
    /// <para>
    /// A path the corpus does not hold is a 404 rather than a CLR exception, because unlike the shim's
    /// resource reader this one is answering a request the corpus itself composed, and several suites fetch
    /// a URL precisely to see it fail.
    /// </para>
    /// </remarks>
    private static WptServerResponse StaticFile(WptServerRequest request, int port)
    {
        if (WptServerWrappers.IsWrapperPath(request.Path) && !WptCorpus.Contains(request.Path))
        {
            return GeneratedWrapper(request, port);
        }

        if (!WptCorpus.Contains(request.Path))
        {
            return new WptServerResponse(404, "Not Found", [("content-type", "text/plain")], []);
        }

        var context = new WptSubstitutionContext(port, request);
        var headers = WptServerFiles.LoadHeaders(request.Path, context, WptCorpus.TryRead);

        // The corpus is held as text, so its bytes are the UTF-8 of that text — which is how it was
        // vendored, and why nothing binary is in the tree.
        var content = WptCorpus.Read(request.Path);

        if (WptServerFiles.WantsSubstitution(request.Path))
        {
            content = WptServerFiles.Substitute(context, content, WptServerFiles.EscapesAsHtml(request.Path));
        }

        content = ApplyQueryPipes(request, context, content);

        return new WptServerResponse(200, "OK", headers, Encoding.UTF8.GetBytes(content));
    }

    /// <summary>
    /// <c>AnyHtmlHandler</c>: the document upstream manufactures for a <c>.any.js</c> file, which exists on no
    /// disk anywhere.
    /// </summary>
    /// <remarks>
    /// Two 404s rather than one, and they are upstream's own. A wrapper whose underlying file is not vendored
    /// is <c>_get_metadata</c>'s <c>OSError</c>, and one whose file does not declare the <c>window</c> global
    /// is <c>check_exposure</c> refusing — "This test cannot be loaded in window mode". The second is what
    /// stops the lane silently running a worker-only suite in a window, where it would assert nothing about
    /// the global it is named for and stay green.
    /// </remarks>
    private static WptServerResponse GeneratedWrapper(WptServerRequest request, int port)
    {
        var underlying = WptServerWrappers.UnderlyingFile(request.Path);

        if (WptCorpus.TryRead(underlying) is not { } source)
        {
            return new WptServerResponse(404, "Not Found", [("content-type", "text/plain")], []);
        }

        if (WptServerWrappers.Window(request.Path, source) is not { } document)
        {
            return new WptServerResponse(404, "Not Found", [("content-type", "text/plain")],
                Bytes("This test cannot be loaded in window mode"));
        }

        var context = new WptSubstitutionContext(port, request);

        // `for header_name, header_value in self.headers + handlers.load_headers(request, path):
        //      response.headers.set(header_name, header_value)` — the wrapper's own content type, then
        // whatever the *underlying* file's `.headers` sidecars ask for, each one set rather than appended.
        // The path is the one on disk and not the one requested, which is the whole reason
        // `_get_filesystem_path` exists; and it is `load_headers` alone, so nothing guesses a content type
        // from the `.any.js` suffix and serves the document as a script.
        var headers = new List<(string Name, string Value)>();
        WptServerFiles.Set(headers, "Content-Type", "text/html");

        foreach (var (name, value) in WptServerFiles.Sidecars(underlying, context, WptCorpus.TryRead))
        {
            WptServerFiles.Set(headers, name, value);
        }

        return new WptServerResponse(200, "OK", headers,
            Encoding.UTF8.GetBytes(ApplyQueryPipes(request, context, document)));
    }

    /// <summary>
    /// The <c>?pipe=</c> half of <c>wrap_pipeline</c>, of which one pipe is implemented and every other one
    /// is refused out loud.
    /// </summary>
    /// <remarks>
    /// <para>
    /// wptserve has fourteen pipes: <c>status</c>, <c>header</c>, <c>slice</c>, <c>trickle</c>, <c>sub</c>,
    /// <c>gzip</c> and the rest. Implementing them on demand is right; implementing <i>none</i> of them and
    /// ignoring the query is not, because a file that asks for <c>?pipe=status(404)</c> and gets a 200 fails
    /// an assertion about the engine instead of reporting that the server does not do that yet. So an
    /// unknown pipe is a 500 naming itself, which is the same shape as an unknown substitution.
    /// </para>
    /// <para>
    /// Nothing in the vendored corpus passes <c>?pipe=</c> today. This exists because the browser lane's
    /// suites do, and because the decision of what happens when they do should not be made by whichever
    /// file gets there first.
    /// </para>
    /// </remarks>
    private static string ApplyQueryPipes(WptServerRequest request, in WptSubstitutionContext context, string content)
    {
        if (!request.TryGetLastQuery("pipe", out var pipes) || pipes.Length == 0)
        {
            return content;
        }

        foreach (var pipe in pipes.Split('|'))
        {
            var open = pipe.IndexOf('(');
            var name = (open < 0 ? pipe : pipe.Substring(0, open)).Trim();
            var arguments = open < 0
                ? ""
                : pipe.Substring(open + 1).TrimEnd().TrimEnd(')');

            if (!string.Equals(name, "sub", StringComparison.Ordinal))
            {
                throw new WptServerFileException(
                    $"the wptserve pipe \"{name}\" is not implemented by this server; see WptServer.ApplyQueryPipes");
            }

            var escaping = arguments.Trim();
            var escapeAsHtml = escaping switch
            {
                "" or "html" => true,
                "none" => false,
                _ => throw new WptServerFileException($"\"{escaping}\" is not an escape type the sub pipe takes"),
            };

            content = WptServerFiles.Substitute(context, content, escapeAsHtml);
        }

        return content;
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

    /// <summary>
    /// The request target with its leading slash and its query removed, percent-decoded:
    /// <c>fetch/api/resources/top.txt</c>. This is the key the corpus is looked up by, which is why it is
    /// decoded — <c>filesystem_path</c> unquotes before it joins the document root, so
    /// <c>/common/blank%2Ehtml</c> is the same file as <c>/common/blank.html</c>.
    /// </summary>
    internal string Path { get; }

    /// <summary>
    /// The path component of the request target exactly as it arrived — leading slash, percent-escapes and
    /// all. wptserve's <c>request.url_parts.path</c>, which is what <c>{{location[path]}}</c> substitutes
    /// and which <see cref="Path"/> is the decoded form of.
    /// </summary>
    internal string RawPath { get; private set; } = "/";

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

    /// <summary>
    /// The request's header lines as they arrived, in order and with their original casing — what
    /// <c>echo-headers.py</c> writes back out.
    /// </summary>
    internal IReadOnlyList<(string Name, string Value)> RawHeaders => _headers;

    /// <summary>
    /// The query string exactly as it arrived, without its <c>?</c>, or the empty string when there was
    /// none — wptserve's <c>request.url_parts.query</c>.
    /// </summary>
    internal string RawQuery { get; private set; } = string.Empty;

    /// <summary>
    /// The <b>last</b> value given for a query parameter, which is what <c>wrap_pipeline</c> reads
    /// <c>?pipe=</c> with (<c>query["pipe"][-1]</c>) — the one place upstream deliberately does not take the
    /// first.
    /// </summary>
    internal bool TryGetLastQuery(string name, out string value)
    {
        value = "";
        var found = false;

        foreach (var (candidate, candidateValue) in _queryPairs)
        {
            if (string.Equals(candidate, name, StringComparison.Ordinal))
            {
                value = candidateValue;
                found = true;
            }
        }

        return found;
    }

    /// <summary>
    /// wptserve's <c>request.POST</c>, reduced to the two forms the corpus posts: an urlencoded body and
    /// a <c>multipart/form-data</c> one. Only the field name and its value are read; a file part's
    /// filename and type are not, because no vendored file asks for them.
    /// </summary>
    internal Dictionary<string, string> PostFields()
    {
        var fields = new Dictionary<string, string>(StringComparer.Ordinal);
        var contentType = HeaderOr("content-type", "");

        if (contentType.StartsWith("multipart/form-data", StringComparison.OrdinalIgnoreCase))
        {
            var marker = "boundary=";
            var at = contentType.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
            if (at < 0)
            {
                return fields;
            }

            var boundary = contentType.Substring(at + marker.Length).Trim().Trim('"');
            foreach (var part in Body.Split("--" + boundary))
            {
                var separator = part.IndexOf("\r\n\r\n", StringComparison.Ordinal);
                if (separator < 0)
                {
                    continue;
                }

                var name = FieldName(part.Substring(0, separator));
                if (name is null)
                {
                    continue;
                }

                // The part ends with the CRLF that belongs to the boundary line after it.
                var value = part.Substring(separator + 4).TrimEnd('\r', '\n');
                fields.TryAdd(name, value);
            }

            return fields;
        }

        foreach (var pair in Body.Split('&'))
        {
            if (pair.Length == 0)
            {
                continue;
            }

            var equals = pair.IndexOf('=');
            var name = equals < 0 ? pair : pair.Substring(0, equals);
            var value = equals < 0 ? string.Empty : pair.Substring(equals + 1);
            fields.TryAdd(Decode(name.Replace('+', ' ')), Decode(value.Replace('+', ' ')));
        }

        return fields;
    }

    /// <summary>
    /// The <c>name</c> parameter of a part's <c>Content-Disposition</c>, or <see langword="null"/> when the
    /// part has none.
    /// </summary>
    /// <remarks>
    /// Quoted or bare. The Fetch Standard's own serializer always quotes, which is what a <c>FormData</c>
    /// sent by the engine looks like; System.Net.Http's does not for a simple name, and the server tests post
    /// one of those.
    /// </remarks>
    private static string? FieldName(string partHeaders)
    {
        const string Marker = "name=";
        var at = partHeaders.IndexOf(Marker, StringComparison.OrdinalIgnoreCase);
        if (at < 0)
        {
            return null;
        }

        var start = at + Marker.Length;
        if (start < partHeaders.Length && partHeaders[start] == '"')
        {
            start++;
            var quoted = partHeaders.IndexOf('"', start);
            return quoted < 0 ? null : partHeaders.Substring(start, quoted - start);
        }

        var end = start;
        while (end < partHeaders.Length && partHeaders[end] is not (';' or '\r' or '\n'))
        {
            end++;
        }

        return partHeaders.Substring(start, end - start).Trim();
    }

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

        // A fragment never reaches a server — a client strips it before it writes the request line — so
        // this is defence rather than a rule of HTTP. It is here because the browser lane composes its own
        // URLs and a "#frag" that leaked through would look like a file the corpus does not hold, which is
        // a 404 several layers away from the mistake.
        var hash = target.IndexOf('#');
        if (hash >= 0)
        {
            target = target.Substring(0, hash);
        }

        var question = target.IndexOf('?');
        var rawPath = question < 0 ? target : target.Substring(0, question);
        var path = DecodePath(rawPath.TrimStart('/'));
        var queryPairs = ParseQuery(question < 0 ? "" : target.Substring(question + 1));

        return new WptServerRequest(method, path, queryPairs, headers, body)
        {
            RawPath = rawPath.Length == 0 ? "/" : rawPath,
            RawQuery = question < 0 ? string.Empty : target.Substring(question + 1),
        };
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

    /// <summary>
    /// Percent-decoding for the <i>path</i>, which is a different job from <see cref="Decode"/> in two ways
    /// and both matter. A <c>+</c> is a plus and not a space — that rule belongs to the query string's
    /// urlencoded form and to nothing else — and the decoded bytes are text rather than a byte string,
    /// because what they become is a key into the corpus and the corpus is keyed by UTF-8 paths.
    /// </summary>
    private static string DecodePath(string value)
    {
        if (value.IndexOf('%') < 0)
        {
            return value;
        }

        var bytes = new List<byte>(value.Length);

        for (var i = 0; i < value.Length; i++)
        {
            var c = value[i];

            if (c == '%' && i + 2 < value.Length
                && Uri.IsHexDigit(value[i + 1]) && Uri.IsHexDigit(value[i + 2]))
            {
                bytes.Add((byte) int.Parse(value.AsSpan(i + 1, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture));
                i += 2;
            }
            else
            {
                // The request line arrived as Latin-1, one char to one byte, so this is that byte back.
                bytes.Add((byte) c);
            }
        }

        return Encoding.UTF8.GetString(bytes.ToArray());
    }
}

/// <summary>A response the server is about to write: status, headers and a body it frames itself.</summary>
internal readonly record struct WptServerResponse(
    int Status,
    string StatusText,
    IReadOnlyList<(string Name, string Value)> Headers,
    byte[] Body);
#endif
