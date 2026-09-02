#if NET8_0_OR_GREATER
#nullable enable

using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Jint;
using Jint.Native;

namespace Jint.Tests.PublicInterface;

/// <summary>
/// The <c>XMLHttpRequest</c> surface a third party actually reaches: the feature flag, the
/// <c>UseXmlHttpRequest</c> extension, the <c>DocumentParser</c> hook, and a synchronous round trip over a
/// real socket.
/// </summary>
/// <remarks>
/// <para>
/// This project has no <c>InternalsVisibleTo</c>, so everything here is reachable by an embedder. The
/// synchronous test runs against its own loopback listener rather than a stub handler, because "the call
/// returns with the response already in hand" is a claim about a real socket and a real wait.
/// </para>
/// <para>
/// The asynchronous tests hand their bodies to <see cref="DedicatedThread.RunAsync"/>, since a response is
/// delivered from a thread-pool continuation and blocking a pool worker to wait for one is the resource
/// inversion that helper exists for.
/// </para>
/// </remarks>
public class WebApiXmlHttpRequestTests
{
    private static readonly TimeSpan TransportSignalCeiling = TimeSpan.FromMinutes(2);

    // ---- the flag ----

    /// <summary>
    /// <see cref="WebApiFeatures.Default"/> never carries it, and neither does <see cref="WebApiFeatures.Fetch"/>:
    /// the interface is its own opt-in.
    /// </summary>
    [Test]
    public void TheFlagIsNeitherInDefaultNorImpliedByFetch()
    {
        (WebApiFeatures.Default & WebApiFeatures.XmlHttpRequest).Should().Be(WebApiFeatures.None);

        new Engine(options => options.UseWebApis())
            .Evaluate("typeof XMLHttpRequest").AsString().Should().Be("undefined");

        new Engine(options => options.UseFetch())
            .Evaluate("typeof XMLHttpRequest").AsString().Should().Be("undefined");
    }

    /// <summary>
    /// Naming the flag directly is the same door as the extension method, and both leave
    /// <c>options.WebApi.Features</c> reading back exactly what the host asked for while the engine carries
    /// the closure.
    /// </summary>
    [Test]
    public void TheFlagAndTheExtensionAreTheSameDoor()
    {
        var byFlag = new Engine(options => options.WebApi.Features = WebApiFeatures.XmlHttpRequest);
        var byExtension = new Engine(options => options.UseXmlHttpRequest());

        foreach (var engine in new[] { byFlag, byExtension })
        {
            engine.Evaluate("typeof XMLHttpRequest").AsString().Should().Be("function");
            engine.Evaluate("typeof XMLHttpRequestUpload").AsString().Should().Be("function");
            engine.Evaluate("typeof XMLHttpRequestEventTarget").AsString().Should().Be("function");
            engine.Evaluate("typeof ProgressEvent").AsString().Should().Be("function");

            // The closure the interface is built out of, and pointedly not `fetch`.
            engine.Evaluate("typeof Headers").AsString().Should().Be("function");
            engine.Evaluate("typeof Blob").AsString().Should().Be("function");
            engine.Evaluate("typeof fetch").AsString().Should().Be("undefined");
        }

        var options = new Options();
        options.UseXmlHttpRequest();
        options.WebApi.Features.Should().Be(WebApiFeatures.XmlHttpRequest);
    }

    /// <summary>
    /// The extension hands over the shared network settings, exactly as <c>UseFetch</c> and
    /// <c>UseEventSource</c> do.
    /// </summary>
    [Test]
    public void TheExtensionConfiguresTheSharedNetworkSettings()
    {
        var options = new Options();
        options.UseXmlHttpRequest(fetch => fetch.BaseUrl = new Uri("https://example.org/base/"));

        options.WebApi.Fetch.BaseUrl.Should().Be(new Uri("https://example.org/base/"));
    }

    /// <summary>
    /// The interface is not the network grant. Without <see cref="WebApiFeatures.Fetch"/> and without a host
    /// <c>HttpClient</c>, a synchronous <c>send()</c> fails the way a blocked <c>fetch</c> does.
    /// </summary>
    [Test]
    public void TheFlagAloneGrantsNoNetworkAccess()
    {
        new Engine(options => options.UseXmlHttpRequest())
            .Evaluate("""
                (() => {
                    const xhr = new XMLHttpRequest();
                    xhr.open('GET', 'https://example.org/a', false);
                    try { xhr.send(); } catch (e) { return e.name; }
                    return 'no throw';
                })()
                """).AsString().Should().Be("NetworkError");
    }

    // ---- the DocumentParser hook ----

