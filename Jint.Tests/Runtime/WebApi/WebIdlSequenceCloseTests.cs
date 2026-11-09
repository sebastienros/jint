#if NET8_0_OR_GREATER
#nullable enable

using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Jint.WebApi.WebSockets;

namespace Jint.Tests.Runtime.WebApi;

/// <summary>
/// Runs <see cref="WebIdlSequenceCloseMatrix"/> over every web-API sequence conversion: an element conversion that
/// throws calls the iterator's <c>return</c> exactly once, while a step that throws and a sequence that runs out call
/// nothing (sebastienros/jint#4431). One lane per conversion site; where a helper serves several operations (Headers'
/// fill serves <c>Request</c>, <c>Response</c> and <c>fetch</c>; the transfer list serves every <c>postMessage</c>;
/// the blob parts serve <c>File</c>; the key usages serve every <c>SubtleCrypto</c> method taking them) the lane takes
/// one of them.
/// <para>
/// The nested conversions, <c>sequence&lt;sequence&lt;ByteString&gt;&gt;</c> (Headers) and
/// <c>sequence&lt;sequence&lt;USVString&gt;&gt;</c> (URLSearchParams), have a lane for each level: a pair whose own
/// conversion fails is the outer sequence's failing element, and a pair element whose conversion fails is the inner
/// one's, which closes the inner iterator and then the outer. Both convert in full before the fill or the initialize
/// steps look at a pair, so their refusal rows (a pair of the wrong size, then pairs that would be accepted or one that
/// fails to convert; a header name that is not a token; a pair of three) read every element and close nothing unless a
/// conversion failed. Node 26's <c>Headers</c> reads in exactly that order, closing nothing in either case, as it
/// never closes; its <c>URLSearchParams</c> refuses mid-iteration and closes. <c>ReadableStream.from</c> is the one async sequence;
/// its <c>async_sequence&lt;any&gt;</c> elements cannot fail to convert, so its conversion row is a sync iterable's
/// rejected value, which CreateAsyncFromSyncIterator closes, and an async generator's, which ends it.
/// </para>
/// </summary>
/// <remarks>
/// Every case builds its own <see cref="Engine"/> and the fixture holds no mutable state, so its cases run in parallel.
/// </remarks>
[Parallelizable(ParallelScope.All)]
public sealed class WebIdlSequenceCloseTests
{
    private const string Lanes = """
        function request(r) {
            return new Promise((resolve, reject) => { r.onsuccess = () => resolve(r.result); r.onerror = () => reject(r.error); });
        }
        function open(upgrade) {
            const r = indexedDB.open('db', 1);
            r.onupgradeneeded = () => upgrade(r.result);
            return request(r);
        }

        lanes.blob = () => ({ goods: ['a'], bad: throwsOnConversion, run: it => new Blob(it) });
        // The nested conversions. `refused` converts, and then the fill or the initialize steps refuse it: a pair of the
        // wrong size first, then pairs that would be accepted, or a pair of three.
        lanes.headers = () => ({
            goods: [plain('a', '1')],
            bad: plain(throwsOnConversion, '1'),
            refused: [plain('a'), plain('b', '2'), plain('c', '3')],
            run: it => new Headers(it),
        });
        lanes.headersInvalidName = () => ({
            goods: [plain('a', '1')],
            bad: plain(throwsOnConversion, '1'),
            refused: [plain('a b', '1'), plain('c', '2'), plain('d', '3')],
            run: it => new Headers(it),
        });
        lanes.headersPair = () => ({ goods: ['a', '1'], bad: throwsOnConversion, refused: ['a', '1', 'x'], run: it => new Headers(plain(it)) });
        lanes.urlSearchParams = () => ({
            goods: [plain('a', '1')],
            bad: plain(throwsOnConversion, '1'),
            refused: [plain('a'), plain('b', '2'), plain('c', '3')],
            run: it => new URLSearchParams(it),
        });
        lanes.urlSearchParamsPair = () => ({
            goods: ['a', '1'],
            bad: throwsOnConversion,
            refused: ['a', '1', 'x'],
            run: it => new URLSearchParams(plain(it)),
        });
        lanes.messageEventPorts = () => ({ goods: [new MessageChannel().port1], bad: {}, run: it => new MessageEvent('message', { ports: it }) });
        lanes.structuredCloneTransfer = () => ({ goods: [new ArrayBuffer(1)], bad: 1, run: it => structuredClone(null, { transfer: it }) });
        lanes.webSocketProtocols = () => ({ goods: ['chat'], bad: throwsOnConversion, run: it => new WebSocket('wss://example.org/', it) });
        lanes.abortSignalAny = () => ({ goods: [new AbortController().signal], bad: {}, run: it => AbortSignal.any(it) });
        lanes.consoleTable = () => ({ goods: ['a'], bad: throwsOnConversion, run: it => console.table([{ a: 1 }], it) });
        lanes.keyUsages = () => ({
            goods: ['sign'],
            bad: throwsOnConversion,
            run: it => crypto.subtle.generateKey({ name: 'HMAC', hash: 'SHA-256' }, false, it),
        });
        lanes.jwkKeyOps = () => ({
            goods: ['sign'],
            bad: throwsOnConversion,
            run: it => crypto.subtle.importKey(
                'jwk', { kty: 'oct', k: 'AAAAAAAAAAAAAAAAAAAAAA', alg: 'HS256', key_ops: it }, { name: 'HMAC', hash: 'SHA-256' }, false, ['sign']),
        });
        lanes.performanceObserver = () => ({
            goods: ['mark'],
            bad: throwsOnConversion,
            run: it => new PerformanceObserver(() => {}).observe({ entryTypes: it }),
        });
        lanes.idbTransaction = () => ({
            goods: ['x'],
            bad: throwsOnConversion,
            run: async it => {
                const db = await open(db => db.createObjectStore('x'));
                try { db.transaction(it); } finally { db.close(); }
            },
        });
        lanes.idbKeyPath = () => ({
            goods: ['x'],
            bad: throwsOnConversion,
            run: async it => {
                // Thrown out of the upgrade it would abort the open instead, so it is carried out of it.
                let error;
                const db = await open(db => { try { db.createObjectStore('s', { keyPath: it }); } catch (e) { error = e; } });
                db.close();
                if (error) throw error;
            },
        });
        lanes.cacheAddAll = () => ({
            goods: ['https://example.org/a'],
            bad: throwsOnConversion,
            run: async it => (await caches.open('c')).addAll(it),
        });

        // The async sequence. A value is only awaited, never converted, so the rejection is the failing element.
        lanes.readableStreamFrom = () => ({
            goods: ['a'],
            bad: { then(resolve, reject) { reject(marker('ConversionError')); } },
            run: async it => {
                const reader = ReadableStream.from(it).getReader();
                while (!(await reader.read()).done) { }
            },
        });
        """;

