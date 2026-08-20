using BenchmarkDotNet.Attributes;
using Jint.Native;

namespace Jint.Benchmark;

/// <summary>
/// The fetch object model — <c>Headers</c>, <c>Request</c> and <c>Response</c>,
/// <see cref="WebApiFeatures.Fetch"/>, https://fetch.spec.whatwg.org/#headers-class and
/// https://fetch.spec.whatwg.org/#request-class.
///
/// <para><b>No request is ever made.</b> Enabling the feature installs <c>fetch</c> as well, and no row
/// here calls it: what is measured is the part of the surface a script pays for on every request it
/// builds and every response it inspects, which is also the part a host that supplies its own transport
/// still pays. Nothing in this class opens a socket, reads a clock or depends on a network being
/// present.</para>
///
/// <para>Four rows. <see cref="HeadersAppendAndIterate"/> is the header list itself — sixteen appends,
/// one of them combining a second value into an existing name, then the full sorted iteration a
/// serializer or a logger does. <see cref="RequestConstruction"/> and <see cref="ResponseConstruction"/>
/// are the two constructors, each of which parses a URL or validates a status, converts a header init and
/// extracts a body. <see cref="RequestClone"/> is the copy path, which a middleware chain runs on every
/// request it forwards.</para>
///
/// <para><b>Engine isolation.</b> Every row gets its own engine carrying <see cref="WebApiFeatures.Fetch"/>
/// (which implies <see cref="WebApiFeatures.Events"/>, <see cref="WebApiFeatures.Url"/> and
/// <see cref="WebApiFeatures.Files"/>, since a <c>Request</c> always has an <c>AbortSignal</c> and a
/// WHATWG URL), warmed with its own fixture and its own script and nothing else — see
/// <see cref="WebApiBenchmarkSupport"/>. Engine construction stays in <c>[GlobalSetup]</c> and never
/// enters the measurement.</para>
/// </summary>
[MemoryDiagnoser]
[HideColumns("Error", "Gen0", "Gen1", "Gen2")]
[BenchmarkCategory(WebApiBenchmarkSupport.Category)]
public class WebApiFetchObjectModelBenchmark
{
    /// <summary>The header set a browser or a service client actually sends, names already lowercase.</summary>
    private const string HeaderData =
        """
        var HEADER_NAMES = [
            'accept', 'accept-encoding', 'accept-language', 'authorization', 'cache-control',
            'content-type', 'cookie', 'if-none-match', 'origin', 'referer', 'user-agent',
            'x-request-id', 'x-forwarded-for', 'x-correlation-id', 'x-api-version', 'x-tenant'
        ];
        var HEADER_VALUES = [
            'application/json, text/plain, */*', 'gzip, deflate, br', 'en-US,en;q=0.9',
            'Bearer eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9', 'no-cache, no-store, max-age=0',
            'application/json; charset=utf-8', 'sid=8f2c1b4e9a; theme=dark', 'W/"a1b2c3d4"',
            'https://app.example.org', 'https://app.example.org/dashboard',
            'Mozilla/5.0 (compatible; Jint/4)', '4f2c1b4e-9a3d-4c1e-9b2a-7d5f0e1c8a6b',
            '203.0.113.7, 198.51.100.42', 'c0ffee-1234', '3', 'acme-corp'
        ];
        """;

    /// <summary>A request and a response init, reused so the rows measure construction and not literal building.</summary>
    private const string InitData =
        """
        var REQUEST_INIT = {
            method: 'POST',
            headers: { 'content-type': 'application/json', 'x-api-version': '3', 'authorization': 'Bearer token' },
            body: '{"id":42,"name":"widget","tags":["a","b","c"]}'
        };
        var RESPONSE_INIT = {
            status: 201,
            statusText: 'Created',
            headers: { 'content-type': 'application/json', 'etag': 'W/"a1b2c3d4"', 'x-request-id': 'c0ffee' }
        };
        var RESPONSE_BODY = '{"ok":true,"items":[1,2,3,4,5,6,7,8,9,10]}';
        """;

    private IsolatedScript _headersAppendAndIterate;
    private IsolatedScript _requestConstruction;
    private IsolatedScript _responseConstruction;
    private IsolatedScript _requestClone;

    [GlobalSetup]
    public void Setup()
    {
        _headersAppendAndIterate = Row(
            HeaderData +
            """

            function headersAppendAndIterate() {
                var n = 0;
                for (var r = 0; r < 100; r++) {
                    var headers = new Headers();
                    for (var i = 0; i < HEADER_NAMES.length; i++) { headers.append(HEADER_NAMES[i], HEADER_VALUES[i]); }
                    headers.append('accept', 'text/html');
                    for (var entry of headers) { n += entry[0].length + entry[1].length; }
                    n += headers.has('cookie') ? 1 : 0;
                }
                return n;
            }
            """,
            "headersAppendAndIterate()");

        _requestConstruction = Row(
            InitData +
            """

            function requestConstruction() {
                var n = 0;
                for (var i = 0; i < 200; i++) {
                    var request = new Request('https://api.example.org/v3/items/' + i + '?full=1', REQUEST_INIT);
                    n += request.method.length + request.url.length
                        + request.headers.get('content-type').length + (request.bodyUsed ? 0 : 1);
                }
                return n;
            }
            """,
            "requestConstruction()");

        _responseConstruction = Row(
            InitData +
            """

            function responseConstruction() {
                var n = 0;
                for (var i = 0; i < 200; i++) {
                    var response = new Response(RESPONSE_BODY, RESPONSE_INIT);
                    n += response.status + response.statusText.length
                        + response.headers.get('etag').length + (response.ok ? 1 : 0);
                }
                return n;
            }
            """,
            "responseConstruction()");

        _requestClone = Row(
            InitData +
            """

            function requestClone() {
                var n = 0;
                for (var i = 0; i < 200; i++) {
                    var request = new Request('https://api.example.org/v3/items', REQUEST_INIT);
                    var copy = request.clone();
                    n += copy.method.length + copy.url.length + (copy.bodyUsed ? 0 : 1);
                }
                return n;
            }
            """,
            "requestClone()");
    }

    private static IsolatedScript Row(string fixture, string call)
        => WebApiBenchmarkSupport.DeterministicRow(WebApiFeatures.Fetch, fixture, call);

    /// <summary>100 header lists of seventeen appends each, then iterated in full.</summary>
    [Benchmark]
    public JsValue HeadersAppendAndIterate() => _headersAppendAndIterate.Run();

    /// <summary>200 <c>Request</c>s, each parsing its own URL and converting a three-name header init.</summary>
    [Benchmark]
    public JsValue RequestConstruction() => _requestConstruction.Run();

    /// <summary>200 <c>Response</c>s over a JSON body and a three-name header init.</summary>
    [Benchmark]
    public JsValue ResponseConstruction() => _responseConstruction.Run();

    /// <summary>200 construct-then-clone pairs, the middleware-forwarding shape.</summary>
    [Benchmark]
    public JsValue RequestClone() => _requestClone.Run();
}