    /// <summary>
    /// <c>Options.WebApi.Xhr.DocumentParser</c> is what makes <c>responseXML</c> answer anything; it is
    /// handed the engine, the decoded body and the essence of the final MIME type.
    /// </summary>
    [Test]
    public Task TheDocumentParserHookReceivesTheBodyAndTheMimeTypeEssence() => DedicatedThread.RunAsync(() =>
    {
        using var server = new LoopbackServer("text/html;charset=UTF-8", "<p>hi</p>");

        var seen = new List<string>();
        var engine = new Engine(options =>
        {
            options.UseFetch().UseXmlHttpRequest(fetch =>
            {
                fetch.BaseUrl = new Uri(server.Origin + "/");
                fetch.UrlFilter = uri => uri.Port == server.Port;
            });

            options.WebApi.Xhr.DocumentParser = (e, text, mimeType) =>
            {
                seen.Add(mimeType + "|" + text);
                return e.Evaluate("({ tag: 'document' })");
            };
        });

        engine.Evaluate("""
            (() => {
                const xhr = new XMLHttpRequest();
                xhr.open('GET', '/doc', false);
                xhr.send();
                return xhr.responseXML.tag;
            })()
            """).AsString().Should().Be("document");

        seen.Should().ContainSingle().Which.Should().Be("text/html|<p>hi</p>");
    });

    /// <summary>
    /// Without the hook the document response is the specification's failure, which is <c>null</c> — for
    /// <c>responseXML</c> and for a <c>document</c> <c>response</c> alike.
    /// </summary>
    [Test]
    public Task WithoutTheHookTheDocumentResponseIsNull() => DedicatedThread.RunAsync(() =>
    {
        using var server = new LoopbackServer("text/html;charset=UTF-8", "<p>hi</p>");

        var engine = XhrEngine(server);
        engine.Evaluate("""
            (() => {
                const xhr = new XMLHttpRequest();
                xhr.open('GET', '/doc', false);
                xhr.responseType = 'document';
                xhr.send();
                return String(xhr.response) + ':' + String(xhr.responseXML);
            })()
            """).AsString().Should().Be("null:null");
    });

    // ---- the synchronous round trip ----

    /// <summary>
    /// <c>open(…, false)</c> against a real socket: the response is readable the instant <c>send()</c>
    /// returns, with nothing pumped and no promise to await.
    /// </summary>
    /// <remarks>
    /// The listener answers only after a delay set from another thread, so the wait is a real one: a design
    /// that pumped the engine instead would never see the response, because nothing on the engine's queue can
    /// make it arrive.
    /// </remarks>
    [Test]
    public Task ASynchronousRequestReturnsWithTheResponseInHand() => DedicatedThread.RunAsync(() =>
    {
        using var server = new LoopbackServer("text/plain;charset=UTF-8", "hello sync") { ResponseDelay = TimeSpan.FromMilliseconds(50) };

        var engine = XhrEngine(server);
        engine.Evaluate("""
            (() => {
                const xhr = new XMLHttpRequest();
                xhr.open('GET', '/sync', false);
                xhr.setRequestHeader('X-Probe', 'yes');
                xhr.send();
                return [xhr.readyState, xhr.status, xhr.statusText, xhr.responseText, xhr.getResponseHeader('content-type')].join('|');
            })()
            """).AsString().Should().Be("4|200|OK|hello sync|text/plain;charset=UTF-8");

        // Lowercased on the wire, because a header list stores the byte-lowercased name and the Fetch
        // Standard makes the original casing unobservable — HTTP header names are case-insensitive.
        server.Received.Should().ContainSingle().Which.Should().Contain("x-probe: yes");
    });

    /// <summary>
    /// The asynchronous form of the same request, delivered through the host's own pump: nothing arrives in
    /// an engine nobody pumps, which is the contract every other web API here has.
    /// </summary>
    [Test]
    public Task AnAsynchronousRequestIsDeliveredOnTheHostsPump() => DedicatedThread.RunAsync(() =>
    {
        using var server = new LoopbackServer("text/plain;charset=UTF-8", "hello async");

        var engine = XhrEngine(server);
        engine.Execute("""
            var log = [];
            var xhr = new XMLHttpRequest();
            xhr.addEventListener('load', () => log.push(xhr.status + ':' + xhr.responseText));
            xhr.open('GET', '/async');
            xhr.send();
            """);

        // Before the pump, nothing at all has happened beyond open()'s own state change.
        engine.Evaluate("log.length").AsNumber().Should().Be(0);

        var deadline = DateTime.UtcNow + TransportSignalCeiling;
        while (engine.Evaluate("log.length").AsNumber() == 0 && DateTime.UtcNow < deadline)
        {
            engine.Tasks.ProcessTasks();
            Thread.Sleep(2);
        }

        engine.Evaluate("log[0]").AsString().Should().Be("200:hello async");
    });