    // Each sync lane, named with the error its bad element throws.
    private static readonly Dictionary<string, string> SyncLanes = new()
    {
        ["blob"] = "ConversionError",
        ["headers"] = "ConversionError",
        ["headersPair"] = "ConversionError",
        ["urlSearchParams"] = "ConversionError",
        ["urlSearchParamsPair"] = "ConversionError",
        ["messageEventPorts"] = "TypeError",
        ["structuredCloneTransfer"] = "TypeError",
        ["webSocketProtocols"] = "ConversionError",
        ["abortSignalAny"] = "TypeError",
        ["consoleTable"] = "ConversionError",
        ["keyUsages"] = "ConversionError",
        ["jwkKeyOps"] = "ConversionError",
        ["performanceObserver"] = "ConversionError",
        ["idbTransaction"] = "ConversionError",
        ["idbKeyPath"] = "ConversionError",
        ["cacheAddAll"] = "ConversionError",
    };

    private static readonly Dictionary<string, string> AsyncLanes = new()
    {
        ["readableStreamFrom"] = "ConversionError",
    };

    // The nested conversions, whose fill or initialize steps refuse what the conversion produced: each named with the
    // error the refusal throws and the one its bad element does.
    private static readonly Dictionary<string, (string Refusal, string Error)> RefusalLanes = new()
    {
        ["headers"] = ("TypeError", "ConversionError"),
        ["headersInvalidName"] = ("TypeError", "ConversionError"),
        ["headersPair"] = ("TypeError", "ConversionError"),
        ["urlSearchParams"] = ("TypeError", "ConversionError"),
        ["urlSearchParamsPair"] = ("TypeError", "ConversionError"),
    };

    public static TestCases<string, string, string, string> EveryLaneUnderEveryProbe
        => WebIdlSequenceCloseMatrix.Cases(SyncLanes, AsyncLanes, RefusalLanes);

    private static readonly Prepared<Script> PreparedHarness = Engine.PrepareScript(WebIdlSequenceCloseMatrix.Harness);
    private static readonly Prepared<Script> PreparedLanes = Engine.PrepareScript(Lanes);

    private static Engine CreateEngine()
    {
        var engine = new Engine(options => options
            .UseWebApis(WebApiFeatures.Default | WebApiFeatures.IndexedDb)
            .UseWebSocket()
            .UseCacheApi()
            .UseFetch(fetch => fetch.HttpClient = new HttpClient(new OkTransport())));

        engine._webApi!.WebSocketConnections = new RefusedSockets();
        engine.Execute(PreparedHarness);
        engine.Execute(PreparedLanes);
        return engine;
    }

    /// <summary>
    /// A conversion that fails on an element closes the iterator once and propagates its own error; a step that
    /// throws, and a sequence that runs out, close nothing.
    /// </summary>
    [TestCaseSource(nameof(EveryLaneUnderEveryProbe))]
    public void EveryLaneClosesOnlyWhenAnElementFailsToConvert(string lane, string probe, string row, string expected)
    {
        using var engine = CreateEngine();
        engine.Evaluate($"probe('{lane}', '{probe}', '{row}')").UnwrapIfPromise(TestBudgets.WedgeCeiling).AsString().Should().Be(expected);
    }

    /// <summary>Answers every request with an empty 200, which is all <c>cache.addAll</c> needs to store it.</summary>
    private sealed class OkTransport : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("") });
    }

    /// <summary>Refuses every connection, so a constructed socket goes nowhere.</summary>
    private sealed class RefusedSockets : IWebSocketConnectionFactory
    {
        public IWebSocketConnection Create(Uri url, IReadOnlyList<string> protocols, long maxMessageBytes, string? userAgent) => new RefusedConnection();
    }

    private sealed class RefusedConnection : IWebSocketConnection
    {
        public string SubProtocol => "";

        public Task ConnectAsync(CancellationToken cancellationToken) => Task.FromException(new IOException("refused"));

        public Task SendAsync(ReadOnlyMemory<byte> payload, bool isText, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task<WebSocketReceipt> ReceiveAsync(CancellationToken cancellationToken) => Task.FromException<WebSocketReceipt>(new IOException("refused"));

        public Task CloseOutputAsync(int? code, string reason, CancellationToken cancellationToken) => Task.CompletedTask;

        public void Abort()
        {
        }

        public void Dispose()
        {
        }
    }
}
#endif
