#if NET8_0_OR_GREATER
#nullable enable

using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Jint.Native;
using Jint.WebApi.WebSockets;

namespace Jint.Tests.Runtime.WebApi;

/// <summary>
/// Pins the one read of <c>@@iterator</c> a WebIDL conversion makes when it decides whether a value is a sequence. The
/// union conversion (https://webidl.spec.whatwg.org/#es-union) and overload resolution
/// (https://webidl.spec.whatwg.org/#es-overloads) both take <c>? GetMethod(V, %Symbol.iterator%)</c>
/// (<a href="https://tc39.es/ecma262/#sec-getmethod">GetMethod</a>) once. When it found a method they create the
/// sequence from V and that method (https://webidl.spec.whatwg.org/#create-sequence-from-iterable), whose
/// <a href="https://tc39.es/ecma262/#sec-getiteratorfrommethod">GetIteratorFromMethod</a> calls it without a second
/// read. GetMethod answers "no method" for undefined and null, which take the union's other arm, and throws a
/// <c>TypeError</c> for anything else that is not callable.
/// <para>
/// Every lane here asked first and then took GetIterator, which read again (sebastienros/jint#4449). The Headers lanes
/// (the constructor, <c>Request</c>'s and <c>Response</c>'s <c>headers</c> member, and <c>fetch</c>'s) asked with a plain
/// Get, so an <c>@@iterator</c> that was neither callable nor nullish was taken for a record. Read counts and the arm a
/// nullish method takes are Node 26's for the Headers lanes. Node's Headers takes the record arm for a non-callable
/// <c>@@iterator</c>, against WebIDL, so those rows follow the specification; the other lanes have no Node counterpart
/// that is a WebIDL binding and follow it alone.
/// </para>
/// </summary>
/// <remarks>
/// Every case builds its own <see cref="Engine"/> and the fixture holds no mutable state, so its cases run in parallel.
/// The worker <c>postMessage</c> lanes need the worker harness and are in <c>WorkerMechanismTests</c>.
/// </remarks>
[Parallelizable(ParallelScope.All)]
public sealed class WebIdlIteratorReadTests
{
    private const string Harness = """
        function request(r) {
            return new Promise((resolve, reject) => { r.onsuccess = () => resolve(r.result); r.onerror = () => reject(r.error); });
        }
        function open(upgrade) {
            const r = indexedDB.open('db', 1);
            r.onupgradeneeded = () => upgrade(r.result);
            return request(r);
        }
        // Runs `f` inside the upgrade and answers what it returned, or the name of what it threw, which would otherwise
        // abort the upgrade.
        async function upgrade(f) {
            let out;
            const db = await open(db => { try { out = f(db); } catch (e) { out = e.name; } });
            db.close();
            return out;
        }

        // Each lane: `method` is the iterator method its sequence arm runs, `own` the own properties its other arm reads
        // (a record, a dictionary, or a toString), and `run` the conversion, answering what it produced.
        const lanes = {
            headers: () => ({
                method: function* () { yield ['z', '9']; },
                own: { a: '1' },
                run: init => [...new Headers(init).keys()].join(','),
            }),
            request: () => ({
                method: function* () { yield ['z', '9']; },
                own: { a: '1' },
                run: init => [...new Request('https://example.org/', { headers: init }).headers.keys()].join(','),
            }),
            response: () => ({
                method: function* () { yield ['z', '9']; },
                own: { a: '1' },
                run: init => [...new Response(null, { headers: init }).headers.keys()].join(','),
            }),
            fetch: () => ({
                method: function* () { yield ['z', '9']; },
                own: { a: '1' },
                run: init => fetch('https://example.org/', { headers: init }).then(() => sentHeaders()),
            }),
            messagePort: () => {
                const a = new ArrayBuffer(1), b = new ArrayBuffer(1);
                return {
                    method: function* () { yield a; },
                    own: { transfer: [b] },
                    run: init => {
                        const ch = new MessageChannel();
                        try { ch.port1.postMessage(null, init); } finally { ch.port1.close(); ch.port2.close(); }
                        return (a.byteLength === 0 ? 'a' : '') + (b.byteLength === 0 ? 'b' : '');
                    },
                };
            },
            webSocket: () => ({
                method: function* () { yield 'chat'; },
                own: { toString() { return 'v2'; } },
                run: init => { new WebSocket('wss://example.org/', init); return offeredProtocols(); },
            }),
            idbTransaction: () => ({
                method: function* () { yield 'x'; },
                own: { toString() { return 'y'; } },
                run: async init => {
                    const db = await open(db => { db.createObjectStore('x'); db.createObjectStore('y'); });
                    try { return Array.from(db.transaction(init).objectStoreNames).join(','); } finally { db.close(); }
                },
            }),
            idbStoreKeyPath: () => ({
                method: function* () { yield 'x'; },
                own: { toString() { return 'y'; } },
                run: init => upgrade(db => JSON.stringify(db.createObjectStore('s', { keyPath: init }).keyPath)),
            }),
            idbIndexKeyPath: () => ({
                method: function* () { yield 'x'; },
                own: { toString() { return 'y'; } },
                run: init => upgrade(db => JSON.stringify(db.createObjectStore('s').createIndex('i', init).keyPath)),
            }),
        };

        // Makes the lane's argument an object whose Symbol.iterator is an accessor - on the object itself ('own') or on
        // its prototype ('proto') - that counts its reads and answers per `answers`, runs the lane and answers
        // "<result> <reads>", the result being what the lane produced or the name of what it threw.
        //   method                 the lane's iterator method, every read.
        //   methodThenUndefined    the lane's method on the first read, undefined on any later one.
        //   undefinedThenMethod    undefined on the first read, the lane's method on any later one.
        //   undefined, null        GetMethod's "no method", which takes the other arm.
        //   number, string, object, boolean, symbol, bigint
        //                          neither callable nor nullish, for which GetMethod throws a TypeError.
        async function reads(laneName, where, answers) {
            const lane = lanes[laneName]();
            let count = 0;
            const answer = {
                method: () => lane.method,
                methodThenUndefined: () => count === 1 ? lane.method : undefined,
                undefinedThenMethod: () => count === 1 ? undefined : lane.method,
                undefined: () => undefined,
                null: () => null,
                number: () => 42,
                string: () => 'x',
                object: () => ({}),
                boolean: () => true,
                symbol: () => Symbol.iterator,
                bigint: () => 1n,
            }[answers];
            const proto = {};
            const init = Object.assign(Object.create(proto), lane.own);
            Object.defineProperty(where === 'own' ? init : proto, Symbol.iterator, { get() { count++; return answer(); } });
            let result;
            try { result = await lane.run(init); } catch (e) { result = e.name; }
            return result + ' ' + count;
        }
        """;

