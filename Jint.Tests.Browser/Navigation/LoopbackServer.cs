using System.Collections.Concurrent;
using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace Jint.Tests.Browser.Navigation;

/// <summary>
/// An in-process HTTP/1.1 origin on the loopback interface, so that a navigation test really opens a socket.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why a server at all.</b> Everything R5 added is about what happens between the page and an origin —
/// cookies recomputed per redirect hop, a <c>Referer</c> on the second navigation, a <c>303</c> turning a
/// <c>POST</c> into a <c>GET</c>, a form's body arriving as the server would read it. A stub in front of
/// <c>HttpClient</c> would test the stub; this tests the transport, the redirect loop and the cookie jar.
/// </para>
/// <para>
/// <b>Why a raw <see cref="TcpListener"/>.</b> The same three reasons <c>Jint.Tests/Wpt/WptServer.cs</c>
/// gives: it binds port <b>0</b> so a run cannot collide with anything else on the machine, it needs no URL
/// ACL, and it needs no ASP.NET Core framework reference. This is the small sibling of that one — it serves
/// routes a test registers rather than a vendored corpus, which is why it is a separate file rather than a
/// link.
/// </para>
/// <para>
/// <b>Every response closes its connection.</b> <c>Connection: close</c> throughout, which makes the body's
/// end unambiguous with no chunked encoder. A test makes a handful of requests, so the churn does not matter.
/// </para>
/// </remarks>
internal sealed class LoopbackServer : IDisposable
{
    private readonly TcpListener _listener;
    private readonly CancellationTokenSource _stopping = new();
    private readonly ConcurrentDictionary<string, Func<LoopbackRequest, LoopbackResponse>> _routes = new(StringComparer.Ordinal);
    private readonly ConcurrentQueue<LoopbackRequest> _received = new();

    private volatile bool _disposed;

    internal LoopbackServer()
    {
        _listener = new TcpListener(IPAddress.Loopback, 0);
        _listener.Start();
        Port = ((IPEndPoint) _listener.LocalEndpoint).Port;
        _ = Task.Run(AcceptAsync);
    }

    internal int Port { get; }

    /// <summary>The origin the pages load from, e.g. <c>http://127.0.0.1:51234</c>.</summary>
    internal string Origin => "http://127.0.0.1:" + Port.ToString(CultureInfo.InvariantCulture);

    /// <summary>The absolute URL of a path on this server.</summary>
    internal string Url(string path) => Origin + (path.StartsWith('/') ? path : "/" + path);

    /// <summary>Whether a URL is one this server would answer — the shape a <c>UrlFilter</c> wants.</summary>
    internal bool Owns(Uri uri)
        => uri.IsLoopback && uri.Port == Port && string.Equals(uri.Scheme, "http", StringComparison.Ordinal);

    /// <summary>Every request the server has answered, oldest first.</summary>
    internal IReadOnlyList<LoopbackRequest> Received => _received.ToArray();

    /// <summary>
    /// Answers a path no <see cref="Map"/> claimed, or <see langword="null"/> to let the 404 stand.
    /// </summary>
    /// <remarks>
    /// A route table is right for a navigation test, which serves five documents it wrote; it is wrong for a
    /// fixture, which is a directory of files nobody wants to enumerate into <c>Map</c> calls. So the
    /// obstacle course hangs its corpus here and still registers routes for the handful of paths a fixture
    /// asks the server to compute — a redirect, a JSON body, a <c>Set-Cookie</c>.
    /// </remarks>
    internal Func<LoopbackRequest, LoopbackResponse?>? Fallback { get; set; }

    /// <summary>Registers a handler for one path.</summary>
    internal LoopbackServer Map(string path, Func<LoopbackRequest, LoopbackResponse> handler)
    {
        _routes[path] = handler;
        return this;
    }

    /// <summary>Registers a static HTML document at one path.</summary>
    internal LoopbackServer MapHtml(string path, string html)
        => Map(path, _ => LoopbackResponse.Html(html));

    /// <summary>Stops the listener and ends every connection. Calling it twice does nothing the second time.</summary>
    /// <remarks>
    /// A suite that hands its server to a <c>LoopbackPage</c> and also writes <c>using var server = …</c>
    /// disposes it twice, and cancelling an already-disposed source throws — which fails whichever test
    /// happens to be shaped that way rather than the one that owns the mistake.
    /// </remarks>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
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
                var stream = client.GetStream();
                var request = await ReadAsync(stream, _stopping.Token).ConfigureAwait(false);

                if (request is null)
                {
                    return;
                }

                _received.Enqueue(request);

                var response = _routes.TryGetValue(request.Path, out var handler)
                    ? handler(request)
                    : Fallback?.Invoke(request) ?? LoopbackResponse.NotFound(request.Path);

