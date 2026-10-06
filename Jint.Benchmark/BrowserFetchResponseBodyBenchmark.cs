#nullable enable

using System.Net;
using System.Net.WebSockets;
using System.Text.Json;
using BenchmarkDotNet.Attributes;
using Jint.Browser;
using Jint.DevTools;

namespace Jint.Benchmark;

/// <summary>Repeated Fetch.getResponseBody replies for binary bodies with and without JSON escaping.</summary>
/// <remarks>
/// Each target-specific setup creates its own page/engine and warms only its row. The first body read,
/// HTTP transport and construction are excluded. Measurements include the public CDP socket, server
/// encoding/serialization, and client JSON parsing/unescaping; they are not isolated encoder timings.
/// A reusable client receive buffer avoids growth/copy costs obscuring the server's copies. Setup checks
/// the complete decoded payload; each measured reply consumes its body string and validates its length.
/// BrowserVersion is an unaffected protocol control, batched 32 times to amortise small-message jitter.
/// No internal Browser or DevTools surface is used despite the benchmark assembly's friend access.
/// </remarks>
[MemoryDiagnoser]
public class BrowserFetchResponseBodyBenchmark
{
    private const string BodyUrl = "https://fetch-benchmark.invalid/body";
    private Browser.Browser _browser = null!;
    private DevToolsServer _server = null!;
    private Page _page = null!;
    private HttpClient _http = null!;
    private ClientWebSocket _socket = null!;
    private byte[] _receive = null!;
    private byte[] _payload = null!;
    private string? _session;
    private string? _request;
    private int _nextId;
    private int _encodedLength;

    [Params(65_536, 1_048_576)]
    public int BodyBytes { get; set; }

    [GlobalSetup(Target = nameof(ZeroBody))]
    public Task SetupZero() => Setup(plusHeavy: false, control: false);

    [GlobalSetup(Target = nameof(PlusHeavyBody))]
    public Task SetupPlus() => Setup(plusHeavy: true, control: false);

    [GlobalSetup(Target = nameof(BrowserVersion))]
    public Task SetupControl() => Setup(plusHeavy: false, control: true);

    private async Task Setup(bool plusHeavy, bool control)
    {
        _payload = new byte[BodyBytes];
        if (plusHeavy)
            for (var i = 0; i < _payload.Length; i++)
                _payload[i] = (i % 3) switch { 0 => 251, 1 => 239, _ => 190 };
        _encodedLength = 4 * ((BodyBytes + 2) / 3);
        _receive = new byte[(plusHeavy ? 6 : 1) * _encodedLength + 8192];
        _http = new HttpClient(new BodyHandler(_payload));
        _browser = new Browser.Browser(new BrowserOptions
        {
            MaxCapturedResponseBytes = 64 * 1024 * 1024,
            // A default BDN run can keep this same response paused beyond the normal fetch timeout.
            FetchTimeout = TimeSpan.FromMinutes(10)
        });
        _server = new DevToolsServer();
        await _server.AddBrowser(_browser);
        _server.Start();
        var context = await _browser.NewContextAsync(new BrowserContextOptions
        {
            HttpClient = _http,
            UrlFilter = uri => uri.Host == "fetch-benchmark.invalid"
        });
        _page = await context.NewPageAsync();
        await _page.SetContentAsync("<!doctype html><title>fetch-body-subject</title>");
        _socket = new ClientWebSocket();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        await _socket.ConnectAsync(new Uri(_server.BrowserWebSocketUrl), timeout.Token);
        if (control)
        {
            if (await BrowserVersion() <= 0) throw new InvalidOperationException("Missing browser version.");
            return;
        }
        string? target = null;
        while (target is null)
        {
            timeout.Token.ThrowIfCancellationRequested();
            using var targets = await Send("Target.getTargets");
            foreach (var entry in targets.RootElement.GetProperty("result").GetProperty("targetInfos").EnumerateArray())
                if (entry.GetProperty("title").GetString() == "fetch-body-subject")
                    target = entry.GetProperty("targetId").GetString();
            if (target is null) await Task.Delay(10, timeout.Token);
        }
        using (var attached = await Send("Target.attachToTarget", new { targetId = target, flatten = true }))
            _session = attached.RootElement.GetProperty("result").GetProperty("sessionId").GetString();
        using (await Send("Fetch.enable", new { patterns = new[] { new { urlPattern = BodyUrl, requestStage = "Response" } } }, _session)) { }
        await _page.EvaluateAsync($$"""
            window.__benchDone = false;
            fetch('{{BodyUrl}}').then(r => r.arrayBuffer()).then(b => {
                window.__benchLength = b.byteLength; window.__benchDone = true;
            }, e => { window.__benchError = String(e); window.__benchDone = true; });
            true
            """);
        while (_request is null)
        {
            using var message = await Receive(timeout.Token);
            if (message.RootElement.TryGetProperty("method", out var method) && method.GetString() == "Fetch.requestPaused")
                _request = message.RootElement.GetProperty("params").GetProperty("requestId").GetString();
        }
        using var warm = await Send("Fetch.getResponseBody", new { requestId = _request }, _session);
        var body = warm.RootElement.GetProperty("result");
        if (!body.GetProperty("base64Encoded").GetBoolean()
            || !Convert.FromBase64String(body.GetProperty("body").GetString()!).AsSpan().SequenceEqual(_payload))
            throw new InvalidOperationException("Fetch fixture payload mismatch.");
    }