    // What each lane produces from its sequence arm and from its other arm.
    private static readonly Dictionary<string, (string Sequence, string Other)> Lanes = new()
    {
        ["headers"] = ("z", "a"),
        ["request"] = ("z", "a"),
        ["response"] = ("z", "a"),
        ["fetch"] = ("z", "a"),
        ["messagePort"] = ("a", "b"),
        ["webSocket"] = ("chat", "v2"),
        ["idbTransaction"] = ("x", "y"),
        ["idbStoreKeyPath"] = ("[\"x\"]", "\"y\""),
        ["idbIndexKeyPath"] = ("[\"x\"]", "\"y\""),
    };

    private static readonly string[] Answers =
    [
        "method",
        "methodThenUndefined",
        "undefinedThenMethod",
        "undefined",
        "null",
        "number",
        "string",
        "object",
        "boolean",
        "symbol",
        "bigint",
    ];

    // One read every time; the method that read found decides the arm.
    private static string Expected(string lane, string answers)
    {
        var (sequence, other) = Lanes[lane];
        return answers switch
        {
            "method" or "methodThenUndefined" => sequence + " 1",
            "undefinedThenMethod" or "undefined" or "null" => other + " 1",
            _ => "TypeError 1",
        };
    }

    public static TestCases<string, string, string, string> EveryLaneUnderEveryAnswer
    {
        get
        {
            var rows = new TestCases<string, string, string, string>();
            foreach (var lane in Lanes.Keys)
            {
                foreach (var answers in Answers)
                {
                    foreach (var where in new[] { "own", "proto" })
                    {
                        rows.Add(lane, answers, where, Expected(lane, answers));
                    }
                }
            }

            return rows;
        }
    }

