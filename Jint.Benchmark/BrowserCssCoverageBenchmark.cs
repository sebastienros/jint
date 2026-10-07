#nullable enable

using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using BenchmarkDotNet.Attributes;
using Jint.Browser;
using Jint.DevTools;

namespace Jint.Benchmark;

/// <summary>Steady-state style reads with CSS coverage off, on this document, or on another document.</summary>
/// <remarks>
/// Each target-specific setup creates one measured page/engine and warms only that row's script.
/// A public CDP connection arms coverage outside measurement; construction, initial sweep and socket
/// traffic are excluded. The subject has 64 elements and 256 competing author rules, including losers.
/// Untracked/other-document rows batch 2,000 reads to amortise the page mailbox; the tracked-document
/// row batches 32 expensive fresh-query reads. PlainProperty batches 10,000 reads while another document
/// is tracked and cannot execute the cascade. Results are per batch, with different batch sizes;
/// compare identical rows between builds rather than interpreting cross-row ratios as per-read costs.
/// The historical second selector pass is gone; these rows expose query reuse and coverage bookkeeping.
/// </remarks>
[MemoryDiagnoser]
public class BrowserCssCoverageBenchmark
{
    private Browser.Browser _browser = null!;
    private DevToolsServer _server = null!;
    private Page _page = null!;
    private ClientWebSocket? _socket;
    private string? _session;
    private int _nextId;
    private string _script = null!;

    [GlobalSetup(Target = nameof(UntrackedStyle))]
    public Task SetupUntracked() => Setup(tracking: 0, plain: false, reads: 2_000);

    [GlobalSetup(Target = nameof(OtherDocumentTrackedStyle))]
    public Task SetupOther() => Setup(tracking: 1, plain: false, reads: 2_000);

    [GlobalSetup(Target = nameof(TrackedStyle))]
    public Task SetupTracked() => Setup(tracking: 2, plain: false, reads: 32);

    [GlobalSetup(Target = nameof(PlainProperty))]
    public Task SetupPlain() => Setup(tracking: 1, plain: true, reads: 10_000);

    private async Task Setup(int tracking, bool plain, int reads)
    {
        _browser = new Browser.Browser();
        _server = new DevToolsServer();
        await _server.AddBrowser(_browser);
        _server.Start();
        _page = await _browser.NewPageAsync();
        var css = string.Concat(Enumerable.Range(0, 256).Select(i => $".used {{ --order:{i}; opacity:.5; }}"));
        await _page.SetContentAsync("<!doctype html><title>css-coverage-subject</title><style>" + css + "</style>"
            + string.Concat(Enumerable.Repeat("<p class='used'>text</p>", 64)));
        await _page.EvaluateAsync<double>("var box=document.querySelector('p'), plain={opacity:'0.5'};0;");

        if (tracking != 0)
        {
            var title = "css-coverage-subject";
            if (tracking == 1)
            {
                var other = await _browser.NewPageAsync();
                await other.SetContentAsync("<!doctype html><title>css-coverage-other</title><style>p {opacity:.5}</style><p>other</p>");
                title = "css-coverage-other";
            }
            _socket = new ClientWebSocket();
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            await _socket.ConnectAsync(new Uri(_server.BrowserWebSocketUrl), timeout.Token);
            string? target = null;
            while (target is null)
            {
                timeout.Token.ThrowIfCancellationRequested();
                var targets = await Send("Target.getTargets");
                foreach (var entry in targets.GetProperty("targetInfos").EnumerateArray())
                    if (entry.GetProperty("title").GetString() == title)
                        target = entry.GetProperty("targetId").GetString();
                if (target is null) await Task.Delay(10, timeout.Token);
            }
            var attachment = await Send("Target.attachToTarget", new { targetId = target, flatten = true });
            _session = attachment.GetProperty("sessionId").GetString();
            await Send("CSS.enable", sessionId: _session);
            await Send("CSS.startRuleUsageTracking", sessionId: _session);
        }

        var read = plain ? "plain.opacity" : "getComputedStyle(box).opacity";
        _script = $$"""
            (() => {
                let sum = 0;
                for (let i = 0; i < {{reads}}; i++) if ({{read}} === '0.5') sum++;
                return sum;
            })()
            """;
        if (await Run() != reads) throw new InvalidOperationException("CSS coverage fixture checksum failed.");
    }

    // Setup/cleanup only. Receive whole messages and ignore events until this command's reply arrives.
    private async Task<JsonElement> Send(string method, object? parameters = null, string? sessionId = null)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var id = ++_nextId;
        var request = JsonSerializer.Serialize(new { id, method, @params = parameters, sessionId });
        await _socket!.SendAsync(Encoding.UTF8.GetBytes(request), WebSocketMessageType.Text, true, timeout.Token);
        var buffer = new byte[8_192];
        while (true)
        {
            using var message = new MemoryStream();
            WebSocketReceiveResult chunk;
            do
            {
                chunk = await _socket.ReceiveAsync(buffer, timeout.Token);
                if (chunk.MessageType == WebSocketMessageType.Close)
                    throw new InvalidOperationException("CSS coverage connection closed before its reply.");
                message.Write(buffer, 0, chunk.Count);
            } while (!chunk.EndOfMessage);
            using var json = JsonDocument.Parse(message.ToArray());
            var reply = json.RootElement;
            if (!reply.TryGetProperty("id", out var responseId) || responseId.GetInt32() != id) continue;
            if (reply.TryGetProperty("error", out var error)) throw new InvalidOperationException(error.GetRawText());
            return reply.GetProperty("result").Clone();
        }
    }

    private Task<double> Run() => _page.EvaluateAsync<double>(_script);

    [Benchmark]
    public Task<double> UntrackedStyle() => Run();

    [Benchmark]
    public Task<double> OtherDocumentTrackedStyle() => Run();

    [Benchmark]
    public Task<double> TrackedStyle() => Run();

    [Benchmark]
    public Task<double> PlainProperty() => Run();

    [GlobalCleanup]
    public async Task Cleanup()
    {
        try
        {
            if (_session is not null) await Send("CSS.stopRuleUsageTracking", sessionId: _session);
        }
        finally
        {
            _socket?.Dispose();
            try { await _server.DisposeAsync(); }
            finally { await _browser.DisposeAsync(); }
        }
    }
}