    private async Task<JsonDocument> Send(string method, object? parameters = null, string? sessionId = null)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var id = ++_nextId;
        var request = JsonSerializer.SerializeToUtf8Bytes(new { id, method, @params = parameters, sessionId });
        await _socket.SendAsync(request, WebSocketMessageType.Text, true, timeout.Token);
        while (true)
        {
            var message = await Receive(timeout.Token);
            var root = message.RootElement;
            if (!root.TryGetProperty("id", out var responseId) || responseId.GetInt32() != id)
            {
                message.Dispose();
                continue;
            }
            if (root.TryGetProperty("error", out var error))
            {
                var text = error.GetRawText();
                message.Dispose();
                throw new InvalidOperationException(text);
            }
            return message;
        }
    }

    private async Task<JsonDocument> Receive(CancellationToken cancellationToken)
    {
        var size = 0;
        ValueWebSocketReceiveResult chunk;
        do
        {
            if (size == _receive.Length) throw new InvalidOperationException("Fetch reply exceeded the fixture's buffer.");
            chunk = await _socket.ReceiveAsync(_receive.AsMemory(size), cancellationToken);
            if (chunk.MessageType == WebSocketMessageType.Close) throw new InvalidOperationException("Fetch connection closed.");
            size += chunk.Count;
        } while (!chunk.EndOfMessage);
        return JsonDocument.Parse(_receive.AsMemory(0, size));
    }

    private async Task<int> ReadBody()
    {
        using var reply = await Send("Fetch.getResponseBody", new { requestId = _request }, _session);
        var result = reply.RootElement.GetProperty("result");
        var text = result.GetProperty("body").GetString()!;
        if (!result.GetProperty("base64Encoded").GetBoolean() || text.Length != _encodedLength)
            throw new InvalidOperationException("Fetch reply checksum failed.");
        return text.Length + text[0] + text[^1];
    }

    [Benchmark]
    public Task<int> ZeroBody() => ReadBody();

    [Benchmark]
    public Task<int> PlusHeavyBody() => ReadBody();

    [Benchmark]
    public async Task<int> BrowserVersion()
    {
        var sum = 0;
        for (var i = 0; i < 32; i++)
        {
            using var reply = await Send("Browser.getVersion");
            sum += reply.RootElement.GetProperty("result").GetProperty("product").GetString()!.Length;
        }
        return sum;
    }

    [GlobalCleanup]
    public async Task Cleanup()
    {
        try
        {
            if (_request is not null)
            {
                using (await Send("Fetch.continueResponse", new { requestId = _request }, _session)) { }
                if (!await _page.WaitForAsync("window.__benchDone", TimeSpan.FromSeconds(30))
                    || await _page.EvaluateAsync<int>("window.__benchLength || -1") != BodyBytes)
                    throw new InvalidOperationException("The page did not receive the original response body.");
            }
        }
        finally
        {
            _socket?.Dispose();
            try { await _server.DisposeAsync(); }
            finally
            {
                await _browser.DisposeAsync();
                _http.Dispose();
            }
        }
    }

    private sealed class BodyHandler(byte[] payload) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var response = new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(payload) };
            response.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/octet-stream");
            response.Headers.Add("Access-Control-Allow-Origin", "*");
            return Task.FromResult(response);
        }
    }
}