                await WriteAsync(stream, response, _stopping.Token).ConfigureAwait(false);
            }
            catch (Exception)
            {
                // A connection the peer dropped mid-request is not a test failure.
            }
        }
    }

    private static async Task<LoopbackRequest?> ReadAsync(NetworkStream stream, CancellationToken token)
    {
        var head = new StringBuilder();
        var buffer = new byte[1];

        while (!head.ToString().EndsWith("\r\n\r\n", StringComparison.Ordinal))
        {
            var read = await stream.ReadAsync(buffer.AsMemory(0, 1), token).ConfigureAwait(false);
            if (read == 0)
            {
                return null;
            }

            head.Append((char) buffer[0]);

            if (head.Length > 64 * 1024)
            {
                return null;
            }
        }

        var lines = head.ToString().Split("\r\n");
        var parts = lines[0].Split(' ');
        if (parts.Length < 2)
        {
            return null;
        }

        var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var rawHeaders = new List<KeyValuePair<string, string>>();

        for (var i = 1; i < lines.Length && lines[i].Length != 0; i++)
        {
            var colon = lines[i].IndexOf(':', StringComparison.Ordinal);
            if (colon <= 0)
            {
                continue;
            }

            var name = lines[i][..colon].Trim();
            var value = lines[i][(colon + 1)..].Trim();
            rawHeaders.Add(new KeyValuePair<string, string>(name, value));
            headers[name] = headers.TryGetValue(name, out var existing) ? existing + ", " + value : value;
        }

        var body = "";
        if (headers.TryGetValue("Content-Length", out var lengthText)
            && int.TryParse(lengthText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var length)
            && length > 0)
        {
            var bytes = new byte[length];
            var offset = 0;

            while (offset < length)
            {
                var read = await stream.ReadAsync(bytes.AsMemory(offset, length - offset), token).ConfigureAwait(false);
                if (read == 0)
                {
                    break;
                }

                offset += read;
            }

            body = Encoding.UTF8.GetString(bytes, 0, offset);
        }

        var rawPath = parts[1];
        var question = rawPath.IndexOf('?', StringComparison.Ordinal);

        return new LoopbackRequest(
            parts[0],
            question < 0 ? rawPath : rawPath[..question],
            rawPath,
            question < 0 ? "" : rawPath[(question + 1)..],
            headers,
            rawHeaders,
            body);
    }

    private static async Task WriteAsync(NetworkStream stream, LoopbackResponse response, CancellationToken token)
    {
        var body = response.RawBody ?? Encoding.UTF8.GetBytes(response.Body);
        var head = new StringBuilder();

        head.Append("HTTP/1.1 ").Append(response.Status.ToString(CultureInfo.InvariantCulture)).Append(' ').Append(response.Reason).Append("\r\n");

        foreach (var header in response.Headers)
        {
            head.Append(header.Key).Append(": ").Append(header.Value).Append("\r\n");
        }

        head.Append("Content-Length: ").Append(body.Length.ToString(CultureInfo.InvariantCulture)).Append("\r\n");
        head.Append("Connection: close\r\n\r\n");

        await stream.WriteAsync(Encoding.ASCII.GetBytes(head.ToString()), token).ConfigureAwait(false);
        await stream.WriteAsync(body, token).ConfigureAwait(false);
        await stream.FlushAsync(token).ConfigureAwait(false);
    }
}

/// <summary>One request the server read, as the test sees it.</summary>
internal sealed record LoopbackRequest(
    string Method,
    string Path,
    string RawPath,
    string Query,
    IReadOnlyDictionary<string, string> Headers,
    IReadOnlyList<KeyValuePair<string, string>> RawHeaders,
    string Body)
{
    internal string? Header(string name) => Headers.TryGetValue(name, out var value) ? value : null;
}

/// <summary>One response the server writes.</summary>
internal sealed class LoopbackResponse
{
    internal int Status { get; init; } = 200;

    internal string Reason { get; init; } = "OK";

    internal List<KeyValuePair<string, string>> Headers { get; } = [];

    internal string Body { get; init; } = "";

    /// <summary>The body as bytes, when the test is about the encoding rather than the text.</summary>
    internal byte[]? RawBody { get; init; }

    internal LoopbackResponse With(string name, string value)
    {
        Headers.Add(new KeyValuePair<string, string>(name, value));
        return this;
    }

    internal static LoopbackResponse Html(string html)
        => new LoopbackResponse { Body = html }.With("Content-Type", "text/html; charset=utf-8");

    internal static LoopbackResponse Text(string text)
        => new LoopbackResponse { Body = text }.With("Content-Type", "text/plain; charset=utf-8");

    internal static LoopbackResponse Json(string json)
        => new LoopbackResponse { Body = json }.With("Content-Type", "application/json");

    internal static LoopbackResponse Bytes(string body, string contentType)
        => new LoopbackResponse { Body = body }.With("Content-Type", contentType);

    internal static LoopbackResponse Script(string source)
        => new LoopbackResponse { Body = source }.With("Content-Type", "text/javascript; charset=utf-8");

    internal static LoopbackResponse Css(string source)
        => new LoopbackResponse { Body = source }.With("Content-Type", "text/css; charset=utf-8");

    /// <summary>
    /// A response whose body is exactly these bytes, for the cases where the encoding is what is under test.
    /// </summary>
    internal static LoopbackResponse Raw(byte[] body, string contentType)
        => new LoopbackResponse { RawBody = body }.With("Content-Type", contentType);

    internal static LoopbackResponse Redirect(int status, string location)
        => new LoopbackResponse { Status = status, Reason = ReasonFor(status) }.With("Location", location);

    internal static LoopbackResponse NotFound(string path)
        => new LoopbackResponse
        {
            Status = 404,
            Reason = "Not Found",
            Body = "<html><head><title>Not found</title></head><body><h1>404</h1><p>" + path + "</p></body></html>",
        }.With("Content-Type", "text/html; charset=utf-8");

    private static string ReasonFor(int status) => status switch
    {
        301 => "Moved Permanently",
        302 => "Found",
        303 => "See Other",
        307 => "Temporary Redirect",
        308 => "Permanent Redirect",
        _ => "OK",
    };
}