    /// <summary>
    /// A host <c>HttpClient</c> is the other network grant, and it is what an embedder interposing a
    /// <c>DelegatingHandler</c> already has.
    /// </summary>
    [Test]
    public Task AHostHttpClientIsAlsoTheGrant() => DedicatedThread.RunAsync(() =>
    {
        using var server = new LoopbackServer("text/plain;charset=UTF-8", "via client");

        var engine = new Engine(options => options.UseXmlHttpRequest(fetch =>
        {
            fetch.HttpClient = new System.Net.Http.HttpClient(new System.Net.Http.SocketsHttpHandler { AllowAutoRedirect = false });
            fetch.BaseUrl = new Uri(server.Origin + "/");
            fetch.UrlFilter = uri => uri.Port == server.Port;
        }));

        engine.Evaluate("""
            (() => {
                const xhr = new XMLHttpRequest();
                xhr.open('GET', '/a', false);
                xhr.send();
                return xhr.responseText;
            })()
            """).AsString().Should().Be("via client");
    });

    /// <summary>
    /// The <c>UrlFilter</c> a host wrote for <c>fetch</c> governs an <c>XMLHttpRequest</c> too: one policy,
    /// however the request was started.
    /// </summary>
    [Test]
    public void TheFetchUrlFilterGovernsAnXmlHttpRequest()
    {
        var engine = new Engine(options => options.UseFetch(fetch => fetch.UrlFilter = _ => false).UseXmlHttpRequest());

        engine.Evaluate("""
            (() => {
                const xhr = new XMLHttpRequest();
                xhr.open('GET', 'https://example.org/a', false);
                try { xhr.send(); } catch (e) { return e.name; }
                return 'no throw';
            })()
            """).AsString().Should().Be("NetworkError");
    }

    /// <summary>
    /// The ordinary embedder shape: the network grant is <c>UseFetch</c>, and the interface is its own
    /// opt-in beside it. The filter bounds every request to this test's own loopback port.
    /// </summary>
    private static Engine XhrEngine(LoopbackServer server)
        => new(options => options.UseFetch().UseXmlHttpRequest(fetch =>
        {
            fetch.BaseUrl = new Uri(server.Origin + "/");
            fetch.UrlFilter = uri => uri.Port == server.Port;
        }));

    /// <summary>
    /// A one-request-per-connection HTTP/1.1 origin on the loopback interface, answering one fixed body.
    /// </summary>
    private sealed class LoopbackServer : IDisposable
    {
        private readonly TcpListener _listener;
        private readonly CancellationTokenSource _stopping = new();
        private readonly string _contentType;
        private readonly string _body;

        internal LoopbackServer(string contentType, string body)
        {
            _contentType = contentType;
            _body = body;
            _listener = new TcpListener(IPAddress.Loopback, 0);
            _listener.Start();
            Port = ((IPEndPoint) _listener.LocalEndpoint).Port;
            _ = Task.Run(AcceptAsync);
        }

        internal int Port { get; }

        internal string Origin => "http://127.0.0.1:" + Port.ToString(CultureInfo.InvariantCulture);

        /// <summary>How long the server waits before answering, so the synchronous wait is a real one.</summary>
        internal TimeSpan ResponseDelay { get; init; }

        internal List<string> Received { get; } = new();

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
                var stream = client.GetStream();
                var buffer = new byte[8192];
                var text = new StringBuilder();

                while (!text.ToString().Contains("\r\n\r\n", StringComparison.Ordinal))
                {
                    var read = await stream.ReadAsync(buffer, _stopping.Token).ConfigureAwait(false);
                    if (read == 0)
                    {
                        return;
                    }

                    text.Append(Encoding.Latin1.GetString(buffer, 0, read));
                }

                lock (Received)
                {
                    Received.Add(text.ToString());
                }

                if (ResponseDelay > TimeSpan.Zero)
                {
                    await Task.Delay(ResponseDelay, _stopping.Token).ConfigureAwait(false);
                }

                var bytes = Encoding.UTF8.GetBytes(_body);
                var response = "HTTP/1.1 200 OK\r\nContent-Type: " + _contentType
                    + "\r\nContent-Length: " + bytes.Length.ToString(CultureInfo.InvariantCulture)
                    + "\r\nConnection: close\r\n\r\n";

                await stream.WriteAsync(Encoding.Latin1.GetBytes(response), _stopping.Token).ConfigureAwait(false);
                await stream.WriteAsync(bytes, _stopping.Token).ConfigureAwait(false);
                await stream.FlushAsync(_stopping.Token).ConfigureAwait(false);
            }
        }

        public void Dispose()
        {
            _stopping.Cancel();
            _listener.Stop();
            _stopping.Dispose();
        }
    }
}
#endif