    private static readonly Prepared<Script> PreparedHarness = Engine.PrepareScript(Harness);

    private static Engine CreateEngine()
    {
        var transport = new RecordingTransport();
        var sockets = new RecordingSockets();
        var engine = new Engine(options => options
            .UseWebApis(WebApiFeatures.Default | WebApiFeatures.IndexedDb)
            .UseWebSocket()
            .UseFetch(fetch => fetch.HttpClient = new HttpClient(transport)));

        engine._webApi!.WebSocketConnections = sockets;
        engine.SetValue("sentHeaders", new Func<string>(() => transport.Sent));
        engine.SetValue("offeredProtocols", new Func<string>(() => sockets.Offered));
        engine.Execute(PreparedHarness);
        return engine;
    }

    /// <summary>The issue's own probe.</summary>
    [Test]
    public void TheIssueProbeMatchesNode()
    {
        using var engine = CreateEngine();
        engine.Evaluate("""
            (() => {
                let reads = 0;
                const init = [["a", "1"]];
                const it = init[Symbol.iterator];
                Object.defineProperty(init, Symbol.iterator, { get() { reads++; return it; } });
                new Headers(init);
                return reads;
            })()
            """).AsNumber().Should().Be(1);
    }

    /// <summary>
    /// Each lane reads <c>@@iterator</c> once and takes the arm that read decides: the sequence arm, iterating with the
    /// method it found even when a second read would find none; the other arm for undefined and null; and a
    /// <c>TypeError</c> for a value that is neither callable nor nullish.
    /// </summary>
    [TestCaseSource(nameof(EveryLaneUnderEveryAnswer))]
    public void EveryLaneReadsIteratorOnce(string lane, string answers, string where, string expected)
    {
        using var engine = CreateEngine();
        engine.Evaluate($"reads('{lane}', '{where}', '{answers}')").UnwrapIfPromise(TestBudgets.WedgeCeiling).AsString().Should().Be(expected);
    }

    /// <summary>
    /// The <c>TypeError</c> a non-callable <c>@@iterator</c> raises is GetMethod's, and it comes from the realm the
    /// conversion runs in.
    /// </summary>
    [Test]
    public void ANonCallableIteratorIsGetMethodsTypeError()
    {
        using var engine = CreateEngine();
        engine.Evaluate("""
            try { new Headers({ [Symbol.iterator]: 42, a: '1' }); 'no error'; }
            catch (e) { (e instanceof TypeError) + ' ' + e.message; }
            """).AsString().Should().Be("true Value returned for property 'Symbol(Symbol.iterator)' of object is not a function");
    }

    /// <summary>Records the header names a request carried that the lanes set, <c>a</c> and <c>z</c>.</summary>
    private sealed class RecordingTransport : HttpMessageHandler
    {
        private volatile string _sent = "";

        internal string Sent => _sent;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            _sent = string.Join(",", request.Headers.Select(h => h.Key).Where(k => k is "a" or "z"));
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("") });
        }
    }

    /// <summary>Records the subprotocols a socket offered, then refuses the connection.</summary>
    private sealed class RecordingSockets : IWebSocketConnectionFactory
    {
        private volatile string _offered = "";

        internal string Offered => _offered;

        public IWebSocketConnection Create(Uri url, IReadOnlyList<string> protocols, long maxMessageBytes, string? userAgent)
        {
            _offered = string.Join(",", protocols);
            return new RefusedConnection();
        }
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
