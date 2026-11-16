using System.Net;
using System.Net.Sockets;
using System.Text;

namespace Jint.Tests.Browser.Navigation;

/// <summary>
/// The WebSocket opening handshake is a request whose credentials mode is "include"
/// (https://websockets.spec.whatwg.org/#concept-websocket-establish), so a page's socket carries the cookies its
/// context's jar holds for the URL. A hub behind a login — SignalR's — refuses a handshake without them.
/// </summary>
public sealed class WebSocketCookieTests
{
    [Test]
    public async Task TheOpeningHandshakeCarriesTheContextsCookies()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint) listener.LocalEndpoint).Port;
        var handshake = ReadHandshakeAsync(listener);

        await using var site = await LoopbackPage.CreateAsync(
            server => server.Map("/", _ => LoopbackResponse.Html("<!doctype html>").With("Set-Cookie", "session=s3cret; Path=/")),
            context => context.UrlFilter = _ => true);

        await site.Page.NavigateAsync(site.Url("/"));
        await site.Page.EvaluateAsync($"new WebSocket('ws://127.0.0.1:{port}/hub')");

        var request = await handshake.WaitAsync(TimeSpan.FromSeconds(10));
        request.Should().StartWith("GET /hub HTTP/1.1");
        request.Should().Contain("Cookie: session=s3cret");
    }

    private static async Task<string> ReadHandshakeAsync(TcpListener listener)
    {
        using var client = await listener.AcceptTcpClientAsync();
        var stream = client.GetStream();
        var buffer = new byte[8192];
        var text = new StringBuilder();
        while (!text.ToString().Contains("\r\n\r\n", StringComparison.Ordinal))
        {
            var read = await stream.ReadAsync(buffer);
            if (read == 0)
            {
                break;
            }

            text.Append(Encoding.ASCII.GetString(buffer, 0, read));
        }

        return text.ToString();
    }
}
